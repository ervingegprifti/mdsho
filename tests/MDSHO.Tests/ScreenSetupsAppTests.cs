using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using MDSHO.Common;
using MDSHO.Data;
using MDSHO.Helpers;
using MDSHO.ViewModels;
using Xunit;
using WinForms = System.Windows.Forms;

namespace MDSHO.Tests {

  /// <summary>
  /// Screen setups in the app: the "Screen setups" tray submenu and the Screen setups section of the Settings window. The
  /// screens and the timer are fakes; no window really moves.
  /// </summary>
  public class ScreenSetupsAppTests {

    private static readonly ScreenInfo Laptop = ScreenSetupTests.Screen(@"\\.\DISPLAY1", 0, 0, 1920, 1080, primary: true);
    private static readonly ScreenInfo Monitor = ScreenSetupTests.Screen(@"\\.\DISPLAY2", 1920, 0, 2560, 1440);

    /// <summary>Gives the app a tracker on fake screens, a fake timer and one fake panel.</summary>
    private sealed class Setup : IDisposable {
      private readonly ScreenSetupTracker old = AppServices.Current.ScreenSetups;
      public readonly ScreenSetupTrackerTests.FakeScreens Screens = new ScreenSetupTrackerTests.FakeScreens();
      public readonly ScreenSetupTests.FakeTimer Timer = new ScreenSetupTests.FakeTimer();
      public readonly ScreenSetupTrackerTests.FakeItem Panel = new ScreenSetupTrackerTests.FakeItem(PlacedKind.Panel, 1, 2500, 100, screen: @"\\.\DISPLAY2");
      public readonly ScreenSetupTracker Tracker;

      public Setup() {
        Tracker = new ScreenSetupTracker(Screens, Timer) { Items = () => new IPlacedItem[] { Panel } };
        AppServices.Current.ScreenSetups = Tracker;
      }

      /// <summary>Docked (laptop and monitor) at start, then undocked: two known setups, the laptop alone is the current one.</summary>
      public void DockedThenUndocked() {
        Screens.Screens = new List<ScreenInfo> { Laptop, Monitor };
        Tracker.Start();
        Screens.Screens = new List<ScreenInfo> { Laptop };
        Panel.Rect = new ScreenRect(50, 60, 300, 400);
        Tracker.DisplayChanged();
        Timer.Fire();
      }

      public void Dispose() {
        AppServices.Current.ScreenSetups = old;
      }
    }

    private static void OnStaThread(Action body) {
      Exception failure = null;
      Thread thread = new Thread(() => {
        try { body(); } catch (Exception ex) { failure = ex; }
      });
      thread.SetApartmentState(ApartmentState.STA);
      thread.Start();
      thread.Join();
      if (failure != null) {
        throw new Exception("STA body failed: " + failure, failure);
      }
    }

    private static WinForms.ToolStripMenuItem SetupsItem(WinForms.ContextMenuStrip menu) {
      return Assert.IsType<WinForms.ToolStripMenuItem>(menu.Items[1]);
    }

    private static List<string> Texts(WinForms.ToolStripMenuItem item) {
      return item.DropDownItems.Cast<WinForms.ToolStripItem>().Select(i => i.Text).ToList();
    }

    private static WinForms.ToolStripItem Item(WinForms.ToolStripMenuItem submenu, string text) {
      return submenu.DropDownItems.Cast<WinForms.ToolStripItem>().Single(i => i.Text == text);
    }

    // ---- Tray menu ----

    [Fact]
    public void The_Screen_setups_submenu_comes_right_after_Bring_panels_to_front() {
      using (TempDb db = new TempDb())
      using (Setup s = new Setup()) {
        s.DockedThenUndocked();
        OnStaThread(() => {
          using (WinForms.ContextMenuStrip menu = App.CreateTrayMenu(null)) {
            Assert.Equal("Bring panels to front", menu.Items[0].Text);
            Assert.Equal("Screen setups", SetupsItem(menu).Text);
            Assert.IsType<WinForms.ToolStripSeparator>(menu.Items[2]);
            Assert.Equal("New window", menu.Items[3].Text);
          }
        });
      }
    }

