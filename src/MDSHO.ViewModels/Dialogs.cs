using System;

namespace MDSHO.ViewModels {

  /// <summary>
  /// Minimal seam for user prompts raised from view models. The app sets MDSHO dialogs at startup; tests replace them.
  /// Until set, they throw instead of showing nothing.
  /// </summary>
  public static class Dialogs {
    [ThreadStatic]
    private static bool? solidIcons;

    /// <summary>The icon set for the prompts raised now: the panel's (see <see cref="UseIcons"/>), solid when none set it.</summary>
    public static bool SolidIcons => solidIcons ?? true;

    /// <summary>
    /// Prompts raised until the result is disposed use the icon set of the panel that raises them (its Solid icons
    /// setting). Scopes nest; disposing one puts the previous set back.
    /// </summary>
    public static IDisposable UseIcons(bool solid) {
      bool? previous = solidIcons;
      solidIcons = solid;
      return new Scope(() => solidIcons = previous);
    }

    private sealed class Scope : IDisposable {
      private Action end;
      public Scope(Action end) { this.end = end; }
      public void Dispose() {
        end?.Invoke();
        end = null;
      }
    }
    /// <summary>(message, caption) => true when the user answered Yes (a Yes/No warning with No as the default in the app).</summary>
    public static Func<string, string, bool> Confirm = (message, caption) =>
      throw new InvalidOperationException("Dialogs.Confirm is not set.");
    /// <summary>(message, caption) shows a problem to the user (an OK warning in the app).</summary>
    public static Action<string, string> ShowMessage = (message, caption) =>
      throw new InvalidOperationException("Dialogs.ShowMessage is not set.");
    /// <summary>(caption, error, detail) shows an error as a bold first line with the detail below it and OK.</summary>
    public static Action<string, string, string> ShowError = (caption, error, detail) =>
      throw new InvalidOperationException("Dialogs.ShowError is not set.");
    /// <summary>(caption, heading, text) shows information: a bold heading, then the text, with OK.</summary>
    public static Action<string, string, string> ShowInfo = (caption, heading, text) =>
      throw new InvalidOperationException("Dialogs.ShowInfo is not set.");
  }
}
