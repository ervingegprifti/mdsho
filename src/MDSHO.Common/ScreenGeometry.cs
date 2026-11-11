using System;
using System.Collections.Generic;

namespace MDSHO.Common {

  /// <summary>A monitor: Bounds and WorkingArea are PHYSICAL pixels (as WinForms Screen reports them), Scale = DPI / 96 of that monitor.</summary>
  public sealed class ScreenInfo {
    /// <summary>Windows name of the screen, such as the display device path.</summary>
    public string DeviceName { get; }
    /// <summary>Whole screen in physical pixels.</summary>
    public ScreenRect Bounds { get; }
    /// <summary>Screen without the taskbar in physical pixels.</summary>
    public ScreenRect WorkingArea { get; }
    /// <summary>Display scale (1.0 = 100%).</summary>
    public double Scale { get; }
    /// <summary>True for the primary screen.</summary>
    public bool IsPrimary { get; }
    /// <summary>Creates the description of a screen.</summary>
    public ScreenInfo(string deviceName, ScreenRect bounds, ScreenRect workingArea, double scale, bool isPrimary) {
      DeviceName = deviceName;
      Bounds = bounds;
      WorkingArea = workingArea;
      Scale = scale <= 0 ? 1.0 : scale;
      IsPrimary = isPrimary;
    }
    /// <summary>Whole screen in device independent pixels, the unit window positions are saved in.</summary>
    public ScreenRect BoundsDip => ScreenGeometry.PhysicalToDip(Bounds, Scale);
    /// <summary>Screen without the taskbar in device independent pixels.</summary>
    public ScreenRect WorkingAreaDip => ScreenGeometry.PhysicalToDip(WorkingArea, Scale);
  }

  /// <summary>Pure geometry for window placement. Window values (Left/Top/Width/Height, database) are DIPs; screens are physical pixels converted with each monitor's own scale.</summary>
  public static class ScreenGeometry {
    /// <summary>Height of the title-bar strip that must stay reachable.</summary>
    public const double TITLE_BAR_HEIGHT = 32;
    /// <summary>Minimum width (DIPs) of the title-bar strip that must be inside a working area.</summary>
    public const double MIN_VISIBLE_WIDTH = 100;
    private const double MIN_VISIBLE_HEIGHT = 16;

    /// <summary>Converts a rectangle from physical pixels to device independent pixels using the screen scale.</summary>
    public static ScreenRect PhysicalToDip(ScreenRect r, double scale) {
      return new ScreenRect(r.Left / scale, r.Top / scale, r.Width / scale, r.Height / scale);
    }
    /// <summary>Converts a rectangle from device independent pixels to physical pixels using the screen scale.</summary>
    public static ScreenRect DipToPhysical(ScreenRect r, double scale) {
      return new ScreenRect(r.Left * scale, r.Top * scale, r.Width * scale, r.Height * scale);
    }

    /// <summary>Top-left (DIPs, truncated like the old int math) of a width x height DIP window centered on the screen.</summary>
    public static ScreenPoint CenterOnScreen(ScreenInfo screen, double widthDip, double heightDip) {
      ScreenRect b = screen.BoundsDip;
      return new ScreenPoint(Math.Truncate(b.Left + (b.Width - widthDip) / 2), Math.Truncate(b.Top + (b.Height - heightDip) / 2));
    }

    /// <summary>Top-left (DIPs) of a new window, at its initial size, centred on the screen.</summary>
    public static ScreenPoint CenterNewWindow(ScreenInfo screen) {
      return CenterOnScreen(screen, AppConstants.InitialWindowWidth, AppConstants.InitialWindowHeight);
    }

    /// <summary>
    /// Top-left (DIPs) of a width x height window centred in the work area. A window bigger than the work area in one
    /// direction is put at the work area's left or top edge in that direction, so its title bar stays on screen.
    /// </summary>
    public static ScreenPoint CenterInWorkArea(ScreenRect workAreaDip, double widthDip, double heightDip) {
      double left = workAreaDip.Left + (workAreaDip.Width - widthDip) / 2;
      double top = workAreaDip.Top + (workAreaDip.Height - heightDip) / 2;
      return new ScreenPoint(Math.Max(workAreaDip.Left, left), Math.Max(workAreaDip.Top, top));
    }

    /// <summary>Top-left (DIPs) of a width x height window centred in the screen's working area (without the taskbar).</summary>
    public static ScreenPoint CenterInWorkArea(ScreenInfo screen, double widthDip, double heightDip) {
      return CenterInWorkArea(screen.WorkingAreaDip, widthDip, heightDip);
    }

    /// <summary>
    /// Top-left (whole PHYSICAL pixels) of a window of the given physical size centred in the screen's working area,
    /// clamped to the work area's left or top edge when the window is bigger. Used to place a window through its handle,
    /// which works on any monitor whatever its scale.
    /// </summary>
    public static ScreenPoint CenterInWorkAreaPhysical(ScreenInfo screen, double widthPx, double heightPx) {
      ScreenRect w = screen.WorkingArea;
      double left = Math.Floor(w.Left + (w.Width - widthPx) / 2);
      double top = Math.Floor(w.Top + (w.Height - heightPx) / 2);
      return new ScreenPoint(Math.Max(w.Left, left), Math.Max(w.Top, top));
    }

