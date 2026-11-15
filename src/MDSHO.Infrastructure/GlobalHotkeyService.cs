using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using MDSHO.Common;

// RegisterHotKey: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey
// WM_HOTKEY: https://learn.microsoft.com/en-us/windows/win32/inputdev/wm-hotkey
// Message-only windows: https://learn.microsoft.com/en-us/windows/win32/winmsg/window-features#message-only-windows

namespace MDSHO.Infrastructure {

  /// <summary>
  /// One global hotkey registered with Windows on a message-only window, created on first use on the calling (UI) thread.
  /// <see cref="Pressed"/> is raised on that thread. MOD_NOREPEAT: holding the keys down raises it once.
  /// </summary>
  public sealed class GlobalHotkeyService : IGlobalHotkeyService {
    private const int WM_HOTKEY = 0x0312;
    private const int HotkeyId = 0x4D44;
    private const uint MOD_NOREPEAT = 0x4000;
    private static readonly IntPtr HWND_MESSAGE = new IntPtr(-3);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint virtualKey);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private MessageWindow window;
    private bool registered;

    /// <inheritdoc/>
    public event Action Pressed;

    /// <inheritdoc/>
    public bool Register(Hotkey hotkey) {
      if (hotkey == null) {
        throw new ArgumentNullException(nameof(hotkey));
      }
      Unregister();
      window ??= new MessageWindow(this);
      registered = RegisterHotKey(window.Handle, HotkeyId, (uint)hotkey.Modifiers | MOD_NOREPEAT, (uint)hotkey.Key);
      return registered;
    }

    /// <inheritdoc/>
    public void Unregister() {
      if (registered) {
        UnregisterHotKey(window.Handle, HotkeyId);
        registered = false;
      }
    }

    /// <summary>Releases the keys and destroys the message window.</summary>
    public void Dispose() {
      Unregister();
      window?.DestroyHandle();
      window = null;
    }

    private void OnHotkey() {
      try {
        Pressed?.Invoke();
      } catch (Exception ex) {
        AppLog.Error(ex, "The Bring panels to front shortcut failed");
      }
    }

    private sealed class MessageWindow : NativeWindow {
      private readonly GlobalHotkeyService owner;

      public MessageWindow(GlobalHotkeyService owner) {
        this.owner = owner;
        CreateHandle(new CreateParams { Parent = HWND_MESSAGE });
      }

      protected override void WndProc(ref Message m) {
        if (m.Msg == WM_HOTKEY && m.WParam == (IntPtr)HotkeyId) {
          owner.OnHotkey();
          return;
        }
        base.WndProc(ref m);
      }
    }
  }
}
