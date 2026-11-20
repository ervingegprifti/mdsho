using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using MDSHO.Common;
using WinRtClipboard = Windows.ApplicationModel.DataTransfer.Clipboard;

// SetWinEventHook: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwineventhook
// GetTokenInformation (TokenIntegrityLevel): https://learn.microsoft.com/en-us/windows/win32/api/securitybaseapi/nf-securitybaseapi-gettokeninformation
// SendInput: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-sendinput
// Clipboard formats kept out of history and cloud sync: https://learn.microsoft.com/en-us/windows/win32/dataxchg/clipboard-formats#cloud-clipboard-and-clipboard-history-formats
// Clipboard history: https://learn.microsoft.com/en-us/uwp/api/windows.applicationmodel.datatransfer.clipboard

namespace MDSHO.Infrastructure {

  /// <summary>
  /// Remembers the last foreground window that isn't MDSHO (a WinEvent hook; call <see cref="Start"/> on the UI thread)
  /// and gives it the focus back.
  /// </summary>
  public sealed class ForegroundWindowTracker : IForegroundWindows, IDisposable {
    private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
    private const uint WINEVENT_OUTOFCONTEXT = 0x0000;
    private const uint WINEVENT_SKIPOWNPROCESS = 0x0002;
    private const int SW_RESTORE = 9;

    private delegate void WinEventProc(IntPtr hook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint thread, uint time);

