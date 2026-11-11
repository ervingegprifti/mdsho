using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using MDSHO.Common;
using MDSHO.Core;
using MDSHO.Data;
using MDSHO.ViewModels;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// Default colours in a panel's Settings window: puts the five colours back to those of a new panel (Icons appearance and
  /// Single click to open stay as they are). Apply saves them; closing without Apply puts the old colours back.
  /// </summary>
  public class PanelDefaultColoursTests {

    private static void WithPanel(Action<DesktopWindowViewModel> body) {
      WpfAppHost.Run(() => {
        using (TempDb db = new TempDb()) {
          AppViewModel app = new AppViewModel();
          body(app.AddWindow("TEST", 0, 0));
        }
      });
    }

    private static void Shown(Window window, Action check) {
      window.WindowStartupLocation = WindowStartupLocation.Manual;
      window.Left = -10000;
      window.Top = -10000;
      window.ShowActivated = false;
      window.Show();
      try {
        WpfAppHost.Pump();
        check();
      } finally {
        if (window.IsVisible) {
          window.Close();
        }
        WpfAppHost.Pump();
      }
    }

    private static Button DefaultColours(Window window) {
      return Assert.IsAssignableFrom<Button>(window.FindName("ButtonDefaultColours"));
    }

    private static void Click(Button button) {
      button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, button));
      WpfAppHost.Pump();
    }

    private static void Paint(DesktopWindowAppearance a) {
      a.BorderColor = "#FF112233";
      a.BackgroundColor = "#FF223344";
      a.TextColor = "#FF334455";
      a.TitleBackgroundColor = "#FF445566";
      a.TitleTextColor = "#FF556677";
    }

    private static DesktopWindow Stored(long id) {
      return DataStore.Windows.GetAll().Single(w => w.ID == id);
    }

    [Fact]
    public void A_new_panel_gets_the_default_colours() {
      WithPanel(win => {
        DesktopWindowAppearance a = win.Appearance;
        Assert.Equal((AppConstants.DefaultWindowBorderColor, AppConstants.DefaultWindowBackgroundColor, AppConstants.DefaultWindowTextColor,
          AppConstants.DefaultWindowTitleBackgroundColor, AppConstants.DefaultWindowTitleTextColor),
          (a.BorderColor, a.BackgroundColor, a.TextColor, a.TitleBackgroundColor, a.TitleTextColor));
        Assert.Equal(("#FF000000", "#99000000", "#FFFFFFFF", "#CC000000", "#FFFFFFFF"),
          (AppConstants.DefaultWindowBorderColor, AppConstants.DefaultWindowBackgroundColor, AppConstants.DefaultWindowTextColor,
           AppConstants.DefaultWindowTitleBackgroundColor, AppConstants.DefaultWindowTitleTextColor));
      });
    }

    [Fact]
    public void The_panel_settings_window_has_Default_colours_left_of_Apply() {
      WithPanel(win => {
        SettingsWindow window = new SettingsWindow(win);
        Shown(window, () => {
          Button defaults = DefaultColours(window);
          Button apply = Assert.IsAssignableFrom<Button>(window.FindName("ButtonApply"));
          Assert.Equal(Visibility.Visible, defaults.Visibility);
          Assert.Equal("Default colours", defaults.Content);
          Assert.True(defaults.TranslatePoint(new Point(0, 0), window).X < apply.TranslatePoint(new Point(0, 0), window).X);
          Assert.Equal("Window appearance", ((Label)window.FindName("LabelAppearanceHeading")).Content);
        });
      });
    }

    [Fact]
    public void Default_colours_resets_only_the_five_colours_and_Apply_saves_them() {
      WithPanel(win => {
        Paint(win.Appearance);
        DataStore.Windows.UpdateBorderColor(win.ID, win.Appearance.BorderColor);
        win.Appearance.SolidIcons = false;
        win.Appearance.SingleClickToOpen = true;
        SettingsWindow window = new SettingsWindow(win);
        Shown(window, () => {
          Click(DefaultColours(window));
          Assert.Equal(AppConstants.DefaultWindowBorderColor, win.Appearance.BorderColor);
          Assert.Equal(AppConstants.DefaultWindowBackgroundColor, win.Appearance.BackgroundColor);
          Assert.Equal(AppConstants.DefaultWindowTextColor, win.Appearance.TextColor);
          Assert.Equal(AppConstants.DefaultWindowTitleBackgroundColor, win.Appearance.TitleBackgroundColor);
          Assert.Equal(AppConstants.DefaultWindowTitleTextColor, win.Appearance.TitleTextColor);
          Assert.False(win.Appearance.SolidIcons);
          Assert.True(win.Appearance.SingleClickToOpen);
          Click(Assert.IsAssignableFrom<Button>(window.FindName("ButtonApply")));
        });
        Assert.Equal(AppConstants.DefaultWindowBorderColor, Stored(win.ID).BorderColor);
        Assert.Equal(AppConstants.DefaultWindowTitleBackgroundColor, Stored(win.ID).TitleBackgroundColor);
      });
    }

    [Fact]
    public void Default_colours_then_closing_without_Apply_puts_the_old_colours_back() {
      WithPanel(win => {
        Paint(win.Appearance);
        SettingsWindow window = new SettingsWindow(win);
        Shown(window, () => {
          Click(DefaultColours(window));
          Assert.Equal(AppConstants.DefaultWindowTextColor, win.Appearance.TextColor);
        });
        Assert.Equal("#FF334455", win.Appearance.TextColor);
        Assert.Equal("#FF112233", win.Appearance.BorderColor);
      });
    }
  }
}
