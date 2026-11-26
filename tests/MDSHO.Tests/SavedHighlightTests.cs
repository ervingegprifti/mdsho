using System.Linq;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using MDSHO.Common;
using MDSHO.Data;
using MDSHO.Helpers;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// Saved colour highlights in the Note window: the text's right-click menu has Highlight (five colours) and Remove
  /// highlight. A saved term colours every match, also after the text changes. Comments and snippets keep the plain menu.
  /// </summary>
  public class SavedHighlightTests {

    private const string Text = "Pay the gas bill\r\nBook the dentist\r\nPay rent on Friday";

    private static Rectangle[] SavedShapes(TextEditWindow window) {
      return LiveHighlightTests.Named<Canvas>(window, "HighlightLayer").Children.OfType<Rectangle>().Where(r => (string)r.Tag == TextHighlighter.SavedTag).ToArray();
    }

    private static MenuItem Item(ContextMenu menu, string tag) {
      return menu.Items.OfType<MenuItem>().Single(i => (string)i.Tag == tag);
    }

    [Fact]
    public void The_note_text_menu_has_cut_copy_paste_highlight_and_remove_highlight() {
      LiveHighlightTests.WithGroup(f => {
        TextEditWindow window = LiveHighlightTests.Open(f, "note");
        LiveHighlightTests.Shown(window, () => {
          TextBox editor = LiveHighlightTests.Named<TextBox>(window, "TextComment");
          editor.Text = Text;
          editor.Select(48, 6);
          ContextMenu menu = window.BuildTextMenu();
          Assert.Equal(new[] { "Cut", "Copy", "Paste", "", "Highlight", "Remove highlight" },
            menu.Items.Cast<object>().Select(i => i is MenuItem m ? (string)m.Header : "").ToArray());
          MenuItem highlight = Item(menu, "Highlight");
          Assert.True(highlight.IsEnabled);
          Assert.Equal(HighlightColors.All, highlight.Items.OfType<MenuItem>().Select(i => (string)i.Header).ToArray());
          Assert.All(highlight.Items.OfType<MenuItem>(), i => Assert.False(i.IsChecked));
          // "Friday" has no saved colour.
          Assert.False(Item(menu, "RemoveHighlight").IsEnabled);
          Assert.Equal(ApplicationCommands.Cut, Item(menu, "Cut").Command);
          Assert.Same(editor, Item(menu, "Cut").CommandTarget);
        });
      });
    }

    [Fact]
    public void Highlight_needs_a_selection_of_at_least_two_letters() {
      LiveHighlightTests.WithGroup(f => {
        TextEditWindow window = LiveHighlightTests.Open(f, "note");
        LiveHighlightTests.Shown(window, () => {
          TextBox editor = LiveHighlightTests.Named<TextBox>(window, "TextComment");
          editor.Text = Text;
          editor.Select(0, 1);
          Assert.False(Item(window.BuildTextMenu(), "Highlight").IsEnabled);
          editor.Select(3, 1);
          Assert.False(Item(window.BuildTextMenu(), "Highlight").IsEnabled);
        });
      });
    }

    [Fact]
    public void Picking_a_colour_saves_the_term_apart_from_the_text_and_colours_every_match() {
      LiveHighlightTests.WithGroup(f => {
        TextEditWindow window = LiveHighlightTests.Open(f, "note");
        LiveHighlightTests.Shown(window, () => {
          TextBox editor = LiveHighlightTests.Named<TextBox>(window, "TextComment");
          editor.Text = Text;
          window.SaveNoteNow();
          editor.Select(0, 3);
          window.PickHighlight("Yellow");
          editor.Select(27, 7);
          window.PickHighlight("Green");
          WpfAppHost.Pump();
          string stored = DataStore.Shortcuts.Get(window.Shortcut.ID).NoteHighlights;
          Assert.Equal(new[] { ("Pay", "Yellow"), ("dentist", "Green") }, NoteHighlights.Parse(stored).Select(h => (h.Term, h.Color)).ToArray());
          Assert.Equal(Text, DataStore.Shortcuts.Get(window.Shortcut.ID).NoteText);
          Assert.Equal(new[] { (0, 3, "Yellow"), (36, 3, "Yellow"), (27, 7, "Green") }, window.SavedMatches.ToArray());
          Rectangle[] shapes = SavedShapes(window);
          Assert.Equal(3, shapes.Length);
          Color yellow = TextHighlighter.SavedColor("Yellow");
          Assert.Contains(shapes, s => ((SolidColorBrush)s.Fill).Color == yellow);
          // The current colour has a tick.
          editor.Select(0, 3);
          MenuItem highlight = Item(window.BuildTextMenu(), "Highlight");
          Assert.True(highlight.Items.OfType<MenuItem>().Single(i => (string)i.Header == "Yellow").IsChecked);
          Assert.True(Item(window.BuildTextMenu(), "RemoveHighlight").IsEnabled);
        });
      });
    }

    [Fact]
    public void Saved_highlights_follow_the_text_as_it_changes() {
      LiveHighlightTests.WithGroup(f => {
        TextEditWindow window = LiveHighlightTests.Open(f, "note");
        LiveHighlightTests.Shown(window, () => {
          TextBox editor = LiveHighlightTests.Named<TextBox>(window, "TextComment");
          editor.Text = Text;
          editor.Select(0, 3);
          window.PickHighlight("Blue");
          editor.Text = "Today: " + Text + "\r\nprepay";
          WpfAppHost.Pump();
          Assert.Equal(new[] { (7, 3, "Blue"), (43, 3, "Blue"), (66, 3, "Blue") }, window.SavedMatches.ToArray());
          Assert.Equal(3, SavedShapes(window).Length);
        });
      });
    }

    [Fact]
    public void Remove_highlight_removes_the_term_under_the_caret() {
      LiveHighlightTests.WithGroup(f => {
        TextEditWindow window = LiveHighlightTests.Open(f, "note");
        LiveHighlightTests.Shown(window, () => {
          TextBox editor = LiveHighlightTests.Named<TextBox>(window, "TextComment");
          editor.Text = Text;
          editor.Select(0, 3);
          window.PickHighlight("Yellow");
          editor.Select(27, 7);
          window.PickHighlight("Green");
          // The caret inside "Pay" on the last line.
          editor.Select(37, 0);
          Assert.True(Item(window.BuildTextMenu(), "RemoveHighlight").IsEnabled);
          window.RemoveHighlight();
          WpfAppHost.Pump();
          Assert.Equal(new[] { "dentist" }, NoteHighlights.Parse(window.Shortcut.NoteHighlights).Select(h => h.Term).ToArray());
          Assert.Equal(new[] { (27, 7, "Green") }, window.SavedMatches.ToArray());
          Assert.Single(SavedShapes(window));
        });
      });
    }

    [Fact]
    public void A_reopened_note_shows_its_saved_highlights() {
      LiveHighlightTests.WithGroup(f => {
        TextEditWindow window = LiveHighlightTests.Open(f, "note");
        window.Shortcut.SaveNoteText(Text);
        window.Shortcut.SaveNoteHighlights(NoteHighlights.With("", "dentist", "Orange"));
        TextEditWindow again = TextEditWindow.ForNote(window.Shortcut);
        LiveHighlightTests.Shown(again, () => {
          Assert.Equal(new[] { (27, 7, "Orange") }, again.SavedMatches.ToArray());
          Assert.Single(SavedShapes(again));
        });
      });
    }

    [Theory]
    [InlineData("comment")]
    [InlineData("snippet")]
    public void Comments_and_snippets_keep_the_plain_text_menu(string kind) {
      LiveHighlightTests.WithGroup(f => {
        TextEditWindow window = LiveHighlightTests.Open(f, kind);
        LiveHighlightTests.Shown(window, () => {
          Assert.Null(LiveHighlightTests.Named<TextBox>(window, "TextComment").ContextMenu);
          Assert.Null(window.BuildTextMenu());
          Assert.Empty(window.SavedMatches);
        });
      });
    }
  }
}