    [Fact]
    public void The_submenu_lists_the_known_setups_most_recently_used_first_with_the_current_one_checked_then_the_two_actions() {
      using (TempDb db = new TempDb())
      using (Setup s = new Setup()) {
        s.DockedThenUndocked();
        OnStaThread(() => {
          using (WinForms.ContextMenuStrip menu = App.CreateTrayMenu(null)) {
            WinForms.ToolStripMenuItem setups = SetupsItem(menu);
            Assert.Equal(new[] { "1 screen: 1920×1080", "2 screens: 1920×1080, 2560×1440", "", "Put panels back for this setup", "Forget the other setups" }, Texts(setups));
            Assert.True(((WinForms.ToolStripMenuItem)setups.DropDownItems[0]).Checked);
            Assert.False(((WinForms.ToolStripMenuItem)setups.DropDownItems[1]).Checked);
            Assert.IsType<WinForms.ToolStripSeparator>(setups.DropDownItems[2]);
            Assert.True(Item(setups, "Put panels back for this setup").Enabled);
            Assert.True(Item(setups, "Forget the other setups").Enabled);
          }
        });
      }
    }

    [Fact]
    public void Clicking_a_setup_in_the_list_does_nothing() {
      using (TempDb db = new TempDb())
      using (Setup s = new Setup()) {
        s.DockedThenUndocked();
        OnStaThread(() => {
          using (WinForms.ContextMenuStrip menu = App.CreateTrayMenu(null)) {
            SetupsItem(menu).DropDownItems[0].PerformClick();
          }
        });
        Assert.Empty(s.Panel.Moves);
        Assert.Equal(2, s.Tracker.KnownSetups().Count);
        Assert.Equal(ScreenSetup.From(new[] { Laptop }).Key, s.Tracker.Current.Key);
      }
    }

    [Fact]
    public void Put_panels_back_moves_them_to_the_saved_places_of_this_setup() {
      using (TempDb db = new TempDb())
      using (Setup s = new Setup()) {
        s.DockedThenUndocked();
        s.Panel.Rect = new ScreenRect(700, 700, 300, 400);
        OnStaThread(() => {
          using (WinForms.ContextMenuStrip menu = App.CreateTrayMenu(null)) {
            Item(SetupsItem(menu), "Put panels back for this setup").PerformClick();
          }
        });
        Assert.Equal(new ScreenRect(50, 60, 300, 400), s.Panel.Rect);
      }
    }

    [Fact]
    public void Forget_the_other_setups_leaves_only_this_one_in_the_list() {
      using (TempDb db = new TempDb())
      using (Setup s = new Setup()) {
        s.DockedThenUndocked();
        OnStaThread(() => {
          using (WinForms.ContextMenuStrip menu = App.CreateTrayMenu(null)) {
            WinForms.ToolStripMenuItem setups = SetupsItem(menu);
            Item(setups, "Forget the other setups").PerformClick();
            Assert.Equal(new[] { "1 screen: 1920×1080", "", "Put panels back for this setup", "Forget the other setups" }, Texts(setups));
            // Nothing else to forget now.
            Assert.False(Item(setups, "Forget the other setups").Enabled);
          }
        });
        Assert.Single(DataStore.ScreenSetups.GetAll());
      }
    }

    [Fact]
    public void With_the_setting_off_the_actions_are_greyed_out_but_the_list_is_not() {
      using (TempDb db = new TempDb())
      using (Setup s = new Setup()) {
        s.DockedThenUndocked();
        s.Tracker.SetEnabled(false);
        OnStaThread(() => {
          using (WinForms.ContextMenuStrip menu = App.CreateTrayMenu(null)) {
            WinForms.ToolStripMenuItem setups = SetupsItem(menu);
            Assert.True(setups.Enabled);
            Assert.True(setups.DropDownItems[0].Enabled);
            Assert.True(setups.DropDownItems[1].Enabled);
            Assert.False(Item(setups, "Put panels back for this setup").Enabled);
            Assert.False(Item(setups, "Forget the other setups").Enabled);
          }
        });
      }
    }

    // ---- Settings window ----

