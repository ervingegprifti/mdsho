using System;
using MDSHO.Common;

namespace MDSHO.ViewModels {

  /// <summary>What MDSHO says when a clipboard button can't paste: a bold heading, then the text.</summary>
  public sealed class ClipboardPasteMessage {
    /// <summary>The title of the message window.</summary>
    public const string Caption = "MDSHO";

    /// <summary>The bold first line.</summary>
    public string Heading { get; }
    /// <summary>The text below the heading.</summary>
    public string Text { get; }

    private ClipboardPasteMessage(string heading, string text) {
      Heading = heading;
      Text = text;
    }

    /// <summary>The message for a paste outcome, or null when there is nothing to say (pasted, or already pasting).</summary>
    /// <param name="outcome">What the paste did.</param>
    /// <param name="name">The button's name.</param>
    /// <param name="singleClick">True in a panel set to Single click to open: the text says "click".</param>
    public static ClipboardPasteMessage For(PasteOutcome outcome, string name, bool singleClick) {
      string paragraph = Environment.NewLine + Environment.NewLine;
      switch (outcome) {
        case PasteOutcome.NoTarget:
        case PasteOutcome.NotEditable:
          return new ClipboardPasteMessage("No place to paste into.",
            "Click in a text field first, then " + (singleClick ? "click" : "double-click") + " \"" + name + "\"." + paragraph
            + "To paste it yourself, right-click the button, choose Copy value to memory, then press Ctrl+V. Your clipboard is unchanged.");
        case PasteOutcome.Elevated:
          return new ClipboardPasteMessage("Can't paste into this window.",
            "It runs as administrator, and Windows doesn't let MDSHO paste into it." + paragraph
            + "Right-click \"" + name + "\", choose Copy value to memory, then press Ctrl+V. Your clipboard is unchanged.");
        case PasteOutcome.ClipboardBusy:
          return new ClipboardPasteMessage("Can't paste right now.", "Another program is using the clipboard. Try again in a moment.");
        default:
          return null;
      }
    }
  }
}
