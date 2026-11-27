using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// The Note, Comment and snippet editors have 20 px of padding, and a click in it puts the caret at the nearest spot of
  /// the text, so the text doesn't have to be hit exactly. Clicks go in as Windows delivers them (see <see cref="RawMouse"/>).
  /// </summary>
  public class EditorPaddingTests {

    private const string Text = "Restart the server now\r\nThen check the dashboard\r\nCall Sam";
    private const int Line2 = 24, Line3 = 50;

    private static int Pace => RawMouse.DoubleClickTime * 2 / 5;

    private static void InEditor(string kind, string text, Action<TextBox> check) {
      LiveHighlightTests.WithGroup(f => {
        TextEditWindow window = LiveHighlightTests.Open(f, kind);
        LiveHighlightTests.Shown(window, () => {
          TextBox editor = LiveHighlightTests.Named<TextBox>(window, "TextComment");
          editor.Text = text;
          editor.Select(text.Length, 0);
          WpfAppHost.Pump();
          check(editor);
        });
      });
    }

    private static Rect At(TextBox editor, int index) {
      return editor.GetRectFromCharacterIndex(index);
    }

    private static double MidY(TextBox editor, int index) {
      Rect r = At(editor, index);
      return r.Y + r.Height / 2;
    }

    private static void Click(TextBox editor, double x, double y, int clicks = 1) {
      RawMouse.Clicks(editor, new Point(x, y), clicks, Pace);
    }

    private static string Lines(int count) {
      return string.Join("\r\n", Enumerable.Range(1, count).Select(i => "Line number " + i));
    }

    private static T Descendant<T>(DependencyObject root, Func<T, bool> match) where T : DependencyObject {
      for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) {
        DependencyObject child = VisualTreeHelper.GetChild(root, i);
        T found = child is T t && match(t) ? t : Descendant(child, match);
        if (found != null) {
          return found;
        }
      }
      return null;
    }

    [Theory]
    [InlineData("note")]
    [InlineData("comment")]
    [InlineData("snippet")]
    public void The_editor_has_20_px_of_padding(string kind) {
      InEditor(kind, Text, editor => {
        Assert.Equal(new Thickness(20), editor.Padding);
        Assert.InRange(At(editor, 0).X, 19.5, 24);
        Assert.InRange(At(editor, 0).Y, 19.5, 24);
      });
    }

    [Theory]
    [InlineData("note")]
    [InlineData("comment")]
    [InlineData("snippet")]
    public void A_click_in_the_left_padding_goes_to_the_start_of_the_line(string kind) {
      InEditor(kind, Text, editor => {
        Click(editor, 6, MidY(editor, Line2 + 5));
        Assert.Equal(Line2, editor.CaretIndex);
        Assert.Equal(0, editor.SelectionLength);
      });
    }

    [Theory]
    [InlineData("note")]
    [InlineData("comment")]
    [InlineData("snippet")]
    public void A_click_in_the_right_padding_goes_to_the_end_of_the_line(string kind) {
      InEditor(kind, Text, editor => {
        editor.Select(0, 0);
        Click(editor, editor.ActualWidth - 6, MidY(editor, 5));
        Assert.Equal(22, editor.CaretIndex);
      });
    }

    // The text box places the caret itself there (it ignores raw test clicks, so the nearest spot is asked directly).
    [Fact]
    public void The_nearest_spot_right_of_a_short_line_is_its_end() {
      InEditor("note", Text, editor => {
        Assert.Equal(Text.Length, MDSHO.Helpers.TextBoxPoints.CaretAt(editor, new Point(editor.ActualWidth - 40, MidY(editor, Line3 + 2))));
        Assert.Equal(22, MDSHO.Helpers.TextBoxPoints.CaretAt(editor, new Point(editor.ActualWidth - 40, MidY(editor, 2))));
        Assert.Equal(Line2 + 1, MDSHO.Helpers.TextBoxPoints.CaretAt(editor, new Point(At(editor, Line2 + 1).X + 1, MidY(editor, Line2 + 1))));
      });
    }

    [Theory]
    [InlineData("note")]
    [InlineData("comment")]
    [InlineData("snippet")]
    public void A_click_in_the_top_padding_goes_to_the_first_line_at_the_same_x(string kind) {
      InEditor(kind, Text, editor => {
        Click(editor, At(editor, 5).X + 1, 6);
        Assert.Equal(5, editor.CaretIndex);
      });
    }

    [Theory]
    [InlineData("note")]
    [InlineData("comment")]
    [InlineData("snippet")]
    public void A_click_in_the_bottom_padding_goes_to_the_last_line_at_the_same_x(string kind) {
      InEditor(kind, Text, editor => {
        editor.Select(0, 0);
        Click(editor, At(editor, Line3 + 3).X + 1, editor.ActualHeight - 6);
        Assert.Equal(Line3 + 3, editor.CaretIndex);
      });
    }

    [Fact]
    public void A_click_in_the_padding_of_scrolled_text_goes_to_a_line_on_screen() {
      InEditor("note", Lines(80), editor => {
        editor.ScrollToLine(40);
        WpfAppHost.Pump();
        int first = editor.GetFirstVisibleLineIndex();
        Assert.True(first > 20, "Scrolled: " + first);
        Click(editor, At(editor, editor.GetCharacterIndexFromLineIndex(first + 1) + 4).X + 1, 6);
        int line = editor.GetLineIndexFromCharacterIndex(editor.CaretIndex);
        Assert.InRange(line, first, first + 1);
        int fifth = editor.GetCharacterIndexFromLineIndex(first + 5);
        Click(editor, 6, MidY(editor, fifth + 3));
        Assert.Equal(fifth, editor.CaretIndex);
      });
    }

    [Fact]
    public void A_click_in_the_left_padding_of_text_scrolled_sideways_goes_to_the_first_letter_on_screen() {
      string text = string.Join(" ", Enumerable.Range(1, 200).Select(i => "word" + i)) + "\r\nshort";
      InEditor("note", text, editor => {
        editor.ScrollToHorizontalOffset(400);
        WpfAppHost.Pump();
        Assert.True(editor.HorizontalOffset > 100, "Scrolled: " + editor.HorizontalOffset);
        Click(editor, 6, MidY(editor, 0));
        Assert.True(editor.CaretIndex > 10, "Caret: " + editor.CaretIndex);
        Assert.InRange(At(editor, editor.CaretIndex).X, 10, 40);
      });
    }

    [Fact]
    public void A_click_in_the_padding_of_wrapped_text_goes_to_that_row() {
      string text = string.Concat(Enumerable.Repeat("plenty of words ", 60)).TrimEnd();
      InEditor("comment", text, editor => {
        editor.TextWrapping = TextWrapping.Wrap;
        WpfAppHost.Pump();
        Assert.True(editor.LineCount > 3, "Wrapped: " + editor.LineCount);
        int row2 = editor.GetCharacterIndexFromLineIndex(2);
        Click(editor, 6, MidY(editor, row2 + 2));
        Assert.Equal(row2, editor.CaretIndex);
        int row1 = editor.GetCharacterIndexFromLineIndex(1);
        Click(editor, editor.ActualWidth - 6, MidY(editor, row1 + 2));
        Assert.InRange(editor.CaretIndex, row2 - 1, row2);
      });
    }

    [Fact]
    public void A_drag_from_the_padding_selects() {
      InEditor("note", Text, editor => {
        Rect to = At(editor, Line2 + 4);
        RawMouse.Drag(editor, new Point(6, MidY(editor, 3)), new Point(to.X + 1, to.Y + to.Height / 2));
        Assert.Equal(0, editor.SelectionStart);
        Assert.Equal(Line2 + 4, editor.SelectionLength);
      });
    }

    [Fact]
    public void Shift_click_in_the_padding_extends_the_selection() {
      InEditor("note", Text, editor => {
        editor.Focus();
        editor.Select(3, 0);
        WpfAppHost.Pump();
        RawMouse.WithShift(() => Click(editor, 6, MidY(editor, Line2 + 5)));
        Assert.Equal(3, editor.SelectionStart);
        Assert.Equal(Line2 - 3, editor.SelectionLength);
      });
    }

    [Fact]
    public void Shift_click_in_the_padding_keeps_the_start_of_a_selection() {
      InEditor("note", Text, editor => {
        editor.Focus();
        editor.Select(3, 5);
        WpfAppHost.Pump();
        RawMouse.WithShift(() => Click(editor, editor.ActualWidth - 6, MidY(editor, Line2 + 5)));
        Assert.Equal(3, editor.SelectionStart);
        Assert.Equal(Line3 - 2 - 3, editor.SelectionLength);
      });
    }

    [Fact]
    public void Double_triple_and_four_clicks_in_the_padding_act_on_the_nearest_spot() {
      InEditor("note", Text, editor => {
        Click(editor, 6, MidY(editor, 3), 2);
        Assert.Equal("Restart", editor.SelectedText);
        Click(editor, editor.ActualWidth - 6, MidY(editor, 3), 2);
        Assert.Equal("now", editor.SelectedText);
        Click(editor, 6, MidY(editor, Line2 + 3), 3);
        Assert.Equal("Then check the dashboard", editor.SelectedText);
        Click(editor, 6, MidY(editor, Line2 + 3), 4);
        Assert.Equal(Text, editor.SelectedText);
      });
    }

    [Fact]
    public void A_click_on_the_scroll_bar_scrolls_and_leaves_the_caret() {
      InEditor("note", Lines(80), editor => {
        editor.Select(0, 0);
        editor.ScrollToHome();
        WpfAppHost.Pump();
        ScrollBar bar = Descendant<ScrollBar>(editor, b => b.Orientation == Orientation.Vertical && b.IsVisible);
        Assert.NotNull(bar);
        RawMouse.Clicks(bar, new Point(bar.ActualWidth / 2, bar.ActualHeight - 30), 1, Pace);
        Assert.True(editor.VerticalOffset > 0, "Scrolled: " + editor.VerticalOffset);
        Assert.Equal(0, editor.CaretIndex);
      });
    }

    [Fact]
    public void Highlights_line_up_with_the_text() {
      LiveHighlightTests.WithGroup(f => {
        TextEditWindow window = LiveHighlightTests.Open(f, "note");
        LiveHighlightTests.Shown(window, () => {
          TextBox editor = LiveHighlightTests.Named<TextBox>(window, "TextComment");
          Canvas layer = LiveHighlightTests.Named<Canvas>(window, "HighlightLayer");
          editor.Text = LiveHighlightTests.ArtText;
          editor.Select(10, 3);
          WpfAppHost.Pump();
          Rectangle first = LiveHighlightTests.LiveShapes(window).OrderBy(Canvas.GetTop).First();
          Point letter = editor.TranslatePoint(At(editor, 2).TopLeft, layer);
          Assert.Equal(letter.X, Canvas.GetLeft(first), 1);
          Assert.Equal(letter.Y, Canvas.GetTop(first), 1);
          Assert.True(Canvas.GetLeft(first) >= 20, "Inside the padding: " + Canvas.GetLeft(first));
        });
      });
    }
  }
}
