using System;
using System.Collections.Generic;
using MDSHO.Common;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// Screen setups, the plain part: which screens make a setup (its key), its name, where an item goes in a setup and the
  /// debounce of display changes. No WPF, no database.
  /// </summary>
  public class ScreenSetupTests {

    internal static ScreenInfo Screen(string name, double left, double top, double width, double height, bool primary = false, double scale = 1.0, double taskbar = 40) {
      return new ScreenInfo(name, new ScreenRect(left, top, width, height), new ScreenRect(left, top, width, height - taskbar), scale, primary);
    }

    private static readonly ScreenInfo Laptop = Screen(@"\\.\DISPLAY1", 0, 0, 1920, 1080, primary: true);
    private static readonly ScreenInfo Monitor = Screen(@"\\.\DISPLAY2", 1920, 0, 2560, 1440);

    // ---- Key and name ----

    [Fact]
    public void The_key_has_each_screens_resolution_the_primary_and_its_column_and_row_and_does_not_depend_on_the_order() {
      ScreenSetup a = ScreenSetup.From(new[] { Laptop, Monitor });
      ScreenSetup b = ScreenSetup.From(new[] { Monitor, Laptop });
      Assert.Equal(a.Key, b.Key);
      Assert.Equal("1920x1080*@c0r0|2560x1440@c1r0", a.Key);
      Assert.Equal(2, a.ScreenCount);
    }

    [Fact]
    public void Renumbered_device_names_are_the_same_setup() {
      // Windows renumbers \\.\DISPLAYn when screens are reconnected or a laptop is docked again.
      ScreenInfo laptop5 = Screen(@"\\.\DISPLAY5", 0, 0, 1920, 1080, primary: true);
      ScreenInfo monitor9 = Screen(@"\\.\DISPLAY9", 1920, 0, 2560, 1440);
      Assert.Equal(ScreenSetup.From(new[] { Laptop, Monitor }).Key, ScreenSetup.From(new[] { laptop5, monitor9 }).Key);
      Assert.Equal(ScreenSetup.From(new[] { Laptop }).Key, ScreenSetup.From(new[] { Screen(@"\\.\DISPLAY3", 0, 0, 1920, 1080, primary: true) }).Key);
    }

    [Fact]
    public void A_small_rearrangement_is_the_same_setup_but_another_side_is_not() {
      string right = ScreenSetup.From(new[] { Laptop, Monitor }).Key;
      // The monitor dragged lower in Display settings, still on the right.
      Assert.Equal(right, ScreenSetup.From(new[] { Laptop, Screen(@"\\.\DISPLAY2", 1920, 300, 2560, 1440) }).Key);
      Assert.Equal(right, ScreenSetup.From(new[] { Laptop, Screen(@"\\.\DISPLAY2", 1920, -500, 2560, 1440) }).Key);
      // On the left or above the laptop it is another setup.
      string left = ScreenSetup.From(new[] { Laptop, Screen(@"\\.\DISPLAY2", -2560, 0, 2560, 1440) }).Key;
      string above = ScreenSetup.From(new[] { Laptop, Screen(@"\\.\DISPLAY2", -300, -1440, 2560, 1440) }).Key;
      Assert.Equal("2560x1440@c0r0|1920x1080*@c1r0", left);
      Assert.Equal("2560x1440@c0r0|1920x1080*@c0r1", above);
      Assert.NotEqual(right, left);
      Assert.NotEqual(right, above);
    }

    [Fact]
    public void Two_identical_monitors_swapped_are_the_same_setup() {
      ScreenInfo a = Screen(@"\\.\DISPLAY2", 1920, 0, 1920, 1080);
      ScreenInfo b = Screen(@"\\.\DISPLAY3", 3840, 0, 1920, 1080);
      ScreenInfo aSwapped = Screen(@"\\.\DISPLAY3", 1920, 0, 1920, 1080);
      ScreenInfo bSwapped = Screen(@"\\.\DISPLAY2", 3840, 0, 1920, 1080);
      Assert.Equal(ScreenSetup.From(new[] { Laptop, a, b }).Key, ScreenSetup.From(new[] { Laptop, aSwapped, bSwapped }).Key);
    }

    [Fact]
    public void A_different_resolution_primary_or_number_of_screens_is_a_different_setup() {
      string key = ScreenSetup.From(new[] { Laptop }).Key;
      Assert.NotEqual(key, ScreenSetup.From(new[] { Screen(@"\\.\DISPLAY1", 0, 0, 1366, 768, primary: true) }).Key);
      Assert.NotEqual(key, ScreenSetup.From(new[] { Laptop, Monitor }).Key);
      // The same two screens with the monitor as the primary one.
      ScreenInfo laptopNotPrimary = Screen(@"\\.\DISPLAY1", -1920, 0, 1920, 1080);
      ScreenInfo monitorPrimary = Screen(@"\\.\DISPLAY2", 0, 0, 2560, 1440, primary: true);
      Assert.Equal("1920x1080@c0r0|2560x1440*@c1r0", ScreenSetup.From(new[] { laptopNotPrimary, monitorPrimary }).Key);
      Assert.NotEqual(ScreenSetup.From(new[] { Laptop, Monitor }).Key, ScreenSetup.From(new[] { laptopNotPrimary, monitorPrimary }).Key);
    }

    [Fact]
    public void A_key_of_1_7_0_is_turned_into_the_new_key_and_name() {
      // 1.7.0 keyed setups by device name and exact bounds; the primary screen is the one at 0,0.
      ScreenSetup setup = ScreenSetup.FromLegacyKey(@"\\.\DISPLAY1@0,0,1920x1080|\\.\DISPLAY2@1920,0,2560x1440");
      Assert.Equal("1920x1080*@c0r0|2560x1440@c1r0", setup.Key);
      Assert.Equal("2 screens: 1920×1080, 2560×1440", setup.Name);
      Assert.Equal(ScreenSetup.From(new[] { Laptop, Monitor }).Key,
        ScreenSetup.FromLegacyKey(@"\\.\DISPLAY7@1920,-200,2560x1440|\\.\DISPLAY4@0,0,1920x1080").Key);
      // A new key, or text that is no 1.7.0 key, gives null.
      Assert.Null(ScreenSetup.FromLegacyKey("1920x1080*@c0r0"));
      Assert.Null(ScreenSetup.FromLegacyKey("nonsense"));
      Assert.Null(ScreenSetup.FromLegacyKey(""));
    }

    [Fact]
    public void The_working_area_and_scale_do_not_change_the_setup() {
      // The taskbar moving or the text size changing is not a new setup.
      ScreenInfo sameScreenOtherTaskbar = Screen(@"\\.\DISPLAY1", 0, 0, 1920, 1080, primary: true, scale: 1.5, taskbar: 80);
      Assert.Equal(ScreenSetup.From(new[] { Laptop }).Key, ScreenSetup.From(new[] { sameScreenOtherTaskbar }).Key);
    }

    [Fact]
    public void The_name_has_the_count_and_the_resolutions_primary_first() {
      Assert.Equal("1 screen: 1920×1080", ScreenSetup.From(new[] { Laptop }).Name);
      Assert.Equal("2 screens: 1920×1080, 2560×1440", ScreenSetup.From(new[] { Monitor, Laptop }).Name);
      // With the monitor as the primary screen it comes first; the others follow from left to right.
      ScreenInfo primaryMonitor = Screen(@"\\.\DISPLAY2", 0, 0, 2560, 1440, primary: true);
      ScreenInfo left = Screen(@"\\.\DISPLAY1", -1920, 0, 1920, 1080);
      ScreenInfo right = Screen(@"\\.\DISPLAY3", 2560, 0, 1280, 1024);
      Assert.Equal("3 screens: 2560×1440, 1920×1080, 1280×1024", ScreenSetup.From(new[] { right, primaryMonitor, left }).Name);
    }

    [Fact]
    public void No_screens_is_an_empty_setup() {
      ScreenSetup none = ScreenSetup.From(new ScreenInfo[0]);
      Assert.Equal("", none.Key);
      Assert.Equal(0, none.ScreenCount);
    }

    // ---- Describing where an item is ----

    [Fact]
    public void Describe_finds_the_screen_the_item_is_mostly_on() {
      IList<ScreenInfo> screens = new[] { Laptop, Monitor };
      ItemPlace place = ScreenSetupPlacement.Describe(new ScreenRect(1800, 100, 300, 400), "old", screens);
      Assert.Equal(@"\\.\DISPLAY2", place.ScreenDeviceName);
      Assert.Equal(DockSide.None, place.Dock);
      Assert.Equal(new ScreenRect(1800, 100, 300, 400), place.Rect);
    }

    [Fact]
    public void Describe_keeps_the_given_screen_when_the_item_is_on_no_screen() {
      ItemPlace place = ScreenSetupPlacement.Describe(new ScreenRect(9000, 9000, 300, 400), @"\\.\DISPLAY9", new[] { Laptop });
      Assert.Equal(@"\\.\DISPLAY9", place.ScreenDeviceName);
    }

    [Fact]
    public void Describe_sees_a_panel_docked_to_the_left_or_right_of_its_screen() {
      IList<ScreenInfo> screens = new[] { Laptop, Monitor };
      Assert.Equal(DockSide.Left, ScreenSetupPlacement.Describe(new ScreenRect(0, 0, 300, 1040), "", screens).Dock);
      Assert.Equal(DockSide.Right, ScreenSetupPlacement.Describe(new ScreenRect(1920 + 2560 - 300, 0, 300, 1400), "", screens).Dock);
      Assert.Equal(DockSide.None, ScreenSetupPlacement.Describe(new ScreenRect(0, 0, 300, 900), "", screens).Dock);
    }

    // ---- Choosing where an item goes ----

    [Fact]
    public void Choose_uses_the_saved_place() {
      IList<ScreenInfo> screens = new[] { Laptop, Monitor };
      ItemPlace saved = new ItemPlace(@"\\.\DISPLAY2", new ScreenRect(2000, 100, 300, 400));
      ItemPlace current = new ItemPlace(@"\\.\DISPLAY1", new ScreenRect(10, 10, 320, 420));
      Assert.Equal(saved, ScreenSetupPlacement.Choose(saved, current, screens));
    }

    [Fact]
    public void Choose_keeps_an_item_without_a_saved_place_where_it_is_when_it_is_on_screen() {
      ItemPlace current = new ItemPlace(@"\\.\DISPLAY1", new ScreenRect(10, 10, 320, 420));
      Assert.Equal(current, ScreenSetupPlacement.Choose(null, current, new[] { Laptop }));
    }

    [Fact]
    public void Choose_pulls_an_item_without_a_saved_place_onto_a_screen_when_it_is_off_screen() {
      ItemPlace current = new ItemPlace(@"\\.\DISPLAY2", new ScreenRect(2000, 100, 300, 400));
      ItemPlace target = ScreenSetupPlacement.Choose(null, current, new[] { Laptop });
      Assert.True(ScreenGeometry.IsTitleBarReachable(target.Rect, new[] { Laptop }));
      Assert.Equal(@"\\.\DISPLAY1", target.ScreenDeviceName);
      Assert.Equal(300, target.Rect.Width);
      Assert.Equal(400, target.Rect.Height);
    }

    [Fact]
    public void Choose_docks_a_saved_docked_panel_again_to_its_screens_working_area() {
      // Saved docked right on the monitor; the taskbar is taller now, so the height follows the working area.
      ScreenInfo monitorTallTaskbar = Screen(@"\\.\DISPLAY2", 1920, 0, 2560, 1440, taskbar: 100);
      ItemPlace saved = new ItemPlace(@"\\.\DISPLAY2", new ScreenRect(1920 + 2560 - 300, 0, 300, 1400), DockSide.Right);
      ItemPlace target = ScreenSetupPlacement.Choose(saved, new ItemPlace(@"\\.\DISPLAY1", new ScreenRect(0, 0, 300, 400)), new[] { Laptop, monitorTallTaskbar });
      Assert.Equal(new ScreenRect(1920 + 2560 - 300, 0, 300, 1340), target.Rect);
      Assert.Equal(DockSide.Right, target.Dock);
      ItemPlace savedLeft = new ItemPlace(@"\\.\DISPLAY2", new ScreenRect(1920, 0, 250, 1400), DockSide.Left);
      Assert.Equal(new ScreenRect(1920, 0, 250, 1340), ScreenSetupPlacement.Choose(savedLeft, savedLeft, new[] { Laptop, monitorTallTaskbar }).Rect);
    }

    [Fact]
    public void Choose_docks_on_the_screen_under_the_saved_place_even_when_its_device_name_changed() {
      ScreenInfo monitorRenamed = Screen(@"\\.\DISPLAY7", 1920, 0, 2560, 1440, taskbar: 100);
      ItemPlace saved = new ItemPlace(@"\\.\DISPLAY2", new ScreenRect(1920 + 2560 - 300, 0, 300, 1400), DockSide.Right);
      ItemPlace target = ScreenSetupPlacement.Choose(saved, saved, new[] { Laptop, monitorRenamed });
      Assert.Equal(new ScreenRect(1920 + 2560 - 300, 0, 300, 1340), target.Rect);
      Assert.Equal(@"\\.\DISPLAY7", target.ScreenDeviceName);
    }

    [Fact]
    public void Choose_never_leaves_a_saved_place_off_screen() {
      ItemPlace saved = new ItemPlace(@"\\.\DISPLAY2", new ScreenRect(5000, 100, 300, 400));
      ItemPlace target = ScreenSetupPlacement.Choose(saved, saved, new[] { Laptop });
      Assert.True(ScreenGeometry.IsTitleBarReachable(target.Rect, new[] { Laptop }));
    }

    // ---- Debounce ----

    internal sealed class FakeTimer : IOneShotTimer {
      public TimeSpan? Delay;
      private Action elapsed;
      public int Starts;
      public bool Running => elapsed != null;
      public void Start(TimeSpan delay, Action elapsed) { Delay = delay; this.elapsed = elapsed; Starts++; }
      public void Stop() { elapsed = null; }
      public void Fire() {
        Action a = elapsed;
        elapsed = null;
        a?.Invoke();
      }
    }

    [Fact]
    public void Several_triggers_settle_once_after_the_delay() {
      FakeTimer timer = new FakeTimer();
      int settled = 0;
      Debouncer debouncer = new Debouncer(timer, TimeSpan.FromSeconds(1.5), () => settled++);
      Assert.False(debouncer.Pending);
      debouncer.Trigger();
      debouncer.Trigger();
      debouncer.Trigger();
      Assert.True(debouncer.Pending);
      Assert.Equal(0, settled);
      Assert.Equal(3, timer.Starts); // each trigger starts the wait again
      Assert.Equal(TimeSpan.FromSeconds(1.5), timer.Delay);
      timer.Fire();
      Assert.Equal(1, settled);
      Assert.False(debouncer.Pending);
      timer.Fire();
      Assert.Equal(1, settled);
    }

    [Fact]
    public void Pending_is_still_true_while_the_settled_action_runs_and_false_after() {
      FakeTimer timer = new FakeTimer();
      Debouncer debouncer = null;
      bool pendingInside = true;
      debouncer = new Debouncer(timer, TimeSpan.FromSeconds(1), () => pendingInside = debouncer.Pending);
      debouncer.Trigger();
      timer.Fire();
      Assert.False(pendingInside);
      Assert.False(debouncer.Pending);
    }

    [Fact]
    public void Cancel_stops_a_pending_settle() {
      FakeTimer timer = new FakeTimer();
      int settled = 0;
      Debouncer debouncer = new Debouncer(timer, TimeSpan.FromSeconds(1), () => settled++);
      debouncer.Trigger();
      debouncer.Cancel();
      Assert.False(debouncer.Pending);
      Assert.False(timer.Running);
      timer.Fire();
      Assert.Equal(0, settled);
    }
  }
}
