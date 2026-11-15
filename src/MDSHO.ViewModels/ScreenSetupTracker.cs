using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MDSHO.Common;
using MDSHO.Core;
using MDSHO.Data;

namespace MDSHO.ViewModels {

  /// <summary>A panel, clone or open note window whose place is remembered per screen setup.</summary>
  public interface IPlacedItem {
    /// <summary>Panel, clone or note.</summary>
    PlacedKind Kind { get; }
    /// <summary>The panel's, clone's or note button's database ID.</summary>
    long ID { get; }
    /// <summary>Its normal (not maximized) place and size now, in DIPs.</summary>
    ScreenRect Rect { get; }
    /// <summary>The screen it was last saved on; used only when it is on no screen now.</summary>
    string ScreenDeviceName { get; }
    /// <summary>Moves (and resizes) it to <paramref name="target"/> and saves that as its own place too.</summary>
    void MoveTo(ItemPlace target, IList<ScreenInfo> screens);
  }

  /// <summary>A screen setup as the tray menu lists it.</summary>
  /// <param name="Name">Its name, numbered when another setup has the same one.</param>
  /// <param name="IsCurrent">True for the screens connected now.</param>
  public sealed record KnownScreenSetup(string Name, bool IsCurrent);

  /// <summary>
  /// Remembers, for each screen setup (the set of connected screens), where every panel, clone and open note window was, and
  /// moves them there when the screens change. Saved on this PC only (not exported).
  /// <list type="bullet">
  /// <item>Places are saved for the current setup at the moments they are saved anyway (end of a move or resize, dock,
  /// centre, a note window settling), from <see cref="Start"/> on. They are saved also while "Remember places for each screen setup" is off, so turning
  /// it on again starts from the latest places.</item>
  /// <item>A display change waits <see cref="SettleDelay"/> for things to settle. Nothing is saved while it waits or while
  /// the moves are applied, and a save from screens that changed without an event is dropped, so Windows piling the
  /// windows onto the remaining screen never overwrites a setup's places.</item>
  /// <item>A known setup puts every item back at its place for that setup (docked panels docked again); a new one leaves
  /// things where Windows put them. Either way an item off every screen is pulled back. With the setting off, nothing
  /// moves, exactly as before.</item>
  /// </list>
  /// </summary>
  public sealed class ScreenSetupTracker {
    /// <summary>The key of the setting in the AppSettings table: <c>On</c> or <c>Off</c>; no row = on.</summary>
    public const string SettingKey = "RememberScreenSetupPlaces";
    /// <summary>How long a display change waits for further changes before the windows move.</summary>
    public static readonly TimeSpan SettleDelay = TimeSpan.FromMilliseconds(1500);
    /// <summary>How many setups are kept: adding one more forgets the least recently used one (never the current one).</summary>
    public const int MaxSetups = 10;

    private readonly IScreenService screens;
    private readonly Debouncer debouncer;
    private bool? enabled;
    private long currentID;
    private bool started;

    /// <summary>Reads the screens from <paramref name="screens"/> and waits for display changes to settle on <paramref name="timer"/>.</summary>
    public ScreenSetupTracker(IScreenService screens, IOneShotTimer timer) {
      this.screens = screens ?? throw new ArgumentNullException(nameof(screens));
      this.debouncer = new Debouncer(timer ?? throw new ArgumentNullException(nameof(timer)), SettleDelay, Settled);
    }

    /// <summary>The clock for the last-used times; tests replace it.</summary>
    public Func<DateTime> UtcNow { get; set; } = () => DateTime.UtcNow;

    /// <summary>Every panel, clone and open note window now (the app sets it at startup).</summary>
    public Func<IEnumerable<IPlacedItem>> Items { get; set; } = () => Array.Empty<IPlacedItem>();

    /// <summary>"Remember places for each screen setup" (Settings): on unless it was turned off.</summary>
    public bool Enabled {
      get {
        if (this.enabled == null) {
          string text = null;
          try {
            text = DataStore.AppSettings.Get(SettingKey);
          } catch (Exception ex) {
            AppLog.Warning(ex, "The screen setups setting could not be read; it is on");
          }
          this.enabled = !string.Equals(text, "Off", StringComparison.OrdinalIgnoreCase);
        }
        return this.enabled.Value;
      }
    }

    /// <summary>The setup of the connected screens, as last identified; null before the first save or <see cref="Start"/>.</summary>
    public ScreenSetup Current { get; private set; }
    /// <summary>True while the windows are being moved to their places.</summary>
    public bool IsApplying { get; private set; }
    /// <summary>True while a display change waits to settle.</summary>
    public bool IsChangePending => this.debouncer.Pending;

