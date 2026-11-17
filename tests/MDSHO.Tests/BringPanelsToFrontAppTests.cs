using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using MDSHO.Common;
using MDSHO.Data;
using MDSHO.Helpers;
using MDSHO.ViewModels;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// "Bring panels to front" in the app: the tray menu item, the Keyboard shortcut section of the Settings window, Esc in a
  /// panel, opening a button and the WPF key mapping. The hotkey is never really registered and no window is really raised.
  /// </summary>
  public class BringPanelsToFrontAppTests {

    /// <summary>Panels that only record what was asked of them.</summary>
    private sealed class RecordingPanels : IPanelWindows {
      public readonly List<string> Calls = new List<string>();
      public IntPtr Foreground() { return (IntPtr)100; }
      public bool IsAppWindow(IntPtr window) { return window == (IntPtr)1; }
      public IList<IntPtr> Panels() { return new List<IntPtr> { (IntPtr)1 }; }
      public IntPtr WindowAbove(IntPtr window) { return IntPtr.Zero; }
      // An app window (100) covers the panel, so a press brings it up.
      public IList<IntPtr> WindowsAbove(IntPtr window) { return new List<IntPtr> { (IntPtr)100 }; }
      public IList<IntPtr> WindowsBelow(IntPtr window) { return new List<IntPtr>(); }
      public bool IsAlive(IntPtr window) { return true; }
      public bool IsTopmost(IntPtr window) { return false; }
      public void Raise(IList<IntPtr> panels) { Calls.Add("raise"); }
      public void PlaceBehind(IntPtr panel, IntPtr insertAfter) { Calls.Add("place"); }
      public void Activate(IntPtr window) { Calls.Add("activate " + (long)window); }
    }

    /// <summary>Gives the app a fake hotkey service, a started shortcut and a panels controller with recorded panels.</summary>
    private sealed class Setup : IDisposable {
      private readonly PanelsToFront oldPanels = AppServices.Current.PanelsToFront;
      private readonly IGlobalHotkeyService oldService = AppServices.Current.HotkeyService;
      private readonly BringToFrontShortcut oldShortcut = AppServices.Current.BringToFront;
      public readonly FakeHotkeyService Service = new FakeHotkeyService();
      public readonly RecordingPanels Panels = new RecordingPanels();
      public readonly PanelsToFront Controller;
      public readonly BringToFrontShortcut Shortcut;

      public Setup() {
        AppServices.Current.HotkeyService = Service;
        Shortcut = new BringToFrontShortcut(Service);
        AppServices.Current.BringToFront = Shortcut;
        Controller = new PanelsToFront(Panels);
        AppServices.Current.PanelsToFront = Controller;
      }

      public void Dispose() {
        AppServices.Current.PanelsToFront = oldPanels;
        AppServices.Current.HotkeyService = oldService;
        AppServices.Current.BringToFront = oldShortcut;
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

    private static System.Windows.Forms.ToolStripMenuItem BringItem(System.Windows.Forms.ContextMenuStrip menu) {
      return (System.Windows.Forms.ToolStripMenuItem)menu.Items[0];
    }

    // ---- Tray menu ----

    [Fact]
    public void The_tray_menu_starts_with_Bring_panels_to_front() {
      using (TempDb db = new TempDb())
      using (Setup s = new Setup()) {
        s.Shortcut.Start();
        OnStaThread(() => {
          using (System.Windows.Forms.ContextMenuStrip menu = App.CreateTrayMenu(null)) {
            Assert.Equal("Bring panels to front", BringItem(menu).Text);
            // "Screen setups" comes right after it, then the separator.
            Assert.Equal("Screen setups", menu.Items[1].Text);
            Assert.IsType<System.Windows.Forms.ToolStripSeparator>(menu.Items[2]);
            Assert.Equal("New window", menu.Items[3].Text);
          }
        });
      }
    }

    [Fact]
    public void The_tray_item_shows_the_current_shortcut_at_the_right_and_nothing_when_Off() {
      using (TempDb db = new TempDb())
      using (Setup s = new Setup()) {
        s.Shortcut.Start();
        OnStaThread(() => {
          using (System.Windows.Forms.ContextMenuStrip menu = App.CreateTrayMenu(null)) {
            System.Windows.Forms.ToolStripMenuItem item = BringItem(menu);
            Assert.True(item.ShowShortcutKeys);
            Assert.Equal("Ctrl+Shift+Alt+M", item.ShortcutKeyDisplayString);
            s.Shortcut.Apply(null);
            Assert.True(string.IsNullOrEmpty(item.ShortcutKeyDisplayString));
            s.Shortcut.Apply(new Hotkey(HotkeyModifiers.Alt | HotkeyModifiers.Win, 0x70));
            Assert.Equal("Alt+Win+F1", item.ShortcutKeyDisplayString);
          }
        });
      }
    }

    [Fact]
    public void The_tray_item_brings_the_panels_to_front_even_when_the_shortcut_is_Off() {
      using (TempDb db = new TempDb())
      using (Setup s = new Setup()) {
        DataStore.AppSettings.Set(BringToFrontShortcut.SettingKey, "Off");
        s.Shortcut.Start();
        OnStaThread(() => {
          using (System.Windows.Forms.ContextMenuStrip menu = App.CreateTrayMenu(null)) {
            BringItem(menu).PerformClick();
          }
        });
        Assert.True(s.Controller.IsInFront);
        Assert.Equal(new[] { "raise" }, s.Panels.Calls);
      }
    }

    // ---- Settings window ----

    private static void WithSettings(Setup s, Action<SettingsWindow> check) {
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

    private static void PressKey(UIElement target, Key key, ModifierKeys modifiers = ModifierKeys.None) {
      SettingsWindow.KeyboardModifiers = () => modifiers;
      try {
        KeyEventArgs args = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(target), 0, key) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
        if ((modifiers & ModifierKeys.Alt) != 0) {
          // With Alt held, WPF reports Key.System and the real key in SystemKey.
          typeof(KeyEventArgs).GetMethod("MarkSystem", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(args, null);
          Assert.Equal(Key.System, args.Key);
          Assert.Equal(key, args.SystemKey);
        }
        target.RaiseEvent(args);
        WpfAppHost.Pump();
      } finally {
        SettingsWindow.KeyboardModifiers = () => Keyboard.Modifiers;
      }
    }

    private static string Text(Button button) { return Convert.ToString(button.Content); }

    [Fact]
    public void Settings_has_a_Keyboard_shortcut_section_on_the_Keyboard_page() {
      using (TempDb db = new TempDb())
      using (Setup s = new Setup()) {
        s.Shortcut.Start();
        WithSettings(s, window => {
          List<string> headings = WpfAppHost.VisualDescendants<Label>(window).Select(l => l.Content as string).ToList();
          Assert.Equal(new[] { "Icons appearance", "Window theme", "Other", "Keyboard shortcut", "Screen setups", "Backups" }, headings);
          Label heading = WpfAppHost.VisualDescendants<Label>(window).Single(l => (l.Content as string) == "Keyboard shortcut");
          Assert.Equal(window.FindResource("StyleSettingsCategory"), heading.Style);
          Assert.True(Named<FrameworkElement>(window, "PageKeyboard").IsAncestorOf(heading));
          Assert.Equal("Bring panels to front", Named<TextBlock>(window, "TextShortcutLabel").Text);
          Assert.Equal("Ctrl + Shift + Alt + M", Text(Named<Button>(window, "ButtonShortcutBox")));
          Assert.Equal("Default", Text(Named<Button>(window, "ButtonShortcutDefault")));
          Assert.Equal("Off", Text(Named<Button>(window, "ButtonShortcutOff")));
          TextBlock hint = Named<TextBlock>(window, "TextShortcutHint");
          Assert.Equal("For all panels, on this PC. Press it again or Esc to send them back.", hint.Text);
          Assert.Equal(TextWrapping.Wrap, hint.TextWrapping);
          Assert.Equal(720, window.Width);
        });
      }
    }

    [Fact]
    public void Clicking_the_box_records_a_new_shortcut_that_Apply_registers_and_saves() {
      using (TempDb db = new TempDb())
      using (Setup s = new Setup()) {
        s.Shortcut.Start();
        WithSettings(s, window => {
          Button box = Named<Button>(window, "ButtonShortcutBox");
          TextBlock hint = Named<TextBlock>(window, "TextShortcutHint");
          Click(box);
          Assert.Equal("Press the new shortcut…", Text(box));
          Assert.Equal("Use Ctrl, Alt or Win with a key. Esc cancels.", hint.Text);
          Assert.Null(s.Service.Registered);
          PressKey(box, Key.K);
          Assert.Equal("Add Ctrl, Alt or Win to the key.", hint.Text);
          Assert.Equal(Colors.Red, ((SolidColorBrush)hint.Foreground).Color);
          PressKey(box, Key.K, ModifierKeys.Control | ModifierKeys.Alt);
          Assert.Equal("Ctrl + Alt + K", Text(box));
          Assert.NotEqual(Colors.Red, ((SolidColorBrush)hint.Foreground).Color);
          Assert.Equal(Hotkey.Default, s.Service.Registered); // not before Apply
          Click(Named<Button>(window, "ButtonApply"));
          Assert.False(window.IsVisible);
        });
        Assert.Equal(new Hotkey(HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x4B), s.Service.Registered);
        Assert.Equal("Ctrl+Alt+K", DataStore.AppSettings.Get(BringToFrontShortcut.SettingKey));
      }
    }

    [Fact]
    public void Esc_while_recording_cancels_and_does_not_close_the_window() {
      using (TempDb db = new TempDb())
      using (Setup s = new Setup()) {
        s.Shortcut.Start();
        WithSettings(s, window => {
          Button box = Named<Button>(window, "ButtonShortcutBox");
          Click(box);
          PressKey(box, Key.Escape);
          Assert.Equal("Ctrl + Shift + Alt + M", Text(box));
          Assert.True(window.IsVisible);
          Assert.Equal(Hotkey.Default, s.Service.Registered);
        });
      }
    }

    [Fact]
    public void Off_and_Default_buttons_change_the_box_and_closing_without_Apply_changes_nothing() {
      using (TempDb db = new TempDb())
      using (Setup s = new Setup()) {
        s.Shortcut.Start();
        WithSettings(s, window => {
          Button box = Named<Button>(window, "ButtonShortcutBox");
          TextBlock hint = Named<TextBlock>(window, "TextShortcutHint");
          Click(Named<Button>(window, "ButtonShortcutOff"));
          Assert.Equal("Off", Text(box));
          Assert.Equal("No shortcut. The tray menu item still works.", hint.Text);
          Click(Named<Button>(window, "ButtonShortcutDefault"));
          Assert.Equal("Ctrl + Shift + Alt + M", Text(box));
          Click(Named<Button>(window, "ButtonShortcutOff"));
          // Closed with the X: nothing changes.
          window.Close();
          WpfAppHost.Pump();
        });
        Assert.Equal(Hotkey.Default, s.Service.Registered);
        Assert.Null(DataStore.AppSettings.Get(BringToFrontShortcut.SettingKey));
      }
    }

    [Fact]
    public void Closing_while_recording_lets_the_saved_shortcut_work_again() {
      using (TempDb db = new TempDb())
      using (Setup s = new Setup()) {
        s.Shortcut.Start();
        WithSettings(s, window => {
          Click(Named<Button>(window, "ButtonShortcutBox"));
          Assert.Null(s.Service.Registered);
          window.Close();
          WpfAppHost.Pump();
        });
        Assert.Equal(Hotkey.Default, s.Service.Registered);
      }
    }

    [Fact]
    public void Apply_with_a_shortcut_another_app_holds_keeps_the_window_open_with_the_red_hint() {
      using (TempDb db = new TempDb())
      using (Setup s = new Setup()) {
        s.Shortcut.Start();
        s.Service.Taken.Add(new Hotkey(HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x4B));
        WithSettings(s, window => {
          Button box = Named<Button>(window, "ButtonShortcutBox");
          TextBlock hint = Named<TextBlock>(window, "TextShortcutHint");
          Click(box);
          PressKey(box, Key.K, ModifierKeys.Control | ModifierKeys.Alt);
          // Apply from another page goes to the Keyboard page, so the red hint is seen.
          ListBox pages = Named<ListBox>(window, "ListSettingsPages");
          pages.SelectedIndex = 0;
          WpfAppHost.Pump();
          Assert.False(hint.IsVisible);
          Click(Named<Button>(window, "ButtonApply"));
          Assert.True(window.IsVisible);
          Assert.Equal("Keyboard", ((ListBoxItem)pages.SelectedItem).Content);
          Assert.True(hint.IsVisible);
          Assert.Equal("Another app already uses this shortcut. Choose another one.", hint.Text);
          Assert.Equal(Colors.Red, ((SolidColorBrush)hint.Foreground).Color);
          Assert.Equal(Hotkey.Default, s.Service.Registered);
        });
      }
    }

    [Fact]
    public void A_shortcut_that_could_not_be_registered_at_startup_shows_the_red_hint_when_Settings_opens() {
      using (TempDb db = new TempDb())
      using (Setup s = new Setup()) {
        DataStore.AppSettings.Set(BringToFrontShortcut.SettingKey, "Ctrl+Alt+K");
        s.Service.Taken.Add(new Hotkey(HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x4B));
        s.Shortcut.Start();
        WithSettings(s, window => {
          TextBlock hint = Named<TextBlock>(window, "TextShortcutHint");
          Assert.Equal("Another app already uses this shortcut. Choose another one.", hint.Text);
          Assert.Equal(Colors.Red, ((SolidColorBrush)hint.Foreground).Color);
        });
      }
    }

    [Fact]
    public void The_longest_hint_wraps_inside_the_body_and_the_buttons_fit() {
      using (TempDb db = new TempDb())
      using (Setup s = new Setup()) {
        s.Shortcut.Start();
        WithSettings(s, window => {
          FrameworkElement body = Named<FrameworkElement>(window, "PanelBody");
          Button off = Named<Button>(window, "ButtonShortcutOff");
          Click(Named<Button>(window, "ButtonShortcutBox"));
          Point offRight = off.TranslatePoint(new Point(off.ActualWidth, 0), body);
          Assert.True(offRight.X <= body.ActualWidth + 0.5, "Off sticks out of the body: " + offRight.X + " > " + body.ActualWidth);
          Button box = Named<Button>(window, "ButtonShortcutBox");
          box.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
          Assert.True(box.DesiredSize.Width <= box.ActualWidth + 0.5, "The box text is clipped while recording.");
        });
      }
    }

    // ---- Esc in a panel ----

    private static void WithPanel(Action<DesktopWindowView> check) {
      WpfAppHost.Run(() => {
        using (TempDb db = new TempDb()) {
          AppViewModel appVm = ViewModelFactory.CreateAppViewModel();
          DesktopWindowView view = new DesktopWindowView(appVm.Windows[0]) { ShowActivated = false, ShowInTaskbar = false, Left = -10000, Top = -10000 };
          try {
            view.Show();
            view.Left = -10000;
            view.Top = -10000;
            WpfAppHost.Pump();
            check(view);
          } finally {
            view.Close();
            WpfAppHost.Pump();
          }
        }
      });
    }

    private static bool PressEscape(Window window) {
      KeyEventArgs args = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(window), 0, Key.Escape) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
      window.RaiseEvent(args);
      WpfAppHost.Pump();
      return args.Handled;
    }

    [Fact]
    public void Esc_in_a_panel_brought_to_front_sends_the_panels_back() {
      using (Setup s = new Setup()) {
        WithPanel(view => {
          s.Controller.Toggle();
          Assert.True(PressEscape(view));
          Assert.False(s.Controller.IsInFront);
          Assert.Equal(new[] { "raise", "place", "activate 100" }, s.Panels.Calls);
        });
      }
    }

    [Fact]
    public void Esc_in_a_panel_that_was_not_brought_to_front_does_nothing() {
      using (Setup s = new Setup()) {
        WithPanel(view => {
          Assert.False(PressEscape(view));
          Assert.Empty(s.Panels.Calls);
        });
      }
    }

    // ---- Opening a button ----

    [Fact]
    public void Opening_a_button_tells_the_app_so_panels_brought_to_front_go_back() {
      Action old = ShortcutViewModel.Opened;
      List<string> opened = new List<string>();
      try {
        using (TempDb db = new TempDb()) {
          ShortcutViewModel.Opened = () => opened.Add("opened");
          AppViewModel app = new AppViewModel();
          DesktopWindowViewModel win = app.AddWindow("TEST", 0, 0);
          ShortcutViewModel command = ViewModelFactory.NewProcessCommandShortcut(app, null, win);
          win.RootShortcuts.Add(command);
          command.StartProcessAction = (v, admin) => { };
          ShortcutViewModel group = ViewModelFactory.NewGroupShortcut(app, null, win);
          win.RootShortcuts.Add(group);
          command.Run();
          Assert.Equal(new[] { "opened" }, opened);
          group.Run();
          command.BeginEdit(EditField.Name);
          command.Run();
          Assert.Equal(new[] { "opened" }, opened);
        }
      } finally {
        ShortcutViewModel.Opened = old;
      }
    }

    [Fact]
    public void The_app_sends_panels_back_when_a_button_opens() {
      using (Setup s = new Setup()) {
        Action old = ShortcutViewModel.Opened;
        try {
          App.ConnectPanelsToFront(s.Controller);
          s.Controller.Toggle();
          ShortcutViewModel.Opened();
          Assert.False(s.Controller.IsInFront);
          Assert.Equal(new[] { "raise", "place" }, s.Panels.Calls);
        } finally {
          ShortcutViewModel.Opened = old;
        }
      }
    }

    // ---- WPF keys ----

    [Fact]
    public void WPF_keys_map_to_the_virtual_keys_and_modifiers_RegisterHotKey_uses() {
      Assert.Equal((HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x4D), KeyInput.ToHotkey(Key.System, Key.M, ModifierKeys.Control | ModifierKeys.Alt));
      Assert.Equal((HotkeyModifiers.Win | HotkeyModifiers.Shift, 0x74), KeyInput.ToHotkey(Key.F5, Key.None, ModifierKeys.Windows | ModifierKeys.Shift));
      Assert.Equal((HotkeyModifiers.Control, 0xA2), KeyInput.ToHotkey(Key.LeftCtrl, Key.None, ModifierKeys.Control));
      Assert.Equal((HotkeyModifiers.None, 0x1B), KeyInput.ToHotkey(Key.Escape, Key.None, ModifierKeys.None));
    }
  }
}
