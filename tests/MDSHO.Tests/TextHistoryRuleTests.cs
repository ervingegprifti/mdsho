using System;
using System.Globalization;
using MDSHO.Core;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// The text history rule (1.21.0): two earlier copies of a note's, snippet's or comment's text. The saved text moves
  /// down at most once per editing burst; a burst starts with the first change after the window opens or after 10
  /// minutes without a change, and a single edit that removes a lot moves it at once.
  /// </summary>
  public class TextHistoryRuleTests {
    private static readonly DateTime T0 = new DateTime(2026, 11, 21, 9, 0, 0);

    // ---- When to keep a copy ----

    [Fact]
    public void The_first_change_after_the_window_opens_keeps_a_copy() {
      TextHistoryTracker tracker = new TextHistoryTracker();
      Assert.True(tracker.OnChange(T0, removedLength: 0, lengthBefore: 10));
    }

    [Fact]
    public void More_typing_in_the_same_burst_keeps_no_more_copies() {
      TextHistoryTracker tracker = new TextHistoryTracker();
      tracker.OnChange(T0, 0, 10);
      for (int i = 1; i < 50; i++) {
        Assert.False(tracker.OnChange(T0.AddSeconds(i * 20), 1, 10 + i));
      }
    }

    [Fact]
    public void A_window_kept_open_all_day_keeps_a_copy_after_each_10_minute_pause() {
      TextHistoryTracker tracker = new TextHistoryTracker();
      Assert.True(tracker.OnChange(T0, 0, 10));
      // Typing every 9 minutes is one long burst.
      Assert.False(tracker.OnChange(T0.AddMinutes(9), 0, 11));
      Assert.False(tracker.OnChange(T0.AddMinutes(18), 0, 12));
      // A 10 minute pause starts a new burst.
      Assert.True(tracker.OnChange(T0.AddMinutes(28), 0, 13));
      Assert.False(tracker.OnChange(T0.AddMinutes(29), 0, 14));
      // Hours later, the same.
      Assert.True(tracker.OnChange(T0.AddHours(6), 0, 15));
      Assert.Equal(TimeSpan.FromMinutes(10), TextHistory.BurstPause);
    }

    [Theory]
    [InlineData(100, 50)]   // half the text
    [InlineData(100, 100)]  // cut all, select all and delete
    [InlineData(1000, 200)] // 200 characters
    [InlineData(1, 1)]
    public void A_single_edit_that_removes_a_lot_keeps_a_copy_at_once_mid_burst(int lengthBefore, int removed) {
      TextHistoryTracker tracker = new TextHistoryTracker();
      tracker.OnChange(T0, 0, lengthBefore);
      Assert.True(tracker.OnChange(T0.AddSeconds(5), removed, lengthBefore));
    }

    [Theory]
    [InlineData(100, 49)]
    [InlineData(1000, 199)]
    [InlineData(0, 0)]
    public void A_smaller_removal_is_part_of_the_burst(int lengthBefore, int removed) {
      TextHistoryTracker tracker = new TextHistoryTracker();
      tracker.OnChange(T0, 0, lengthBefore);
      Assert.False(tracker.OnChange(T0.AddSeconds(5), removed, lengthBefore));
    }

    [Fact]
    public void A_big_removal_does_not_end_the_burst_for_the_next_small_change() {
      TextHistoryTracker tracker = new TextHistoryTracker();
      tracker.OnChange(T0, 0, 100);
      Assert.True(tracker.OnChange(T0.AddSeconds(5), 100, 100));
      Assert.False(tracker.OnChange(T0.AddSeconds(6), 0, 0));
    }

    // ---- Moving the copies down ----

    [Fact]
    public void Keeping_a_copy_moves_the_saved_text_to_before_and_before_to_earlier() {
      TextHistorySlots before = new TextHistorySlots("b", "2026-11-21T08:00:00", "e", "2026-11-20T18:30:00");
      TextHistorySlots after = TextHistory.Keep("now text", before, T0);
      Assert.Equal(new TextHistorySlots("now text", TextHistory.Stamp(T0), "b", "2026-11-21T08:00:00"), after);
    }

    [Fact]
    public void Nothing_moves_when_the_saved_text_equals_before_or_is_empty() {
      TextHistorySlots slots = new TextHistorySlots("same", "2026-11-21T08:00:00", "e", "2026-11-20T18:30:00");
      Assert.Same(slots, TextHistory.Keep("same", slots, T0));
      Assert.Same(slots, TextHistory.Keep("", slots, T0));
    }

    [Fact]
    public void Picking_before_swaps_it_with_the_text_now_so_it_can_be_undone() {
      TextHistorySlots slots = new TextHistorySlots("b", "2026-11-21T08:00:00", "e", "2026-11-20T18:30:00");
      (string text, TextHistorySlots after) = TextHistory.Pick(1, "now", slots, T0);
      Assert.Equal("b", text);
      Assert.Equal(new TextHistorySlots("now", TextHistory.Stamp(T0), "e", "2026-11-20T18:30:00"), after);
      (string back, TextHistorySlots again) = TextHistory.Pick(1, text, after, T0.AddMinutes(1));
      Assert.Equal("now", back);
      Assert.Equal("b", again.Previous1);
    }

    [Fact]
    public void Picking_earlier_puts_the_text_now_in_before_and_before_in_earlier() {
      TextHistorySlots slots = new TextHistorySlots("b", "2026-11-21T08:00:00", "e", "2026-11-20T18:30:00");
      (string text, TextHistorySlots after) = TextHistory.Pick(2, "now", slots, T0);
      Assert.Equal("e", text);
      Assert.Equal(new TextHistorySlots("now", TextHistory.Stamp(T0), "b", "2026-11-21T08:00:00"), after);
    }

    // ---- How the menu shows a copy ----

    [Fact]
    public void The_time_says_today_or_the_day_and_month() {
      DateTime now = new DateTime(2026, 11, 21, 15, 0, 0);
      Assert.Equal("today 14:05", TextHistory.TimeLabel("2026-11-21T14:05:00", now));
      Assert.Equal("20 Nov 18:30", TextHistory.TimeLabel("2026-11-20T18:30:00", now));
      Assert.Equal("3 Jan 2025 08:00", TextHistory.TimeLabel("2025-01-03T08:00:00", now));
      Assert.Equal("", TextHistory.TimeLabel("", now));
      Assert.Equal("", TextHistory.TimeLabel("not a time", now));
    }

    [Fact]
    public void The_first_line_is_cut_short() {
      Assert.Equal("Shopping", TextHistory.FirstLine("Shopping\r\nmilk\r\neggs"));
      Assert.Equal("milk", TextHistory.FirstLine("\r\n  \r\n  milk  \r\neggs"));
      Assert.Equal(new string('x', 39) + "…", TextHistory.FirstLine(new string('x', 60)));
      Assert.Equal("", TextHistory.FirstLine(""));
    }

    [Fact]
    public void The_stamp_is_iso_local_time_to_the_second() {
      Assert.Equal("2026-11-21T09:00:00", TextHistory.Stamp(T0));
      Assert.Equal(T0, DateTime.ParseExact(TextHistory.Stamp(T0), "yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture));
    }
  }
}
