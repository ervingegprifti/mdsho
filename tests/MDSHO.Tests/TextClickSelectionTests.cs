using MDSHO.Common;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// What a double click (a word, without the space after it) and a triple click (the whole paragraph, without its line
  /// break) select in the Note and Comment windows.
  /// </summary>
  public class TextClickSelectionTests {

    private static string Selected(string text, (int Start, int Length) range) {
      return text.Substring(range.Start, range.Length);
    }

    [Theory]
    [InlineData("Restart the server now", 9, "the")]
    [InlineData("Restart the server now", 8, "the")]
    [InlineData("Restart the server now", 0, "Restart")]
    [InlineData("Restart the server now", 21, "now")]
    [InlineData("Restart the server now", 22, "now")]
    [InlineData("SRV_01 is down", 2, "SRV_01")]
    [InlineData("port 8080, then", 6, "8080")]
    [InlineData("Ask Ånna før", 5, "Ånna")]
    public void A_double_click_selects_the_word_without_the_space_after_it(string text, int index, string expected) {
      Assert.Equal(expected, Selected(text, TextClickSelection.WordAt(text, index)));
    }

    [Fact]
    public void A_double_click_on_spaces_selects_the_spaces() {
      string text = "one   two";
      Assert.Equal("   ", Selected(text, TextClickSelection.WordAt(text, 4)));
    }

    [Fact]
    public void A_double_click_on_punctuation_selects_only_the_punctuation() {
      string text = "stop, then...  go";
      Assert.Equal(",", Selected(text, TextClickSelection.WordAt(text, 4)));
      Assert.Equal("...", Selected(text, TextClickSelection.WordAt(text, 11)));
    }

    [Fact]
    public void A_double_click_past_the_end_of_a_line_selects_its_last_word_not_the_line_break() {
      string text = "first line\r\nsecond";
      Assert.Equal("line", Selected(text, TextClickSelection.WordAt(text, 10)));
      Assert.Equal("line", Selected(text, TextClickSelection.WordAt(text, 11)));
    }

    [Fact]
    public void A_double_click_on_an_empty_line_selects_nothing() {
      string text = "a\r\n\r\nb";
      (int start, int length) = TextClickSelection.WordAt(text, 3);
      Assert.Equal(0, length);
      Assert.Equal(3, start);
    }

    [Fact]
    public void Empty_text_selects_nothing() {
      Assert.Equal((0, 0), TextClickSelection.WordAt("", 0));
      Assert.Equal((0, 0), TextClickSelection.ParagraphAt("", 0));
      Assert.Equal((0, 0), TextClickSelection.WordAt(null, 0));
    }

    [Theory]
    [InlineData(0, "1. Tell the team.")]
    [InlineData(5, "1. Tell the team.")]
    [InlineData(17, "1. Tell the team.")]
    [InlineData(19, "2. Stop the web service, then the database.")]
    [InlineData(40, "2. Stop the web service, then the database.")]
    [InlineData(70, "3. Restart.")]
    public void A_triple_click_selects_the_whole_paragraph_without_its_line_break(int index, string expected) {
      string text = "1. Tell the team.\r\n2. Stop the web service, then the database.\r\n3. Restart.";
      Assert.Equal(expected, Selected(text, TextClickSelection.ParagraphAt(text, index)));
    }

    [Fact]
    public void A_triple_click_works_with_plain_new_lines_too() {
      string text = "one\ntwo words\nthree";
      Assert.Equal("two words", Selected(text, TextClickSelection.ParagraphAt(text, 6)));
    }

    [Fact]
    public void Out_of_range_indexes_are_clamped() {
      string text = "one two";
      Assert.Equal("two", Selected(text, TextClickSelection.WordAt(text, 99)));
      Assert.Equal("one", Selected(text, TextClickSelection.WordAt(text, -5)));
      Assert.Equal("one two", Selected(text, TextClickSelection.ParagraphAt(text, 99)));
    }
  }
}
