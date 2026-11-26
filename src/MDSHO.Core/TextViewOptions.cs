namespace MDSHO.Core {

  /// <summary>
  /// The More options settings of one note, comment or snippet window, saved with its button: Highlight current line,
  /// Highlight whole words only, Highlight matching case only and Wrap text. A note keeps its wrap with its window place,
  /// so its WrapText here is not used.
  /// </summary>
  public sealed record TextViewOptions(bool CurrentLine = true, bool WholeWords = false, bool MatchCase = false, bool WrapText = false) {
    /// <summary>A new item: current line on, the rest off.</summary>
    public static readonly TextViewOptions Default = new TextViewOptions();
  }
}
