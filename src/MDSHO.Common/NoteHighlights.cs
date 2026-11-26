using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MDSHO.Common {

  /// <summary>The colours a note's text can be highlighted in, by name (the names are saved).</summary>
  public static class HighlightColors {
    /// <summary>Yellow, Green, Blue, Pink and Orange, in menu order.</summary>
    public static readonly IReadOnlyList<string> All = new[] { "Yellow", "Green", "Blue", "Pink", "Orange" };

    /// <summary>True for one of <see cref="All"/>.</summary>
    public static bool IsKnown(string color) => color != null && All.Contains(color);
  }

  /// <summary>One saved highlight: a term and its colour.</summary>
  public sealed class SavedHighlight {
    /// <summary>The text to colour wherever it appears (case-insensitive).</summary>
    [JsonPropertyName("term")]
    public string Term { get; set; } = "";
    /// <summary>One of <see cref="HighlightColors.All"/>.</summary>
    [JsonPropertyName("color")]
    public string Color { get; set; } = "";
  }

  /// <summary>
  /// The saved colour highlights of a note (Shortcuts.NoteHighlights): terms with a colour, not places, so they colour
  /// every match (see <see cref="TextMatches"/>) even after the text changes. Stored as a JSON list, "" when there are
  /// none; the note's text itself stays plain.
  /// </summary>
  public static class NoteHighlights {

    /// <summary>The saved highlights, in the order they were added. Broken values, unknown colours and short terms are skipped.</summary>
    public static IReadOnlyList<SavedHighlight> Parse(string stored) {
      if (string.IsNullOrWhiteSpace(stored)) {
        return Array.Empty<SavedHighlight>();
      }
      List<SavedHighlight> list;
      try {
        list = JsonSerializer.Deserialize<List<SavedHighlight>>(stored);
      } catch (JsonException) {
        return Array.Empty<SavedHighlight>();
      }
      return (list ?? new List<SavedHighlight>()).Where(h => h != null && TextMatches.IsSearchable(h.Term) && HighlightColors.IsKnown(h.Color)).ToList();
    }

    /// <summary>The stored value of these highlights; "" for none.</summary>
    public static string Format(IEnumerable<SavedHighlight> highlights) {
      List<SavedHighlight> list = highlights.ToList();
      return list.Count == 0 ? "" : JsonSerializer.Serialize(list);
    }

    /// <summary>The saved highlight of a term (case-insensitive), or null.</summary>
    public static SavedHighlight Find(string stored, string term) {
      return Parse(stored).FirstOrDefault(h => Same(h.Term, term));
    }

    /// <summary>Adds a term with a colour; a term saved already (in any case) takes the new term and colour in its place.</summary>
    public static string With(string stored, string term, string color) {
      List<SavedHighlight> list = Parse(stored).ToList();
      if (!TextMatches.IsSearchable(term) || !HighlightColors.IsKnown(color)) {
        return Format(list);
      }
      int at = list.FindIndex(h => Same(h.Term, term));
      SavedHighlight added = new SavedHighlight { Term = term, Color = color };
      if (at >= 0) {
        list[at] = added;
      } else {
        list.Add(added);
      }
      return Format(list);
    }

    /// <summary>Removes a term (case-insensitive).</summary>
    public static string Without(string stored, string term) {
      return Format(Parse(stored).Where(h => !Same(h.Term, term)));
    }

    /// <summary>Every match of every saved term with its colour: term by term, each in order.</summary>
    public static IEnumerable<(int Start, int Length, string Color)> Matches(string text, string stored) {
      foreach (SavedHighlight h in Parse(stored)) {
        foreach ((int start, int length) in TextMatches.Find(text, h.Term)) {
          yield return (start, length, h.Color);
        }
      }
    }

    /// <summary>
    /// The saved highlight a menu acts on: the one whose term is the selected text, else the one with a match around the
    /// caret (the start of the selection); null when there is none.
    /// </summary>
    public static SavedHighlight TermAt(string text, string stored, int selectionStart, int selectionLength) {
      IReadOnlyList<SavedHighlight> all = Parse(stored);
      if (selectionLength > 0 && text != null && selectionStart >= 0 && selectionStart + selectionLength <= text.Length) {
        string selected = text.Substring(selectionStart, selectionLength);
        SavedHighlight same = all.FirstOrDefault(h => Same(h.Term, selected));
        if (same != null) {
          return same;
        }
        if (TextMatches.IsSearchable(selected)) {
          return null;
        }
      }
      return all.FirstOrDefault(h => TextMatches.Find(text, h.Term).Any(m => m.Start <= selectionStart && selectionStart < m.Start + m.Length));
    }

    private static bool Same(string a, string b) {
      return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }
  }
}
