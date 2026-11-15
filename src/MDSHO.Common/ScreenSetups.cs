using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace MDSHO.Common {

  /// <summary>The kind of window a saved place belongs to. Stored in the database.</summary>
  public enum PlacedKind {
    Panel = 0,
    Clone = 1,
    /// <summary>A note window, by its button's ID.</summary>
    Note = 2
  }

  /// <summary>The screen edge a window is docked to. Stored in the database.</summary>
  public enum DockSide {
    None = 0,
    Left = 1,
    Right = 2
  }

  /// <summary>Where a window is or should go: its screen, its normal place in DIPs, and its dock side.</summary>
  public sealed record ItemPlace(string ScreenDeviceName, ScreenRect Rect, DockSide Dock = DockSide.None);

  /// <summary>
  /// A screen setup: the screens' resolutions, which one is primary, and how they are arranged.
  /// Device names don't count, because Windows renumbers them on reconnect.
  /// </summary>
  public sealed class ScreenSetup {
    /// <summary>Identifies the setup, for example <c>1920x1080*@c0r0|2560x1440@c1r0</c>. See docs/database.md.</summary>
    public string Key { get; }
    /// <summary>The name in the tray menu, for example "2 screens: 1920×1080, 2560×1440".</summary>
    public string Name { get; }
    public int ScreenCount { get; }

    private ScreenSetup(string key, string name, int screenCount) {
      Key = key;
      Name = name;
      ScreenCount = screenCount;
    }

    private static readonly Regex LegacyScreen = new Regex(@"^.*@(-?\d+),(-?\d+),(\d+)x(\d+)$", RegexOptions.CultureInvariant);

    /// <summary>Reads a key written by 1.7.0. Returns null for any other text.</summary>
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
        // Windows always puts the primary screen at 0,0.
        screens.Add(new ScreenInfo(part.Substring(0, part.LastIndexOf('@')), bounds, bounds, 1.0, left == 0 && top == 0));
      }
      return From(screens);
    }

    /// <summary>The setup of these screens.</summary>
    public static ScreenSetup From(IEnumerable<ScreenInfo> screens) {
      List<ScreenInfo> list = (screens ?? Enumerable.Empty<ScreenInfo>()).Where(s => s != null).ToList();
      // Column and row count the screens fully left of and above each screen, so small moves don't change the key.
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

  /// <summary>Works out where a window is, and where it should go, in a screen setup.</summary>
  public static class ScreenSetupPlacement {

    /// <summary>The screen the window overlaps most, or null.</summary>
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

    /// <summary>The window's screen and dock side. Uses <paramref name="fallbackScreen"/> when it is on no screen.</summary>
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

    /// <summary>The saved place if there is one, otherwise the current one. Always pulled onto a screen.</summary>
    public static ItemPlace Choose(ItemPlace saved, ItemPlace current, IList<ScreenInfo> screens) {
      ItemPlace wanted = saved ?? current;
      if (saved != null && saved.Dock != DockSide.None) {
        // Find the screen by place first: its saved device name may be out of date.
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

  /// <summary>A timer that calls back once after a delay.</summary>
  public interface IOneShotTimer {
    /// <summary>Calls <paramref name="elapsed"/> after <paramref name="delay"/>, replacing any waiting call.</summary>
    void Start(TimeSpan delay, Action elapsed);
    /// <summary>Drops the waiting call.</summary>
    void Stop();
  }

  /// <summary>Runs an action once, a delay after the last of several triggers.</summary>
  public sealed class Debouncer {
    private readonly IOneShotTimer timer;
    private readonly TimeSpan delay;
    private readonly Action settled;

    public Debouncer(IOneShotTimer timer, TimeSpan delay, Action settled) {
      this.timer = timer ?? throw new ArgumentNullException(nameof(timer));
      this.delay = delay;
      this.settled = settled ?? throw new ArgumentNullException(nameof(settled));
    }

    /// <summary>True from a trigger until the action starts.</summary>
    public bool Pending { get; private set; }

    /// <summary>Starts the wait again.</summary>
    public void Trigger() {
      Pending = true;
      timer.Stop();
      timer.Start(delay, Fire);
    }

    /// <summary>Drops a waiting action.</summary>
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
