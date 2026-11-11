using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using MDSHO.ViewModels;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// A panel has no bottom bar: Settings sits in the title bar just left of Dock left, the PRO badge and its trial code
  /// are gone, and the button list takes the whole space below the title bar, with the rounded bottom corners.
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
    public void Settings_is_in_the_title_bar_just_left_of_Dock_left() {
      WithView((w, view) => {
        Button settings = Named<Button>(view, "ButtonHeaderSettings");
        Button dockLeft = Named<Button>(view, "ButtonHeaderDockWindowLeft");
        Grid header = Named<Grid>(view, "GridHeader");
        Assert.Same(header, settings.Parent);
        Assert.Equal(Grid.GetColumn(dockLeft) - 1, Grid.GetColumn(settings));
        Assert.Equal(w.Appearance.Icons.Settings, settings.Content);
        Assert.Equal("Settings", settings.ToolTip);
        Assert.Equal(Visibility.Visible, settings.Visibility);
        double settingsX = settings.TranslatePoint(new Point(0, 0), header).X;
        double dockX = dockLeft.TranslatePoint(new Point(0, 0), header).X;
        Assert.Equal(dockX - settings.ActualWidth, settingsX, 1);
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
