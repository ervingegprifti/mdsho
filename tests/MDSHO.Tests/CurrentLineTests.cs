using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using MDSHO.Data;
using MDSHO.Helpers;
using MDSHO.ViewModels;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// The current line highlight of the Note, Comment and snippet windows: the caret's row gets a faint shade (3% darker on
  /// a light background, 3% lighter on a dark one), edge to edge, under the highlights, also while the window isn't
  /// focused. "Highlight current line" in the More options menu turns it off and on for that note, comment or snippet.
  /// </summary>
  public class CurrentLineTests {

    private const string Text = "Buy milk\r\nCall the dentist\r\nSend the report";

    private static Rectangle[] LineShapes(TextEditWindow window) {
      return LiveHighlightTests.Named<Canvas>(window, "LineLayer").Children.OfType<Rectangle>().ToArray();
    }

    private static MenuItem Item(ContextMenu menu, string tag) => menu.Items.OfType<MenuItem>().Single(i => i.Tag as string == tag);

    private static void Click(MenuItem item) {
      item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent, item));
      WpfAppHost.Pump();
    }

    [Theory]
    [InlineData("#FFF4F3ED", false)]
    [InlineData("#FFFFF4F7", false)]
    [InlineData("#FFFFB300", false)]
    [InlineData("#FFE9E2BA", false)]
    [InlineData("#FF26344A", true)]
    [InlineData("#FF202020", true)]
    [InlineData("#80000000", true)]
    public void The_shade_is_3_percent_darker_on_light_and_lighter_on_dark_backgrounds(string background, bool dark) {
      Color shade = TextHighlighter.CurrentLineColor((Color)ColorConverter.ConvertFromString(background));
      Assert.Equal(8, shade.A);
      Assert.Equal(dark ? Colors.White.R : Colors.Black.R, shade.R);
      Assert.Equal(shade.R, shade.G);
      Assert.Equal(shade.R, shade.B);
    }

    [Theory]
    [InlineData("note")]
    [InlineData("comment")]
    [InlineData("snippet")]
    public void The_caret_row_is_shaded_edge_to_edge_and_follows_the_caret(string kind) {
      LiveHighlightTests.WithGroup(f => {
        TextEditWindow window = LiveHighlightTests.Open(f, kind);
        LiveHighlightTests.Shown(window, () => {
          TextBox editor = LiveHighlightTests.Named<TextBox>(window, "TextComment");
          Canvas layer = LiveHighlightTests.Named<Canvas>(window, "LineLayer");
          editor.Text = Text;
          editor.CaretIndex = 14;
          WpfAppHost.Pump();
          Rectangle line = Assert.Single(LineShapes(window));
          Rect row = editor.GetRectFromCharacterIndex(14);
          double top = editor.TranslatePoint(new Point(0, row.Top), layer).Y;
          Assert.Equal(top, Canvas.GetTop(line), 1);
          Assert.Equal(row.Height, line.Height, 1);
          Assert.Equal(0, Canvas.GetLeft(line), 1);
          Assert.True(line.Width >= editor.ActualWidth - SystemParameters.VerticalScrollBarWidth - 1, "Edge to edge: " + line.Width + " of " + editor.ActualWidth);
          Assert.False(layer.IsHitTestVisible);
          // The arrows, a click or typing move the caret: the shade follows.
          editor.CaretIndex = Text.Length;
          WpfAppHost.Pump();
          Rect last = editor.GetRectFromCharacterIndex(Text.Length);
          Assert.Equal(editor.TranslatePoint(new Point(0, last.Top), layer).Y, Canvas.GetTop(Assert.Single(LineShapes(window))), 1);
        });
      });
    }

    [Fact]
    public void The_shade_is_made_from_the_note_background() {
      LiveHighlightTests.WithGroup(f => {
        TextEditWindow window = LiveHighlightTests.Open(f, "note");
        LiveHighlightTests.Shown(window, () => {
          TextBox editor = LiveHighlightTests.Named<TextBox>(window, "TextComment");
          editor.Text = Text;
          WpfAppHost.Pump();
          Assert.Equal(Color.FromArgb(8, 0, 0, 0), ((SolidColorBrush)Assert.Single(LineShapes(window)).Fill).Color);
          window.Shortcut.NoteColors.BackgroundColor = "#FF26344A";
          WpfAppHost.Pump();
          Assert.Equal(Color.FromArgb(8, 255, 255, 255), ((SolidColorBrush)Assert.Single(LineShapes(window)).Fill).Color);
        });
      });
    }

    [Fact]
    public void With_wrap_text_only_the_wrapped_row_with_the_caret_is_shaded() {
      LiveHighlightTests.WithGroup(f => {
        TextEditWindow window = LiveHighlightTests.Open(f, "note");
        LiveHighlightTests.Shown(window, () => {
          TextBox editor = LiveHighlightTests.Named<TextBox>(window, "TextComment");
          editor.Text = string.Join(" ", Enumerable.Repeat("word", 120));
          Click(Item(window.BuildTitleMenu(), "Wrap"));
          Assert.True(window.IsWrapText);
          editor.CaretIndex = 300;
          WpfAppHost.Pump();
          Rect row = editor.GetRectFromCharacterIndex(300);
          Rectangle line = Assert.Single(LineShapes(window));
          Assert.True(editor.GetLineIndexFromCharacterIndex(300) > 0, "The text should wrap");
          Assert.Equal(row.Height, line.Height, 1);
        });
      });
    }

    [Fact]
    public void The_shade_is_under_the_highlights_and_stays_when_the_window_is_not_focused() {
      LiveHighlightTests.WithGroup(f => {
        TextEditWindow window = LiveHighlightTests.Open(f, "note");
        LiveHighlightTests.Shown(window, () => {
          Grid body = LiveHighlightTests.Named<Grid>(window, "Body");
          Assert.True(body.Children.IndexOf(LiveHighlightTests.Named<Canvas>(window, "LineLayer")) < body.Children.IndexOf(LiveHighlightTests.Named<Canvas>(window, "HighlightLayer")));
          TextBox editor = LiveHighlightTests.Named<TextBox>(window, "TextComment");
          editor.Text = Text;
          WpfAppHost.Pump();
          Assert.Single(LineShapes(window));
          // The note isn't in use any more: the focus goes elsewhere. The shade stays.
          System.Windows.Input.Keyboard.ClearFocus();
          WpfAppHost.Pump();
          Assert.False(editor.IsKeyboardFocused);
          Assert.Single(LineShapes(window));
        });
      });
    }

    [Theory]
    [InlineData("note")]
    [InlineData("comment")]
    [InlineData("snippet")]
    public void Highlight_current_line_follows_wrap_text_in_the_menu_ticked_by_default(string kind) {
      LiveHighlightTests.WithGroup(f => {
        TextEditWindow window = LiveHighlightTests.Open(f, kind);
        LiveHighlightTests.Shown(window, () => {
          ContextMenu menu = window.BuildTitleMenu();
          MenuItem[] items = menu.Items.OfType<MenuItem>().ToArray();
          MenuItem line = Item(menu, "CurrentLine");
          Assert.Equal("Highlight current line", line.Header);
          Assert.Equal(System.Array.IndexOf(items, Item(menu, "Wrap")) + 1, System.Array.IndexOf(items, line));
          Assert.True(line.IsChecked);
        });
      });
    }

    [Fact]
    public void Turning_it_off_and_on_saves_it_for_that_note_only() {
      LiveHighlightTests.WithGroup(f => {
        TextEditWindow window = LiveHighlightTests.Open(f, "note");
        ShortcutViewModel note = f.Group.Children.Last();
        LiveHighlightTests.Shown(window, () => {
          LiveHighlightTests.Named<TextBox>(window, "TextComment").Text = Text;
          WpfAppHost.Pump();
          Click(Item(window.BuildTitleMenu(), "CurrentLine"));
          Assert.False(note.TextViewOptions.CurrentLine);
          Assert.Equal(0, DataStore.Shortcuts.Get(note.ID).TextCurrentLine);
          Assert.Empty(LineShapes(window));
          Assert.False(Item(window.BuildTitleMenu(), "CurrentLine").IsChecked);
          // Other windows keep their own choice: the comment of the group and another note.
          TextEditWindow comment = LiveHighlightTests.Open(f, "comment");
          LiveHighlightTests.Shown(comment, () => Assert.Single(LineShapes(comment)));
          TextEditWindow other = LiveHighlightTests.Open(f, "note");
          LiveHighlightTests.Shown(other, () => Assert.Single(LineShapes(other)));
          Click(Item(window.BuildTitleMenu(), "CurrentLine"));
          Assert.True(note.TextViewOptions.CurrentLine);
          Assert.Equal(1, DataStore.Shortcuts.Get(note.ID).TextCurrentLine);
          Assert.Single(LineShapes(window));
        });
      });
    }

    [Fact]
    public void It_is_no_longer_an_app_setting() {
      LiveHighlightTests.WithGroup(f => {
        Assert.DoesNotContain("HighlightCurrentLine", PortableSettings.Keys);
        Assert.Null(typeof(PortableSettings).Assembly.GetType("MDSHO.Data.CurrentLineSetting"));
      });
    }
  }
}
