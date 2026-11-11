using System;
using System.Collections.Generic;
using System.Linq;

namespace MDSHO.Common {

  /// <summary>A top-level window of another app, as "Find Other Lost Windows" sees it.</summary>
  /// <param name="Handle">The window handle.</param>
  /// <param name="Title">The window title (for logging and tests only).</param>
  /// <param name="Bounds">Where the window is, in physical pixels.</param>
  /// <param name="IsMaximized">True when the window is maximized; it is restored before it moves.</param>
  public sealed record OtherWindow(IntPtr Handle, string Title, ScreenRect Bounds, bool IsMaximized);

  /// <summary>Lists and moves the windows of other apps.</summary>
  public interface IOtherWindowsService {
    /// <summary>
    /// The visible, normal (not minimized) top-level windows of other apps on the current virtual desktop: windows with a
    /// title bar, without tool windows, hidden (cloaked) windows and MDSHO's own windows.
    /// </summary>
    IList<OtherWindow> List();

    /// <summary>Moves (and if needed resizes) a window to <paramref name="bounds"/>; false when Windows refused, for example for an app running as administrator.</summary>
    bool MoveTo(OtherWindow window, ScreenRect bounds);
  }

  /// <summary>What "Find Other Lost Windows" did.</summary>
  /// <param name="Moved">Windows moved onto the target screen.</param>
  /// <param name="Skipped">Lost windows that Windows didn't let MDSHO move.</param>
  public sealed record LostWindowsResult(int Moved, int Skipped) {

    /// <summary>The message shown afterwards.</summary>
    public string Message {
      get {
        if (Moved == 0 && Skipped == 0) {
          return "No lost windows found. Every window is on a screen.";
        }
        List<string> lines = new List<string>();
        if (Moved > 0) {
          lines.Add("Moved " + Count(Moved) + " to this screen.");
        }
        if (Skipped > 0) {
          lines.Add("Skipped " + Count(Skipped) + " that Windows didn't let MDSHO move, for example "
            + (Skipped == 1 ? "an app" : "apps") + " running as administrator.");
        }
        return string.Join(Environment.NewLine, lines);
      }
    }

    private static string Count(int n) {
      return n + (n == 1 ? " window" : " windows");
    }
  }

  /// <summary>
  /// "Find Other Lost Windows": a window of another app is lost when its title bar can't be grabbed on any screen (for
  /// example it opened at the position of a screen that is no longer connected). Lost windows move to the middle of the
  /// target screen, keeping their size unless they are bigger than the screen, and cascade so none hides another.
  /// </summary>
  public static class LostWindows {
    /// <summary>The height of the strip at the top of a window that counts as its title bar.</summary>
    public const double TitleBarHeight = 32;
    /// <summary>How much of the title bar must be on a screen, in width and height, to grab it (less for a smaller window).</summary>
    public const double MinGrabWidth = 64;
    /// <summary>See <see cref="MinGrabWidth"/>.</summary>
    public const double MinGrabHeight = 16;
    /// <summary>How far each further window is shifted right and down.</summary>
    public const double CascadeStep = 32;

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

    /// <summary>Where the <paramref name="index"/>-th lost window goes on <paramref name="workArea"/>: centred, cascaded, always fully inside.</summary>
    public static ScreenRect Place(ScreenRect window, ScreenRect workArea, int index) {
      double width = Math.Min(window.Width, workArea.Width);
      double height = Math.Min(window.Height, workArea.Height);
      double x = workArea.X + (workArea.Width - width) / 2;
      double y = workArea.Y + (workArea.Height - height) / 2;
      // How many cascade steps still fit before a window would leave the work area; then start again from the middle.
      double room = Math.Min(workArea.Right - width - x, workArea.Bottom - height - y);
      int steps = room >= CascadeStep ? (int)(room / CascadeStep) + 1 : 1;
      double offset = (index % steps) * CascadeStep;
      return new ScreenRect(x + offset, y + offset, width, height);
    }

    /// <summary>Moves every lost window of another app onto <paramref name="target"/> and says how many moved and how many Windows refused.</summary>
    public static LostWindowsResult Rescue(IOtherWindowsService windows, IEnumerable<ScreenInfo> screens, ScreenInfo target) {
      List<ScreenRect> workAreas = screens.Select(s => s.WorkingArea).ToList();
      int moved = 0;
      int skipped = 0;
      int index = 0;
      foreach (OtherWindow window in windows.List()) {
        if (!IsLost(window.Bounds, workAreas)) {
          continue;
        }
        ScreenRect bounds = Place(window.Bounds, target.WorkingArea, index);
        index++;
        if (windows.MoveTo(window, bounds)) {
          moved++;
        } else {
          skipped++;
        }
      }
      return new LostWindowsResult(moved, skipped);
    }
  }
}
