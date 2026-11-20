using System;
using System.Collections.Generic;
using System.Linq;

namespace MDSHO.Common {

  /// <summary>The icon of an MDSHO message: Info for results, Warning for questions, destructive actions and errors.</summary>
  public enum MessageIcon {
    /// <summary>The info icon.</summary>
    Info,
    /// <summary>The warning icon.</summary>
    Warning,
  }

  /// <summary>The buttons of an MDSHO message.</summary>
  public enum MessageButtons {
    /// <summary>OK.</summary>
    OK,
    /// <summary>OK and Cancel.</summary>
    OKCancel,
    /// <summary>Yes and No.</summary>
    YesNo,
    /// <summary>Yes, No and Cancel.</summary>
    YesNoCancel,
  }

  /// <summary>The answer to an MDSHO message.</summary>
  public enum MessageResult {
    /// <summary>No answer (yet).</summary>
    None,
    /// <summary>OK.</summary>
    OK,
    /// <summary>Cancel.</summary>
    Cancel,
    /// <summary>Yes.</summary>
    Yes,
    /// <summary>No.</summary>
    No,
  }

  /// <summary>
  /// The choices of the one MDSHO dialog, without WPF: the buttons of each set in order, the answer Esc and the close
  /// button give, the default button, the button labels, and how a message splits into its bold first line and the rest.
  /// </summary>
  public static class MessageRules {

    /// <summary>The buttons of the set, in order (the first one is the dark one).</summary>
    public static IReadOnlyList<MessageResult> ButtonsOf(MessageButtons buttons) {
      switch (buttons) {
        case MessageButtons.OKCancel: return new[] { MessageResult.OK, MessageResult.Cancel };
        case MessageButtons.YesNo: return new[] { MessageResult.Yes, MessageResult.No };
        case MessageButtons.YesNoCancel: return new[] { MessageResult.Yes, MessageResult.No, MessageResult.Cancel };
        default: return new[] { MessageResult.OK };
      }
    }

    /// <summary>The answer of Esc and the close button: Cancel, else No, else OK (OK alone).</summary>
    public static MessageResult CancelResultOf(MessageButtons buttons) {
      IReadOnlyList<MessageResult> all = ButtonsOf(buttons);
      if (all.Contains(MessageResult.Cancel)) {
        return MessageResult.Cancel;
      }
      return all.Contains(MessageResult.No) ? MessageResult.No : MessageResult.OK;
    }

    /// <summary>The default button: the asked one when the set has it, else the first.</summary>
    public static MessageResult DefaultOf(MessageButtons buttons, MessageResult requested) {
      IReadOnlyList<MessageResult> all = ButtonsOf(buttons);
      return all.Contains(requested) ? requested : all[0];
    }

    /// <summary>The text of the button.</summary>
    public static string Label(MessageResult result) {
      return result == MessageResult.None ? "" : result.ToString();
    }

    /// <summary>
    /// The first line is the bold heading; the rest is the text, without the blank lines between them. Nothing is dropped
    /// but those blank lines and the outer white space.
    /// </summary>
    public static (string Heading, string Text) Split(string message) {
      string all = (message ?? "").Replace("\r\n", "\n").Trim();
      int lineEnd = all.IndexOf('\n');
      if (lineEnd < 0) {
        return (all, "");
      }
      return (all.Substring(0, lineEnd).TrimEnd(), all.Substring(lineEnd + 1).TrimStart('\n').TrimEnd());
    }
  }
}
