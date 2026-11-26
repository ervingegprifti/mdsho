using System.Linq;
using MDSHO.Common;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// Match options (1.27.0): "Highlight whole words only" and "Highlight matching case only". Off, a text matches
  /// anywhere and ignores case, as before. Whole words: no letter, digit or _ right before the start or after the end.
  /// </summary>
  public class MatchOptionsTests {

    private const string Text = "Start the art class. Art_room, ART, art2, (art) and the art-show";

    private static (int, int)[] Find(string text, string term, bool wholeWords, bool matchCase) {
      return TextMatches.Find(text, term, new MatchOptions(wholeWords, matchCase)).ToArray();
    }

    [Fact]
    public void Off_matches_anywhere_and_ignores_case_as_before() {
      Assert.Equal(TextMatches.Find(Text, "art").ToArray(), Find(Text, "art", false, false));
      Assert.Equal(default, MatchOptions.Off);
      Assert.Equal(7, Find(Text, "art", false, false).Length);
    }

    [Fact]
    public void Whole_words_skips_matches_inside_other_words() {
      // "art", "ART", "(art)" and "art-show"; not "Start", "Art_room" or "art2".
      Assert.Equal(new[] { (10, 3), (31, 3), (43, 3), (56, 3) }, Find(Text, "art", true, false));
    }

    [Fact]
    public void Matching_case_skips_other_cases() {
      Assert.Equal(new[] { (2, 3), (10, 3), (36, 3), (43, 3), (56, 3) }, Find(Text, "art", false, true));
      Assert.Equal(new[] { (21, 3) }, Find(Text, "Art", false, true));
    }

    [Fact]
    public void Both_together() {
      Assert.Equal(new[] { (10, 3), (43, 3), (56, 3) }, Find(Text, "art", true, true));
      Assert.Equal(new[] { (31, 3) }, Find(Text, "ART", true, true));
    }

    [Theory]
    [InlineData("the art", true, false, new[] { 6, 52 })]
    [InlineData("the ar", true, false, new int[0])]
    [InlineData("he art", true, false, new int[0])]
    [InlineData("the art", false, false, new[] { 6, 52 })]
    public void A_selection_of_several_words_works_too(string term, bool wholeWords, bool matchCase, int[] starts) {
      Assert.Equal(starts, Find(Text, term, wholeWords, matchCase).Select(m => m.Item1).ToArray());
    }

    [Fact]
    public void Digits_and_underscores_count_as_word_characters() {
      Assert.Empty(Find("x_ab ab1 1ab", "ab", true, false));
      Assert.Equal(new[] { (0, 2), (3, 2) }, Find("ab ab", "ab", true, false));
      Assert.Equal(new[] { (2, 2) }, Find("é ab", "ab", true, false));
      Assert.Empty(Find("éab", "ab", true, false));
    }

    [Fact]
    public void A_match_that_is_not_a_whole_word_does_not_hide_a_later_overlapping_one() {
      // "aaa aa": "aa" at 0 is inside "aaa"; the whole word "aa" at 4 is still found.
      Assert.Equal(new[] { (4, 2) }, Find("aaa aa", "aa", true, false));
    }

    [Fact]
    public void The_two_character_minimum_stays() {
      Assert.Empty(Find("a a a", "a", true, true));
      Assert.Empty(Find("a  a", "  ", true, false));
    }

    [Fact]
    public void Saved_highlights_follow_the_options() {
      string stored = NoteHighlights.With("", "art", "Yellow");
      Assert.Equal(7, NoteHighlights.Matches(Text, stored).Count());
      Assert.Equal(new[] { 10, 43, 56 }, NoteHighlights.Matches(Text, stored, new MatchOptions(true, true)).Select(m => m.Start).ToArray());
    }

    [Fact]
    public void The_saved_term_around_the_caret_follows_the_options() {
      string stored = NoteHighlights.With("", "art", "Yellow");
      // The caret in "Start": a match when matching anywhere, none with whole words.
      Assert.NotNull(NoteHighlights.TermAt(Text, stored, 3, 0));
      Assert.Null(NoteHighlights.TermAt(Text, stored, 3, 0, new MatchOptions(true, false)));
      Assert.NotNull(NoteHighlights.TermAt(Text, stored, 11, 0, new MatchOptions(true, false)));
    }
  }
}
