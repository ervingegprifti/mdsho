using System;
using System.Windows;
using System.Windows.Threading;
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
    /// <remarks>
    /// The handlers can run on any thread (the finalizer thread for a lost task, a pool thread for a crash there), and a
    /// WPF window can only be made on the UI thread: the window is sent there. This never throws.
    /// </remarks>
    public static void ShowUnhandled(string source, Exception ex) {
      AppLog.Error(ex, "Unhandled exception caught by {Source}", source);
      void Show() {
        try {
          ShowWindow(new ErrorWindow(ex));
        } catch (Exception windowError) {
          AppLog.Error(windowError, "The error window could not be shown");
        }
      }
      try {
        Dispatcher dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.HasShutdownStarted) {
          return; // No UI thread any more: the log line is all we can do.
        }
        if (dispatcher.CheckAccess()) {
          Show();
        } else {
          dispatcher.BeginInvoke((Action)Show);
        }
      } catch (Exception dispatchError) {
        AppLog.Error(dispatchError, "The error window could not be shown");
      }
    }
  }

}
