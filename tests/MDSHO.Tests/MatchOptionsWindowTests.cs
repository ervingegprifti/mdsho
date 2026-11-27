using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;
using MDSHO.Common;
using MDSHO.Core;
using MDSHO.Data;
using MDSHO.ViewModels;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// "Highlight whole words only" and "Highlight matching case only" in the More options menu of notes, comments and
  /// snippets (1.27.0). They change the live highlights, the match count and a note's saved colours at once, and each
  /// item keeps its own choice, like Highlight current line and (comments and snippets) Wrap text.
  /// </summary>
  public class MatchOptionsWindowTests {

    private static MenuItem Item(ContextMenu menu, string tag) => menu.Items.OfType<MenuItem>().Single(i => i.Tag as string == tag);

    private static void Click(MenuItem item) {
      item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent, item));
      WpfAppHost.Pump();
    }

    private static ShortcutViewModel Item(LiveHighlightTests.Fixture f, string kind) {
      return kind == "comment" ? f.Group : f.Group.Children.Last();
    }

    private static TextViewOptions Saved(LiveHighlightTests.Fixture f, string kind) {
      ShortcutViewModel item = Item(f, kind);
      return kind == "comment" ? item.CommentViewOptions : item.TextViewOptions;
    }

    [Theory]
    [InlineData("note")]
    [InlineData("comment")]
    [InlineData("snippet")]
    public void The_menu_has_both_items_unticked_with_tooltips_in_their_place(string kind) {
      LiveHighlightTests.WithGroup(f => {
        TextEditWindow window = LiveHighlightTests.Open(f, kind);
        LiveHighlightTests.Shown(window, () => {
          ContextMenu menu = window.BuildTitleMenu();
          MenuItem whole = Item(menu, "WholeWords");
          MenuItem match = Item(menu, "MatchCase");
          Assert.Equal("Highlight whole words only", whole.Header);
          Assert.Equal("Highlight matching case only", match.Header);
          Assert.Equal("When on, selecting “art” won't light up “start”.", whole.ToolTip);
          Assert.Equal("When on, selecting “art” won't light up “Art”.", match.ToolTip);
          Assert.False(whole.IsChecked);
          Assert.False(match.IsChecked);
          object[] items = menu.Items.Cast<object>().ToArray();
          int at = System.Array.IndexOf(items, whole);
          Assert.IsType<Separator>(items[at - 1]);
          Assert.Same(match, items[at + 1]);
          if (kind == "note") {
            Assert.Same(Item(menu, "Colours"), items[at - 2]);
            Assert.Same(Item(menu, "ClearHighlights"), items[at + 2]);
          } else {
            Assert.Same(Item(menu, "Font"), items[at - 2]);
            Assert.IsType<Separator>(items[at + 2]);
            Assert.Same(Item(menu, "Close"), items[at + 3]);
          }
        });
      });
    }

    [Theory]
    [InlineData("note")]
    [InlineData("comment")]
    [InlineData("snippet")]
    public void Toggling_redraws_the_live_highlights_and_the_count_at_once_and_saves_for_the_item(string kind) {
      LiveHighlightTests.WithGroup(f => {
        TextEditWindow window = LiveHighlightTests.Open(f, kind);
        LiveHighlightTests.Shown(window, () => {
          TextBox editor = LiveHighlightTests.Named<TextBox>(window, "TextComment");
          TextBlock count = LiveHighlightTests.Named<TextBlock>(window, "TextMatchCount");
          editor.Text = LiveHighlightTests.ArtText;
          editor.Select(10, 3);
          WpfAppHost.Pump();
          Assert.Equal(new[] { (2, 3), (30, 3) }, window.LiveMatches.ToArray());
          Click(Item(window.BuildTitleMenu(), "WholeWords"));
          // "art" in "Start" is not a whole word any more.
          Assert.Equal(new[] { (30, 3) }, window.LiveMatches.ToArray());
          Assert.Equal("2 matches", count.Text);
          Assert.Single(LiveHighlightTests.LiveShapes(window));
          Assert.True(Item(window.BuildTitleMenu(), "WholeWords").IsChecked);
          Assert.Equal(new TextViewOptions(true, true, false, false), Saved(f, kind));
          Click(Item(window.BuildTitleMenu(), "MatchCase"));
          // "ART" has another case.
          Assert.Empty(window.LiveMatches);
          Assert.Equal(Visibility.Collapsed, count.Visibility);
          Assert.Empty(LiveHighlightTests.LiveShapes(window));
          Assert.True(Item(window.BuildTitleMenu(), "MatchCase").IsChecked);
          Assert.Equal(new TextViewOptions(true, true, true, false), Saved(f, kind));
          Click(Item(window.BuildTitleMenu(), "WholeWords"));
          Click(Item(window.BuildTitleMenu(), "MatchCase"));
          Assert.Equal(new[] { (2, 3), (30, 3) }, window.LiveMatches.ToArray());
          Assert.Equal(TextViewOptions.Default, Saved(f, kind));
        });
      });
    }

    [Fact]
    public void A_note_s_saved_colours_follow_the_options() {
      LiveHighlightTests.WithGroup(f => {
        TextEditWindow window = LiveHighlightTests.Open(f, "note");
        LiveHighlightTests.Shown(window, () => {
          LiveHighlightTests.Named<TextBox>(window, "TextComment").Text = LiveHighlightTests.ArtText;
          f.Group.Children.Last().SaveNoteHighlights(NoteHighlights.With("", "art", "Yellow"));
          WpfAppHost.Pump();
          Assert.Equal(new[] { 2, 10, 30 }, window.SavedMatches.Select(m => m.Start).ToArray());
          Click(Item(window.BuildTitleMenu(), "WholeWords"));
          Assert.Equal(new[] { 10, 30 }, window.SavedMatches.Select(m => m.Start).ToArray());
          Click(Item(window.BuildTitleMenu(), "MatchCase"));
          Assert.Equal(new[] { 10 }, window.SavedMatches.Select(m => m.Start).ToArray());
          Assert.Single(LiveHighlightTests.Named<Canvas>(window, "HighlightLayer").Children.OfType<Rectangle>(), r => (string)r.Tag == "Saved");
        });
      });
    }

    [Theory]
    [InlineData("note")]
    [InlineData("comment")]
    [InlineData("snippet")]
    public void A_window_opens_with_the_item_s_saved_choices(string kind) {
      LiveHighlightTests.WithGroup(f => {
        if (kind != "comment") {
          // Creates the item first, so its choices can be saved before the window opens.
          LiveHighlightTests.Open(f, kind).Close();
        }
        ShortcutViewModel item = Item(f, kind);
        TextViewOptions choice = new TextViewOptions(CurrentLine: false, WholeWords: true, MatchCase: false, WrapText: false);
        item.SaveViewOptions(kind == "comment", choice);
        TextEditWindow window = kind == "note" ? TextEditWindow.ForNote(item) : kind == "snippet" ? TextEditWindow.ForSnippet(item) : TextEditWindow.ForComment(item);
        LiveHighlightTests.Shown(window, () => {
          TextBox editor = LiveHighlightTests.Named<TextBox>(window, "TextComment");
          editor.Text = LiveHighlightTests.ArtText;
          editor.Select(10, 3);
          WpfAppHost.Pump();
          Assert.Equal(new[] { (30, 3) }, window.LiveMatches.ToArray());
          ContextMenu menu = window.BuildTitleMenu();
          Assert.Equal((false, true, false), (Item(menu, "CurrentLine").IsChecked, Item(menu, "WholeWords").IsChecked, Item(menu, "MatchCase").IsChecked));
          Assert.Empty(LiveHighlightTests.Named<Canvas>(window, "LineLayer").Children);
        });
      });
    }

    [Fact]
    public void A_note_and_its_comment_keep_their_own_choices() {
      LiveHighlightTests.WithGroup(f => {
        TextEditWindow note = LiveHighlightTests.Open(f, "note");
        ShortcutViewModel item = f.Group.Children.Last();
        LiveHighlightTests.Shown(note, () => Click(Item(note.BuildTitleMenu(), "MatchCase")));
        Assert.True(item.TextViewOptions.MatchCase);
        Assert.False(item.CommentViewOptions.MatchCase);
        TextEditWindow comment = TextEditWindow.ForComment(item);
        LiveHighlightTests.Shown(comment, () => {
          Assert.False(Item(comment.BuildTitleMenu(), "MatchCase").IsChecked);
          Click(Item(comment.BuildTitleMenu(), "WholeWords"));
        });
        Assert.Equal((false, true), (item.TextViewOptions.WholeWords, item.CommentViewOptions.WholeWords));
        Shortcut row = DataStore.Shortcuts.Get(item.ID);
        Assert.Equal((1L, 0L, 0L, 1L), (row.TextMatchCase, row.TextWholeWords, row.CommentMatchCase, row.CommentWholeWords));
      });
    }
  }
}
