using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MDSHO.Common;
using MDSHO.ViewModels;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// The MDSHO icon at the top left of a panel's title bar: a double-click opens About (like the title menu's About),
  /// while a double-click on the rest of the title bar still centres the window on its screen.
  /// </summary>
  public class TitleIconDoubleClickTests {

    /// <summary>Builds a real panel off screen, with About replaced by a recorder.</summary>
    private static void WithPanel(Action<DesktopWindowView, DesktopWindowViewModel, List<IconGlyphs>> check) {
      WpfAppHost.Run(() => {
        using (TempDb db = new TempDb()) {
          AppViewModel appVm = ViewModelFactory.CreateAppViewModel();
          DesktopWindowViewModel windowVm = appVm.Windows[0];
          DesktopWindowView view = new DesktopWindowView(windowVm) { ShowActivated = false, ShowInTaskbar = false };
          List<IconGlyphs> aboutOpened = new List<IconGlyphs>();
          view.ShowAbout = icons => aboutOpened.Add(icons);
          try {
            view.Show();
            view.Left = -10000;
            view.Top = -10000;
            WpfAppHost.Pump();
            // Not the centred size, so a centring would show.
            windowVm.Width = AppConstants.InitialWindowWidth + 37;
            check(view, windowVm, aboutOpened);
          } finally {
            view.Close();
            WpfAppHost.Pump();
          }
        }
      });
    }

    /// <summary>What the title bar gets on a double-click whose mouse-down started on <paramref name="source"/>.</summary>
    private static void DoubleClickOn(DesktopWindowView view, object source) {
      ContentControl header = (ContentControl)view.FindName("ContentControlHeader");
      MouseButtonEventArgs args = new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) {
        RoutedEvent = Control.MouseDoubleClickEvent,
        Source = source
      };
      header.RaiseEvent(args);
      WpfAppHost.Pump();
    }

    [Fact]
    public void Double_clicking_the_title_icon_opens_about_with_the_window_icons_and_does_not_centre() {
      WithPanel((view, windowVm, aboutOpened) => {
        DoubleClickOn(view, view.FindName("TextBlockHeaderIcon"));
        Assert.Single(aboutOpened);
        Assert.Same(windowVm.Appearance.Icons, aboutOpened[0]);
        Assert.Equal((long)(AppConstants.InitialWindowWidth + 37), windowVm.Width);
      });
    }

    [Fact]
    public void Double_clicking_the_rest_of_the_title_bar_still_centres_and_does_not_open_about() {
      WithPanel((view, windowVm, aboutOpened) => {
        DoubleClickOn(view, view.FindName("GridHeader"));
        Assert.Empty(aboutOpened);
        Assert.Equal((long)AppConstants.InitialWindowWidth, windowVm.Width);
      });
    }

    [Fact]
    public void The_title_menu_about_opens_about_the_same_way() {
      WithPanel((view, windowVm, aboutOpened) => {
        MenuItem about = (MenuItem)view.FindName("MenuItemHeaderAboutWindow");
        about.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent, about));
        WpfAppHost.Pump();
        Assert.Single(aboutOpened);
        Assert.Same(windowVm.Appearance.Icons, aboutOpened[0]);
      });
    }

    [Fact]
    public void The_title_icon_is_hit_on_its_whole_box_not_only_on_the_glyph() {
      WithPanel((view, windowVm, aboutOpened) => {
        TextBlock icon = (TextBlock)view.FindName("TextBlockHeaderIcon");
        Assert.NotNull(icon.Background);
      });
    }
  }
}