    private static void WithSettings(Action<SettingsWindow> check) {
      WpfAppHost.Run(() => {
        DesktopWindowViewModel w = new AppViewModel().AddWindow("TEST", 100, 100);
        SettingsWindow settings = new SettingsWindow(w) {
          WindowStartupLocation = WindowStartupLocation.Manual,
          Left = -10000,
          Top = -10000,
          ShowActivated = false,
          ShowInTaskbar = false
        };
        settings.Show();
        try {
          WpfAppHost.Pump();
          check(settings);
        } finally {
          settings.Close();
          WpfAppHost.Pump();
        }
      });
    }

    private static T Named<T>(FrameworkElement root, string name) where T : class {
      return Assert.IsAssignableFrom<T>(root.FindName(name));
    }

    private static void Click(Button button) {
      button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, button));
      WpfAppHost.Pump();
    }

    [Fact]
    public void Settings_has_a_Screen_setups_section_on_the_Screens_page_with_the_checkbox_on_and_the_hint() {
      using (TempDb db = new TempDb())
      using (Setup s = new Setup()) {
        WithSettings(window => {
          List<Label> labels = WpfAppHost.VisualDescendants<Label>(window).ToList();
          Assert.Equal(new[] { "Icons appearance", "Window theme", "Other", "Keyboard shortcut", "Screen setups" }, labels.Select(l => l.Content as string).ToArray());
          Assert.Equal(window.FindResource("StyleSettingsCategory"), labels.Last().Style);
          CheckBox check = Named<CheckBox>(window, "CheckRememberScreenSetups");
          Assert.Equal("Remember places for each screen setup", check.Content);
          Assert.True(check.IsChecked);
          Assert.True(Named<FrameworkElement>(window, "PageScreens").IsAncestorOf(check));
          TextBlock hint = Named<TextBlock>(window, "TextScreenSetupsHint");
          Assert.Equal("For all panels and notes, on this PC. When screens are connected or disconnected, they move to where they were last time with those screens.", hint.Text);
          // The same style as the shortcut hint.
          TextBlock shortcutHint = Named<TextBlock>(window, "TextShortcutHint");
          Assert.Equal(TextWrapping.Wrap, hint.TextWrapping);
          Assert.Equal(shortcutHint.Margin, hint.Margin);
          Assert.Equal(shortcutHint.Foreground.ToString(), hint.Foreground.ToString());
          Assert.Equal(560, window.Width);
          // The checkbox fits on one line.
          check.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
          Assert.True(check.DesiredSize.Width <= check.ActualWidth + 0.5, "The checkbox is clipped.");
        });
      }
    }

    [Fact]
    public void Turning_it_off_takes_effect_on_Apply() {
      using (TempDb db = new TempDb())
      using (Setup s = new Setup()) {
        WithSettings(window => {
          Named<CheckBox>(window, "CheckRememberScreenSetups").IsChecked = false;
          WpfAppHost.Pump();
          Assert.True(s.Tracker.Enabled); // not before Apply
          Click(Named<Button>(window, "ButtonApply"));
          Assert.False(window.IsVisible);
        });
        Assert.False(s.Tracker.Enabled);
        Assert.Equal("Off", DataStore.AppSettings.Get(ScreenSetupTracker.SettingKey));
      }
    }

    [Fact]
    public void Closing_without_Apply_changes_nothing() {
      using (TempDb db = new TempDb())
      using (Setup s = new Setup()) {
        WithSettings(window => {
          Named<CheckBox>(window, "CheckRememberScreenSetups").IsChecked = false;
          window.Close();
          WpfAppHost.Pump();
        });
        Assert.True(s.Tracker.Enabled);
        Assert.Null(DataStore.AppSettings.Get(ScreenSetupTracker.SettingKey));
      }
    }

    [Fact]
    public void The_checkbox_shows_the_saved_setting_and_Apply_turns_it_back_on() {
      using (TempDb db = new TempDb())
      using (Setup s = new Setup()) {
        s.Tracker.SetEnabled(false);
        WithSettings(window => {
          CheckBox check = Named<CheckBox>(window, "CheckRememberScreenSetups");
          Assert.False(check.IsChecked);
          check.IsChecked = true;
          Click(Named<Button>(window, "ButtonApply"));
        });
        Assert.True(s.Tracker.Enabled);
      }
    }
  }
}
