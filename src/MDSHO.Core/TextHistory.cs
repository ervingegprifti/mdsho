using System;
using System.Globalization;

namespace MDSHO.Core {

  /// <summary>The two earlier copies of a text and when each was kept ("yyyy-MM-ddTHH:mm:ss" local time, or empty).</summary>
  public sealed record TextHistorySlots(string Previous1, string Previous1Time, string Previous2, string Previous2Time) {
    /// <summary>No copies yet.</summary>
    public static readonly TextHistorySlots Empty = new TextHistorySlots("", "", "", "");
  }

  /// <summary>
  /// The text history of notes, snippets and comments: the text moves to "Before" (and "Before" to "Earlier") at the first
  /// change after the window opens or for a big removal (see <see cref="TextHistoryTracker"/>), and a copy can be picked
  /// back, which keeps the text you had so the pick can be undone. Nothing is time-based; the times are only labels.
  /// </summary>
  public static class TextHistory {
    /// <summary>A single edit that removes at least this many characters keeps a copy at once.</summary>
    public const int BigRemovalLength = 200;
    /// <summary>How many characters of the first line the menu shows.</summary>
    public const int FirstLineLength = 40;

    private const string StampFormat = "yyyy-MM-dd'T'HH:mm:ss";

    /// <summary>True when one edit removes half the text or more, or at least <see cref="BigRemovalLength"/> characters.</summary>
    public static bool IsBigRemoval(int removedLength, int lengthBefore) {
      return removedLength > 0 && (removedLength >= BigRemovalLength || removedLength * 2 >= lengthBefore);
    }

    /// <summary>A time as stored: local time to the second.</summary>
    public static string Stamp(DateTime time) {
      return time.ToString(StampFormat, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Keeps the saved text as "Before" and moves "Before" to "Earlier". Returns the same slots when the saved text is
    /// empty or equals "Before" (nothing worth keeping).
    /// </summary>
    public static TextHistorySlots Keep(string saved, TextHistorySlots slots, DateTime now) {
      saved ??= "";
      if (saved.Length == 0 || saved == slots.Previous1) {
        return slots;
      }
      return new TextHistorySlots(saved, Stamp(now), slots.Previous1, slots.Previous1Time);
    }

    /// <summary>
    /// Picks copy 1 ("Before") or 2 ("Earlier"): returns its text, to become the text now, and the slots after. The text
    /// you had becomes "Before"; picking "Before" again undoes the pick.
    /// </summary>
    public static (string Text, TextHistorySlots Slots) Pick(int slot, string current, TextHistorySlots slots, DateTime now) {
      current ??= "";
      if (slot == 1) {
        return (slots.Previous1, new TextHistorySlots(current, Stamp(now), slots.Previous2, slots.Previous2Time));
      }
      if (slot == 2) {
        return (slots.Previous2, new TextHistorySlots(current, Stamp(now), slots.Previous1, slots.Previous1Time));
      }
      throw new ArgumentOutOfRangeException(nameof(slot));
    }

    /// <summary>"today 14:05", "20 Nov 18:30", or with the year when it is not this year; empty for no or a bad time.</summary>
    public static string TimeLabel(string stamp, DateTime now) {
      if (!DateTime.TryParseExact(stamp, StampFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime time)) {
        return "";
      }
      CultureInfo en = CultureInfo.InvariantCulture;
      if (time.Date == now.Date) {
        return "today " + time.ToString("HH:mm", en);
      }
      return time.Year == now.Year ? time.ToString("d MMM HH:mm", en) : time.ToString("d MMM yyyy HH:mm", en);
    }

    /// <summary>The first line that isn't blank, trimmed and cut to <see cref="FirstLineLength"/> characters with "…".</summary>
    public static string FirstLine(string text) {
      foreach (string line in (text ?? "").Split('\n')) {
        string trimmed = line.Trim();
        if (trimmed.Length > 0) {
          return trimmed.Length <= FirstLineLength ? trimmed : trimmed.Substring(0, FirstLineLength - 1) + "…";
        }
      }
      return "";
    }
  }

  /// <summary>
  /// Decides, for one open window, when a change keeps a copy: the first change after the window opens (each window,
  /// also one MDSHO reopens at start, gets a new tracker), and at once for a single edit that removes a lot. No timers.
  /// </summary>
  public sealed class TextHistoryTracker {
    private bool changed;

    /// <summary>Called for each change of the text; true when the text before it should be kept now.</summary>
    public bool OnChange(int removedLength, int lengthBefore) {
      bool first = !this.changed;
      this.changed = true;
      return first || TextHistory.IsBigRemoval(removedLength, lengthBefore);
    }
  }
}