    [DllImport("user32.dll")]
    private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr module, WinEventProc callback, uint processId, uint threadId, uint flags);
    [DllImport("user32.dll")]
    private static extern bool UnhookWinEvent(IntPtr hook);
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr hWnd);
    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);
    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int command);
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    private readonly ForegroundHistory history = new ForegroundHistory(Environment.ProcessId);
    // Kept in a field so the garbage collector never frees the callback while Windows still calls it.
    private readonly WinEventProc callback;
    private IntPtr hook;

    /// <summary>Creates the tracker (not started).</summary>
    public ForegroundWindowTracker() {
      this.callback = OnForeground;
    }

    /// <summary>Starts listening; the window in front now counts as the first one.</summary>
    public void Start() {
      if (this.hook != IntPtr.Zero) {
        return;
      }
      Observe(GetForegroundWindow());
      this.hook = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND, IntPtr.Zero, this.callback, 0, 0,
        WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);
      if (this.hook == IntPtr.Zero) {
        AppLog.Warning(null, "The foreground window hook could not be set; clipboard buttons can't find the window to paste into");
      }
    }

    private void OnForeground(IntPtr hook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint thread, uint time) {
      Observe(hwnd);
    }

    private void Observe(IntPtr hwnd) {
      if (hwnd == IntPtr.Zero) {
        return;
      }
      GetWindowThreadProcessId(hwnd, out uint processId);
      this.history.Observe(hwnd, (int)processId);
    }

    /// <summary>The last foreground window that isn't MDSHO and still shows; zero when none.</summary>
    public IntPtr LastOtherWindow {
      get {
        IntPtr last = this.history.Last;
        if (last != IntPtr.Zero && (!IsWindow(last) || !IsWindowVisible(last))) {
          this.history.Forget(last);
          return IntPtr.Zero;
        }
        return last;
      }
    }

    /// <summary>Brings the window to the front (restored when minimized); true when it is the foreground window then.</summary>
    public bool Activate(IntPtr window) {
      if (IsIconic(window)) {
        ShowWindow(window, SW_RESTORE);
      }
      SetForegroundWindow(window);
      return GetForegroundWindow() == window;
    }

    /// <summary>Stops listening.</summary>
    public void Dispose() {
      if (this.hook != IntPtr.Zero) {
        UnhookWinEvent(this.hook);
        this.hook = IntPtr.Zero;
      }
    }
  }

  /// <summary>Compares the integrity level of a window's process with MDSHO's (UIPI blocks input to a higher level).</summary>
  public sealed class ElevationCheck : IElevationCheck {
    private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
    private const uint TOKEN_QUERY = 0x0008;
    private const int TokenIntegrityLevel = 25;
    private const int ERROR_ACCESS_DENIED = 5;

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint access, bool inherit, uint processId);
    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();
    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr handle);
    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);
    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool GetTokenInformation(IntPtr token, int infoClass, IntPtr info, int length, out int returnLength);
    [DllImport("advapi32.dll")]
    private static extern IntPtr GetSidSubAuthorityCount(IntPtr sid);
    [DllImport("advapi32.dll")]
    private static extern IntPtr GetSidSubAuthority(IntPtr sid, uint index);

    /// <summary>True when the window's process runs at a higher integrity level than MDSHO.</summary>
    public bool IsBlockedByElevation(IntPtr window) {
      try {
        int? own = IntegrityOf(GetCurrentProcess(), out _);
        if (own == null) {
          return false;
        }
        GetWindowThreadProcessId(window, out uint processId);
        if (processId == 0) {
          return false;
        }
        IntPtr process = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, processId);
        if (process == IntPtr.Zero) {
          return ElevationRules.IsBlocked(null, own.Value, Marshal.GetLastWin32Error() == ERROR_ACCESS_DENIED);
        }
        try {
          int? target = IntegrityOf(process, out bool accessDenied);
          return ElevationRules.IsBlocked(target, own.Value, accessDenied);
        } finally {
          CloseHandle(process);
        }
      } catch (Exception ex) {
        AppLog.Warning(null, "The elevation check failed: {ErrorType}", ex.GetType().FullName);
        return false;
      }
    }

    // The integrity level RID of the process token (0x2000 medium, 0x3000 high), or null.
    private static int? IntegrityOf(IntPtr process, out bool accessDenied) {
      accessDenied = false;
      if (!OpenProcessToken(process, TOKEN_QUERY, out IntPtr token)) {
        accessDenied = Marshal.GetLastWin32Error() == ERROR_ACCESS_DENIED;
        return null;
      }
      try {
        GetTokenInformation(token, TokenIntegrityLevel, IntPtr.Zero, 0, out int length);
        if (length <= 0) {
          return null;
        }
        IntPtr buffer = Marshal.AllocHGlobal(length);
        try {
          if (!GetTokenInformation(token, TokenIntegrityLevel, buffer, length, out _)) {
            return null;
          }
          // TOKEN_MANDATORY_LABEL starts with SID_AND_ATTRIBUTES, whose first field is the SID pointer.
          IntPtr sid = Marshal.ReadIntPtr(buffer);
          int count = Marshal.ReadByte(GetSidSubAuthorityCount(sid));
          return Marshal.ReadInt32(GetSidSubAuthority(sid, (uint)(count - 1)));
        } finally {
          Marshal.FreeHGlobal(buffer);
        }
      } finally {
        CloseHandle(token);
      }
    }
  }

  /// <summary>Sends Ctrl+V with SendInput.</summary>
  public sealed class PasteKeySender : IKeySender {
    private const uint INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const ushort VK_CONTROL = 0x11;
    private const ushort VK_V = 0x56;

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT {
      public ushort wVk;
      public ushort wScan;
      public uint dwFlags;
      public uint time;
      public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT {
      public int dx;
      public int dy;
      public uint mouseData;
      public uint dwFlags;
      public uint time;
      public IntPtr dwExtraInfo;
    }

    // The union is as big as its largest member (MOUSEINPUT), so the size matches Windows' INPUT.
    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion {
      [FieldOffset(0)] public MOUSEINPUT mi;
      [FieldOffset(0)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT {
      public uint type;
      public InputUnion u;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint count, INPUT[] inputs, int size);

    /// <summary>Sends Ctrl down, V down, V up, Ctrl up. Throws when Windows took none of them.</summary>
    public void SendPaste() {
      INPUT[] inputs = {
        Key(VK_CONTROL, 0), Key(VK_V, 0), Key(VK_V, KEYEVENTF_KEYUP), Key(VK_CONTROL, KEYEVENTF_KEYUP)
      };
      uint sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
      if (sent != inputs.Length) {
        throw new Win32Exception(Marshal.GetLastWin32Error());
      }
    }

    private static INPUT Key(ushort key, uint flags) {
      return new INPUT { type = INPUT_KEYBOARD, u = new InputUnion { ki = new KEYBDINPUT { wVk = key, dwFlags = flags } } };
    }
  }

  /// <summary>Waits with Task.Delay.</summary>
  public sealed class TaskPasteDelay : IPasteDelay {
    /// <summary>A task that ends after the time.</summary>
    public Task Delay(TimeSpan time) {
      return Task.Delay(time);
    }
  }

  /// <summary>
  /// The Windows clipboard for clipboard buttons: classic Win32 calls for the content, WinRT for clipboard history. Call it
  /// on the UI thread. Never logs or shows the content.
  /// </summary>
  public sealed class PasteClipboard : IPasteClipboard {
    private const uint CF_UNICODETEXT = 13;
    private const uint GMEM_MOVEABLE = 0x0002;
    private static readonly IntPtr HWND_MESSAGE = new IntPtr(-3);
    // Formats whose data is not memory MDSHO can copy (GDI objects, owner-drawn, private handles).
    private static readonly HashSet<uint> NotCopyable = new HashSet<uint> { 2, 3, 9, 14, 0x0080, 0x0082, 0x0083, 0x008E };

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool OpenClipboard(IntPtr owner);
    [DllImport("user32.dll")]
    private static extern bool CloseClipboard();
    [DllImport("user32.dll")]
    private static extern bool EmptyClipboard();
    [DllImport("user32.dll")]
    private static extern uint EnumClipboardFormats(uint format);
    [DllImport("user32.dll")]
    private static extern int CountClipboardFormats();
    [DllImport("user32.dll")]
    private static extern IntPtr GetClipboardData(uint format);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetClipboardData(uint format, IntPtr data);
    [DllImport("user32.dll")]
    private static extern uint GetClipboardSequenceNumber();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern uint RegisterClipboardFormat(string name);
    [DllImport("kernel32.dll")]
    private static extern IntPtr GlobalAlloc(uint flags, UIntPtr bytes);
    [DllImport("kernel32.dll")]
    private static extern IntPtr GlobalFree(IntPtr memory);
    [DllImport("kernel32.dll")]
    private static extern IntPtr GlobalLock(IntPtr memory);
    [DllImport("kernel32.dll")]
    private static extern bool GlobalUnlock(IntPtr memory);
    [DllImport("kernel32.dll")]
    private static extern UIntPtr GlobalSize(IntPtr memory);

    private System.Windows.Forms.NativeWindow owner;

    // The clipboard needs an owner window for SetClipboardData after EmptyClipboard: a hidden message-only window.
    private IntPtr Owner {
      get {
        if (this.owner == null) {
          this.owner = new System.Windows.Forms.NativeWindow();
          this.owner.CreateHandle(new System.Windows.Forms.CreateParams { Parent = HWND_MESSAGE });
        }
        return this.owner.Handle;
      }
    }

    /// <summary>Windows' clipboard sequence number.</summary>
    public uint SequenceNumber => GetClipboardSequenceNumber();

    /// <summary>True when the clipboard has no formats.</summary>
    public bool IsEmpty() {
      return CountClipboardFormats() == 0;
    }

    /// <summary>True when Windows clipboard history is on.</summary>
    public bool IsHistoryEnabled() {
      try {
        return WinRtClipboard.IsHistoryEnabled();
      } catch (Exception ex) {
        AppLog.Warning(null, "Clipboard history could not be checked: {ErrorType}", ex.GetType().FullName);
        return false;
      }
    }

    /// <summary>The newest clipboard history item, or null.</summary>
    public async Task<object> CurrentHistoryItemAsync() {
      try {
        var result = await WinRtClipboard.GetHistoryItemsAsync();
        if (result.Status == Windows.ApplicationModel.DataTransfer.ClipboardHistoryItemsResultStatus.Success && result.Items.Count > 0) {
          return result.Items[0];
        }
        AppLog.Information("Clipboard history items not available: {Status}", result.Status.ToString());
      } catch (Exception ex) {
        AppLog.Warning(null, "Clipboard history could not be read: {ErrorType}", ex.GetType().FullName);
      }
      return null;
    }

    /// <summary>Makes the history item the current content again.</summary>
    public Task<bool> RestoreHistoryItemAsync(object item) {
      try {
        var status = WinRtClipboard.SetHistoryItemAsContent((Windows.ApplicationModel.DataTransfer.ClipboardHistoryItem)item);
        if (status != Windows.ApplicationModel.DataTransfer.SetHistoryItemAsContentStatus.Success) {
          AppLog.Information("Clipboard history item not put back: {Status}", status.ToString());
        }
        return Task.FromResult(status == Windows.ApplicationModel.DataTransfer.SetHistoryItemAsContentStatus.Success);
      } catch (Exception ex) {
        AppLog.Warning(null, "Clipboard history item could not be put back: {ErrorType}", ex.GetType().FullName);
        return Task.FromResult(false);
      }
    }

    /// <summary>A copy in memory of every format that is plain memory; null when none could be copied.</summary>
    public object CopyAll() {
      if (!Open()) {
        return null;
      }
      try {
        List<KeyValuePair<uint, byte[]>> formats = new List<KeyValuePair<uint, byte[]>>();
        int seen = 0;
        for (uint format = EnumClipboardFormats(0); format != 0; format = EnumClipboardFormats(format)) {
          seen++;
          // 0x0200-0x02FF are private handles, 0x0300-0x03FF GDI objects.
          if (NotCopyable.Contains(format) || (format >= 0x0200 && format <= 0x03FF)) {
            continue;
          }
          byte[] bytes = Read(GetClipboardData(format));
          if (bytes != null) {
            formats.Add(new KeyValuePair<uint, byte[]>(format, bytes));
          }
        }
        return seen > 0 && formats.Count == 0 ? null : formats;
      } finally {
        CloseClipboard();
      }
    }

    /// <summary>Puts the text on the clipboard with the formats that keep it out of clipboard history and cloud sync.</summary>
    public bool SetPrivateText(string text) {
      if (!Open()) {
        return false;
      }
      try {
        EmptyClipboard();
        byte[] chars = System.Text.Encoding.Unicode.GetBytes((text ?? "") + "\0");
        if (!Set(CF_UNICODETEXT, chars)) {
          return false;
        }
        byte[] zero = BitConverter.GetBytes(0);
        return Set(RegisterClipboardFormat("ExcludeClipboardContentFromMonitorProcessing"), zero)
          && Set(RegisterClipboardFormat("CanIncludeInClipboardHistory"), zero)
          && Set(RegisterClipboardFormat("CanUploadToCloudClipboard"), zero);
      } finally {
        CloseClipboard();
      }
    }

    /// <summary>Puts back a copy made by <see cref="CopyAll"/>.</summary>
    public bool RestoreCopy(object copy) {
      if (!(copy is List<KeyValuePair<uint, byte[]>> formats) || !Open()) {
        return false;
      }
      try {
        EmptyClipboard();
        bool all = true;
        foreach (KeyValuePair<uint, byte[]> format in formats) {
          all &= Set(format.Key, format.Value);
        }
        return all;
      } finally {
        CloseClipboard();
      }
    }

    /// <summary>Empties the clipboard.</summary>
    public bool Clear() {
      if (!Open()) {
        return false;
      }
      try {
        return EmptyClipboard();
      } finally {
        CloseClipboard();
      }
    }

    // Another program may hold the clipboard for a moment: try a few times.
    private bool Open() {
      for (int i = 0; i < 10; i++) {
        if (OpenClipboard(Owner)) {
          return true;
        }
        System.Threading.Thread.Sleep(20);
      }
      AppLog.Warning(null, "The clipboard could not be opened: error {ErrorCode}", Marshal.GetLastWin32Error());
      return false;
    }

    private static byte[] Read(IntPtr handle) {
      if (handle == IntPtr.Zero) {
        return null;
      }
      long size = (long)GlobalSize(handle);
      if (size <= 0) {
        return null;
      }
      IntPtr data = GlobalLock(handle);
      if (data == IntPtr.Zero) {
        return null;
      }
      try {
        byte[] bytes = new byte[size];
        Marshal.Copy(data, bytes, 0, bytes.Length);
        return bytes;
      } finally {
        GlobalUnlock(handle);
      }
    }

    // On success the clipboard owns the memory; otherwise it is freed here.
    private static bool Set(uint format, byte[] bytes) {
      if (format == 0) {
        return false;
      }
      IntPtr memory = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)Math.Max(1, bytes.Length));
      if (memory == IntPtr.Zero) {
        return false;
      }
      IntPtr data = GlobalLock(memory);
      if (data == IntPtr.Zero) {
        GlobalFree(memory);
        return false;
      }
      Marshal.Copy(bytes, 0, data, bytes.Length);
      GlobalUnlock(memory);
      if (SetClipboardData(format, memory) == IntPtr.Zero) {
        GlobalFree(memory);
        return false;
      }
      return true;
    }
  }
}
