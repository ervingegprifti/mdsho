using System.Windows;
using System.Windows.Controls;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// Clicks in the Note, Comment and snippet editors as Windows delivers them (see <see cref="RawMouse"/>). WPF measures
  /// its double-click time from the first click of a run, so a 4th click usually comes after it and WPF counts it as a
  /// new single click; the editor counts the run itself, each click within the double-click time of the one before.
  /// </summary>
  public class RealClicksTests {

    private const string Text = "Restart the server now.\r\nThen check the dashboard.";

    private static string SelectedAfter(string kind, int clicks, int gap) {
      string selected = null;
      LiveHighlightTests.WithGroup(f => {
        TextEditWindow window = LiveHighlightTests.Open(f, kind);
        LiveHighlightTests.Shown(window, () => {
          TextBox editor = LiveHighlightTests.Named<TextBox>(window, "TextComment");
          editor.Text = Text;
          editor.Select(0, 0);
          WpfAppHost.Pump();
          // On "server", in the first line.
          Rect server = editor.GetRectFromCharacterIndex(14);
          RawMouse.Clicks(editor, new Point(server.X + 2, server.Y + server.Height / 2), clicks, gap);
          selected = editor.SelectedText;
        });
      });
      return selected;
    }

    // A steady pace: each click well within the double-click time of the one before, but four of them take longer than it.
    private static int Pace => RawMouse.DoubleClickTime * 2 / 5;

    [Theory]
    [InlineData("note")]
    [InlineData("comment")]
    [InlineData("snippet")]
    public void Four_clicks_at_a_normal_pace_select_all_the_text(string kind) {
      Assert.Equal(Text, SelectedAfter(kind, 4, Pace));
    }

    [Fact]
    public void Five_clicks_still_select_all_the_text() {
      Assert.Equal(Text, SelectedAfter("note", 5, Pace));
    }

    [Fact]
    public void Two_and_three_clicks_select_the_word_and_the_line() {
      Assert.Equal("server", SelectedAfter("note", 2, Pace));
      Assert.Equal("Restart the server now.", SelectedAfter("note", 3, Pace));
    }

    [Fact]
    public void Slow_clicks_are_single_clicks() {
      Assert.Equal("", SelectedAfter("note", 4, RawMouse.DoubleClickTime + 100));
    }
  }
}
