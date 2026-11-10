using System;
using MDSHO.Common;

namespace MDSHO {

  /// <summary>Shows the modal error window for an unexpected exception and writes it to the log first.</summary>
  public static class ErrorDialog {
    /// <summary>Shows the built window (modal). Tests replace it so no window opens.</summary>
    public static Action<ErrorWindow> ShowWindow = window => window.ShowDialog();

    /// <summary>Logs and shows an exception with the default message.</summary>
    public static void ShowDialog(Exception ex) {
      AppLog.Error(ex, "Error shown: {Message}", ErrorWindow.DefaultMessage);
      ShowWindow(new ErrorWindow(ex));
    }

    /// <summary>Logs and shows an exception with a plain-English message.</summary>
    public static void ShowDialog(string message, Exception ex) {
      AppLog.Error(ex, "Error shown: {Message}", string.IsNullOrEmpty(message) ? ErrorWindow.DefaultMessage : message);
      ShowWindow(new ErrorWindow(message, ex));
    }

    /// <summary>Logs and shows an exception nobody caught; <paramref name="source"/> says which handler caught it.</summary>
    public static void ShowUnhandled(string source, Exception ex) {
      AppLog.Error(ex, "Unhandled exception caught by {Source}", source);
      ShowWindow(new ErrorWindow(ex));
    }
  }

}
