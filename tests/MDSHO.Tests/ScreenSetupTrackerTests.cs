using System;
using System.Collections.Generic;
using System.Linq;
using MDSHO.Common;
using MDSHO.Core;
using MDSHO.Data;
using MDSHO.ViewModels;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// The screen setups tracker: remembers places per screen setup and moves panels, clones and note windows when the
  /// screens change. Screens, items and the timer are fakes; the database is a temp one.
  /// </summary>
  public class ScreenSetupTrackerTests : IDisposable {
    private readonly TempDb db = new TempDb();
    public void Dispose() { db.Dispose(); }

    internal sealed class FakeScreens : IScreenService {
      public List<ScreenInfo> Screens = new List<ScreenInfo>();
      public IList<ScreenInfo> AllScreens() { return Screens.ToList(); }
      public ScreenInfo PrimaryScreen() { return Screens.FirstOrDefault(s => s.IsPrimary) ?? Screens[0]; }
      public ScreenInfo ScreenFromHandle(IntPtr windowHandle) { return PrimaryScreen(); }
      public ScreenInfo ScreenAtCursor() { return PrimaryScreen(); }
    }

    /// <summary>An item that records its moves; while a move runs it asks the tracker to remember it, like a window would.</summary>
    internal sealed class FakeItem : IPlacedItem {
      public FakeItem(PlacedKind kind, long id, double left, double top, double width = 300, double height = 400, string screen = @"\\.\DISPLAY1") {
        Kind = kind;
        ID = id;
        Rect = new ScreenRect(left, top, width, height);
        ScreenDeviceName = screen;
      }
      public PlacedKind Kind { get; }
      public long ID { get; }
      public ScreenRect Rect { get; set; }
      public string ScreenDeviceName { get; set; }
      public List<ItemPlace> Moves = new List<ItemPlace>();
      public ScreenSetupTracker RememberWhileMoving;
      public void MoveTo(ItemPlace target, IList<ScreenInfo> screens) {
        Moves.Add(target);
        Rect = target.Rect;
        ScreenDeviceName = target.ScreenDeviceName;
        RememberWhileMoving?.Remember(this);
      }
    }

    private static readonly ScreenInfo Laptop = ScreenSetupTests.Screen(@"\\.\DISPLAY1", 0, 0, 1920, 1080, primary: true);
    private static readonly ScreenInfo Monitor = ScreenSetupTests.Screen(@"\\.\DISPLAY2", 1920, 0, 2560, 1440);

    private readonly FakeScreens screens = new FakeScreens();
    private readonly ScreenSetupTests.FakeTimer timer = new ScreenSetupTests.FakeTimer();
    private readonly List<IPlacedItem> items = new List<IPlacedItem>();

    private ScreenSetupTracker NewTracker() {
      ScreenSetupTracker tracker = new ScreenSetupTracker(screens, timer);
      tracker.Items = () => items;
      return tracker;
    }

    private static string KeyOf(params ScreenInfo[] s) { return ScreenSetup.From(s).Key; }

    private List<ScreenSetupPlace> PlacesOf(params ScreenInfo[] s) {
      return DataStore.ScreenSetups.GetPlaces(DataStore.ScreenSetups.Get(KeyOf(s)).ID);
    }

    // Docked: the monitor's laptop-and-monitor setup is known with a panel on the monitor; undocking is a new setup.
    private (ScreenSetupTracker tracker, FakeItem panel) DockedThenUndocked() {
      screens.Screens = new List<ScreenInfo> { Laptop, Monitor };
      FakeItem panel = new FakeItem(PlacedKind.Panel, 1, 2500, 100, screen: @"\\.\DISPLAY2");
      items.Add(panel);
      ScreenSetupTracker tracker = NewTracker();
      tracker.Start();
      // Undocked: Windows piles the panel onto the laptop screen.
      screens.Screens = new List<ScreenInfo> { Laptop };
      panel.Rect = new ScreenRect(50, 60, 300, 400);
      panel.ScreenDeviceName = @"\\.\DISPLAY1";
      tracker.DisplayChanged();
      timer.Fire();
      return (tracker, panel);
    }

    // ---- The setting ----

    [Fact]
    public void Remember_places_is_on_at_first_and_saved_in_AppSettings() {
      ScreenSetupTracker tracker = NewTracker();
      Assert.True(tracker.Enabled);
      tracker.SetEnabled(false);
      Assert.False(tracker.Enabled);
      Assert.Equal("Off", DataStore.AppSettings.Get(ScreenSetupTracker.SettingKey));
      Assert.False(NewTracker().Enabled);
      tracker.SetEnabled(true);
      Assert.Equal("On", DataStore.AppSettings.Get(ScreenSetupTracker.SettingKey));
      Assert.True(NewTracker().Enabled);
    }

    // ---- Startup ----

    [Fact]
    public void Startup_in_a_new_setup_moves_nothing_and_saves_every_place_for_it() {
      screens.Screens = new List<ScreenInfo> { Laptop, Monitor };
      FakeItem panel = new FakeItem(PlacedKind.Panel, 1, 100, 100);
      FakeItem clone = new FakeItem(PlacedKind.Clone, 1, 2500, 100, screen: "stale");
      FakeItem note = new FakeItem(PlacedKind.Note, 7, 0, 0, 300, 1040);
      items.AddRange(new[] { panel, clone, note });
      ScreenSetupTracker tracker = NewTracker();
      tracker.Start();
      Assert.Empty(panel.Moves);
      Assert.Empty(clone.Moves);
      Assert.Equal(KeyOf(Laptop, Monitor), tracker.Current.Key);
      List<ScreenSetupPlace> saved = PlacesOf(Laptop, Monitor);
      Assert.Equal(3, saved.Count);
      ScreenSetupPlace cloneSaved = saved.Single(p => p.Kind == (long)PlacedKind.Clone);
      Assert.Equal(@"\\.\DISPLAY2", cloneSaved.ScreenDeviceName); // the screen it is on, not the stale one
      Assert.Equal(2500, cloneSaved.Left);
      Assert.Equal((long)DockSide.Left, saved.Single(p => p.Kind == (long)PlacedKind.Note).Dock);
    }

    [Fact]
    public void Startup_in_a_known_setup_puts_items_back_at_their_saved_places() {
      long setup = DataStore.ScreenSetups.Ensure(KeyOf(Laptop), ScreenSetup.From(new[] { Laptop }).Name);
      DataStore.ScreenSetups.SavePlace(setup, new ScreenSetupPlace { Kind = 0, ItemID = 1, ScreenDeviceName = @"\\.\DISPLAY1", Left = 500, Top = 200, Width = 310, Height = 410 });
      screens.Screens = new List<ScreenInfo> { Laptop };
      FakeItem panel = new FakeItem(PlacedKind.Panel, 1, 100, 100);
      FakeItem newer = new FakeItem(PlacedKind.Panel, 2, 120, 120);
      items.AddRange(new[] { panel, newer });
      NewTracker().Start();
      Assert.Equal(new ScreenRect(500, 200, 310, 410), panel.Rect);
      Assert.Empty(newer.Moves); // no saved place for this setup and on screen: stays
      Assert.Equal(2, PlacesOf(Laptop).Count); // the newer one is saved from now on
    }

    // A note button in a new panel, as a row; returns its ID.
    private long AddNote() {
      long windowID = DataStore.Windows.Create("D", 0, 0).ID;
      db.Exec("INSERT INTO Shortcuts (ParentMDSHOWindowID,Name,Target,Ext,Guid,IsGroup,IsGroupExpanded,Comment,IsMarked,MarkColor,IsProcessCommand,ProcessFileName,ProcessArguments,IsNote,NoteText) VALUES "
        + "(" + windowID + ",'N','','','',0,0,'',0,'',0,'','',1,'text');");
      return db.Scalar("SELECT max(ID) FROM Shortcuts");
    }

    [Fact]
    public void Saves_before_startup_are_ignored_so_startup_still_uses_the_saved_places() {
      // Note windows reopen (and save their place) before the tracker starts: that must not overwrite the known places.
      long noteID = AddNote();
      long setup = DataStore.ScreenSetups.Ensure(KeyOf(Laptop), ScreenSetup.From(new[] { Laptop }).Name);
      DataStore.ScreenSetups.SavePlace(setup, new ScreenSetupPlace { Kind = 2, ItemID = noteID, ScreenDeviceName = @"\\.\DISPLAY1", Left = 500, Top = 200, Width = 310, Height = 410 });
      screens.Screens = new List<ScreenInfo> { Laptop };
      FakeItem note = new FakeItem(PlacedKind.Note, noteID, 100, 100);
      items.Add(note);
      ScreenSetupTracker tracker = NewTracker();
      tracker.Remember(note);
      Assert.Equal(500, PlacesOf(Laptop).Single().Left);
      tracker.Start();
      Assert.Equal(new ScreenRect(500, 200, 310, 410), note.Rect);
    }

    [Fact]
    public void Startup_cleans_up_places_of_items_that_are_gone() {
      DesktopWindow w = DataStore.Windows.Create("D", 0, 0);
      long setup = DataStore.ScreenSetups.Ensure("old", "old");
      DataStore.ScreenSetups.SavePlace(setup, new ScreenSetupPlace { Kind = 0, ItemID = w.ID + 50, ScreenDeviceName = "D", Width = 1, Height = 1 });
      screens.Screens = new List<ScreenInfo> { Laptop };
      NewTracker().Start();
      Assert.Empty(DataStore.ScreenSetups.GetPlaces(setup));
    }

    [Fact]
    public void Startup_with_the_setting_off_moves_nothing_but_still_saves_places() {
      long setup = DataStore.ScreenSetups.Ensure(KeyOf(Laptop), "x");
      DataStore.ScreenSetups.SavePlace(setup, new ScreenSetupPlace { Kind = 0, ItemID = 1, ScreenDeviceName = @"\\.\DISPLAY1", Left = 500, Top = 200, Width = 310, Height = 410 });
      DataStore.AppSettings.Set(ScreenSetupTracker.SettingKey, "Off");
      screens.Screens = new List<ScreenInfo> { Laptop };
      FakeItem panel = new FakeItem(PlacedKind.Panel, 1, 100, 100);
      items.Add(panel);
      NewTracker().Start();
      Assert.Empty(panel.Moves);
      Assert.Equal(100, PlacesOf(Laptop).Single().Left);
    }

    // ---- Display changes ----

    [Fact]
    public void A_display_change_waits_for_things_to_settle_and_counts_several_events_as_one() {
      (ScreenSetupTracker tracker, FakeItem panel) = DockedThenUndocked();
      screens.Screens = new List<ScreenInfo> { Laptop, Monitor };
      tracker.DisplayChanged();
      tracker.DisplayChanged();
      tracker.DisplayChanged();
      Assert.True(tracker.IsChangePending);
      Assert.Empty(panel.Moves);
      Assert.Equal(ScreenSetupTracker.SettleDelay, timer.Delay);
      Assert.True(ScreenSetupTracker.SettleDelay >= TimeSpan.FromSeconds(1) && ScreenSetupTracker.SettleDelay <= TimeSpan.FromSeconds(2));
      timer.Fire();
      Assert.False(tracker.IsChangePending);
      Assert.Single(panel.Moves);
    }

    [Fact]
    public void A_new_setup_leaves_things_where_Windows_put_them_and_starts_saving_for_it() {
      (ScreenSetupTracker tracker, FakeItem panel) = DockedThenUndocked();
      Assert.Empty(panel.Moves);
      Assert.Equal(KeyOf(Laptop), tracker.Current.Key);
      Assert.Equal(50, PlacesOf(Laptop).Single().Left);
      // The docked setup still has the place on the monitor.
      Assert.Equal(2500, PlacesOf(Laptop, Monitor).Single().Left);
    }

    [Fact]
    public void A_new_setup_moves_an_item_that_is_off_screen_back_onto_a_screen() {
      (ScreenSetupTracker tracker, FakeItem panel) = DockedThenUndocked();
      FakeItem lost = new FakeItem(PlacedKind.Clone, 3, 3000, 100, screen: @"\\.\DISPLAY2");
      items.Add(lost);
      screens.Screens = new List<ScreenInfo> { Screen1366() };
      tracker.DisplayChanged();
      timer.Fire();
      Assert.Single(lost.Moves);
      Assert.True(ScreenGeometry.IsTitleBarReachable(lost.Rect, screens.Screens));
    }

    private static ScreenInfo Screen1366() { return ScreenSetupTests.Screen(@"\\.\DISPLAY1", 0, 0, 1366, 768, primary: true); }

    [Fact]
    public void Going_back_to_a_known_setup_puts_every_item_back_at_its_place_for_that_setup() {
      (ScreenSetupTracker tracker, FakeItem panel) = DockedThenUndocked();
      // Moved by hand on the laptop.
      panel.Rect = new ScreenRect(70, 80, 300, 400);
      tracker.Remember(panel);
      // Docked again.
      screens.Screens = new List<ScreenInfo> { Laptop, Monitor };
      panel.Rect = new ScreenRect(70, 80, 300, 400);
      tracker.DisplayChanged();
      timer.Fire();
      Assert.Equal(new ScreenRect(2500, 100, 300, 400), panel.Rect);
      Assert.Equal(@"\\.\DISPLAY2", panel.ScreenDeviceName);
      // And undocked again: back where it was moved by hand.
      screens.Screens = new List<ScreenInfo> { Laptop };
      tracker.DisplayChanged();
      timer.Fire();
      Assert.Equal(new ScreenRect(70, 80, 300, 400), panel.Rect);
    }

    [Fact]
    public void The_moves_never_overwrite_the_places_of_another_setup() {
      (ScreenSetupTracker tracker, FakeItem panel) = DockedThenUndocked();
      panel.RememberWhileMoving = tracker; // a window saves itself when it moves
      screens.Screens = new List<ScreenInfo> { Laptop, Monitor };
      tracker.DisplayChanged();
      timer.Fire();
      Assert.Equal(2500, PlacesOf(Laptop, Monitor).Single().Left);
      Assert.Equal(50, PlacesOf(Laptop).Single().Left);
    }

    [Fact]
    public void Nothing_is_saved_while_a_display_change_is_pending() {
      (ScreenSetupTracker tracker, FakeItem panel) = DockedThenUndocked();
      tracker.DisplayChanged();
      panel.Rect = new ScreenRect(900, 900, 300, 400); // Windows moving things around
      tracker.Remember(panel);
      Assert.Equal(50, PlacesOf(Laptop).Single().Left);
    }

    [Fact]
    public void Nothing_is_saved_while_moves_are_applied() {
      (ScreenSetupTracker tracker, FakeItem panel) = DockedThenUndocked();
      bool applyingSeen = false;
      FakeItem watcher = new FakeItem(PlacedKind.Panel, 9, 0, 0);
      items.Add(watcher);
      panel.RememberWhileMoving = null;
      // While the panel moves, the tracker says it is applying and a save is ignored.
      FakeItemProbe probe = new FakeItemProbe(panel, tracker, () => applyingSeen = tracker.IsApplying, watcher);
      items[0] = probe;
      screens.Screens = new List<ScreenInfo> { Laptop, Monitor };
      tracker.DisplayChanged();
      timer.Fire();
      Assert.True(applyingSeen);
      Assert.False(tracker.IsApplying);
      // The watcher's save during the move was ignored; it was saved afterwards at the place it really is.
      Assert.Equal(0, PlacesOf(Laptop, Monitor).Single(p => p.ItemID == 9).Left);
    }

    /// <summary>Wraps an item: while it moves, runs a check and asks the tracker to remember another item at a wrong place.</summary>
    private sealed class FakeItemProbe : IPlacedItem {
      private readonly FakeItem inner;
      private readonly ScreenSetupTracker tracker;
      private readonly Action check;
      private readonly FakeItem other;
      public FakeItemProbe(FakeItem inner, ScreenSetupTracker tracker, Action check, FakeItem other) {
        this.inner = inner;
        this.tracker = tracker;
        this.check = check;
        this.other = other;
      }
      public PlacedKind Kind => inner.Kind;
      public long ID => inner.ID;
      public ScreenRect Rect => inner.Rect;
      public string ScreenDeviceName => inner.ScreenDeviceName;
      public void MoveTo(ItemPlace target, IList<ScreenInfo> screens) {
        check();
        ScreenRect real = other.Rect;
        other.Rect = new ScreenRect(4000, 500, 300, 400);
        tracker.Remember(other);
        other.Rect = real;
        inner.MoveTo(target, screens);
      }
    }

    [Fact]
    public void A_save_for_screens_that_changed_without_an_event_is_not_saved_and_starts_the_wait() {
      screens.Screens = new List<ScreenInfo> { Laptop, Monitor };
      FakeItem panel = new FakeItem(PlacedKind.Panel, 1, 2500, 100);
      items.Add(panel);
      ScreenSetupTracker tracker = NewTracker();
      tracker.Start();
      screens.Screens = new List<ScreenInfo> { Laptop };
      panel.Rect = new ScreenRect(10, 10, 300, 400);
      tracker.Remember(panel);
      Assert.Equal(2500, PlacesOf(Laptop, Monitor).Single().Left);
      Assert.True(tracker.IsChangePending);
    }

    [Fact]
    public void Docked_panels_are_docked_again_on_their_screen() {
      screens.Screens = new List<ScreenInfo> { Laptop, Monitor };
      FakeItem panel = new FakeItem(PlacedKind.Panel, 1, 1920 + 2560 - 300, 0, 300, 1400, @"\\.\DISPLAY2");
      items.Add(panel);
      ScreenSetupTracker tracker = NewTracker();
      tracker.Start();
      screens.Screens = new List<ScreenInfo> { Laptop };
      panel.Rect = new ScreenRect(10, 10, 300, 1000);
      tracker.DisplayChanged();
      timer.Fire();
      // Docked again with a taller taskbar on the monitor.
      screens.Screens = new List<ScreenInfo> { Laptop, ScreenSetupTests.Screen(@"\\.\DISPLAY2", 1920, 0, 2560, 1440, taskbar: 100) };
      tracker.DisplayChanged();
      timer.Fire();
      Assert.Equal(new ScreenRect(1920 + 2560 - 300, 0, 300, 1340), panel.Rect);
      Assert.Equal((long)DockSide.Right, PlacesOf(Laptop, Monitor).Single().Dock);
    }

    [Fact]
    public void With_the_setting_off_a_display_change_moves_nothing() {
      (ScreenSetupTracker tracker, FakeItem panel) = DockedThenUndocked();
      tracker.SetEnabled(false);
      FakeItem lost = new FakeItem(PlacedKind.Clone, 3, 3000, 100);
      items.Add(lost);
      screens.Screens = new List<ScreenInfo> { Laptop, Monitor };
      panel.Rect = new ScreenRect(60, 60, 300, 400);
      tracker.DisplayChanged();
      timer.Fire();
      Assert.Empty(panel.Moves);
      Assert.Equal(KeyOf(Laptop, Monitor), tracker.Current.Key);
      screens.Screens = new List<ScreenInfo> { Laptop };
      tracker.DisplayChanged();
      timer.Fire();
      Assert.Empty(lost.Moves); // not even pulled back on screen: exactly as before
    }

    // ---- Tray menu actions ----

    [Fact]
    public void Put_back_moves_everything_to_the_current_setups_saved_places() {
      (ScreenSetupTracker tracker, FakeItem panel) = DockedThenUndocked();
      panel.Rect = new ScreenRect(400, 400, 300, 400); // moved, but not saved (for example by an app)
      Assert.Equal(1, tracker.PutBack());
      Assert.Equal(new ScreenRect(50, 60, 300, 400), panel.Rect);
    }

    [Fact]
    public void Put_back_does_nothing_when_the_setting_is_off() {
      (ScreenSetupTracker tracker, FakeItem panel) = DockedThenUndocked();
      tracker.SetEnabled(false);
      panel.Rect = new ScreenRect(400, 400, 300, 400);
      Assert.Equal(0, tracker.PutBack());
      Assert.Empty(panel.Moves);
    }

    [Fact]
    public void Known_setups_lists_every_setup_most_recently_used_first_with_the_current_one_marked() {
      (ScreenSetupTracker tracker, FakeItem _) = DockedThenUndocked();
      Assert.Equal(new[] { ("1 screen: 1920×1080", true), ("2 screens: 1920×1080, 2560×1440", false) },
        tracker.KnownSetups().Select(s => (s.Name, s.IsCurrent)).ToArray());
      // Docked again: the docked setup is the most recently used one.
      screens.Screens = new List<ScreenInfo> { Laptop, Monitor };
      tracker.DisplayChanged();
      timer.Fire();
      Assert.Equal(new[] { ("2 screens: 1920×1080, 2560×1440", true), ("1 screen: 1920×1080", false) },
        tracker.KnownSetups().Select(s => (s.Name, s.IsCurrent)).ToArray());
    }

    [Fact]
    public void A_screen_with_a_new_device_name_is_the_same_setup_and_gets_no_number() {
      (ScreenSetupTracker tracker, FakeItem panel) = DockedThenUndocked();
      // Docked again, and Windows numbered the monitor DISPLAY5 this time.
      screens.Screens = new List<ScreenInfo> { Laptop, ScreenSetupTests.Screen(@"\\.\DISPLAY5", 1920, 0, 2560, 1440) };
      tracker.DisplayChanged();
      timer.Fire();
      Assert.Equal(new ScreenRect(2500, 100, 300, 400), panel.Rect);
      Assert.Equal(new[] { "2 screens: 1920×1080, 2560×1440", "1 screen: 1920×1080" }, tracker.KnownSetups().Select(s => s.Name).ToArray());
    }

    [Fact]
    public void Only_different_setups_with_the_same_name_are_numbered() {
      // The monitor on the right, then on the left of the laptop: same resolutions, so the same name, but another setup.
      screens.Screens = new List<ScreenInfo> { Laptop, Monitor };
      ScreenSetupTracker tracker = NewTracker();
      tracker.Start();
      screens.Screens = new List<ScreenInfo> { Laptop, ScreenSetupTests.Screen(@"\\.\DISPLAY2", -2560, 0, 2560, 1440) };
      tracker.DisplayChanged();
      timer.Fire();
      Assert.Equal(new[] { "2 screens: 1920×1080, 2560×1440 (2)", "2 screens: 1920×1080, 2560×1440" }, tracker.KnownSetups().Select(s => s.Name).ToArray());
    }

    private static ScreenInfo Single(int width) { return ScreenSetupTests.Screen(@"\\.\DISPLAY1", 0, 0, width, 1080, primary: true); }

    private void Switch(ScreenSetupTracker tracker, int width) {
      screens.Screens = new List<ScreenInfo> { Single(width) };
      tracker.DisplayChanged();
      timer.Fire();
    }

    [Fact]
    public void At_most_10_setups_are_kept_and_the_least_recently_used_one_is_forgotten_with_its_places() {
      screens.Screens = new List<ScreenInfo> { Single(1001) };
      ScreenSetupTracker tracker = NewTracker();
      items.Add(new FakeItem(PlacedKind.Panel, 1, 10, 10));
      tracker.Start();
      for (int width = 1002; width <= 1010; width++) {
        Switch(tracker, width);
      }
      Assert.Equal(ScreenSetupTracker.MaxSetups, DataStore.ScreenSetups.GetAll().Count);
      Assert.Equal(10, ScreenSetupTracker.MaxSetups);
      // 1001 used again, then an 11th setup: 1002 is now the least recently used one.
      Switch(tracker, 1001);
      long forgotten = DataStore.ScreenSetups.Get(KeyOf(Single(1002))).ID;
      Switch(tracker, 1011);
      List<string> keys = DataStore.ScreenSetups.GetAll().Select(s => s.Key).ToList();
      Assert.Equal(10, keys.Count);
      Assert.DoesNotContain(KeyOf(Single(1002)), keys);
      Assert.Contains(KeyOf(Single(1001)), keys);
      Assert.Contains(KeyOf(Single(1011)), keys);
      Assert.Equal(0, db.Scalar("SELECT count(*) FROM ScreenSetupPlaces WHERE SetupID = " + forgotten));
      Assert.Equal(KeyOf(Single(1011)), tracker.Current.Key);
      Assert.Equal("1 screen: 1011×1080", tracker.KnownSetups().First().Name);
    }

    [Fact]
    public void The_current_setup_is_never_forgotten_by_the_limit() {
      // 12 setups from older data, all used later than the current one would be by time stamp alone.
      for (int width = 2001; width <= 2012; width++) {
        long id = DataStore.ScreenSetups.Ensure(KeyOf(Single(width)), "x" + width);
        DataStore.ScreenSetups.Touch(id, "2099-01-01T00:00:" + (width - 2000).ToString("00") + ".0000000Z");
      }
      screens.Screens = new List<ScreenInfo> { Single(1500) };
      ScreenSetupTracker tracker = NewTracker();
      tracker.Start();
      List<string> keys = DataStore.ScreenSetups.GetAll().Select(s => s.Key).ToList();
      Assert.Equal(10, keys.Count);
      Assert.Contains(KeyOf(Single(1500)), keys);
      Assert.True(tracker.KnownSetups().First().IsCurrent);
    }

    [Fact]
    public void Forget_the_other_setups_keeps_only_the_current_one() {
      (ScreenSetupTracker tracker, FakeItem panel) = DockedThenUndocked();
      int changed = 0;
      tracker.Changed += () => changed++;
      tracker.ForgetOthers();
      Assert.Equal(new[] { "1 screen: 1920×1080" }, tracker.KnownSetups().Select(s => s.Name).ToArray());
      Assert.Null(DataStore.ScreenSetups.Get(KeyOf(Laptop, Monitor)));
      Assert.Equal(1, changed);
      // Docking again is now a new setup: nothing moves.
      screens.Screens = new List<ScreenInfo> { Laptop, Monitor };
      tracker.DisplayChanged();
      timer.Fire();
      Assert.Empty(panel.Moves);
    }

    [Fact]
    public void An_error_while_settling_never_escapes() {
      (ScreenSetupTracker tracker, FakeItem _) = DockedThenUndocked();
      tracker.Items = () => throw new InvalidOperationException("boom");
      screens.Screens = new List<ScreenInfo> { Laptop, Monitor };
      tracker.DisplayChanged();
      timer.Fire();
      Assert.False(tracker.IsApplying);
    }

    // ---- Panels and clones as items ----

    [Fact]
    public void A_panel_item_moves_its_view_model_and_saves_the_panels_own_place_too() {
      DesktopWindowViewModel panel = new AppViewModel().AddWindow(@"\\.\DISPLAY1", 100, 100);
      DesktopWindowPlacedItem item = new DesktopWindowPlacedItem(panel);
      Assert.Equal(PlacedKind.Panel, item.Kind);
      Assert.Equal(panel.ID, item.ID);
      item.MoveTo(new ItemPlace(@"\\.\DISPLAY2", new ScreenRect(2000, 50, 320, 1400), DockSide.None), new[] { Laptop, Monitor });
      Assert.Equal(new ScreenRect(2000, 50, 320, 1400), item.Rect);
      Assert.Equal(@"\\.\DISPLAY2", panel.ScreenDeviceName);
      DesktopWindow row = DataStore.Windows.Get(panel.ID);
      Assert.Equal(2000, row.Left);
      Assert.Equal(1400, row.Height);
      Assert.Equal(@"\\.\DISPLAY2", row.ScreenDeviceName);
      Assert.False(panel.IsDocked);
      item.MoveTo(new ItemPlace(@"\\.\DISPLAY2", new ScreenRect(1920, 0, 320, 1400), DockSide.Left), new[] { Laptop, Monitor });
      Assert.True(panel.IsDocked);
    }

    [Fact]
    public void A_clone_item_saves_the_clones_own_place() {
      DesktopWindowViewModel panel = new AppViewModel().AddWindow(@"\\.\DISPLAY1", 100, 100);
      panel.CloneWindow(@"\\.\DISPLAY2", 2000, 100);
      DesktopWindowViewModel clone = panel.Clones.Single();
      DesktopWindowPlacedItem item = new DesktopWindowPlacedItem(clone);
      Assert.Equal(PlacedKind.Clone, item.Kind);
      Assert.Equal(clone.ID, item.ID);
      item.MoveTo(new ItemPlace(@"\\.\DISPLAY1", new ScreenRect(10, 20, 300, 400)), new[] { Laptop });
      Assert.Equal(10, DataStore.Clones.Get(clone.ID).Left);
      Assert.Equal(@"\\.\DISPLAY1", DataStore.Clones.Get(clone.ID).ScreenDeviceName);
    }
  }
}