    /// <summary>
    /// Top-left (whole PHYSICAL pixels) of a window of the given physical size placed beside another window, <paramref name="gapPx"/>
    /// away: on its right when there is room, otherwise on its left, otherwise on the side with more room. The tops line up,
    /// and the window is kept inside the work area.
    /// </summary>
    public static ScreenPoint BesideWindowPhysical(ScreenRect ownerPx, double widthPx, double heightPx, ScreenRect workAreaPx, double gapPx) {
      double right = ownerPx.Right + gapPx;
      double left = ownerPx.Left - gapPx - widthPx;
      double x;
      if (right + widthPx <= workAreaPx.Right) {
        x = right;
      } else if (left >= workAreaPx.Left) {
        x = left;
      } else {
        x = (workAreaPx.Right - ownerPx.Right) >= (ownerPx.Left - workAreaPx.Left) ? right : left;
      }
      x = Math.Max(workAreaPx.Left, Math.Min(x, workAreaPx.Right - widthPx));
      double y = Math.Max(workAreaPx.Top, Math.Min(ownerPx.Top, workAreaPx.Bottom - heightPx));
      return new ScreenPoint(Math.Floor(x), Math.Floor(y));
    }

    /// <summary>ScreenRect (DIPs) filling the working area height, at its left edge.</summary>
    public static ScreenRect DockLeft(ScreenInfo screen, double widthDip) {
      ScreenRect w = screen.WorkingAreaDip;
      return new ScreenRect(w.Left, w.Top, widthDip, w.Height);
    }
    /// <summary>ScreenRect (DIPs) filling the working area height, at its right edge.</summary>
    public static ScreenRect DockRight(ScreenInfo screen, double widthDip) {
      ScreenRect w = screen.WorkingAreaDip;
      return new ScreenRect(w.Right - widthDip, w.Top, widthDip, w.Height);
    }

    /// <summary>
    /// True when the window rect (DIPs) is docked to the left or right edge of the screen's working area: it fills the
    /// working area height and touches its left or right edge. Allows 1 DIP for positions saved as whole numbers.
    /// </summary>
    public static bool IsDockedToSide(ScreenRect windowDip, ScreenInfo screen) {
      const double tolerance = 1.0;
      ScreenRect w = screen.WorkingAreaDip;
      bool fullHeight = Math.Abs(windowDip.Top - w.Top) <= tolerance && Math.Abs(windowDip.Bottom - w.Bottom) <= tolerance;
      bool atLeft = Math.Abs(windowDip.Left - w.Left) <= tolerance;
      bool atRight = Math.Abs(windowDip.Right - w.Right) <= tolerance;
      return fullHeight && (atLeft || atRight);
    }

    /// <summary>True if the title-bar strip of the window rect (DIPs) sufficiently intersects the working area of any screen.</summary>
    public static bool IsTitleBarReachable(ScreenRect windowDip, IEnumerable<ScreenInfo> screens) {
      ScreenRect strip = new ScreenRect(windowDip.Left, windowDip.Top, windowDip.Width, Math.Min(windowDip.Height, TITLE_BAR_HEIGHT));
      foreach (ScreenInfo screen in screens) {
        ScreenRect i = ScreenRect.Intersect(strip, screen.WorkingAreaDip);
        if (!i.IsEmpty && i.Width >= Math.Min(strip.Width, MIN_VISIBLE_WIDTH) && i.Height >= Math.Min(strip.Height, MIN_VISIBLE_HEIGHT)) {
          return true;
        }
      }
      return false;
    }

    /// <summary>Returns the rect unchanged if reachable; otherwise moves it to the named screen (else primary, else first), shrunk and clamped into its working area.</summary>
    public static ScreenRect EnsureOnScreen(ScreenRect windowDip, string screenDeviceName, IList<ScreenInfo> screens) {
      if (screens == null || screens.Count == 0 || IsTitleBarReachable(windowDip, screens)) {
        return windowDip;
      }
      ScreenInfo target = null;
      foreach (ScreenInfo s in screens) {
        if (string.Equals(s.DeviceName, screenDeviceName, StringComparison.OrdinalIgnoreCase)) { target = s; break; }
      }
      if (target == null) {
        foreach (ScreenInfo s in screens) {
          if (s.IsPrimary) { target = s; break; }
        }
      }
      if (target == null) { target = screens[0]; }
      ScreenRect wa = target.WorkingAreaDip;
      double width = Math.Min(windowDip.Width, wa.Width);
      double height = Math.Min(windowDip.Height, wa.Height);
      double left = Math.Max(wa.Left, Math.Min(windowDip.Left, wa.Right - width));
      double top = Math.Max(wa.Top, Math.Min(windowDip.Top, wa.Bottom - height));
      return new ScreenRect(left, top, width, height);
    }
  }
}
