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
  }
}
