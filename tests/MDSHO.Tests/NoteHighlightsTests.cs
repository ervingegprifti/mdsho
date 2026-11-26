using System.Linq;
using MDSHO.Common;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// The saved colour highlights of a note: terms with a colour, kept apart from the note's text (Shortcuts.NoteHighlights),
  /// so they colour every match even after the text changes.
  /// </summary>
  public class NoteHighlightsTests {

    [Fact]
    public void There_are_five_colours() {
      Assert.Equal(new[] { "Yellow", "Green", "Blue", "Pink", "Orange" }, HighlightColors.All);
      Assert.True(HighlightColors.IsKnown("Pink"));
      Assert.False(HighlightColors.IsKnown("Purple"));
      Assert.False(HighlightColors.IsKnown(null));
    }

    [Fact]
    public void Empty_or_broken_values_have_no_highlights() {
      Assert.Empty(NoteHighlights.Parse(""));
      Assert.Empty(NoteHighlights.Parse(null));
      Assert.Empty(NoteHighlights.Parse("not json"));
      Assert.Empty(NoteHighlights.Parse("{\"term\":\"pay\"}"));
      Assert.Equal("", NoteHighlights.Format(Enumerable.Empty<SavedHighlight>()));
    }

    [Fact]
    public void A_term_is_added_with_its_colour_and_read_back() {
      string stored = NoteHighlights.With("", "pay", "Yellow");
      stored = NoteHighlights.With(stored, "dentist", "Green");
      SavedHighlight[] all = NoteHighlights.Parse(stored).ToArray();
      Assert.Equal(new[] { ("pay", "Yellow"), ("dentist", "Green") }, all.Select(h => (h.Term, h.Color)).ToArray());
      Assert.Equal("Green", NoteHighlights.Find(stored, "DENTIST").Color);
      Assert.Null(NoteHighlights.Find(stored, "Friday"));
    }

    [Fact]
    public void Highlighting_a_saved_term_again_changes_its_colour_whatever_its_case() {
      string stored = NoteHighlights.With("", "pay", "Yellow");
      stored = NoteHighlights.With(stored, "PAY", "Blue");
      SavedHighlight only = Assert.Single(NoteHighlights.Parse(stored));
      Assert.Equal(("PAY", "Blue"), (only.Term, only.Color));
    }

    [Fact]
    public void Removing_a_term_ignores_case_and_the_last_one_leaves_an_empty_value() {
      string stored = NoteHighlights.With(NoteHighlights.With("", "pay", "Yellow"), "dentist", "Green");
      stored = NoteHighlights.Without(stored, "Pay");
      Assert.Equal(new[] { "dentist" }, NoteHighlights.Parse(stored).Select(h => h.Term).ToArray());
      Assert.Equal("", NoteHighlights.Without(stored, "dentist"));
      Assert.Equal(stored, NoteHighlights.Without(stored, "not there"));
    }

    [Theory]
    [InlineData("a")]
    [InlineData("  ")]
    [InlineData("")]
    public void A_term_too_short_to_search_or_an_unknown_colour_is_not_saved(string term) {
      Assert.Equal("", NoteHighlights.With("", term, "Yellow"));
      Assert.Equal("", NoteHighlights.With("", "pay", "Purple"));
    }

    [Fact]
    public void Terms_keep_spaces_line_breaks_quotes_and_any_letters() {
      string term = "say \"hi\"\r\nÅnna, ü \\ é";
      string stored = NoteHighlights.With("", term, "Pink");
      Assert.Equal(term, Assert.Single(NoteHighlights.Parse(stored)).Term);
    }

    [Fact]
    public void Unknown_colours_and_short_terms_in_a_stored_value_are_skipped() {
      string stored = "[{\"term\":\"pay\",\"color\":\"Yellow\"},{\"term\":\"x\",\"color\":\"Green\"},{\"term\":\"rent\",\"color\":\"Purple\"}]";
      Assert.Equal(new[] { "pay" }, NoteHighlights.Parse(stored).Select(h => h.Term).ToArray());
    }

    [Fact]
    public void The_matches_of_every_term_are_found_with_its_colour() {
      string text = "Pay the gas bill\r\nBook the dentist\r\nPay rent on Friday";
      string stored = NoteHighlights.With(NoteHighlights.With("", "pay", "Yellow"), "dentist", "Green");
      var matches = NoteHighlights.Matches(text, stored).ToArray();
      Assert.Equal(new[] { (0, 3, "Yellow"), (36, 3, "Yellow"), (27, 7, "Green") },
        matches.Select(m => (m.Start, m.Length, m.Color)).ToArray());
    }

    [Fact]
    public void The_saved_term_at_the_caret_or_the_selection_is_found() {
      string text = "Pay the gas bill\r\nBook the dentist\r\nPay rent on Friday";
      string stored = NoteHighlights.With(NoteHighlights.With("", "pay", "Yellow"), "dentist", "Green");
      // The selection is a saved term.
      Assert.Equal("dentist", NoteHighlights.TermAt(text, stored, 27, 7)?.Term);
      // The caret is inside a match ("Pay" on the last line).
      Assert.Equal("pay", NoteHighlights.TermAt(text, stored, 37, 0)?.Term);
      // Not on a highlight.
      Assert.Null(NoteHighlights.TermAt(text, stored, 45, 0));
      Assert.Null(NoteHighlights.TermAt(text, stored, 48, 6));
    }
  }
}
