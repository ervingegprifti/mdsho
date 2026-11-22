namespace MDSHO.ViewModels {

  /// <summary>One line of a note's or snippet's tooltip: its text, grey for "(empty)" and "… N more lines".</summary>
  public sealed class ToolTipLine {
    public ToolTipLine(string text, bool isGrey = false) {
      this.Text = text;
      this.IsGrey = isGrey;
    }

    /// <summary>The text of the line.</summary>
    public string Text { get; }

    /// <summary>True for a grey line ("(empty)" or "… N more lines") rather than the button's own text.</summary>
    public bool IsGrey { get; }
  }
}
