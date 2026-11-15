using System;

namespace MDSHO.ViewModels {

  /// <summary>
  /// Minimal seam for user prompts raised from view models. The app sets message boxes at startup; tests replace them.
  /// Until set, they throw instead of showing nothing.
  /// </summary>
  public static class Dialogs {
    /// <summary>(message, caption) => true when the user answered Yes (a Yes/No warning with No as the default in the app).</summary>
    public static Func<string, string, bool> Confirm = (message, caption) =>
      throw new InvalidOperationException("Dialogs.Confirm is not set.");
    /// <summary>(message, caption) shows a problem to the user (an OK warning in the app).</summary>
    public static Action<string, string> ShowMessage = (message, caption) =>
      throw new InvalidOperationException("Dialogs.ShowMessage is not set.");
    /// <summary>(caption, error, detail) shows an error in red with a line below it and OK (a small MDSHO window in the app).</summary>
    public static Action<string, string, string> ShowError = (caption, error, detail) =>
      throw new InvalidOperationException("Dialogs.ShowError is not set.");
  }
}
