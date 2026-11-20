using System;
using MDSHO.Common;

namespace MDSHO {

  /// <summary>
  /// The error window, built on the one MDSHO dialog: a warning icon, a short plain-English message as the bold first
  /// line and the exception's message below it. The full details (stack trace, inner exceptions) are not shown; Copy
  /// details puts them on the clipboard and Open logs folder shows the log files.
  /// </summary>
  public class ErrorWindow : MessageWindow {

    /// <summary>The message shown when the caller gives none.</summary>
    public const string DefaultMessage = "Something went wrong.";

    /// <summary>The line below the exception's message.</summary>
    public const string LogNote = "The details were saved in the log file. Use Copy details to include them in a bug report.";

    /// <summary>Puts text on the clipboard. Tests replace it so the real clipboard is never touched.</summary>
    public static Action<string> CopyText = text => Helpers.AppServices.Current.Clipboard.SetText(text);

    /// <summary>Opens the logs folder. Tests replace it so Explorer never opens.</summary>
    public static Action OpenLogs = () => Infrastructure.Logging.AppLogging.OpenLogsFolder(Helpers.AppServices.Current.AppData, Helpers.AppServices.Current.Shell);

    /// <summary>The full text Copy details copies: the message, the app version and the whole exception.</summary>
    public string Details { get; }

    /// <summary>Creates the error window with the default message.</summary>
    public ErrorWindow(Exception ex) : this("", ex) { }

    /// <summary>Creates the error window; an empty <paramref name="message"/> shows "Something went wrong.".</summary>
    public ErrorWindow(string message, Exception ex)
        : base("MDSHO error", Shown(message), ex?.Message ?? "", MessageIcon.Warning, MessageButtons.OK, MessageResult.OK) {
      this.Width = 520;
      this.Note = LogNote;
      this.Details = Shown(message) + Environment.NewLine + Environment.NewLine
        + "MDSHO " + typeof(ErrorWindow).Assembly.GetName().Version + Environment.NewLine
        + (ex?.ToString() ?? "");
      AddExtraButton("ButtonCopyDetails", "Copy details", () => Run(() => CopyText(this.Details)));
      AddExtraButton("ButtonOpenLogs", "Open logs folder", () => Run(OpenLogs));
    }

    private static string Shown(string message) {
      return string.IsNullOrWhiteSpace(message) ? DefaultMessage : message;
    }

    /// <summary>A failing button must not open another error window from inside this one.</summary>
    private static void Run(Action action) {
      try {
        action();
      } catch (Exception ex) {
        AppLog.Warning(ex, "An error window button failed");
      }
    }
  }
}
