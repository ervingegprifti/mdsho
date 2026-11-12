using System;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using MDSHO.ViewModels;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// Thin scroll bars in every MDSHO window (one shared style): a 3 px line that widens to 8 px when the mouse is over it,
  /// no arrow buttons, in a thin column of their own so the content never goes under them, in the colour of the content's text.
  /// </summary>
  public class ThinScrollBarTests {

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject {
      for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) {
        DependencyObject child = VisualTreeHelper.GetChild(root, i);
        if (child is T match) {
          yield return match;
        }
        foreach (T nested in Descendants<T>(child)) {
          yield return nested;
        }
      }
    }

    private static Window Host(UIElement content) {
      Window window = new Window {
        WindowStartupLocation = WindowStartupLocation.Manual, Left = -10000, Top = -10000, Width = 300, Height = 200,
        ShowActivated = false, ShowInTaskbar = false, Content = content
      };
      window.Show();
      WpfAppHost.Pump();
      return window;
    }

    // A scroll viewer with content bigger than itself both ways.
    private static ScrollViewer Overflowing(Brush foreground) {
      return new ScrollViewer {
        Width = 200, Height = 120, Foreground = foreground,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        Content = new Border { Width = 600, Height = 900 }
      };
    }

    private static ScrollBar Bar(DependencyObject root, Orientation orientation) {
      return Descendants<ScrollBar>(root).Single(b => b.Orientation == orientation && b.Visibility == Visibility.Visible);
    }

    private static Thumb ThumbOf(ScrollBar bar) {
      return Descendants<Thumb>(bar).Single();
    }

    [Fact]
    public void A_vertical_bar_is_8_px_wide_with_a_3_px_handle_on_its_right_and_no_arrow_buttons() {
      WpfAppHost.Run(() => {
        ScrollViewer viewer = Overflowing(Brushes.Black);
        Window window = Host(viewer);
        try {
          ScrollBar bar = Bar(viewer, Orientation.Vertical);
          Assert.Equal(8, bar.ActualWidth, 1);
          Thumb thumb = ThumbOf(bar);
          Assert.Equal(3, thumb.ActualWidth, 1);
          Assert.Equal(bar.ActualWidth, thumb.TranslatePoint(new Point(thumb.ActualWidth, 0), bar).X, 1);
          // Only the page areas of the track repeat: no line up / line down arrows.
          Assert.DoesNotContain(Descendants<RepeatButton>(bar), r => r.Command == ScrollBar.LineUpCommand || r.Command == ScrollBar.LineDownCommand);
        } finally {
          window.Close();
        }
      });
    }

    [Fact]
    public void A_horizontal_bar_is_8_px_high_with_a_3_px_handle_at_its_bottom() {
      WpfAppHost.Run(() => {
        ScrollViewer viewer = Overflowing(Brushes.Black);
        Window window = Host(viewer);
        try {
          ScrollBar bar = Bar(viewer, Orientation.Horizontal);
          Assert.Equal(8, bar.ActualHeight, 1);
          Thumb thumb = ThumbOf(bar);
          Assert.Equal(3, thumb.ActualHeight, 1);
          Assert.Equal(bar.ActualHeight, thumb.TranslatePoint(new Point(0, thumb.ActualHeight), bar).Y, 1);
          Assert.DoesNotContain(Descendants<RepeatButton>(bar), r => r.Command == ScrollBar.LineLeftCommand || r.Command == ScrollBar.LineRightCommand);
        } finally {
          window.Close();
        }
      });
    }

    [Fact]
    public void The_mouse_over_a_bar_widens_its_handle_to_8_px_and_shows_a_faint_track() {
      WpfAppHost.Run(() => {
        ScrollViewer viewer = Overflowing(Brushes.Black);
        Window window = Host(viewer);
        try {
          ScrollBar bar = Bar(viewer, Orientation.Vertical);
          Trigger hover = bar.Template.Triggers.OfType<Trigger>().Single(t => t.Property == UIElement.IsMouseOverProperty && Equals(t.Value, true));
          Setter width = hover.Setters.OfType<Setter>().Single(s => s.TargetName == "Thumb" && s.Property == FrameworkElement.WidthProperty);
          Assert.Equal(8.0, width.Value);
          Setter track = hover.Setters.OfType<Setter>().Single(s => s.TargetName == "TrackBackground" && s.Property == UIElement.OpacityProperty);
          Assert.True((double)track.Value > 0);
          Assert.Equal(0.0, ((FrameworkElement)bar.Template.FindName("TrackBackground", bar)).Opacity);
        } finally {
          window.Close();
        }
      });
    }

    [Fact]
    public void The_handle_has_the_colour_of_the_content_text_see_through() {
      WpfAppHost.Run(() => {
        ScrollViewer viewer = Overflowing(new SolidColorBrush(Color.FromRgb(0xC0, 0x10, 0x20)));
        Window window = Host(viewer);
        try {
          Thumb thumb = ThumbOf(Bar(viewer, Orientation.Vertical));
          Border handle = Descendants<Border>(thumb).First();
          Assert.Equal(Color.FromRgb(0xC0, 0x10, 0x20), Assert.IsType<SolidColorBrush>(handle.Background).Color);
          Assert.InRange(thumb.Opacity, 0.3, 0.6);
        } finally {
          window.Close();
        }
      });
    }

    [Fact]
    public void The_content_stops_before_the_bars_in_a_thin_column_and_row_of_their_own() {
      WpfAppHost.Run(() => {
        ScrollViewer viewer = Overflowing(Brushes.Black);
        Window window = Host(viewer);
        try {
          Assert.Equal(Visibility.Visible, viewer.ComputedVerticalScrollBarVisibility);
          Assert.Equal(Visibility.Visible, viewer.ComputedHorizontalScrollBarVisibility);
          // The 8 px bar with 2 px either side: 12 px taken, not the 17 px of a standard bar.
          Assert.Equal(viewer.ActualWidth - 12, viewer.ViewportWidth, 1);
          Assert.Equal(viewer.ActualHeight - 12, viewer.ViewportHeight, 1);
          ScrollBar vertical = Bar(viewer, Orientation.Vertical);
          ScrollBar horizontal = Bar(viewer, Orientation.Horizontal);
          Assert.True(vertical.TranslatePoint(new Point(0, 0), viewer).X >= viewer.ViewportWidth);
          Assert.True(horizontal.TranslatePoint(new Point(0, 0), viewer).Y >= viewer.ViewportHeight);
        } finally {
          window.Close();
        }
      });
    }

    [Fact]
    public void A_panel_list_scroll_bar_uses_the_panel_text_colour() {
      using (TempDb db = new TempDb()) {
        WpfAppHost.Run(() => {
          DesktopWindowViewModel w = new AppViewModel().AddWindow("TEST", 100, 100);
          w.Appearance.TextColor = "#FF40C0A0";
          for (int i = 0; i < 40; i++) {
            w.NewGroupShortcutCommand.Execute(null);
          }
          DesktopWindowView view = new DesktopWindowView(w) { ShowActivated = false, ShowInTaskbar = false, Left = -10000, Top = -10000, Height = 300 };
          view.Show();
          try {
            WpfAppHost.Pump();
            TreeView tree = Descendants<TreeView>(view).Single();
            ScrollBar bar = Bar(tree, Orientation.Vertical);
            Border handle = Descendants<Border>(ThumbOf(bar)).First();
            Assert.Equal(Color.FromArgb(0xFF, 0x40, 0xC0, 0xA0), Assert.IsType<SolidColorBrush>(handle.Background).Color);
          } finally {
            view.Close();
          }
        });
      }
    }
  }
}
