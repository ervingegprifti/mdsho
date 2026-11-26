using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using MDSHO.Common;

namespace MDSHO.Helpers {

  /// <summary>
  /// Draws highlights over a text box, which can't colour parts of its own text. While text is selected, every other
  /// place with the same text gets a soft grey highlight (see <see cref="TextMatches"/>). Only the lines on screen are
  /// drawn, so long notes stay fast; the shapes are redrawn after each change, scroll or resize.
  /// </summary>
  public sealed class TextHighlighter {
    /// <summary>The Tag of a live highlight shape.</summary>
    public const string LiveTag = "Live";
    /// <summary>The most shapes drawn at once.</summary>
    public const int MaxShapes = 500;

    private static readonly IReadOnlyList<(int Start, int Length)> None = Array.Empty<(int Start, int Length)>();

    private readonly TextBox box;
    private readonly Canvas layer;
    private bool drawQueued;

    /// <summary>The other places with the selected text (not the selection itself), in order.</summary>
    public IReadOnlyList<(int Start, int Length)> LiveMatches { get; private set; } = None;

    /// <summary>How many times the selected text appears, the selection included (0 when nothing is searched).</summary>
    public int MatchCount { get; private set; }

    /// <summary>Raised after <see cref="LiveMatches"/> and <see cref="MatchCount"/> change.</summary>
    public event Action Changed;

    public TextHighlighter(TextBox box, Canvas layer) {
      this.box = box;
      this.layer = layer;
      box.SelectionChanged += (s, e) => Update();
      box.TextChanged += (s, e) => Update();
      box.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler((s, e) => QueueDraw()));
      box.SizeChanged += (s, e) => QueueDraw();
      // The highlight is made from the text colour, so it follows the note's colours and the window theme.
      // Removed when the window closes, since the descriptor would keep the text box alive.
      DependencyPropertyDescriptor foreground = DependencyPropertyDescriptor.FromProperty(Control.ForegroundProperty, typeof(TextBox));
      EventHandler colourChanged = (s, e) => QueueDraw();
      foreground.AddValueChanged(box, colourChanged);
      box.Unloaded += (s, e) => foreground.RemoveValueChanged(box, colourChanged);
    }

    // Finds the matches of the selected text again.
    private void Update() {
      string selected = this.box.SelectedText;
      if (!TextMatches.IsSearchable(selected)) {
        this.LiveMatches = None;
        this.MatchCount = 0;
      } else {
        List<(int Start, int Length)> all = TextMatches.Find(this.box.Text, selected);
        int start = this.box.SelectionStart;
        this.MatchCount = all.Count;
        this.LiveMatches = all.Where(m => m.Start != start).ToList();
      }
      this.Changed?.Invoke();
      QueueDraw();
    }

    /// <summary>Redraws the shapes once layout has run (after a change of text, size, font or wrapping).</summary>
    public void QueueDraw() {
      if (this.drawQueued) {
        return;
      }
      this.drawQueued = true;
      // Loaded runs after layout, so the character places are known.
      this.box.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(Draw));
    }

    private void Draw() {
      this.drawQueued = false;
      this.layer.Children.Clear();
      if (this.LiveMatches.Count == 0 || !this.box.IsLoaded) {
        return;
      }
      (int first, int end) = VisibleRange();
      if (end <= first) {
        return;
      }
      this.layer.Clip = new RectangleGeometry(Viewport());
      Point offset = this.box.TranslatePoint(new Point(0, 0), this.layer);
      Color text = (this.box.Foreground as SolidColorBrush)?.Color ?? Colors.Black;
      // The text colour, faint: a grey wash on light and dark notes alike, which no saved colour looks like.
      Brush fill = Frozen(Color.FromArgb(0x30, text.R, text.G, text.B));
      Brush stroke = Frozen(Color.FromArgb(0x80, text.R, text.G, text.B));
      foreach ((int start, int length) in this.LiveMatches) {
        if (start + length <= first) {
          continue;
        }
        if (start >= end || this.layer.Children.Count >= MaxShapes) {
          break;
        }
        foreach (Rect rect in RectsOf(start, length)) {
          Rectangle shape = new Rectangle {
            Width = rect.Width, Height = rect.Height, Fill = fill, Stroke = stroke, StrokeThickness = 1,
            RadiusX = 2, RadiusY = 2, Tag = LiveTag, SnapsToDevicePixels = true
          };
          Canvas.SetLeft(shape, rect.X + offset.X);
          Canvas.SetTop(shape, rect.Y + offset.Y);
          this.layer.Children.Add(shape);
        }
      }
    }

    private static Brush Frozen(Color color) {
      SolidColorBrush brush = new SolidColorBrush(color);
      brush.Freeze();
      return brush;
    }

    // The characters of the lines on screen: [first, end).
    private (int First, int End) VisibleRange() {
      int firstLine = this.box.GetFirstVisibleLineIndex();
      int lastLine = this.box.GetLastVisibleLineIndex();
      if (firstLine < 0) {
        return (0, 0);
      }
      int first = this.box.GetCharacterIndexFromLineIndex(firstLine);
      int end = lastLine >= firstLine ? this.box.GetCharacterIndexFromLineIndex(lastLine) + this.box.GetLineLength(lastLine) : this.box.Text.Length;
      return (Math.Max(0, first), Math.Min(this.box.Text.Length, end));
    }

    // One rectangle per line the match covers (a match can go over line breaks, or wrap), in text box coordinates.
    private IEnumerable<Rect> RectsOf(int start, int length) {
      string text = this.box.Text;
      Rect current = Rect.Empty;
      for (int i = start; i < start + length && i < text.Length; i++) {
        if (text[i] == '\r' || text[i] == '\n') {
          if (!current.IsEmpty) {
            yield return current;
            current = Rect.Empty;
          }
          continue;
        }
        Rect leading = this.box.GetRectFromCharacterIndex(i);
        Rect trailing = this.box.GetRectFromCharacterIndex(i, true);
        if (leading.IsEmpty || trailing.IsEmpty) {
          continue;
        }
        Rect character = new Rect(new Point(Math.Min(leading.Left, trailing.Left), leading.Top), new Point(Math.Max(leading.Left, trailing.Left), leading.Bottom));
        if (!current.IsEmpty && Math.Abs(current.Top - character.Top) < 0.5) {
          current.Union(character);
        } else {
          if (!current.IsEmpty) {
            yield return current;
          }
          current = character;
        }
      }
      if (!current.IsEmpty) {
        yield return current;
      }
    }

    // The part of the text box that shows text (inside the padding, not over the scroll bars), in layer coordinates.
    private Rect Viewport() {
      ScrollContentPresenter presenter = Descendant<ScrollContentPresenter>(this.box);
      if (presenter == null || presenter.ActualWidth <= 0) {
        return new Rect(this.layer.RenderSize);
      }
      return presenter.TransformToVisual(this.layer).TransformBounds(new Rect(presenter.RenderSize));
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
