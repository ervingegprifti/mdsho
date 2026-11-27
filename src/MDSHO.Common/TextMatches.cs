using System.Collections.Generic;
using System.Globalization;

namespace MDSHO.Common {

  /// <summary>
  /// How a text matches: <see cref="WholeWords"/> skips matches inside other words (a letter, digit or _ right before or
  /// after), <see cref="MatchCase"/> skips other cases. Both off by default: anywhere, ignoring case.
  /// </summary>
  public readonly record struct MatchOptions(bool WholeWords, bool MatchCase) {
    /// <summary>Anywhere, ignoring case.</summary>
    public static readonly MatchOptions Off = default;
  }

  /// <summary>
  /// Finds every place a text appears in a note, for the live highlights of the selected text and the saved colour
  /// highlights: by default case-insensitive and anywhere (also inside other words), see <see cref="MatchOptions"/>;
  /// never overlapping, and only for a text with at least 2 characters that are not spaces.
  /// </summary>
  public static class TextMatches {
    /// <summary>The most matches one search finds, so long notes stay fast.</summary>
    public const int MaxMatches = 10000;

    /// <summary>True when the text has at least 2 characters that are not spaces or line breaks.</summary>
    public static bool IsSearchable(string term) {
      if (string.IsNullOrEmpty(term)) {
        return false;
      }
      int count = 0;
      foreach (char c in term) {
        if (!char.IsWhiteSpace(c) && ++count >= 2) {
          return true;
        }
      }
      return false;
    }

    /// <summary>The start and length of each match, in order; at most <paramref name="max"/>.</summary>
    public static List<(int Start, int Length)> Find(string text, string term, int max = MaxMatches) {
      return Find(text, term, MatchOptions.Off, max);
    }

    /// <summary>The start and length of each match with these options, in order; at most <paramref name="max"/>.</summary>
    public static List<(int Start, int Length)> Find(string text, string term, MatchOptions options, int max = MaxMatches) {
      List<(int Start, int Length)> found = new List<(int Start, int Length)>();
      if (string.IsNullOrEmpty(text) || !IsSearchable(term)) {
        return found;
      }
      CompareInfo compare = CultureInfo.InvariantCulture.CompareInfo;
      CompareOptions how = options.MatchCase ? CompareOptions.Ordinal : CompareOptions.OrdinalIgnoreCase;
      int from = 0;
      while (found.Count < max && from <= text.Length - term.Length) {
        int at = compare.IndexOf(text, term, from, how);
        if (at < 0) {
          break;
        }
        if (options.WholeWords && !IsWholeWord(text, at, term.Length)) {
          // Not a whole word: a later match may start inside this one.
          from = at + 1;
          continue;
        }
        found.Add((at, term.Length));
        from = at + term.Length;
      }
      return found;
    }

    /// <summary>True for a letter, digit or _.</summary>
    public static bool IsWordCharacter(char c) {
      return char.IsLetterOrDigit(c) || c == '_';
    }

    // No word character right before the start or right after the end.
    private static bool IsWholeWord(string text, int start, int length) {
      int end = start + length;
      return (start == 0 || !IsWordCharacter(text[start - 1])) && (end >= text.Length || !IsWordCharacter(text[end]));
    }

    /// <summary>"3 matches" for the title bar; empty when the text appears only once or not at all.</summary>
    public static string CountLabel(int count) {
      if (count < 2) {
        return "";
      }
      return count >= MaxMatches ? MaxMatches.ToString(CultureInfo.InvariantCulture) + "+ matches"
        : count.ToString(CultureInfo.InvariantCulture) + " matches";
    }
  }
}
