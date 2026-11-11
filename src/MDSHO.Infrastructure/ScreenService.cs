using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using MDSHO.Common;

namespace MDSHO.Infrastructure {

  /// <summary>Reads the monitors: bounds and working area in physical pixels, and each monitor's own DPI scale.</summary>
  public sealed class ScreenService : IScreenService {

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int left, top, right, bottom; }
    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromRect(ref RECT lprc, uint dwFlags);
    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

    /// <summary>Every connected screen.</summary>
    public IList<ScreenInfo> AllScreens() {
      List<ScreenInfo> list = new List<ScreenInfo>();
      foreach (Screen screen in Screen.AllScreens) {
        list.Add(ToScreenInfo(screen));
      }
      return list;
    }

    /// <summary>The primary screen.</summary>
    public ScreenInfo PrimaryScreen() {
      return ToScreenInfo(Screen.PrimaryScreen);
    }

    /// <summary>The screen a window (by its handle) is mostly on.</summary>
    public ScreenInfo ScreenFromHandle(IntPtr windowHandle) {
      return ToScreenInfo(Screen.FromHandle(windowHandle));
    }

    /// <summary>Describes one screen: bounds and working area in pixels, its scale and whether it is the primary one.</summary>
    /// <summary>The screen the mouse pointer is on.</summary>
    public ScreenInfo ScreenAtCursor() {
      return ToScreenInfo(Screen.FromPoint(Cursor.Position));
    }

    private static ScreenInfo ToScreenInfo(Screen screen) {
      System.Drawing.Rectangle b = screen.Bounds;
      System.Drawing.Rectangle w = screen.WorkingArea;
      return new ScreenInfo(screen.DeviceName,
        new ScreenRect(b.Left, b.Top, b.Width, b.Height),
        new ScreenRect(w.Left, w.Top, w.Width, w.Height),
        ScaleOf(screen), screen.Primary);
    }

    /// <summary>The display scale of the screen's monitor (1.0 = 100%, 1.5 = 150%), or 1.0 when it cannot be read.</summary>
    private static double ScaleOf(Screen screen) {
      try {
        RECT r = new RECT { left = screen.Bounds.Left, top = screen.Bounds.Top, right = screen.Bounds.Right, bottom = screen.Bounds.Bottom };
        IntPtr monitor = MonitorFromRect(ref r, 2 /* MONITOR_DEFAULTTONEAREST */);
        if (monitor != IntPtr.Zero && GetDpiForMonitor(monitor, 0 /* MDT_EFFECTIVE_DPI */, out uint dpiX, out uint _) == 0 && dpiX > 0) {
          return dpiX / 96.0;
        }
      } catch (Exception) {
        // Fall through to 100%.
      }
      return 1.0;
    }
  }
}
