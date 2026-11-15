using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

// GetWindow: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getwindow
// SetWindowPos: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowpos
// SetForegroundWindow: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setforegroundwindow

namespace MDSHO.Infrastructure {

  /// <summary>Thin Win32 calls on top-level windows for "Bring panels to front": the z-order and the window in front.</summary>
  public static class WindowZOrder {
    private const uint GW_HWNDNEXT = 2;
    private const uint GW_HWNDPREV = 3;
    private const int GWL_EXSTYLE = -20;
    private const long WS_EX_TOPMOST = 0x00000008;
    private const uint SWP_NOSIZE = 0x0001, SWP_NOMOVE = 0x0002, SWP_NOACTIVATE = 0x0010;
    private const int SW_RESTORE = 9;
    // A z-order walk never needs more steps than there are windows; this only guards against a changing list.
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

    /// <summary>The window in front, or zero.</summary>
    public static IntPtr Foreground() {
      return GetForegroundWindow();
    }

    /// <summary>True for a window of this process (a panel, a note, a message box, the tray menu).</summary>
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

    /// <summary>The visible top-level window right above this one, or zero when none is.</summary>
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

    /// <summary>These windows in z-order, the top one first (windows not found in the walk come last).</summary>
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

    /// <summary>Puts the window right behind <paramref name="insertAfter"/> (zero = HWND_TOP) without activating it.</summary>
    public static void PlaceBehind(IntPtr window, IntPtr insertAfter) {
      SetWindowPos(window, insertAfter, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
    }

    /// <summary>Brings the window to the front with the keyboard focus (restored first when it is minimized).</summary>
    public static void Activate(IntPtr window) {
      if (IsIconic(window)) {
        ShowWindow(window, SW_RESTORE);
      }
      SetForegroundWindow(window);
    }
  }
}