    /// <summary>Raised when the setting or the known setups changed.</summary>
    public event Action Changed;

    /// <summary>Settings, Apply: turns "Remember places for each screen setup" on or off and saves it.</summary>
    public void SetEnabled(bool on) {
      DataStore.AppSettings.Set(SettingKey, on ? "On" : "Off");
      this.enabled = on;
      AppLog.Information("Remember places for each screen setup turned {State}", on ? "on" : "off");
      Changed?.Invoke();
    }

    /// <summary>
    /// At startup, after the windows opened: cleans up places of items that are gone, then uses the saved places when the
    /// screens are a known setup (and the setting is on) and saves the places for this setup. Never throws.
    /// </summary>
    public void Start() {
      this.started = true;
      try {
        MergeDuplicates();
      } catch (Exception ex) {
        AppLog.Warning(ex, "Screen setups with the same screens could not be merged");
      }
      try {
        int removed = DataStore.ScreenSetups.DeletePlacesOfMissingItems();
        if (removed > 0) {
          AppLog.Information("Removed {Count} screen setup places of windows that are gone", removed);
        }
      } catch (Exception ex) {
        AppLog.Warning(ex, "Screen setup places of windows that are gone could not be removed");
      }
      Settled();
    }

    /// <summary>Screens were connected or disconnected, or a resolution changed: waits for things to settle, then moves the windows.</summary>
    public void DisplayChanged() {
      this.debouncer.Trigger();
    }

    /// <summary>
    /// Saves where <paramref name="item"/> is now as its place in the current setup. Ignored before <see cref="Start"/> (note
    /// windows reopen before it, and a known setup's places must win), while a change waits and while windows move. Never throws.
    /// </summary>
    public void Remember(IPlacedItem item) {
      if (item == null || !this.started || IsApplying || IsChangePending) {
        return;
      }
      try {
        IList<ScreenInfo> now = this.screens.AllScreens();
        if (!IsCurrentSetup(now)) {
          return;
        }
        DataStore.ScreenSetups.SavePlace(this.currentID, ToRecord(item, now));
      } catch (Exception ex) {
        AppLog.Warning(ex, "Could not save a place for the screen setup");
      }
    }

    /// <summary>
    /// "Put panels back for this setup": moves every item to its saved place in the current setup (an item without one is
    /// only pulled onto a screen when it is off screen). Returns how many moved; nothing moves when the setting is off.
    /// </summary>
    public int PutBack() {
      if (!Enabled || IsChangePending) {
        return 0;
      }
      IList<ScreenInfo> now = this.screens.AllScreens();
      if (!IsCurrentSetup(now)) {
        return 0;
      }
      return ApplyAndSave(now, move: true);
    }

    /// <summary>"Forget the other setups": deletes every setup but the current one, with their places.</summary>
    public void ForgetOthers() {
      IList<ScreenInfo> now = this.screens.AllScreens();
      if (!IsCurrentSetup(now)) {
        return;
      }
      DataStore.ScreenSetups.DeleteAllExcept(this.currentID);
      AppLog.Information("Forgot every screen setup but {Setup}", Current.Name);
      Changed?.Invoke();
    }

    /// <summary>
    /// The known setups, most recently used first, with the current one marked. Two different setups can have the same
    /// name (the same resolutions arranged differently, for example a monitor left or right of the laptop); the one added
    /// later is numbered, for example "2 screens: 1920×1080, 2560×1440 (2)".
    /// </summary>
    public IList<KnownScreenSetup> KnownSetups() {
      string currentKey = Current?.Key ?? ScreenSetup.From(this.screens.AllScreens()).Key;
      Dictionary<string, int> seen = new Dictionary<string, int>(StringComparer.Ordinal);
      List<(ScreenSetupRecord setup, string name)> named = new List<(ScreenSetupRecord, string)>();
      foreach (ScreenSetupRecord setup in DataStore.ScreenSetups.GetAll()) {
        string name = setup.Name ?? "";
        seen[name] = seen.TryGetValue(name, out int count) ? count + 1 : 1;
        named.Add((setup, seen[name] == 1 ? name : name + " (" + seen[name] + ")"));
      }
      return named.OrderByDescending(n => n.setup, Recency)
        .Select(n => new KnownScreenSetup(n.name, n.setup.Key == currentKey)).ToList();
    }

    // Older first: by last-used time (empty = never), then by ID (added later = newer).
    private static readonly IComparer<ScreenSetupRecord> Recency = Comparer<ScreenSetupRecord>.Create((a, b) => {
      int byTime = string.CompareOrdinal(a.LastUsed ?? "", b.LastUsed ?? "");
      return byTime != 0 ? byTime : a.ID.CompareTo(b.ID);
    });

