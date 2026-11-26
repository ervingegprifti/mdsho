using System.Collections.Generic;
using System.Globalization;

namespace MDSHO.Common {

  /// <summary>
  /// Finds every place a text appears in a note, for the live highlights of the selected text and the saved colour
  /// highlights: case-insensitive, anywhere (also inside other words), never overlapping, and only for a text with at
  /// least 2 characters that are not spaces.
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
      List<(int Start, int Length)> found = new List<(int Start, int Length)>();
      if (string.IsNullOrEmpty(text) || !IsSearchable(term)) {
        return found;
      }
      CompareInfo compare = CultureInfo.InvariantCulture.CompareInfo;
      int from = 0;
      while (found.Count < max && from <= text.Length - term.Length) {
        int at = compare.IndexOf(text, term, from, CompareOptions.OrdinalIgnoreCase);
        if (at < 0) {
          break;
        }
        found.Add((at, term.Length));
        from = at + term.Length;
      }
      return found;
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
