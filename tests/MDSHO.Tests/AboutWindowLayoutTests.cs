using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// Layout of the real About window: the copyright line must not wrap, and the third-party list must fit inside the
  /// window with its own scroll bar so every entry can be reached.
  /// </summary>
  public class AboutWindowLayoutTests {

    // The window's content below its title bar (row 1 of the window's root grid), inside the rounded frame.
    private static FrameworkElement ContentArea(Window window) {
      return (FrameworkElement)((Grid)window.Content).Children[1];
    }

    /// <summary>Shows the About window off screen, runs <paramref name="check"/> on it, then closes it.</summary>
    private static void WithAboutWindow(Action<AboutWindow> check) {
      WpfAppHost.Run(() => {
        AboutWindow window = new AboutWindow {
          WindowStartupLocation = WindowStartupLocation.Manual,
          Left = -10000,
          Top = -10000,
          ShowActivated = false,
          ShowInTaskbar = false
        };
        window.Show();
        try {
          WpfAppHost.Pump();
          check(window);
        } finally {
          window.Close();
        }
      });
    }

    [Fact]
    public void Copyright_fits_on_one_line() {
      WithAboutWindow(window => {
        TextBlock copyright = (TextBlock)window.FindName("TextCopyright");
        double lineHeight = copyright.FontSize * copyright.FontFamily.LineSpacing;
        Assert.True(copyright.ActualHeight < lineHeight * 1.5,
          $"Copyright wraps: height {copyright.ActualHeight:0.#} for a line height of {lineHeight:0.#}.");
      });
    }

    [Fact]
    public void Third_party_list_fits_in_the_window_and_scrolls() {
      WithAboutWindow(window => {
        ItemsControl items = (ItemsControl)window.FindName("ItemsThirdPartyNotices");
        DependencyObject parent = VisualTreeHelper.GetParent(items);
        while (parent != null && parent is not ScrollViewer) {
          parent = VisualTreeHelper.GetParent(parent);
        }
        ScrollViewer scroll = Assert.IsType<ScrollViewer>(parent);
        // The window's own root visual is exactly the visible client area; the content grid may be taller and get clipped.
        FrameworkElement client = ContentArea(window); // below the title bar, inside the frame
        double bottom = scroll.TranslatePoint(new Point(0, scroll.ActualHeight), client).Y;
        Assert.True(bottom <= client.ActualHeight + 0.5,
          $"The third-party list ends at {bottom:0.#}, below the window's visible height {client.ActualHeight:0.#}, so the last entries can't be seen.");
        Assert.True(scroll.ExtentHeight <= scroll.ViewportHeight || scroll.ScrollableHeight > 0,
          "The third-party list is taller than its area but can't scroll.");
      });
    }

    /// <summary>The <see cref="ScrollViewer"/> around the third-party list.</summary>
    private static ScrollViewer NoticesScroll(AboutWindow window) {
      ItemsControl items = (ItemsControl)window.FindName("ItemsThirdPartyNotices");
      DependencyObject parent = VisualTreeHelper.GetParent(items);
      while (parent != null && parent is not ScrollViewer) {
        parent = VisualTreeHelper.GetParent(parent);
      }
      return Assert.IsType<ScrollViewer>(parent);
    }

    private static double X(FrameworkElement element, Visual relativeTo) {
      return element.TranslatePoint(new Point(0, 0), (UIElement)relativeTo).X;
    }

    private static bool HasAncestor(DependencyObject element, Func<DependencyObject, bool> match) {
      DependencyObject parent = VisualTreeHelper.GetParent(element);
      while (parent != null) {
        if (match(parent)) {
          return true;
        }
        parent = VisualTreeHelper.GetParent(parent);
      }
      return false;
    }

    /// <summary>The visible rows of the label grid as (row, column-0 label, column-1 value), top to bottom.</summary>
    private static List<(int Row, FrameworkElement Label, FrameworkElement Value)> LabelRows(AboutWindow window) {
      Grid grid = Assert.IsType<Grid>(window.FindName("GridAboutLabels"));
      List<FrameworkElement> cells = grid.Children.OfType<FrameworkElement>().Where(c => c.Visibility == Visibility.Visible).ToList();
      return cells.Where(c => Grid.GetColumn(c) == 0)
        .Select(label => (Grid.GetRow(label), label, cells.Single(v => Grid.GetColumn(v) == 1 && Grid.GetRow(v) == Grid.GetRow(label))))
        .OrderBy(r => r.Item1)
        .ToList();
    }

    private static string LabelText(FrameworkElement label) {
      return label is Label l ? l.Content?.ToString() : ((TextBlock)label).Text;
    }

    /// <summary>The text of all runs, also inside hyperlinks (a TextRange drops a trailing hyperlink).</summary>
    private static string InlineText(IEnumerable<Inline> inlines) {
      return string.Concat(inlines.Select(i => i is Run r ? r.Text : i is Span s ? InlineText(s.Inlines) : ""));
    }

    [Fact]
    public void Window_is_720_wide_resizable_and_opens_tall_enough_to_show_every_notice() {
      WithAboutWindow(window => {
        Assert.Equal(720, window.Width);
        Assert.Equal(ResizeMode.CanResize, window.ResizeMode);
        Assert.True(window.MinWidth >= 400 && window.MinHeight >= 300, "Min size " + window.MinWidth + " x " + window.MinHeight);
        window.UpdateLayout();
        ScrollViewer scroll = NoticesScroll(window);
        WpfAppHost.Pump();
        Assert.Equal(Visibility.Collapsed, scroll.ComputedVerticalScrollBarVisibility);
        // Never taller than the screen's work area.
        Assert.True(window.ActualHeight <= SystemParameters.WorkArea.Height + 1, "Height " + window.ActualHeight + " is above the work area " + SystemParameters.WorkArea.Height);
      });
    }

    [Fact]
    public void Made_smaller_by_hand_the_notices_scroll() {
      WithAboutWindow(window => {
        window.Height = 420;
        window.UpdateLayout();
        WpfAppHost.Pump();
        ScrollViewer scroll = NoticesScroll(window);
        Assert.True(scroll.ScrollableHeight > 0, "The shorter window does not scroll its notices.");
      });
    }

    [Fact]
    public void First_row_says_how_to_pronounce_MDSHO_with_the_word_in_italics() {
      WithAboutWindow(window => {
        var rows = LabelRows(window);
        Assert.Equal("Pronounced:", LabelText(rows[0].Label).Trim());
        TextBlock value = Assert.IsType<TextBlock>(window.FindName("TextPronounced"));
        Assert.Same(value, rows[0].Value);
        Assert.Equal("em-dee-show (M, D, then \"show\")", new TextRange(value.ContentStart, value.ContentEnd).Text);
        Run word = value.Inlines.OfType<Run>().First();
        Assert.Equal("em-dee-show", word.Text);
        Assert.Equal(FontStyles.Italic, word.FontStyle);
      });
    }

    [Fact]
    public void All_label_values_start_in_one_column() {
      WithAboutWindow(window => {
        var rows = LabelRows(window);
        Assert.Equal(new[] { "Pronounced:", "Version:", "Developer:", "E-mail:", "Source code:", "Privacy policy:", "Terms & conditions:", "Microsoft Store:" },
          rows.Select(r => LabelText(r.Label).Trim()));
        double labelX = X(rows[0].Label, window);
        double valueX = X(rows[0].Value, window);
        Assert.True(valueX > labelX, "Values do not start right of the labels.");
        foreach (var row in rows) {
          Assert.Equal(labelX, X(row.Label, window), 1);
          Assert.Equal(valueX, X(row.Value, window), 1);
        }
        // The rows keep their named values.
        Assert.Same(window.FindName("LabelVersion"), rows[1].Value);
        Assert.Equal("gegprifti.ervin@gmail.com", ((Label)rows[3].Value).Content);
      });
    }

    [Fact]
    public void The_outer_margin_is_26_pixels_like_the_button_height_in_the_other_windows() {
      Assert.Equal(26, AboutWindow.OuterMargin);
    }

    [Fact]
    public void Content_has_the_outer_margin_on_the_left() {
      WithAboutWindow(window => {
        Visual client = ContentArea(window); // below the title bar, inside the frame
        double m = AboutWindow.OuterMargin;
        Assert.Equal(m, X(LabelRows(window)[0].Label, client), 1);
        Assert.Equal(m, X((FrameworkElement)window.FindName("TextCopyright"), client), 1);
        Assert.Equal(m, X((FrameworkElement)window.FindName("LabelThirdPartyTitle"), client), 1);
        Assert.Equal(m, X((FrameworkElement)window.FindName("ItemsThirdPartyNotices"), client), 1);
      });
    }

    [Fact]
    public void The_title_starts_one_outer_margin_from_the_top() {
      WithAboutWindow(window => {
        Visual client = ContentArea(window); // below the title bar, inside the frame
        StackPanel about = (StackPanel)window.FindName("PanelAbout");
        Label title = about.Children.OfType<Label>().First();
        Assert.Equal(AboutWindow.OuterMargin, title.TranslatePoint(new Point(0, 0), (UIElement)client).Y, 1);
        Assert.Equal(AboutWindow.OuterMargin, X(title, client), 1);
      });
    }

    [Fact]
    public void The_last_notice_ends_one_outer_margin_above_the_bottom_when_scrolled_down() {
      WithAboutWindow(window => {
        // Made smaller by hand, so the notices really scroll.
        window.Height = 420;
        window.UpdateLayout();
        ItemsControl items = (ItemsControl)window.FindName("ItemsThirdPartyNotices");
        ScrollViewer scroll = NoticesScroll(window);
        scroll.ScrollToBottom();
        window.UpdateLayout();
        WpfAppHost.Pump();
        TextBlock last = WpfAppHost.VisualDescendants<TextBlock>(items.ItemContainerGenerator.ContainerFromIndex(items.Items.Count - 1)).First();
        double bottom = last.TranslatePoint(new Point(0, last.ActualHeight), scroll).Y;
        Assert.Equal(AboutWindow.OuterMargin, scroll.ActualHeight - bottom, 1);
      });
    }

    [Fact]
    public void Third_party_list_has_the_outer_margin_on_the_right_like_the_left() {
      WithAboutWindow(window => {
        double m = AboutWindow.OuterMargin;
        ItemsControl items = (ItemsControl)window.FindName("ItemsThirdPartyNotices");
        Assert.Equal(new Thickness(m, 0, m, 0), items.Margin);
        // Every notice line, also the widest, ends at least one outer margin before the visible right edge of the list.
        ScrollViewer scroll = NoticesScroll(window);
        for (int i = 0; i < items.Items.Count; i++) {
          TextBlock line = WpfAppHost.VisualDescendants<TextBlock>(items.ItemContainerGenerator.ContainerFromIndex(i)).First();
          double right = line.TranslatePoint(new Point(line.DesiredSize.Width, 0), scroll).X;
          Assert.True(right <= scroll.ViewportWidth - m + 0.5,
            $"{ThirdPartyNotices.All[i].Name} ends at {right:0.#}, less than {m} px from the right edge {scroll.ViewportWidth:0.#}.");
        }
      });
    }

    [Fact]
    public void Copyright_is_left_aligned_at_the_label_indent() {
      WithAboutWindow(window => {
        TextBlock copyright = (TextBlock)window.FindName("TextCopyright");
        Assert.Equal(TextAlignment.Left, copyright.TextAlignment);
        Assert.Equal(HorizontalAlignment.Left, copyright.HorizontalAlignment);
        Assert.Equal(X(LabelRows(window)[0].Label, window), X(copyright, window), 1);
      });
    }

    [Fact]
    public void Third_party_heading_is_in_the_white_area_above_the_list_and_does_not_scroll() {
      WithAboutWindow(window => {
        FrameworkElement heading = Assert.IsAssignableFrom<FrameworkElement>(window.FindName("LabelThirdPartyTitle"));
        Panel about = Assert.IsAssignableFrom<Panel>(window.FindName("PanelAbout"));
        Assert.False(HasAncestor(heading, p => p is ScrollViewer), "The heading scrolls with the list.");
        Assert.True(HasAncestor(heading, p => ReferenceEquals(p, about)), "The heading is not in the white top area.");
        ScrollViewer scroll = NoticesScroll(window);
        double headingBottom = heading.TranslatePoint(new Point(0, heading.ActualHeight), window).Y;
        double aboutBottom = about.TranslatePoint(new Point(0, about.ActualHeight), window).Y;
        double scrollTop = scroll.TranslatePoint(new Point(0, 0), window).Y;
        Assert.True(headingBottom <= aboutBottom + 0.5 && aboutBottom <= scrollTop + 0.5, "The heading is not right above the list.");
        TextBlock copyright = (TextBlock)window.FindName("TextCopyright");
        double copyrightBottom = copyright.TranslatePoint(new Point(0, copyright.ActualHeight), window).Y;
        Assert.True(heading.TranslatePoint(new Point(0, 0), window).Y - copyrightBottom >= 10, "No gap between the copyright and the heading.");
      });
    }

    [Fact]
    public void Each_notice_is_one_line_that_does_not_wrap() {
      WithAboutWindow(window => {
        ItemsControl items = (ItemsControl)window.FindName("ItemsThirdPartyNotices");
        Assert.Equal(ThirdPartyNotices.All.Count, items.Items.Count);
        for (int i = 0; i < items.Items.Count; i++) {
          ThirdPartyNotice notice = ThirdPartyNotices.All[i];
          FrameworkElement container = Assert.IsAssignableFrom<FrameworkElement>(items.ItemContainerGenerator.ContainerFromIndex(i));
          List<TextBlock> texts = WpfAppHost.VisualDescendants<TextBlock>(container).ToList();
          Assert.NotEmpty(texts);
          Assert.All(texts, t => Assert.Equal(TextWrapping.NoWrap, t.TextWrapping));
          double lineHeight = texts[0].FontSize * texts[0].FontFamily.LineSpacing;
          Assert.True(texts.Max(t => t.ActualHeight) < lineHeight * 1.5 && container.ActualHeight < lineHeight * 2.5,
            $"Notice {notice.Name} is {container.ActualHeight:0.#} high, more than one line ({lineHeight:0.#}).");
          // Name (bold) version, License link (licence type), Project link.
          string text = string.Concat(texts.Select(t => InlineText(t.Inlines)));
          Assert.Equal(notice.Name + " " + notice.Version + ", License (" + notice.License + "), Project", text);
        }
      });
    }

    [Fact]
    public void Notices_scroll_sideways_only_when_needed_and_the_current_ones_fit() {
      WithAboutWindow(window => {
        ScrollViewer scroll = NoticesScroll(window);
        Assert.Equal(ScrollBarVisibility.Auto, scroll.HorizontalScrollBarVisibility);
        Assert.Equal(ScrollBarVisibility.Auto, scroll.VerticalScrollBarVisibility);
        Assert.True(scroll.ExtentWidth <= scroll.ViewportWidth + 0.5,
          $"The notices need a horizontal scroll bar: {scroll.ExtentWidth:0.#} wide in {scroll.ViewportWidth:0.#}.");
        Assert.Equal(Visibility.Collapsed, scroll.ComputedHorizontalScrollBarVisibility);
      });
    }

    [Fact]
    public void Notice_links_keep_their_addresses() {
      WithAboutWindow(window => {
        ItemsControl items = (ItemsControl)window.FindName("ItemsThirdPartyNotices");
        for (int i = 0; i < items.Items.Count; i++) {
          DependencyObject container = items.ItemContainerGenerator.ContainerFromIndex(i);
          List<Hyperlink> links = WpfAppHost.VisualDescendants<TextBlock>(container).SelectMany(t => t.Inlines.OfType<Hyperlink>()).ToList();
          ThirdPartyNotice notice = ThirdPartyNotices.All[i];
          Assert.Equal(new[] { notice.LicenseUrl, notice.ProjectUrl }, links.Select(l => l.NavigateUri));
        }
      });
    }
  }
}