    /// <summary>
    /// At startup: setups whose keys now mean the same screens (keys written by 1.7.0 with device names and exact offsets,
    /// see <see cref="ScreenSetup.FromLegacyKey"/>) become one, under the new key. The most recently used one stays, with
    /// its places; places it lacks are taken from the others, the more recently used first.
    /// </summary>
    private static void MergeDuplicates() {
      List<ScreenSetupRecord> all = DataStore.ScreenSetups.GetAll();
      var groups = all.Select(r => (record: r, setup: ScreenSetup.FromLegacyKey(r.Key)))
        .GroupBy(x => x.setup != null ? x.setup.Key : x.record.Key, StringComparer.Ordinal)
        .Where(g => g.Count() > 1 || g.First().setup != null)
        .ToList();
      if (groups.Count == 0) {
        return;
      }
      int merged = 0;
      using (ITransactionScope tx = DataStore.BeginTransaction()) {
        foreach (var group in groups) {
          var ordered = group.OrderByDescending(x => x.record, Recency).ToList();
          ScreenSetupRecord keep = ordered[0].record;
          foreach (var other in ordered.Skip(1)) {
            DataStore.ScreenSetups.CopyMissingPlaces(other.record.ID, keep.ID);
            DataStore.ScreenSetups.Delete(other.record.ID);
            merged++;
          }
          ScreenSetup setup = group.Select(x => x.setup).FirstOrDefault(s => s != null);
          string name = setup != null ? setup.Name : keep.Name;
          if (keep.Key != group.Key || keep.Name != name) {
            DataStore.ScreenSetups.Rekey(keep.ID, group.Key, name);
          }
        }
        tx.Commit();
      }
      AppLog.Information("Screen setups moved to the new keys; {Count} duplicates merged", merged);
    }

    // The current setup was just used: records the time (later than any recorded, so the order is right even when the
    // clock is coarse or was set back), then forgets the least recently used setups beyond MaxSetups.
    private void MarkUsed() {
      List<ScreenSetupRecord> all = DataStore.ScreenSetups.GetAll();
      DateTime stamp = UtcNow().ToUniversalTime();
      foreach (ScreenSetupRecord setup in all) {
        if (setup.ID != this.currentID && DateTime.TryParse(setup.LastUsed, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime used)) {
          used = used.ToUniversalTime();
          if (used >= stamp) {
            stamp = used.AddTicks(1);
          }
        }
      }
      DataStore.ScreenSetups.Touch(this.currentID, stamp.ToString("o", CultureInfo.InvariantCulture));
      foreach (ScreenSetupRecord old in all.Where(s => s.ID != this.currentID).OrderByDescending(s => s, Recency).Skip(MaxSetups - 1)) {
        DataStore.ScreenSetups.Delete(old.ID);
        AppLog.Information("Forgot the least recently used screen setup {Setup}", old.Name);
      }
    }

    // The screens settled (or the app started): identify the setup, move the windows when it is known, save the places.
    private void Settled() {
      try {
        IList<ScreenInfo> now = this.screens.AllScreens();
        if (now.Count == 0) {
          return;
        }
        ScreenSetup setup = ScreenSetup.From(now);
        bool known = DataStore.ScreenSetups.Get(setup.Key) != null;
        bool changed = Current == null || Current.Key != setup.Key;
        Current = setup;
        this.currentID = DataStore.ScreenSetups.Ensure(setup.Key, setup.Name);
        MarkUsed();
        int moved = ApplyAndSave(now, move: Enabled);
        if (changed) {
          AppLog.Information("Screen setup {Setup} ({State}); moved {Moved} windows", setup.Name, known ? "known" : "new", moved);
          Changed?.Invoke();
        }
      } catch (Exception ex) {
        AppLog.Error(ex, "Could not move the windows for the screen setup");
      }
    }

