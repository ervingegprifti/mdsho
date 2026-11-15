using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MDSHO.Common;
using MDSHO.Core;
using MDSHO.Data;

namespace MDSHO.ViewModels {

  /// <summary>A panel, clone or open note whose place is saved per screen setup.</summary>
  public interface IPlacedItem {
    PlacedKind Kind { get; }
    /// <summary>The panel's, clone's or note button's ID.</summary>
    long ID { get; }
    /// <summary>Its normal place now, in DIPs.</summary>
    ScreenRect Rect { get; }
    /// <summary>Its last saved screen. Used only when it is on no screen.</summary>
    string ScreenDeviceName { get; }
    /// <summary>Moves it and saves that as its own place too.</summary>
    void MoveTo(ItemPlace target, IList<ScreenInfo> screens);
  }

  /// <summary>A screen setup in the tray list.</summary>
  public sealed record KnownScreenSetup(string Name, bool IsCurrent);

  /// <summary>
  /// Saves window places per screen setup and moves the windows back when the screens change.
  /// Places are saved even while the setting is off; only the moving stops.
  /// </summary>
  public sealed class ScreenSetupTracker {
    /// <summary>The AppSettings key: "On" or "Off". No row means on.</summary>
    public const string SettingKey = "RememberScreenSetupPlaces";
    /// <summary>How long to wait after a display change before moving windows.</summary>
    public static readonly TimeSpan SettleDelay = TimeSpan.FromMilliseconds(1500);
    /// <summary>How many setups to keep. The current one is never forgotten.</summary>
    public const int MaxSetups = 10;

    private readonly IScreenService screens;
    private readonly Debouncer debouncer;
    private bool? enabled;
    private long currentID;
    private bool started;

    public ScreenSetupTracker(IScreenService screens, IOneShotTimer timer) {
      this.screens = screens ?? throw new ArgumentNullException(nameof(screens));
      this.debouncer = new Debouncer(timer ?? throw new ArgumentNullException(nameof(timer)), SettleDelay, Settled);
    }

    /// <summary>The clock for last-used times. Tests replace it.</summary>
    public Func<DateTime> UtcNow { get; set; } = () => DateTime.UtcNow;

    /// <summary>Every panel, clone and open note. The app sets it at startup.</summary>
    public Func<IEnumerable<IPlacedItem>> Items { get; set; } = () => Array.Empty<IPlacedItem>();

    /// <summary>"Remember places for each screen setup". On unless turned off.</summary>
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

    /// <summary>The current setup, or null before <see cref="Start"/>.</summary>
    public ScreenSetup Current { get; private set; }
    /// <summary>True while windows are being moved.</summary>
    public bool IsApplying { get; private set; }
    /// <summary>True while waiting for a display change to settle.</summary>
    public bool IsChangePending => this.debouncer.Pending;

    /// <summary>Raised when the setting or the list of setups changes.</summary>
    public event Action Changed;

    /// <summary>Saves the setting.</summary>
    public void SetEnabled(bool on) {
      DataStore.AppSettings.Set(SettingKey, on ? "On" : "Off");
      this.enabled = on;
      AppLog.Information("Remember places for each screen setup turned {State}", on ? "on" : "off");
      Changed?.Invoke();
    }

    /// <summary>At startup, after the windows open: tidies the data, then applies the current setup. Never throws.</summary>
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

    /// <summary>The screens changed: waits for them to settle, then moves the windows.</summary>
    public void DisplayChanged() {
      this.debouncer.Trigger();
    }

    /// <summary>
    /// Saves where the item is now. Ignored before <see cref="Start"/>, because notes reopen before it and would
    /// overwrite the saved places. Also ignored while the screens change or windows move. Never throws.
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

    /// <summary>"Put panels back for this setup". Returns how many windows moved.</summary>
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

    /// <summary>"Forget the other setups".</summary>
    public void ForgetOthers() {
      IList<ScreenInfo> now = this.screens.AllScreens();
      if (!IsCurrentSetup(now)) {
        return;
      }
      DataStore.ScreenSetups.DeleteAllExcept(this.currentID);
      AppLog.Information("Forgot every screen setup but {Setup}", Current.Name);
      Changed?.Invoke();
    }

    /// <summary>The known setups, most recently used first. A repeated name gets a number, for example "(2)".</summary>
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

    // Oldest first: by last-used time, then by ID.
    private static readonly IComparer<ScreenSetupRecord> Recency = Comparer<ScreenSetupRecord>.Create((a, b) => {
      int byTime = string.CompareOrdinal(a.LastUsed ?? "", b.LastUsed ?? "");
      return byTime != 0 ? byTime : a.ID.CompareTo(b.ID);
    });

    // Turns 1.7.0 keys into new ones and merges setups that end up the same. The most recently used one wins;
    // it takes any places it lacks from the others.
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

    // Stamps the current setup as used, then forgets the oldest setups beyond MaxSetups.
    // The stamp is always later than any other, even if the clock is coarse or was set back.
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

    // Finds the setup, moves the windows if the setting is on, and saves the places.
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

    // Moves each item to its saved place if asked, then saves every item's place.
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

    // False if the screens changed without an event yet. Windows can move windows before the event comes,
    // so we start the wait instead of saving into the wrong setup.
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

  /// <summary>A panel or clone as a placed item. Moving it moves its view model.</summary>
  public sealed class DesktopWindowPlacedItem : IPlacedItem {
    private readonly DesktopWindowViewModel window;

    public DesktopWindowPlacedItem(DesktopWindowViewModel window) {
      this.window = window ?? throw new ArgumentNullException(nameof(window));
    }

    public PlacedKind Kind => this.window.IsClone ? PlacedKind.Clone : PlacedKind.Panel;
    public long ID => this.window.ID;
    public ScreenRect Rect => new ScreenRect(this.window.Left, this.window.Top, this.window.Width, this.window.Height);
    public string ScreenDeviceName => this.window.ScreenDeviceName;

    /// <summary>Moves it and saves the place in its own row, so a restart opens it there.</summary>
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
