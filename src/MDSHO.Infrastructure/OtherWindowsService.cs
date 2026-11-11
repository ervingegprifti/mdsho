using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using MDSHO.Common;

namespace MDSHO.Infrastructure {

  /// <summary>Lists and moves the top-level windows of other apps through the Windows API (see <see cref="IOtherWindowsService"/>).</summary>
  public sealed class OtherWindowsService : IOtherWindowsService {
    private const int GWL_STYLE = -16;
    private const int GWL_EXSTYLE = -20;
    private const long WS_CHILD = 0x40000000L;
    private const long WS_CAPTION = 0x00C00000L;
    private const long WS_EX_TOOLWINDOW = 0x00000080L;
    private const long WS_EX_APPWINDOW = 0x00040000L;
    private const int DWMWA_CLOAKED = 14;
    private const int SW_RESTORE = 9;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_NOOWNERZORDER = 0x0200;
    /// <summary>A move counts as done when the window ends up this close (in pixels) to where it was sent.</summary>
    private const double MoveTolerance = 2;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int left, top, right, bottom; }

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);
    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);
    [DllImport("user32.dll")]
    private static extern bool IsZoomed(IntPtr hWnd);
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int index);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextLength(IntPtr hWnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int maxCount);
    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int command);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr insertAfter, int x, int y, int width, int height, uint flags);
    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr hWnd, int attribute, out int value, int size);

    private readonly uint excludedProcessId;

    /// <summary>Creates the service; windows of <paramref name="excludedProcessId"/> (by default this process, so MDSHO's own windows) are never listed.</summary>
    public OtherWindowsService(uint? excludedProcessId = null) {
      this.excludedProcessId = excludedProcessId ?? (uint)Environment.ProcessId;
    }

    /// <inheritdoc />
    public IList<OtherWindow> List() {
      List<OtherWindow> windows = new List<OtherWindow>();
      EnumWindows((hWnd, _) => {
        try {
          OtherWindow window = Describe(hWnd);
          if (window != null) {
            windows.Add(window);
          }
        } catch (Exception ex) {
          Debug.WriteLine(ex);
        }
        return true;
      }, IntPtr.Zero);
      return windows;
    }

    private OtherWindow Describe(IntPtr hWnd) {
      if (!IsWindowVisible(hWnd) || IsIconic(hWnd)) {
        return null;
      }
      GetWindowThreadProcessId(hWnd, out uint processId);
      if (excludedProcessId != 0 && processId == excludedProcessId) {
        return null;
      }
      long style = GetWindowLongPtr(hWnd, GWL_STYLE).ToInt64();
      long exStyle = GetWindowLongPtr(hWnd, GWL_EXSTYLE).ToInt64();
      if ((style & WS_CHILD) != 0 || (style & WS_CAPTION) != WS_CAPTION) {
        return null;
      }
      if ((exStyle & WS_EX_TOOLWINDOW) != 0 && (exStyle & WS_EX_APPWINDOW) == 0) {
        return null;
      }
      // Windows on another virtual desktop, and suspended Store app windows, are hidden by "cloaking", not moved off screen.
      if (DwmGetWindowAttribute(hWnd, DWMWA_CLOAKED, out int cloaked, sizeof(int)) == 0 && cloaked != 0) {
        return null;
      }
      string title = Title(hWnd);
      if (string.IsNullOrWhiteSpace(title) || !GetWindowRect(hWnd, out RECT r) || r.right <= r.left || r.bottom <= r.top) {
        return null;
      }
      return new OtherWindow(hWnd, title, ToScreenRect(r), IsZoomed(hWnd));
    }

    /// <inheritdoc />
    public bool MoveTo(OtherWindow window, ScreenRect bounds) {
      if (window.IsMaximized) {
        ShowWindow(window.Handle, SW_RESTORE);
      }
      int x = (int)Math.Round(bounds.X);
      int y = (int)Math.Round(bounds.Y);
      if (!SetWindowPos(window.Handle, IntPtr.Zero, x, y, (int)Math.Round(bounds.Width), (int)Math.Round(bounds.Height),
          SWP_NOZORDER | SWP_NOACTIVATE | SWP_NOOWNERZORDER)) {
        return false;
      }
      // Windows may "succeed" without moving a window it protects; check where the window really is.
      return GetWindowRect(window.Handle, out RECT now) && Math.Abs(now.left - x) <= MoveTolerance && Math.Abs(now.top - y) <= MoveTolerance;
    }

    private static string Title(IntPtr hWnd) {
      int length = GetWindowTextLength(hWnd);
      if (length <= 0) {
        return string.Empty;
      }
      StringBuilder text = new StringBuilder(length + 1);
      GetWindowText(hWnd, text, text.Capacity);
      return text.ToString();
    }

    private static ScreenRect ToScreenRect(RECT r) {
      return new ScreenRect(r.left, r.top, r.right - r.left, r.bottom - r.top);
    }
  }
}