    // Moves every item to its place in the current setup (when asked), then saves where every item is for this setup.
    private int ApplyAndSave(IList<ScreenInfo> now, bool move) {
      List<IPlacedItem> items = Items().ToList();
      int moved = 0;
      if (move) {
        Dictionary<(long, long), ScreenSetupPlace> saved = DataStore.ScreenSetups.GetPlaces(this.currentID).ToDictionary(p => (p.Kind, p.ItemID));
        IsApplying = true;
        try {
          foreach (IPlacedItem item in items) {
            ItemPlace current = ScreenSetupPlacement.Describe(item.Rect, item.ScreenDeviceName, now);
            ItemPlace savedPlace = saved.TryGetValue(((long)item.Kind, item.ID), out ScreenSetupPlace row) ? ToPlace(row) : null;
            ItemPlace target = ScreenSetupPlacement.Choose(savedPlace, current, now);
            if (target.Rect != item.Rect) {
              item.MoveTo(target, now);
              moved++;
            }
          }
        } finally {
          IsApplying = false;
        }
      }
      using (ITransactionScope tx = DataStore.BeginTransaction()) {
        foreach (IPlacedItem item in items) {
          DataStore.ScreenSetups.SavePlace(this.currentID, ToRecord(item, now));
        }
        tx.Commit();
      }
      return moved;
    }

    // True when the screens are still the current setup (identifying it the first time). Screens that changed without an
    // event (yet) start the wait instead.
    private bool IsCurrentSetup(IList<ScreenInfo> now) {
      if (now.Count == 0) {
        return false;
      }
      ScreenSetup setup = ScreenSetup.From(now);
      if (Current == null) {
        Current = setup;
        this.currentID = DataStore.ScreenSetups.Ensure(setup.Key, setup.Name);
        MarkUsed();
        return true;
      }
      if (Current.Key != setup.Key) {
        DisplayChanged();
        return false;
      }
      return true;
    }

    private static ScreenSetupPlace ToRecord(IPlacedItem item, IList<ScreenInfo> now) {
      ItemPlace place = ScreenSetupPlacement.Describe(item.Rect, item.ScreenDeviceName, now);
      return new ScreenSetupPlace {
        Kind = (long)item.Kind,
        ItemID = item.ID,
        ScreenDeviceName = place.ScreenDeviceName,
        Left = (long)Math.Round(place.Rect.Left),
        Top = (long)Math.Round(place.Rect.Top),
        Width = (long)Math.Round(place.Rect.Width),
        Height = (long)Math.Round(place.Rect.Height),
        Dock = (long)place.Dock
      };
    }

    private static ItemPlace ToPlace(ScreenSetupPlace row) {
      DockSide dock = Enum.IsDefined(typeof(DockSide), (int)row.Dock) ? (DockSide)row.Dock : DockSide.None;
      return new ItemPlace(row.ScreenDeviceName, new ScreenRect(row.Left, row.Top, row.Width, row.Height), dock);
    }
  }

  /// <summary>A panel or a clone as a <see cref="IPlacedItem"/>: moving it moves its view model (and so its window) and saves its own place.</summary>
  public sealed class DesktopWindowPlacedItem : IPlacedItem {
    private readonly DesktopWindowViewModel window;

    /// <summary>The item of this panel or clone.</summary>
    public DesktopWindowPlacedItem(DesktopWindowViewModel window) {
      this.window = window ?? throw new ArgumentNullException(nameof(window));
    }

    /// <summary>Panel or clone.</summary>
    public PlacedKind Kind => this.window.IsClone ? PlacedKind.Clone : PlacedKind.Panel;
    /// <summary>The panel's or clone's ID.</summary>
    public long ID => this.window.ID;
    /// <summary>Where it is (DIPs).</summary>
    public ScreenRect Rect => new ScreenRect(this.window.Left, this.window.Top, this.window.Width, this.window.Height);
    /// <summary>The screen it was last saved on.</summary>
    public string ScreenDeviceName => this.window.ScreenDeviceName;

    /// <summary>Moves it and saves the place in its own row (MDSHOWindows or MDSHOWindowClones), so a restart opens it there too.</summary>
    public void MoveTo(ItemPlace target, IList<ScreenInfo> screens) {
      long left = (long)Math.Round(target.Rect.Left);
      long top = (long)Math.Round(target.Rect.Top);
      long width = (long)Math.Round(target.Rect.Width);
      long height = (long)Math.Round(target.Rect.Height);
      string screen = target.ScreenDeviceName ?? "";
      using (ITransactionScope tx = DataStore.BeginTransaction()) {
        if (this.window.IsClone) {
          DataStore.Clones.UpdateLeftTopWidthHeight(ID, left, top, width, height);
          DataStore.Clones.UpdateLeftTopScreenDeviceName(ID, left, top, screen);
        } else {
          DataStore.Windows.UpdateLeftTopWidthHeight(ID, left, top, width, height);
          DataStore.Windows.UpdateLeftTopScreenDeviceName(ID, left, top, screen);
        }
        tx.Commit();
      }
      this.window.Left = left;
      this.window.Top = top;
      this.window.Width = width;
      this.window.Height = height;
      this.window.ScreenDeviceName = screen;
      this.window.RefreshDocked(screens);
    }
  }
}
