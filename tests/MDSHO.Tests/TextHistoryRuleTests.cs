using System;
using System.Globalization;
using MDSHO.Core;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// The text history rules (1.22.0) that need no window: what a big removal is, how a copy is stamped, and how the menu
  /// labels it ("Today 14:05:09", "Yesterday 18:30:00", "19 Nov 16:44:00" and the first line, "(empty)" for an empty text).
  /// Nothing is time-based; the times are only labels.
  /// </summary>
  public class TextHistoryRuleTests {
    private static readonly DateTime T0 = new DateTime(2026, 11, 21, 9, 0, 0);

    [Fact]
    public void Nothing_is_time_based() {
      Assert.DoesNotContain(typeof(TextHistory).GetFields(), f => f.FieldType == typeof(TimeSpan));
    }

    [Theory]
    [InlineData(100, 50)]   // half the text
    [InlineData(100, 100)]  // cut all, select all and delete
    [InlineData(1000, 200)] // 200 characters
    [InlineData(1, 1)]
    public void A_single_edit_that_removes_a_lot_is_a_big_removal(int lengthBefore, int removed) {
      Assert.True(TextHistory.IsBigRemoval(removed, lengthBefore));
    }

    [Theory]
    [InlineData(100, 49)]
    [InlineData(1000, 199)]
    [InlineData(0, 0)]
    public void A_smaller_removal_is_not(int lengthBefore, int removed) {
      Assert.False(TextHistory.IsBigRemoval(removed, lengthBefore));
    }

    // 1.22.2: the time shows seconds.
    [Fact]
    public void The_time_says_today_yesterday_or_the_day_and_month_with_seconds() {
      DateTime now = new DateTime(2026, 11, 21, 15, 0, 0);
      Assert.Equal("Today 14:05:09", TextHistory.TimeLabel("2026-11-21T14:05:09", now));
      Assert.Equal("Yesterday 14:05:09", TextHistory.TimeLabel("2026-11-20T14:05:09", now));
      Assert.Equal("19 Nov 14:05:09", TextHistory.TimeLabel("2026-11-19T14:05:09", now));
      Assert.Equal("19 Nov 2025 14:05:09", TextHistory.TimeLabel("2025-11-19T14:05:09", now));
      Assert.Equal("3 Jan 2025 08:00:00", TextHistory.TimeLabel("2025-01-03T08:00:00", now));
      Assert.Equal("Yesterday 23:59:59", TextHistory.TimeLabel("2026-12-31T23:59:59", new DateTime(2027, 1, 1, 0, 5, 0)));
      Assert.Equal("", TextHistory.TimeLabel("", now));
      Assert.Equal("", TextHistory.TimeLabel("not a time", now));
    }

    [Fact]
    public void A_menu_line_is_the_time_and_the_first_line() {
      DateTime now = new DateTime(2026, 11, 21, 15, 0, 0);
      Assert.Equal("Today 14:05:00  ·  Buy milk ↵", TextHistory.MenuLine("2026-11-21T14:05:00", "\r\nBuy milk\r\neggs", now));
      Assert.Equal("Yesterday 18:30:00  ·  (empty)", TextHistory.MenuLine("2026-11-20T18:30:00", "  \r\n", now));
      Assert.Equal("(empty)", TextHistory.MenuLine("", "", now));
      Assert.Equal("Buy milk", TextHistory.MenuLine("bad", "Buy milk", now));
    }

    // The first line rules (1.22.2): the first non-blank line, trimmed. A long line keeps its first 40 characters then
    // "…" with no space ("…" only ever means the line was cut); more lines after it add a space and "↵" (U+21B5).
    private static readonly string X40 = new string('x', 40);

    [Fact]
    public void One_short_line_shows_as_it_is() {
      Assert.Equal("ABC", TextHistory.FirstLine("ABC"));
      Assert.Equal("Shopping list", TextHistory.FirstLine("  Shopping list  "));
      Assert.Equal("Shopping list", TextHistory.FirstLine("Shopping list\r\n  \r\n"));
      Assert.Equal(X40, TextHistory.FirstLine(X40));
    }

    [Fact]
    public void One_long_line_keeps_40_characters_then_dots_with_no_space() {
      Assert.Equal(X40 + "…", TextHistory.FirstLine(new string('x', 60)));
      Assert.Equal(X40 + "…", TextHistory.FirstLine(X40 + "y"));
    }

    [Fact]
    public void Spaces_at_the_cut_are_trimmed() {
      Assert.Equal(new string('x', 37) + "…", TextHistory.FirstLine(new string('x', 37) + "     more words"));
      Assert.Equal(new string('x', 37) + "… ↵", TextHistory.FirstLine(new string('x', 37) + "     more words\nnext"));
    }

    [Fact]
    public void More_lines_after_a_short_line_add_a_space_and_the_enter_symbol() {
      Assert.Equal("\u21B5", "↵");
      Assert.Equal("tre ↵", TextHistory.FirstLine("tre\nkater"));
      Assert.Equal("ABC ↵", TextHistory.FirstLine("ABC\nSSS\nDDD"));
      Assert.Equal("Shopping list ↵", TextHistory.FirstLine("Shopping list\r\nmilk\r\neggs"));
      Assert.Equal(X40 + " ↵", TextHistory.FirstLine(X40 + "\nmore"));
    }

    [Fact]
    public void More_lines_after_a_long_line_cut_it_with_dots_then_add_a_space_and_the_enter_symbol() {
      Assert.Equal(X40 + "… ↵", TextHistory.FirstLine(new string('x', 60) + "\r\nmore"));
    }

    [Fact]
    public void Leading_blank_lines_are_skipped() {
      Assert.Equal("milk ↵", TextHistory.FirstLine("\r\n  \r\n  milk  \r\neggs"));
      Assert.Equal("milk", TextHistory.FirstLine("\n\t\n  milk  \n  \n"));
    }

    [Fact]
    public void No_text_is_empty() {
      Assert.Equal("", TextHistory.FirstLine(""));
      Assert.Equal("", TextHistory.FirstLine(null));
      Assert.Equal("", TextHistory.FirstLine(" \r\n\t "));
      Assert.Equal("a ↵", TextHistory.FirstLine("a\nb"));
    }

    [Fact]
    public void The_stamp_is_iso_local_time_to_the_second() {
      Assert.Equal("2026-11-21T09:00:00", TextHistory.Stamp(T0));
      Assert.Equal(T0, DateTime.ParseExact(TextHistory.Stamp(T0), "yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture));
    }
  }
}
