using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace MDSHO.Common {

  /// <summary>What kind of window a remembered place belongs to. The numbers are stored in the database.</summary>
  public enum PlacedKind {
    /// <summary>A panel (MDSHOWindows.ID).</summary>
    Panel = 0,
    /// <summary>A clone of a panel (MDSHOWindowClones.ID).</summary>
    Clone = 1,
    /// <summary>A note window (the note button's Shortcuts.ID).</summary>
    Note = 2
  }

  /// <summary>Whether a window is docked to a side of its screen's working area. The numbers are stored in the database.</summary>
  public enum DockSide {
    /// <summary>Not docked.</summary>
    None = 0,
    /// <summary>Docked to the left edge.</summary>
    Left = 1,
    /// <summary>Docked to the right edge.</summary>
    Right = 2
  }

  /// <summary>Where a window is (or should go) in one screen setup: its screen, its normal place and size (DIPs) and its dock side.</summary>
  /// <param name="ScreenDeviceName">The screen the window is mostly on.</param>
  /// <param name="Rect">Left, top, width and height in DIPs, like the saved window positions.</param>
  /// <param name="Dock">The side it is docked to, if any.</param>
  public sealed record ItemPlace(string ScreenDeviceName, ScreenRect Rect, DockSide Dock = DockSide.None);

  /// <summary>
  /// A screen setup: the connected screens' resolutions, which one is primary, and how they are arranged (which screens
  /// are left of, right of, above or below which). Device names (<c>\\.\DISPLAYn</c>, which Windows renumbers when
  /// screens are reconnected or a laptop is docked again), exact offsets, the working area and the display scale don't
  /// count, so dragging a monitor a little in Display settings or moving the taskbar is still the same setup. Two
  /// identical monitors swapped are the same setup too.
  /// </summary>
  public sealed class ScreenSetup {
    /// <summary>
    /// Identifies the setup: every screen as <c>widthxheight</c>, a <c>*</c> for the primary screen, then <c>@c</c> its column
    /// and <c>r</c> its row, joined with <c>|</c> in column, then row order, for example
    /// <c>1920x1080*@c0r0|2560x1440@c1r0</c> (a monitor right of the primary laptop screen). The column is the number of
    /// screens entirely left of the screen, the row the number entirely above it, so only the order along each axis counts,
    /// not the pixel offsets. Empty without screens.
    /// </summary>
    public string Key { get; }
    /// <summary>
    /// The name shown in the tray menu: the number of screens and their resolutions, primary screen first, then the
    /// others from left to right, for example "2 screens: 1920×1080, 2560×1440".
    /// </summary>
    public string Name { get; }
    /// <summary>How many screens the setup has.</summary>
    public int ScreenCount { get; }

    private ScreenSetup(string key, string name, int screenCount) {
      Key = key;
      Name = name;
      ScreenCount = screenCount;
    }

    private static readonly Regex LegacyScreen = new Regex(@"^.*@(-?\d+),(-?\d+),(\d+)x(\d+)$", RegexOptions.CultureInvariant);

    /// <summary>
    /// The setup of a key written by MDSHO 1.7.0 (<c>DeviceName@left,top,widthxheight</c> per screen, joined with <c>|</c>),
    /// with the screen at 0,0 as the primary one (Windows puts the primary screen there); null for any other text.
    /// </summary>
    public static ScreenSetup FromLegacyKey(string key) {
      if (string.IsNullOrEmpty(key)) {
        return null;
      }
      List<ScreenInfo> screens = new List<ScreenInfo>();
      foreach (string part in key.Split('|')) {
        Match m = LegacyScreen.Match(part);
        if (!m.Success) {
          return null;
        }
        double left = double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
        double top = double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
        ScreenRect bounds = new ScreenRect(left, top, double.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture), double.Parse(m.Groups[4].Value, CultureInfo.InvariantCulture));
        screens.Add(new ScreenInfo(part.Substring(0, part.LastIndexOf('@')), bounds, bounds, 1.0, left == 0 && top == 0));
      }
      return From(screens);
    }

    /// <summary>The setup of these screens.</summary>
    public static ScreenSetup From(IEnumerable<ScreenInfo> screens) {
      List<ScreenInfo> list = (screens ?? Enumerable.Empty<ScreenInfo>()).Where(s => s != null).ToList();
      string key = string.Join("|", list
        .Select(s => (column: list.Count(o => !ReferenceEquals(o, s) && o.Bounds.Right <= s.Bounds.Left),
                      row: list.Count(o => !ReferenceEquals(o, s) && o.Bounds.Bottom <= s.Bounds.Top),
                      text: Whole(s.Bounds.Width) + "x" + Whole(s.Bounds.Height) + (s.IsPrimary ? "*" : "")))
        .OrderBy(s => s.column).ThenBy(s => s.row).ThenBy(s => s.text, StringComparer.Ordinal)
        .Select(s => s.text + "@c" + s.column.ToString(CultureInfo.InvariantCulture) + "r" + s.row.ToString(CultureInfo.InvariantCulture)));
      IEnumerable<ScreenInfo> named = list.OrderByDescending(s => s.IsPrimary).ThenBy(s => s.Bounds.Left).ThenBy(s => s.Bounds.Top);
      string name = list.Count + (list.Count == 1 ? " screen: " : " screens: ")
        + string.Join(", ", named.Select(s => Whole(s.Bounds.Width) + "×" + Whole(s.Bounds.Height)));
      return new ScreenSetup(key, name, list.Count);
    }

    private static string Whole(double value) {
      return ((long)Math.Round(value)).ToString(CultureInfo.InvariantCulture);
    }
  }

  /// <summary>Pure geometry for screen setups: describing where a window is, and choosing where it goes in a setup.</summary>
  public static class ScreenSetupPlacement {

    /// <summary>The screen whose bounds (DIPs) overlap the window most, or null when it is on no screen.</summary>
    public static ScreenInfo ScreenOf(ScreenRect rectDip, IList<ScreenInfo> screens) {
      ScreenInfo best = null;
      double bestArea = 0;
      foreach (ScreenInfo screen in screens ?? new List<ScreenInfo>()) {
        ScreenRect overlap = ScreenRect.Intersect(rectDip, screen.BoundsDip);
        double area = overlap.IsEmpty ? 0 : overlap.Width * overlap.Height;
        if (area > bestArea) {
          best = screen;
          bestArea = area;
        }
      }
      return best;
    }

    /// <summary>
    /// Where a window is: the screen it is mostly on (or <paramref name="fallbackScreen"/> when it is on none) and whether it
    /// is docked to the left or right of that screen's working area.
    /// </summary>
    public static ItemPlace Describe(ScreenRect rectDip, string fallbackScreen, IList<ScreenInfo> screens) {
      ScreenInfo screen = ScreenOf(rectDip, screens);
      if (screen == null) {
        return new ItemPlace(fallbackScreen ?? "", rectDip, DockSide.None);
      }
      DockSide dock = DockSide.None;
      if (ScreenGeometry.IsDockedToSide(rectDip, screen)) {
        dock = Math.Abs(rectDip.Left - screen.WorkingAreaDip.Left) <= 1.0 ? DockSide.Left : DockSide.Right;
      }
      return new ItemPlace(screen.DeviceName, rectDip, dock);
    }

    /// <summary>
    /// Where a window goes: its <paramref name="saved"/> place for this setup when there is one (docked again to its
    /// screen's working area when it was docked), otherwise where it is now. Either way it is pulled onto a screen when its
    /// title bar can't be reached (the same rule as at startup).
    /// </summary>
    public static ItemPlace Choose(ItemPlace saved, ItemPlace current, IList<ScreenInfo> screens) {
      ItemPlace wanted = saved ?? current;
      if (saved != null && saved.Dock != DockSide.None) {
        // The screen under the saved place (device names can change between connections), else the one with its name.
        ScreenInfo dockScreen = ScreenOf(saved.Rect, screens)
          ?? (screens ?? new List<ScreenInfo>()).FirstOrDefault(s => string.Equals(s.DeviceName, saved.ScreenDeviceName, StringComparison.OrdinalIgnoreCase));
        if (dockScreen != null) {
          ScreenRect docked = saved.Dock == DockSide.Left
            ? ScreenGeometry.DockLeft(dockScreen, saved.Rect.Width)
            : ScreenGeometry.DockRight(dockScreen, saved.Rect.Width);
          return new ItemPlace(dockScreen.DeviceName, docked, saved.Dock);
        }
      }
      if (screens == null || screens.Count == 0 || ScreenGeometry.IsTitleBarReachable(wanted.Rect, screens)) {
        return wanted;
      }
      ScreenRect onScreen = ScreenGeometry.EnsureOnScreen(wanted.Rect, wanted.ScreenDeviceName, screens);
      return Describe(onScreen, wanted.ScreenDeviceName, screens);
    }
  }

  /// <summary>A timer that calls back once after a delay; starting it again replaces the waiting call.</summary>
  public interface IOneShotTimer {
    /// <summary>Calls <paramref name="elapsed"/> once after <paramref name="delay"/>, instead of any call still waiting.</summary>
    void Start(TimeSpan delay, Action elapsed);
    /// <summary>Drops the waiting call, if any.</summary>
    void Stop();
  }

  /// <summary>
  /// Waits for things to settle: every <see cref="Trigger"/> starts the wait again, and the settled action runs once, a
  /// delay after the last trigger. Display changes come as several events in a row; they count as one.
  /// </summary>
  public sealed class Debouncer {
    private readonly IOneShotTimer timer;
    private readonly TimeSpan delay;
    private readonly Action settled;

    /// <summary>Uses <paramref name="timer"/> to run <paramref name="settled"/> <paramref name="delay"/> after the last trigger.</summary>
    public Debouncer(IOneShotTimer timer, TimeSpan delay, Action settled) {
      this.timer = timer ?? throw new ArgumentNullException(nameof(timer));
      this.delay = delay;
      this.settled = settled ?? throw new ArgumentNullException(nameof(settled));
    }

    /// <summary>True from a trigger until the settled action starts.</summary>
    public bool Pending { get; private set; }

    /// <summary>Something changed: (re)starts the wait.</summary>
    public void Trigger() {
      Pending = true;
      timer.Stop();
      timer.Start(delay, Fire);
    }

    /// <summary>Drops a waiting settle.</summary>
    public void Cancel() {
      Pending = false;
      timer.Stop();
    }

    private void Fire() {
      if (!Pending) {
        return;
      }
      Pending = false;
      settled();
    }
  }
}
