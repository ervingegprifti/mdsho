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
    /// "Today 14:05:09", "Yesterday 14:05:09", "19 Nov 14:05:09", or "19 Nov 2025 14:05:09" when it is not this year;
    /// empty for no or a bad time.
    /// </summary>
    public static string TimeLabel(string stamp, DateTime now) {
      if (!DateTime.TryParseExact(stamp, StampFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime time)) {
        return "";
      }
      CultureInfo en = CultureInfo.InvariantCulture;
      if (time.Date == now.Date) {
        return "Today " + time.ToString("HH:mm:ss", en);
      }
      if (time.Date == now.Date.AddDays(-1)) {
        return "Yesterday " + time.ToString("HH:mm:ss", en);
      }
      return time.Year == now.Year ? time.ToString("d MMM HH:mm:ss", en) : time.ToString("d MMM yyyy HH:mm:ss", en);
    }

    /// <summary>A copy in the menu: its time and its first line, "(empty)" for an empty text.</summary>
    public static string MenuLine(string keptAt, string text, DateTime now) {
      return MenuLine(keptAt, text, now, "");
    }

    /// <summary>Like <see cref="MenuLine(string, string, DateTime)"/>, with "from PC" after the time for a text another PC sent.</summary>
    public static string MenuLine(string keptAt, string text, DateTime now, string fromPc) {
      string first = FirstLine(text);
      if (first.Length == 0) {
        first = "(empty)";
      }
      if (!string.IsNullOrWhiteSpace(fromPc)) {
        first = "from " + fromPc.Trim() + "  ·  " + first;
      }
      string when = TimeLabel(keptAt, now);
      return when.Length == 0 ? first : when + "  ·  " + first;
    }

    /// <summary>"↵" (U+21B5) after the first line when more lines follow.</summary>
    public const string MoreLines = "\u21B5";

    /// <summary>
    /// The first line that isn't blank, trimmed: a short line as it is; a longer line cut to its first
    /// <see cref="FirstLineLength"/> characters (spaces at the cut trimmed) then "…", so "…" only ever means the line was
    /// cut; when more lines follow, a space and "↵" after that. Empty for no text.
    /// </summary>
    public static string FirstLine(string text) {
      string[] lines = (text ?? "").Split('\n');
      for (int i = 0; i < lines.Length; i++) {
        string line = lines[i].Trim();
        if (line.Length == 0) {
          continue;
        }
        if (line.Length > FirstLineLength) {
          line = line.Substring(0, FirstLineLength).TrimEnd() + "…";
        }
        bool more = lines.Skip(i + 1).Any(l => !string.IsNullOrWhiteSpace(l));
        return more ? line + " " + MoreLines : line;
      }
      return "";
    }
  }
}
