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
  /// place with the same text gets a soft grey highlight (see <see cref="TextMatches"/>); a note's saved terms colour
  /// every match in their colour (see <see cref="NoteHighlights"/>). Only the lines on screen are drawn, so long notes
  /// stay fast; the shapes are redrawn after each change, scroll or resize.
  /// </summary>
  public sealed class TextHighlighter {
    /// <summary>The Tag of a live highlight shape.</summary>
    public const string LiveTag = "Live";
    /// <summary>The Tag of a saved colour highlight shape.</summary>
    public const string SavedTag = "Saved";
    /// <summary>The most shapes drawn at once.</summary>
    public const int MaxShapes = 500;

    /// <summary>The fill of a saved colour: see-through, so the text stays readable on light and dark notes.</summary>
    public static Color SavedColor(string name) {
      switch (name) {
        case "Yellow": return Color.FromArgb(0x80, 0xFF, 0xD8, 0x00);
        case "Green": return Color.FromArgb(0x70, 0x4C, 0xC9, 0x4C);
        case "Blue": return Color.FromArgb(0x70, 0x4D, 0xA6, 0xFF);
        case "Pink": return Color.FromArgb(0x70, 0xFF, 0x6E, 0xB4);
        case "Orange": return Color.FromArgb(0x78, 0xFF, 0x99, 0x33);
        default: return Colors.Transparent;
      }
    }

    /// <summary>The Tag of the current line shape.</summary>
    public const string LineTag = "Line";

    /// <summary>
    /// The current line shade: 3% black on a light background, 3% white on a dark one (by its lightness; a see-through
    /// background counts by its colour), so it reads as a hint on every note colour and theme.
    /// </summary>
    public static Color CurrentLineColor(Color background) {
      double Linear(byte v) {
        double c = v / 255.0;
        return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
      }
      double lightness = 0.2126 * Linear(background.R) + 0.7152 * Linear(background.G) + 0.0722 * Linear(background.B);
      // 0.179 is where black and white text have the same contrast: above it a background counts as light.
      return lightness > 0.179 ? Color.FromArgb(8, 0, 0, 0) : Color.FromArgb(8, 255, 255, 255);
    }

    private readonly Canvas lineLayer;
    private bool currentLine;

    /// <summary>True to shade the caret's row (Highlight current line).</summary>
    public bool CurrentLine {
      get => this.currentLine;
      set {
        this.currentLine = value;
        QueueDraw();
      }
    }

    /// <summary>The menu swatch of a saved colour (solid).</summary>
    public static Color SwatchColor(string name) {
      Color c = SavedColor(name);
      return Color.FromRgb(c.R, c.G, c.B);
    }

    private static readonly IReadOnlyList<(int Start, int Length)> None = Array.Empty<(int Start, int Length)>();
    private static readonly IReadOnlyList<(int Start, int Length, string Color)> NoSaved = Array.Empty<(int Start, int Length, string Color)>();

    private readonly TextBox box;
    private readonly Canvas layer;
    private bool drawQueued;
    private string savedTerms = "";
    // The matches of the saved terms, found again when the text or the terms change (null = not found yet).
    private IReadOnlyList<(int Start, int Length, string Color)> savedMatches;

    /// <summary>The other places with the selected text (not the selection itself), in order.</summary>
    public IReadOnlyList<(int Start, int Length)> LiveMatches { get; private set; } = None;

    /// <summary>How many times the selected text appears, the selection included (0 when nothing is searched).</summary>
    public int MatchCount { get; private set; }

    /// <summary>Raised after <see cref="LiveMatches"/> and <see cref="MatchCount"/> change.</summary>
    public event Action Changed;

    /// <summary>The saved terms and colours (the stored value of <see cref="NoteHighlights"/>); "" for none.</summary>
    public string SavedTerms {
      get => this.savedTerms;
      set {
        this.savedTerms = value ?? "";
        this.savedMatches = null;
        QueueDraw();
      }
    }

    /// <summary>Every match of the saved terms with its colour, term by term.</summary>
    public IReadOnlyList<(int Start, int Length, string Color)> SavedMatches {
      get {
        if (this.savedMatches == null) {
          this.savedMatches = this.savedTerms.Length == 0 ? NoSaved : NoteHighlights.Matches(this.box.Text, this.savedTerms).ToList();
        }
        return this.savedMatches;
      }
    }

    public TextHighlighter(TextBox box, Canvas layer, Canvas lineLayer = null) {
      this.box = box;
      this.layer = layer;
      this.lineLayer = lineLayer;
      box.SelectionChanged += (s, e) => Update();
      box.TextChanged += (s, e) => {
        this.savedMatches = null;
        Update();
      };
      box.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler((s, e) => QueueDraw()));
      box.SizeChanged += (s, e) => QueueDraw();
      // The highlight is made from the text colour, so it follows the note's colours and the window theme.
      // Removed when the window closes, since the descriptor would keep the text box alive.
      DependencyPropertyDescriptor foreground = DependencyPropertyDescriptor.FromProperty(Control.ForegroundProperty, typeof(TextBox));
      // The current line is made from the background, so it follows that too.
      DependencyPropertyDescriptor background = DependencyPropertyDescriptor.FromProperty(Control.BackgroundProperty, typeof(TextBox));
      EventHandler colourChanged = (s, e) => QueueDraw();
      foreground.AddValueChanged(box, colourChanged);
      background.AddValueChanged(box, colourChanged);
      box.Unloaded += (s, e) => {
        foreground.RemoveValueChanged(box, colourChanged);
        background.RemoveValueChanged(box, colourChanged);
      };
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
      DrawCurrentLine();
      this.layer.Children.Clear();
      if ((this.LiveMatches.Count == 0 && this.SavedMatches.Count == 0) || !this.box.IsLoaded) {
        return;
      }
      (int first, int end) = VisibleRange();
      if (end <= first) {
        return;
      }
      this.layer.Clip = new RectangleGeometry(Viewport(this.layer));
      Point offset = this.box.TranslatePoint(new Point(0, 0), this.layer);
      // The saved colours first, so a live highlight shows on top of them.
      Dictionary<string, Brush> fills = new Dictionary<string, Brush>();
      foreach ((int start, int length, string color) in this.SavedMatches) {
        if (!fills.TryGetValue(color, out Brush brush)) {
          brush = fills[color] = Frozen(SavedColor(color));
        }
        AddShapes(start, length, first, end, offset, brush, null, SavedTag);
      }
      Color text = (this.box.Foreground as SolidColorBrush)?.Color ?? Colors.Black;
      // The text colour, faint: a grey wash on light and dark notes alike, which no saved colour looks like.
      Brush fill = Frozen(Color.FromArgb(0x30, text.R, text.G, text.B));
      Brush stroke = Frozen(Color.FromArgb(0x80, text.R, text.G, text.B));
      foreach ((int start, int length) in this.LiveMatches) {
        if (start >= end) {
          break;
        }
        AddShapes(start, length, first, end, offset, fill, stroke, LiveTag);
      }
    }

    // The caret's row (only that row when the text wraps), edge to edge across the text area, on its own layer under the
    // highlights. It doesn't depend on focus, so it stays while the window isn't in use.
    private void DrawCurrentLine() {
      if (this.lineLayer == null) {
        return;
      }
      this.lineLayer.Children.Clear();
      if (!this.currentLine || !this.box.IsLoaded) {
        return;
      }
      Rect caret = this.box.GetRectFromCharacterIndex(Math.Min(this.box.CaretIndex, this.box.Text.Length));
      if (caret.IsEmpty) {
        return;
      }
      Rect viewport = Viewport(this.lineLayer);
      double top = this.box.TranslatePoint(new Point(0, caret.Top), this.lineLayer).Y;
      if (top + caret.Height <= viewport.Top || top >= viewport.Bottom) {
        return;
      }
      // From the left edge to the scroll bar, only inside the rows on screen.
      this.lineLayer.Clip = new RectangleGeometry(new Rect(0, viewport.Top, viewport.Right, viewport.Height));
      Color background = (this.box.Background as SolidColorBrush)?.Color ?? Colors.White;
      Rectangle line = new Rectangle { Width = viewport.Right, Height = caret.Height, Fill = Frozen(CurrentLineColor(background)), Tag = LineTag };
      Canvas.SetLeft(line, 0);
      Canvas.SetTop(line, top);
      this.lineLayer.Children.Add(line);
    }

    // The shapes of one match, when it is on screen.
    private void AddShapes(int start, int length, int first, int end, Point offset, Brush fill, Brush stroke, string tag) {
      if (start + length <= first || start >= end) {
        return;
      }
      foreach (Rect rect in RectsOf(start, length)) {
        if (this.layer.Children.Count >= MaxShapes) {
          return;
        }
        Rectangle shape = new Rectangle {
          Width = rect.Width, Height = rect.Height, Fill = fill, Stroke = stroke, StrokeThickness = stroke == null ? 0 : 1,
          RadiusX = 2, RadiusY = 2, Tag = tag, SnapsToDevicePixels = true
        };
        Canvas.SetLeft(shape, rect.X + offset.X);
        Canvas.SetTop(shape, rect.Y + offset.Y);
        this.layer.Children.Add(shape);
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

    // The part of the text box that shows text (inside the padding, not over the scroll bars), in the target's coordinates.
    private Rect Viewport(Canvas target) {
      ScrollContentPresenter presenter = Descendant<ScrollContentPresenter>(this.box);
      if (presenter == null || presenter.ActualWidth <= 0) {
        return new Rect(target.RenderSize);
      }
      return presenter.TransformToVisual(target).TransformBounds(new Rect(presenter.RenderSize));
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
