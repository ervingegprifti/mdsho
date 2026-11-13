using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using MDSHO.ViewModels;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// A panel has no bottom bar: Settings (gear) and Colours (brush) sit in the title bar just left of Dock left, the PRO
  /// badge and its trial code are gone, and the button list takes the whole space below the title bar, with the rounded
  /// bottom corners.
  /// </summary>
  public class PanelTitleBarSettingsTests {

    private static void WithView(Action<DesktopWindowViewModel, DesktopWindowView> body) {
      using (TempDb db = new TempDb()) {
        WpfAppHost.Run(() => {
          DesktopWindowViewModel w = new AppViewModel().AddWindow("TEST", 100, 100);
          DesktopWindowView view = new DesktopWindowView(w) { ShowActivated = false, ShowInTaskbar = false, Left = -10000, Top = -10000 };
          view.Show();
          try {
            WpfAppHost.Pump();
            body(w, view);
          } finally {
            view.Close();
          }
        });
      }
    }

    private static T Named<T>(FrameworkElement root, string name) where T : class {
      return Assert.IsAssignableFrom<T>(root.FindName(name));
    }

    [Fact]
    public void Settings_then_Colours_are_in_the_title_bar_just_left_of_Dock_left() {
      WithView((w, view) => {
        Button settings = Named<Button>(view, "ButtonHeaderSettings");
        Button colors = Named<Button>(view, "ButtonHeaderColors");
        Button dockLeft = Named<Button>(view, "ButtonHeaderDockWindowLeft");
        Grid header = Named<Grid>(view, "GridHeader");
        Assert.Same(header, settings.Parent);
        Assert.Same(header, colors.Parent);
        Assert.Equal(Grid.GetColumn(colors) - 1, Grid.GetColumn(settings));
        Assert.Equal(Grid.GetColumn(dockLeft) - 1, Grid.GetColumn(colors));
        Assert.Equal(w.Appearance.Icons.Settings, settings.Content);
        Assert.Equal("Settings", settings.ToolTip);
        Assert.Equal(w.Appearance.Icons.Brush, colors.Content);
        Assert.Equal("Colours", colors.ToolTip);
        Assert.Equal("Colours", System.Windows.Automation.AutomationProperties.GetName(colors));
        Assert.Equal(Visibility.Visible, settings.Visibility);
        Assert.Equal(Visibility.Visible, colors.Visibility);
        Assert.Same(settings.Style, colors.Style);
        double settingsX = settings.TranslatePoint(new Point(0, 0), header).X;
        double colorsX = colors.TranslatePoint(new Point(0, 0), header).X;
        double dockX = dockLeft.TranslatePoint(new Point(0, 0), header).X;
        Assert.Equal(colorsX - settings.ActualWidth, settingsX, 1);
        Assert.Equal(dockX - colors.ActualWidth, colorsX, 1);
        // The brush follows the Solid icons setting.
        w.Appearance.SolidIcons = !w.Appearance.SolidIcons;
        WpfAppHost.Pump();
        Assert.Equal(w.Appearance.Icons.Brush, colors.Content);
      });
    }

    [Fact]
    public void Settings_and_Colours_open_20_pixels_beside_the_panel_owned_by_it() {
      WithView((w, view) => {
        Assert.Equal(20, DesktopWindowView.DialogGap);
        foreach (string name in new[] { "ButtonHeaderSettings", "ButtonHeaderColors" }) {
          Func<Window, bool?> old = DesktopWindowView.ShowSettingsDialog;
          Window shown = null;
          DesktopWindowView.ShowSettingsDialog = s => { shown = s; return false; };
          try {
            Button button = Named<Button>(view, name);
            button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, button));
            WpfAppHost.Pump();
          } finally {
            DesktopWindowView.ShowSettingsDialog = old;
          }
          Assert.NotNull(shown);
          Assert.Same(view, shown.Owner);
          Assert.Equal(WindowStartupLocation.Manual, shown.WindowStartupLocation);
          // The first guess; once shown it is placed on its handle, right of the panel when there is room, else left.
          Assert.Equal(view.Left + view.ActualWidth + 20, shown.Left, 1);
          Assert.Equal(view.Top, shown.Top, 1);
          shown.Close();
        }
      });
    }

    [Fact]
    public void The_title_bar_Colours_button_opens_the_Colours_window_for_the_panel() {
      WithView((w, view) => {
        Func<Window, bool?> old = DesktopWindowView.ShowSettingsDialog;
        Window shown = null;
        DesktopWindowView.ShowSettingsDialog = s => { shown = s; return false; };
        try {
          Button colors = Named<Button>(view, "ButtonHeaderColors");
          colors.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, colors));
          WpfAppHost.Pump();
        } finally {
          DesktopWindowView.ShowSettingsDialog = old;
        }
        ColorsWindow window = Assert.IsType<ColorsWindow>(shown);
        Assert.Same(w, window.DataContext);
        Assert.StartsWith("Window colours: " + w.Appearance.Title, window.Title);
        window.Close();
      });
    }

    [Fact]
    public void The_title_bar_Settings_button_opens_the_Settings_window_for_the_panel() {
      WithView((w, view) => {
        Func<Window, bool?> old = DesktopWindowView.ShowSettingsDialog;
        Window shown = null;
        DesktopWindowView.ShowSettingsDialog = s => { shown = s; return false; };
        try {
          Button settings = Named<Button>(view, "ButtonHeaderSettings");
          settings.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, settings));
          WpfAppHost.Pump();
        } finally {
          DesktopWindowView.ShowSettingsDialog = old;
        }
        SettingsWindow window = Assert.IsType<SettingsWindow>(shown);
        Assert.Same(w, window.DataContext);
        window.Close();
      });
    }

    [Fact]
    public void There_is_no_bottom_bar_and_the_list_fills_the_space_below_the_title_bar() {
      WithView((w, view) => {
        Assert.Null(view.FindName("BorderFooterRoundedCorners"));
        Assert.Null(view.FindName("GridFooter"));
        Assert.Null(view.FindName("ButtonFooterSettings"));
        Assert.Null(view.FindName("ButtonFooterPRO"));
        Border header = Named<Border>(view, "BorderHeaderRoundedCorners");
        Border body = Named<Border>(view, "BorderBodyRoundedCorners");
        Grid root = Assert.IsType<Grid>(view.Content);
        Assert.Equal(2, root.RowDefinitions.Count);
        Assert.Equal(root.ActualHeight, header.ActualHeight + body.ActualHeight, 1);
        Assert.Equal(new Thickness(1, 0, 1, 1), body.BorderThickness);
      });
    }

    [Fact]
    public void The_list_has_the_rounded_bottom_corners_and_is_square_when_docked() {
      WithView((w, view) => {
        Border body = Named<Border>(view, "BorderBodyRoundedCorners");
        Assert.Equal(new CornerRadius(0, 0, 5, 5), body.CornerRadius);
        w.ApplyDock(-10000, -10000, 500);
        WpfAppHost.Pump();
        Assert.Equal(new CornerRadius(0), body.CornerRadius);
      });
    }

    [Fact]
    public void The_PRO_badge_and_trial_are_gone() {
      Assert.Null(typeof(AppViewModel).GetProperty("Trial"));
      Assert.Null(typeof(AppViewModel).GetProperty("ShowPRO"));
      Assert.Null(typeof(AppViewModel).GetProperty("NewCommandMenuItemHeader"));
      Assert.Null(typeof(ShortcutViewModel).GetProperty("ShowPRO"));
      Assert.Null(typeof(DesktopWindowView).GetField("TextBlockPRO", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public));
      WithView((w, view) => {
        Assert.Null(view.TryFindResource("StyleFooterButtonPRO"));
        Assert.Null(view.TryFindResource("StyleFooterMostLeftButton"));
      });
    }
  }
}
