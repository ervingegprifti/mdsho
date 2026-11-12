namespace MDSHO.Common {

  /// <summary>
  /// What a double or triple click selects in a text editor: a double click selects the word under the mouse without the
  /// space after it (or the run of spaces, or of punctuation, it is on); a triple click selects the whole paragraph (the
  /// text between line breaks) without its line break.
  /// </summary>
  public static class TextClickSelection {

    /// <summary>The start and length of the word at <paramref name="index"/> (a character index; clamped into the text).</summary>
    public static (int Start, int Length) WordAt(string text, int index) {
      if (string.IsNullOrEmpty(text)) {
        return (0, 0);
      }
      int i = Clamp(text, index);
      if (IsLineBreak(text[i])) {
        // Past the end of a line: the line's last word, or nothing on an empty line.
        int breakStart = text[i] == '\n' && i > 0 && text[i - 1] == '\r' ? i - 1 : i;
        if (breakStart == 0 || IsLineBreak(text[breakStart - 1])) {
          return (breakStart, 0);
        }
        i = breakStart - 1;
      }
      int kind = KindOf(text[i]);
      int start = i;
      while (start > 0 && SameRun(kind, text[start - 1], text[i])) {
        start--;
      }
      int end = i + 1;
      while (end < text.Length && SameRun(kind, text[end], text[i])) {
        end++;
      }
      return (start, end - start);
    }

    /// <summary>The start and length of the paragraph at <paramref name="index"/>, without its line break.</summary>
    public static (int Start, int Length) ParagraphAt(string text, int index) {
      if (string.IsNullOrEmpty(text)) {
        return (0, 0);
      }
      int i = Clamp(text, index);
      if (text[i] == '\n' && i > 0 && text[i - 1] == '\r') {
        i--;
      }
      int start = i;
      while (start > 0 && !IsLineBreak(text[start - 1])) {
        start--;
      }
      int end = i;
      while (end < text.Length && !IsLineBreak(text[end])) {
        end++;
      }
      return (start, end - start);
    }

    private const int Word = 0, Space = 1, Punctuation = 2;

    private static int Clamp(string text, int index) {
      return index < 0 ? 0 : index >= text.Length ? text.Length - 1 : index;
    }

    private static bool IsLineBreak(char c) {
      return c == '\r' || c == '\n';
    }

    private static int KindOf(char c) {
      if (char.IsLetterOrDigit(c) || c == '_') {
        return Word;
      }
      return char.IsWhiteSpace(c) ? Space : Punctuation;
    }

    // Words and spaces run over any of their kind; punctuation runs over the same character only (so "..." but not ",(").
    private static bool SameRun(int kind, char c, char clicked) {
      if (IsLineBreak(c)) {
        return false;
      }
      return kind == Punctuation ? c == clicked : KindOf(c) == kind;
    }
  }
}
