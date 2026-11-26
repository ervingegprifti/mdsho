using System.Linq;
using MDSHO.Common;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// Finding every place a text appears in a note, for the live highlights of the selected text and the saved colour
  /// highlights: case-insensitive, anywhere (also inside other words), and only for at least 2 characters that are not spaces.
  /// </summary>
  public class TextMatchesTests {

    private static string[] Found(string text, string term) {
      return TextMatches.Find(text, term).Select(m => text.Substring(m.Start, m.Length)).ToArray();
    }

    [Fact]
    public void Every_match_is_found_case_insensitive_and_inside_other_words() {
      string text = "Start the art class plan\r\nBuy ART paper and glue\r\nCall Sam about the trip";
      Assert.Equal(new[] { "art", "art", "ART" }, Found(text, "art"));
      Assert.Equal(new[] { 2, 10, 30 }, TextMatches.Find(text, "art").Select(m => m.Start).ToArray());
      Assert.All(TextMatches.Find(text, "art"), m => Assert.Equal(3, m.Length));
    }

    [Fact]
    public void Matches_do_not_overlap() {
      Assert.Equal(new[] { 0, 2 }, TextMatches.Find("aaaaa", "aa").Select(m => m.Start).ToArray());
    }

    [Theory]
    [InlineData("a")]
    [InlineData(" a ")]
    [InlineData("   ")]
    [InlineData("\r\n")]
    [InlineData("\t\t")]
    [InlineData("")]
    [InlineData(null)]
    public void Less_than_two_characters_that_are_not_spaces_find_nothing(string term) {
      Assert.False(TextMatches.IsSearchable(term));
      Assert.Empty(TextMatches.Find("a a a   \r\n\t\t aa", term));
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("a b")]
    [InlineData(" ab")]
    [InlineData("the end\r\nnext")]
    public void Two_characters_that_are_not_spaces_are_enough(string term) {
      Assert.True(TextMatches.IsSearchable(term));
    }

    [Fact]
    public void A_term_with_spaces_and_line_breaks_matches_exactly_those() {
      string text = "the end\r\nnext\r\nThe End\r\nNext";
      Assert.Equal(new[] { "the end\r\nnext", "The End\r\nNext" }, Found(text, "the end\r\nnext"));
    }

    [Fact]
    public void Empty_text_finds_nothing() {
      Assert.Empty(TextMatches.Find("", "art"));
      Assert.Empty(TextMatches.Find(null, "art"));
    }

    [Fact]
    public void The_search_stops_at_the_limit_so_long_notes_stay_fast() {
      string text = string.Concat(Enumerable.Repeat("ab ", 50000));
      Assert.Equal(TextMatches.MaxMatches, TextMatches.Find(text, "ab").Count);
      Assert.Equal(5, TextMatches.Find(text, "ab", 5).Count);
    }

    [Theory]
    [InlineData(0, "")]
    [InlineData(1, "")]
    [InlineData(2, "2 matches")]
    [InlineData(3, "3 matches")]
    [InlineData(TextMatches.MaxMatches, "10000+ matches")]
    public void The_title_shows_the_count_only_when_the_text_appears_more_than_once(int count, string expected) {
      Assert.Equal(expected, TextMatches.CountLabel(count));
    }
  }
}
