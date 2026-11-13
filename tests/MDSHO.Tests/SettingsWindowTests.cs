using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using MDSHO.ViewModels;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// The real Settings window of a panel, shown off screen: Icons appearance and Other, then Apply. The colours are in the
  /// Colours window (the brush button next to the gear, see ColorsWindowTests).
  /// </summary>
  public class SettingsWindowTests {

    // The window's content below its title bar (row 1 of the window's root grid), inside the rounded frame.
    private static FrameworkElement ContentArea(Window window) {
      return (FrameworkElement)((Grid)window.Content).Children[1];
    }

    private static void WithSettings(Action<SettingsWindow, DesktopWindowViewModel> check) {
      using (TempDb db = new TempDb()) {
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
            check(settings, w);
          } finally {
            settings.Close();
            WpfAppHost.Pump();
          }
        });
      }
    }

    private static T Named<T>(FrameworkElement root, string name) where T : class {
      return Assert.IsAssignableFrom<T>(root.FindName(name));
    }

    [Fact]
    public void Settings_has_Icons_appearance_and_Other_and_no_colours() {
      WithSettings((window, w) => {
        List<string> headings = WpfAppHost.VisualDescendants<Label>(window).Select(l => l.Content as string).ToList();
        Assert.Equal(new[] { "Icons appearance", "Other" }, headings);
        Assert.Null(window.FindName("ListColorTargets"));
        Assert.Null(window.FindName("PickerColor"));
        Assert.Null(window.FindName("ButtonDefaultColours"));
        Assert.Null(window.FindName("ButtonSetAsDefault"));
        Assert.StartsWith("Settings", window.Title);
      });
    }

    [Fact]
    public void Icons_appearance_and_single_click_to_open_still_work() {
      WithSettings((window, w) => {
        List<RadioButton> radios = WpfAppHost.VisualDescendants<RadioButton>(window).ToList();
        Assert.Equal(new[] { "Solid icons", "Outlined icons" }, radios.Select(r => (string)r.Content).ToArray());
        radios[1].IsChecked = true;
        WpfAppHost.Pump();
        Assert.False(w.Appearance.SolidIcons);
        CheckBox single = Named<CheckBox>(window, "CheckSingleClickToOpen");
        Assert.Equal("Single click to open", single.Content);
        bool before = w.Appearance.SingleClickToOpen;
        single.IsChecked = !before;
        WpfAppHost.Pump();
        Assert.Equal(!before, w.Appearance.SingleClickToOpen);
      });
    }

    [Fact]
    public void Every_outer_margin_is_the_apply_button_height_and_nothing_is_clipped() {
      WithSettings((window, w) => {
        Assert.Equal(ResizeMode.NoResize, window.ResizeMode);
        FrameworkElement client = ContentArea(window);
        FrameworkElement body = Named<FrameworkElement>(window, "PanelBody");
        Button apply = Named<Button>(window, "ButtonApply");
        double h = apply.ActualHeight;
        Assert.True(h > 10, "Apply height " + h);
        Assert.Equal(new Thickness(22, 5, 22, 5), apply.Padding);
        Point topLeft = body.TranslatePoint(new Point(0, 0), client);
        Assert.Equal(h, topLeft.X, 1);
        Assert.Equal(h, topLeft.Y, 1);
        Assert.Equal(client.ActualWidth - h, topLeft.X + body.ActualWidth, 1);
        // The window is just tall enough that the content's bottom margin is the button height too.
        FrameworkElement footer = Named<FrameworkElement>(window, "Footer");
        double footerTop = footer.TranslatePoint(new Point(0, 0), client).Y;
        Assert.Equal(footerTop - h, topLeft.Y + body.ActualHeight, 1.5);
        Point applyTopLeft = apply.TranslatePoint(new Point(0, 0), footer);
        Assert.Equal(h, applyTopLeft.Y, 1);
        Assert.Equal(footer.ActualHeight - h, applyTopLeft.Y + apply.ActualHeight, 1);
        Assert.Equal(footer.ActualWidth - h, applyTopLeft.X + apply.ActualWidth, 1);
        double bottom = footer.TranslatePoint(new Point(0, footer.ActualHeight), client).Y;
        Assert.Equal(client.ActualHeight, bottom, 1);
        // The longest line fits without wrapping or clipping.
        CheckBox version = Named<CheckBox>(window, "CheckShowVersionInTitle");
        version.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Assert.True(version.DesiredSize.Width <= version.ActualWidth + 0.5, "Show version in title is clipped.");
      });
    }

    [Fact]
    public void Closing_without_Apply_puts_the_settings_back() {
      WithSettings((window, w) => {
        bool solid = w.Appearance.SolidIcons;
        bool single = w.Appearance.SingleClickToOpen;
        w.Appearance.SolidIcons = !solid;
        w.Appearance.SingleClickToOpen = !single;
        window.Close();
        WpfAppHost.Pump();
        Assert.Equal(solid, w.Appearance.SolidIcons);
        Assert.Equal(single, w.Appearance.SingleClickToOpen);
      });
    }
  }
}
