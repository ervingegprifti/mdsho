using System;
using System.Windows;

namespace MDSHO {

  /// <summary>
  /// The error window: a short plain-English message and the exception's message. The full details (stack trace, inner
  /// exceptions) are not shown; Copy details puts them on the clipboard and Open logs folder shows the log files.
  /// </summary>
  public partial class ErrorWindow : Window {

    /// <summary>The message shown when the caller gives none.</summary>
    public const string DefaultMessage = "Something went wrong.";

    /// <summary>Puts text on the clipboard. Tests replace it so the real clipboard is never touched.</summary>
    public static Action<string> CopyText = text => Helpers.AppServices.Current.Clipboard.SetText(text);

    /// <summary>Opens the logs folder. Tests replace it so Explorer never opens.</summary>
    public static Action OpenLogs = () => Infrastructure.Logging.AppLogging.OpenLogsFolder(Helpers.AppServices.Current.AppData, Helpers.AppServices.Current.Shell);

    /// <summary>The full text Copy details copies: the message, the app version and the whole exception.</summary>
    public string Details { get; }

    /// <summary>Creates the error window with the default message.</summary>
    public ErrorWindow(Exception ex) : this("", ex) { }

    /// <summary>Creates the error window; an empty <paramref name="message"/> shows "Something went wrong.".</summary>
    public ErrorWindow(string message, Exception ex) {
      InitializeComponent();
#if DEBUG
      this.Title += " DEBUG";
#endif
      string shown = string.IsNullOrWhiteSpace(message) ? DefaultMessage : message;
      this.TextMessage.Text = shown;
      this.TextDetail.Text = ex?.Message ?? "";
      this.Details = shown + Environment.NewLine + Environment.NewLine
        + "MDSHO " + typeof(ErrorWindow).Assembly.GetName().Version + Environment.NewLine
        + (ex?.ToString() ?? "");

      this.ButtonCopyDetails.Click += (s, e) => Run(() => CopyText(this.Details));
      this.ButtonOpenLogs.Click += (s, e) => Run(OpenLogs);
      this.ButtonOK.Click += (s, e) => Close();
      // The title bar shows the warning icon; the taskbar keeps the MDSHO logo.
      ViewModels.IconGlyphs icons = new ViewModels.IconGlyphs(true);
      this.TitleBar.Glyph = icons.Warning;
      this.TitleBar.CloseGlyph = icons.Times;
    }

    /// <summary>A failing button must not open another error window from inside this one.</summary>
    private static void Run(Action action) {
      try {
        action();
      } catch (Exception ex) {
        Common.AppLog.Warning(ex, "An error window button failed");
      }
    }
  }
}
