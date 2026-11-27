using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace MDSHO.Helpers {

  /// <summary>
  /// Where a point of a text box falls: on the text itself (the text box handles the mouse there), in the padding or on a
  /// scroll bar, and the caret position nearest to it.
  /// </summary>
  public static class TextBoxPoints {

    /// <summary>True when <paramref name="source"/> (what the mouse is on) is part of a scroll bar of <paramref name="box"/>.</summary>
    public static bool OnScrollBar(TextBox box, DependencyObject source) {
      for (DependencyObject d = source; d != null && d != box; d = Parent(d)) {
        if (d is ScrollBar) {
          return true;
        }
      }
      return false;
    }

    /// <summary>
    /// True when <paramref name="at"/> (in the text box's coordinates) is on the text view, where the text box places the
    /// caret itself. Elsewhere (the padding) it ignores the click.
    /// </summary>
    public static bool OnTextView(TextBox box, Point at) {
      FrameworkElement view = TextView(box);
      if (view == null) {
        return true;
      }
      Point p = box.TranslatePoint(at, view);
      return new Rect(view.RenderSize).Contains(p);
    }

    /// <summary>True when <paramref name="at"/> (in the text box's coordinates) is inside the text box but not on the text view.</summary>
    public static bool InPadding(TextBox box, Point at) {
      return new Rect(box.RenderSize).Contains(at) && !OnTextView(box, at);
    }

    /// <summary>The part of the text box that shows text, inside the padding and the scroll bars, in its coordinates.</summary>
    public static Rect TextArea(TextBox box) {
      ScrollContentPresenter presenter = Descendant<ScrollContentPresenter>(box);
      if (presenter == null || presenter.ActualWidth <= 0) {
        return new Rect(box.RenderSize);
      }
      return presenter.TransformToVisual(box).TransformBounds(new Rect(presenter.RenderSize));
    }

    /// <summary>
    /// The caret position nearest to <paramref name="at"/> (in the text box's coordinates): the point is moved into the
    /// text area first, so the padding left of a line gives its start, right of it its end, above the text the first line
    /// on screen and below it the last one, at the same x.
    /// </summary>
    public static int CaretAt(TextBox box, Point at) {
      string text = box.Text ?? "";
      if (text.Length == 0) {
        return 0;
      }
      Rect area = TextArea(box);
      // Just inside the edges, so the point is on a line on screen.
      Point p = new Point(Clamp(at.X, area.Left + 1, area.Right - 1), Clamp(at.Y, area.Top + 1, area.Bottom - 1));
      int index = box.GetCharacterIndexFromPoint(p, true);
      if (index < 0) {
        return text.Length;
      }
      index = Math.Min(index, text.Length - 1);
      int line = box.GetLineIndexFromCharacterIndex(index);
      int caret = index;
      // Past the middle of the character: after it.
      Rect leading = box.GetRectFromCharacterIndex(index);
      Rect trailing = box.GetRectFromCharacterIndex(index, true);
      if (!leading.IsEmpty && !trailing.IsEmpty && p.X > (leading.Left + trailing.Left) / 2) {
        caret++;
      }
      if (line < 0) {
        return caret;
      }
      // Within the line, never inside or after its line break.
      int start = box.GetCharacterIndexFromLineIndex(line);
      int end = start + box.GetLineLength(line);
      while (end > start && (text[end - 1] == '\n' || text[end - 1] == '\r')) {
        end--;
      }
      return (int)Clamp(caret, start, end);
    }

    private static double Clamp(double value, double min, double max) {
      return max < min ? min : Math.Max(min, Math.Min(max, value));
    }

    // The element that shows the text and takes the text box's own clicks.
    private static FrameworkElement TextView(TextBox box) {
      ScrollContentPresenter presenter = Descendant<ScrollContentPresenter>(box);
      return presenter != null && VisualTreeHelper.GetChildrenCount(presenter) > 0 ? VisualTreeHelper.GetChild(presenter, 0) as FrameworkElement : null;
    }

    private static DependencyObject Parent(DependencyObject d) {
      return d is Visual || d is System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d);
    }

    private static T Descendant<T>(DependencyObject root) where T : DependencyObject {
      for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) {
        DependencyObject child = VisualTreeHelper.GetChild(root, i);
        T found = child as T ?? Descendant<T>(child);
        if (found != null) {
          return found;
        }
      }
      return null;
    }
  }
}
