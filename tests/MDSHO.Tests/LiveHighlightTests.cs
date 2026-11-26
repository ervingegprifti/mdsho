using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using MDSHO.Common;
using MDSHO.Data;
using MDSHO.ViewModels;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// Live highlights in the Note, Comment and snippet windows: while text is selected, every other place with the same
  /// text gets a soft grey highlight, and the title bar shows how many there are.
  /// </summary>
  public class LiveHighlightTests {

    internal const string ArtText = "Start the art class plan\r\nBuy ART paper and glue\r\nCall Sam about the trip";

    internal sealed class Fixture {
      public AppViewModel App;
      public DesktopWindowViewModel Win;
      public ShortcutViewModel Group;
    }

    internal static void WithGroup(Action<Fixture> body) {
      WpfAppHost.Run(() => {
        Action<ShortcutViewModel> oldNoteEditor = ShortcutViewModel.OpenNoteEditor;
        ShortcutViewModel.OpenNoteEditor = s => { };
        try {
          using (TempDb db = new TempDb()) {
            Fixture f = new Fixture();
            f.App = new AppViewModel();
            f.Win = f.App.AddWindow("TEST", 0, 0);
            f.Group = ViewModelFactory.NewGroupShortcut(f.App, null, f.Win);
            f.Win.RootShortcuts.Add(f.Group);
            body(f);
          }
        } finally {
          ShortcutViewModel.OpenNoteEditor = oldNoteEditor;
        }
      });
    }

    internal static TextEditWindow Open(Fixture f, string kind) {
      switch (kind) {
        case "note":
          f.Group.NewNoteShortcutCommand.Execute(null);
          return TextEditWindow.ForNote(f.Group.Children.Last());
        case "snippet":
          f.Group.NewSnippetShortcutCommand.Execute(null);
          return TextEditWindow.ForSnippet(f.Group.Children.Last());
        default:
          return TextEditWindow.ForComment(f.Group);
      }
    }

    internal static void Shown(Window window, Action check) {
      window.WindowStartupLocation = WindowStartupLocation.Manual;
      window.Left = -10000;
      window.Top = -10000;
      window.ShowActivated = false;
      window.Show();
      try {
        WpfAppHost.Pump();
        check();
      } finally {
        if (window.IsVisible) {
          window.Close();
        }
        WpfAppHost.Pump();
      }
    }

    internal static T Named<T>(Window window, string name) where T : class {
      return Assert.IsAssignableFrom<T>(window.FindName(name));
    }

    // The highlight shapes drawn now for the live matches.
    internal static Rectangle[] LiveShapes(TextEditWindow window) {
      return Named<Canvas>(window, "HighlightLayer").Children.OfType<Rectangle>().Where(r => (string)r.Tag == "Live").ToArray();
    }

    [Theory]
    [InlineData("note")]
    [InlineData("comment")]
    [InlineData("snippet")]
    public void Selected_text_highlights_every_other_match_and_the_title_shows_the_count(string kind) {
      WithGroup(f => {
        TextEditWindow window = Open(f, kind);
        Shown(window, () => {
          TextBox editor = Named<TextBox>(window, "TextComment");
          TextBlock count = Named<TextBlock>(window, "TextMatchCount");
          editor.Text = ArtText;
          editor.Select(10, 3);
          WpfAppHost.Pump();
          // Only the matching letters: "art" inside "Start" and "ART"; not the selected one.
          Assert.Equal(new[] { (2, 3), (30, 3) }, window.LiveMatches.ToArray());
          Assert.Equal("3 matches", count.Text);
          Assert.Equal(Visibility.Visible, count.Visibility);
          Assert.Equal(2, LiveShapes(window).Length);
          Assert.False(Named<Canvas>(window, "HighlightLayer").IsHitTestVisible);
        });
      });
    }

    [Theory]
    [InlineData(10, 1)]
    [InlineData(3, 2)]
    [InlineData(5, 1)]
    [InlineData(0, 0)]
    public void One_character_spaces_or_no_selection_highlight_nothing(int start, int length) {
      WithGroup(f => {
        TextEditWindow window = Open(f, "note");
        Shown(window, () => {
          TextBox editor = Named<TextBox>(window, "TextComment");
          editor.Text = "a b  a b  a b";
          editor.Select(start, length);
          WpfAppHost.Pump();
          Assert.Empty(window.LiveMatches);
          Assert.Empty(LiveShapes(window));
          Assert.Equal(Visibility.Collapsed, Named<TextBlock>(window, "TextMatchCount").Visibility);
        });
      });
    }

    [Fact]
    public void The_highlights_follow_the_selection_and_clear_when_it_goes() {
      WithGroup(f => {
        TextEditWindow window = Open(f, "note");
        Shown(window, () => {
          TextBox editor = Named<TextBox>(window, "TextComment");
          TextBlock count = Named<TextBlock>(window, "TextMatchCount");
          editor.Text = ArtText;
          editor.Select(10, 3);
          WpfAppHost.Pump();
          Assert.Equal(2, window.LiveMatches.Count);
          // "the" appears twice: "the art" and "the trip".
          editor.Select(6, 3);
          WpfAppHost.Pump();
          Assert.Equal(new[] { (65, 3) }, window.LiveMatches.ToArray());
          Assert.Equal("2 matches", count.Text);
          // A text that appears only once shows no count.
          editor.Select(14, 5);
          WpfAppHost.Pump();
          Assert.Empty(window.LiveMatches);
          Assert.Equal(Visibility.Collapsed, count.Visibility);
          editor.Select(10, 3);
          WpfAppHost.Pump();
          editor.Select(0, 0);
          WpfAppHost.Pump();
          Assert.Empty(window.LiveMatches);
          Assert.Empty(LiveShapes(window));
          Assert.Equal(Visibility.Collapsed, count.Visibility);
        });
      });
    }

    [Fact]
    public void Typing_updates_the_highlights() {
      WithGroup(f => {
        TextEditWindow window = Open(f, "note");
        Shown(window, () => {
          TextBox editor = Named<TextBox>(window, "TextComment");
          editor.Text = ArtText;
          editor.Select(10, 3);
          WpfAppHost.Pump();
          editor.Text = ArtText + "\r\nart again";
          editor.Select(10, 3);
          WpfAppHost.Pump();
          Assert.Equal(3, window.LiveMatches.Count);
          Assert.Equal("4 matches", Named<TextBlock>(window, "TextMatchCount").Text);
        });
      });
    }

    [Theory]
    [InlineData("note", "#FF000000")]
    [InlineData("note", "#FFDCE6F0")]
    [InlineData("comment", null)]
    public void The_highlight_is_a_soft_grey_made_from_the_text_colour(string kind, string textColor) {
      WithGroup(f => {
        TextEditWindow window = Open(f, kind);
        if (textColor != null) {
          window.Shortcut.NoteColors.TextColor = textColor;
        }
        Shown(window, () => {
          TextBox editor = Named<TextBox>(window, "TextComment");
          editor.Text = ArtText;
          editor.Select(10, 3);
          WpfAppHost.Pump();
          Color text = ((SolidColorBrush)editor.Foreground).Color;
          Rectangle shape = LiveShapes(window).First();
          Color fill = ((SolidColorBrush)shape.Fill).Color;
          Color stroke = ((SolidColorBrush)shape.Stroke).Color;
          // The text colour, faint: grey on a light note and on a dark one, never a colour of its own.
          Assert.Equal((text.R, text.G, text.B), (fill.R, fill.G, fill.B));
          Assert.Equal((text.R, text.G, text.B), (stroke.R, stroke.G, stroke.B));
          Assert.InRange(fill.A, 0x20, 0x40);
          Assert.InRange(stroke.A, 0x60, 0xA0);
        });
      });
    }

    [Fact]
    public void A_long_note_draws_only_the_matches_on_screen() {
      WithGroup(f => {
        TextEditWindow window = Open(f, "note");
        Shown(window, () => {
          TextBox editor = Named<TextBox>(window, "TextComment");
          editor.Text = string.Join("\r\n", Enumerable.Repeat("art and more art", 20000));
          editor.Select(0, 3);
          WpfAppHost.Pump();
          Assert.Equal(TextMatches.MaxMatches - 1, window.LiveMatches.Count);
          Assert.Equal("10000+ matches", Named<TextBlock>(window, "TextMatchCount").Text);
          Assert.InRange(LiveShapes(window).Length, 1, 200);
        });
      });
    }
  }
}
