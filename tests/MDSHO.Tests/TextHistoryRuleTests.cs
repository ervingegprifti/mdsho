using System;
using System.Globalization;
using MDSHO.Core;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// The text history rules (1.22.0) that need no window: what a big removal is, how a copy is stamped, and how the menu
  /// labels it ("Today 14:05", "Yesterday 18:30", "19 Nov 16:44" and the first line, "(empty)" for an empty text).
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

    [Fact]
    public void The_time_says_today_yesterday_or_the_day_and_month() {
      DateTime now = new DateTime(2026, 11, 21, 15, 0, 0);
      Assert.Equal("Today 14:05", TextHistory.TimeLabel("2026-11-21T14:05:00", now));
      Assert.Equal("Yesterday 18:30", TextHistory.TimeLabel("2026-11-20T18:30:00", now));
      Assert.Equal("19 Nov 16:44", TextHistory.TimeLabel("2026-11-19T16:44:00", now));
      Assert.Equal("3 Jan 2025 08:00", TextHistory.TimeLabel("2025-01-03T08:00:00", now));
      Assert.Equal("Yesterday 23:59", TextHistory.TimeLabel("2026-12-31T23:59:00", new DateTime(2027, 1, 1, 0, 5, 0)));
      Assert.Equal("", TextHistory.TimeLabel("", now));
      Assert.Equal("", TextHistory.TimeLabel("not a time", now));
    }

    [Fact]
    public void A_menu_line_is_the_time_and_the_first_line() {
      DateTime now = new DateTime(2026, 11, 21, 15, 0, 0);
      Assert.Equal("Today 14:05  ·  Buy milk…", TextHistory.MenuLine("2026-11-21T14:05:00", "\r\nBuy milk\r\neggs", now));
      Assert.Equal("Yesterday 18:30  ·  (empty)", TextHistory.MenuLine("2026-11-20T18:30:00", "  \r\n", now));
      Assert.Equal("(empty)", TextHistory.MenuLine("", "", now));
      Assert.Equal("Buy milk", TextHistory.MenuLine("bad", "Buy milk", now));
    }

    // "…" whenever the text goes on past what is shown: a long first line, or more text on later lines.
    [Fact]
    public void The_first_line_is_cut_short_and_ends_in_dots_when_the_text_goes_on() {
      Assert.Equal("Shopping list…", TextHistory.FirstLine("Shopping list\r\nmilk\r\neggs"));
      Assert.Equal("milk…", TextHistory.FirstLine("\r\n  \r\n  milk  \r\neggs"));
      Assert.Equal("Shopping list", TextHistory.FirstLine("Shopping list\r\n  \r\n"));
      Assert.Equal("Shopping list", TextHistory.FirstLine("  Shopping list  "));
      Assert.Equal(new string('x', 39) + "…", TextHistory.FirstLine(new string('x', 60)));
      Assert.Equal(new string('x', 39) + "…", TextHistory.FirstLine(new string('x', 60) + "\r\nmore"));
      Assert.Equal(new string('x', 40) + "…", TextHistory.FirstLine(new string('x', 40) + "\nmore"));
      Assert.Equal(new string('x', 40), TextHistory.FirstLine(new string('x', 40)));
      Assert.Equal("", TextHistory.FirstLine(""));
      Assert.DoesNotContain("...", TextHistory.FirstLine("a\nb"));
    }

    [Fact]
    public void The_stamp_is_iso_local_time_to_the_second() {
      Assert.Equal("2026-11-21T09:00:00", TextHistory.Stamp(T0));
      Assert.Equal(T0, DateTime.ParseExact(TextHistory.Stamp(T0), "yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture));
    }
  }
}
