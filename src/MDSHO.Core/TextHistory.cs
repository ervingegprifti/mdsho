using System;
using System.Globalization;
using System.Linq;

namespace MDSHO.Core {

  /// <summary>
  /// The text history of notes, snippets and comments (the History table): what a big removal is, how a copy is stamped
  /// and how the menu shows it. Nothing is time-based; the times are only labels.
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
    /// "Today 14:05", "Yesterday 18:30", "19 Nov 16:44", or with the year when it is not this year; empty for no or a bad
    /// time.
    /// </summary>
    public static string TimeLabel(string stamp, DateTime now) {
      if (!DateTime.TryParseExact(stamp, StampFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime time)) {
        return "";
      }
      CultureInfo en = CultureInfo.InvariantCulture;
      if (time.Date == now.Date) {
        return "Today " + time.ToString("HH:mm", en);
      }
      if (time.Date == now.Date.AddDays(-1)) {
        return "Yesterday " + time.ToString("HH:mm", en);
      }
      return time.Year == now.Year ? time.ToString("d MMM HH:mm", en) : time.ToString("d MMM yyyy HH:mm", en);
    }

    /// <summary>A copy in the menu: its time and its first line, "(empty)" for an empty text.</summary>
    public static string MenuLine(string keptAt, string text, DateTime now) {
      string first = FirstLine(text);
      if (first.Length == 0) {
        first = "(empty)";
      }
      string when = TimeLabel(keptAt, now);
      return when.Length == 0 ? first : when + "  ·  " + first;
    }

    /// <summary>
    /// The first line that isn't blank, trimmed and cut to <see cref="FirstLineLength"/> characters. It ends in "…"
    /// whenever the text goes on past what is shown: a longer line, or more text on a later line.
    /// </summary>
    public static string FirstLine(string text) {
      string[] lines = (text ?? "").Split('\n');
      for (int i = 0; i < lines.Length; i++) {
        string trimmed = lines[i].Trim();
        if (trimmed.Length == 0) {
          continue;
        }
        if (trimmed.Length > FirstLineLength) {
          return trimmed.Substring(0, FirstLineLength - 1) + "…";
        }
        bool more = lines.Skip(i + 1).Any(l => !string.IsNullOrWhiteSpace(l));
        return more ? trimmed + "…" : trimmed;
      }
      return "";
    }
  }
}
