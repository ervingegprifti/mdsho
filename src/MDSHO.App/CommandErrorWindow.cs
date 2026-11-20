using System;
using MDSHO.Common;

namespace MDSHO {

  /// <summary>
  /// The one MDSHO dialog with a warning icon, an error as the bold first line, a line below it and OK, e.g. when a
  /// shortcut could not open or a run command did not run.
  /// </summary>
  public class CommandErrorWindow : MessageWindow {

    /// <summary>Shows the built window (modal). Tests replace it so no window opens.</summary>
    public static Action<CommandErrorWindow> ShowWindow = window => window.ShowDialog();

    /// <summary>Creates the window.</summary>
    /// <param name="title">The title, e.g. the command's name.</param>
    /// <param name="error">The error, the bold first line.</param>
    /// <param name="detail">The line below the error.</param>
    /// <param name="solidIcons">The icon set of the panel the button is on.</param>
    public CommandErrorWindow(string title, string error, string detail, bool solidIcons = true)
        : base(title, error, detail, MessageIcon.Warning, MessageButtons.OK, MessageResult.OK, solidIcons) {
    }
  }
}
