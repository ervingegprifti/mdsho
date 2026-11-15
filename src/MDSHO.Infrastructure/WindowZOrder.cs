using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

// GetWindow: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getwindow
// SetWindowPos: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowpos
// SetForegroundWindow: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setforegroundwindow

namespace MDSHO.Infrastructure {

  /// <summary>Win32 calls for the z-order and the foreground window.</summary>
  public static class WindowZOrder {
    private const uint GW_HWNDNEXT = 2;
    private const uint GW_HWNDPREV = 3;
    private const int GWL_EXSTYLE = -20;
    private const long WS_EX_TOPMOST = 0x00000008;
    private const long WS_EX_TOOLWINDOW = 0x00000080;
    private const long WS_EX_APPWINDOW = 0x00040000;
    private const int DWMWA_CLOAKED = 14;
    private const uint SWP_NOSIZE = 0x0001, SWP_NOMOVE = 0x0002, SWP_NOACTIVATE = 0x0010;
    private const int SW_RESTORE = 9;
    // Guards against a z-order list that changes while we walk it.
    private const int MaxWalk = 10000;

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")]
    private static extern IntPtr GetWindow(IntPtr hWnd, uint command);
    [DllImport("user32.dll")]
    private static extern IntPtr GetTopWindow(IntPtr hWnd);
    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr hWnd);
    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);
    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int command);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int index);
    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr insertAfter, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder className, int maxCount);
    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr hWnd, int attribute, out int value, int size);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int left, top, right, bottom; }

    /// <summary>The window in front, or zero.</summary>
    public static IntPtr Foreground() {
      return GetForegroundWindow();
    }

    /// <summary>True for any window of this process.</summary>
    public static bool IsOwnWindow(IntPtr window) {
      if (window == IntPtr.Zero) {
        return false;
      }
      GetWindowThreadProcessId(window, out uint processId);
      return processId == (uint)Environment.ProcessId;
    }

    /// <summary>True while the window exists.</summary>
    public static bool IsAlive(IntPtr window) {
      return window != IntPtr.Zero && IsWindow(window);
    }

    /// <summary>True for an always-on-top window.</summary>
    public static bool IsTopmost(IntPtr window) {
      return (GetWindowLongPtr(window, GWL_EXSTYLE).ToInt64() & WS_EX_TOPMOST) != 0;
    }

    /// <summary>The visible window right above this one, or zero.</summary>
    public static IntPtr VisibleWindowAbove(IntPtr window) {
      IntPtr above = window;
      for (int i = 0; i < MaxWalk; i++) {
        above = GetWindow(above, GW_HWNDPREV);
        if (above == IntPtr.Zero || IsWindowVisible(above)) {
          return above;
        }
      }
      return IntPtr.Zero;
    }

    /// <summary>The windows shown above this one, nearest first.</summary>
    public static IList<IntPtr> ShownWindowsAbove(IntPtr window) {
      return Walk(window, GW_HWNDPREV);
    }

    /// <summary>The windows shown below this one, nearest first.</summary>
    public static IList<IntPtr> ShownWindowsBelow(IntPtr window) {
      return Walk(window, GW_HWNDNEXT);
    }

    private static IList<IntPtr> Walk(IntPtr window, uint direction) {
      List<IntPtr> shown = new List<IntPtr>();
      IntPtr current = window;
      for (int i = 0; i < MaxWalk; i++) {
        current = GetWindow(current, direction);
        if (current == IntPtr.Zero) {
          break;
        }
        if (IsShown(current)) {
          shown.Add(current);
        }
      }
      return shown;
    }

    /// <summary>
    /// True for a window the user can see. Cloaked windows are on another virtual desktop or are suspended Store apps.
    /// The desktop itself (Progman, WorkerW) and tool windows don't count.
    /// </summary>
    public static bool IsShown(IntPtr window) {
      if (!IsWindowVisible(window) || IsIconic(window)) {
        return false;
      }
      long exStyle = GetWindowLongPtr(window, GWL_EXSTYLE).ToInt64();
      if ((exStyle & WS_EX_TOOLWINDOW) != 0 && (exStyle & WS_EX_APPWINDOW) == 0) {
        return false;
      }
      if (DwmGetWindowAttribute(window, DWMWA_CLOAKED, out int cloaked, sizeof(int)) == 0 && cloaked != 0) {
        return false;
      }
      if (!GetWindowRect(window, out RECT r) || r.right <= r.left || r.bottom <= r.top) {
        return false;
      }
      System.Text.StringBuilder className = new System.Text.StringBuilder(64);
      GetClassName(window, className, className.Capacity);
      string name = className.ToString();
      return name != "Progman" && name != "WorkerW";
    }

    /// <summary>These windows in z-order, top first. Windows not found come last.</summary>
    public static IList<IntPtr> TopFirst(IEnumerable<IntPtr> windows) {
      List<IntPtr> wanted = windows.ToList();
      Dictionary<IntPtr, int> rank = new Dictionary<IntPtr, int>();
      IntPtr current = GetTopWindow(IntPtr.Zero);
      for (int i = 0; i < MaxWalk && current != IntPtr.Zero && rank.Count < wanted.Count; i++) {
        if (wanted.Contains(current) && !rank.ContainsKey(current)) {
          rank[current] = i;
        }
        current = GetWindow(current, GW_HWNDNEXT);
      }
      return wanted.OrderBy(w => rank.TryGetValue(w, out int r) ? r : int.MaxValue).ToList();
    }

    /// <summary>Puts the window behind <paramref name="insertAfter"/> without activating it. Zero means on top.</summary>
    public static void PlaceBehind(IntPtr window, IntPtr insertAfter) {
      SetWindowPos(window, insertAfter, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
    }

    /// <summary>Restores the window if minimized and brings it to the front with the focus.</summary>
    public static void Activate(IntPtr window) {
      if (IsIconic(window)) {
        ShowWindow(window, SW_RESTORE);
      }
      SetForegroundWindow(window);
    }
  }
}
