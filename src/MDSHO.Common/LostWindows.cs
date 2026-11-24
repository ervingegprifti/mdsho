using System;
using System.Collections.Generic;
using System.Linq;

namespace MDSHO.Common {

  /// <summary>
  /// A window is lost when its title bar can't be grabbed on any screen (for example it opened at the position of a screen
  /// that is no longer connected). Notes and comments use it to open where they can be seen.
  /// </summary>
  public static class LostWindows {
    /// <summary>The height of the strip at the top of a window that counts as its title bar.</summary>
    public const double TitleBarHeight = 32;
    /// <summary>How much of the title bar must be on a screen, in width and height, to grab it (less for a smaller window).</summary>
    public const double MinGrabWidth = 64;
    /// <summary>See <see cref="MinGrabWidth"/>.</summary>
    public const double MinGrabHeight = 16;

    /// <summary>True when not enough of the window's title bar is inside any work area to drag the window back.</summary>
    public static bool IsLost(ScreenRect window, IEnumerable<ScreenRect> workAreas) {
      List<ScreenRect> areas = workAreas.Where(a => !a.IsEmpty).ToList();
      if (areas.Count == 0) {
        return false;
      }
      ScreenRect titleBar = new ScreenRect(window.X, window.Y, window.Width, Math.Min(window.Height, TitleBarHeight));
      double needWidth = Math.Min(MinGrabWidth, titleBar.Width);
      double needHeight = Math.Min(MinGrabHeight, titleBar.Height);
      foreach (ScreenRect area in areas) {
        ScreenRect visible = ScreenRect.Intersect(titleBar, area);
        if (!visible.IsEmpty && visible.Width >= needWidth && visible.Height >= needHeight) {
          return false;
        }
      }
      return true;
    }
  }
}
