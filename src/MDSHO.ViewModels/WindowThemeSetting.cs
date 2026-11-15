using System;
using MDSHO.Common;
using MDSHO.Data;

namespace MDSHO.ViewModels {

  /// <summary>The "Window theme" choice in Settings.</summary>
  public enum WindowThemeChoice { SameAsWindows, Light, Dark }

  /// <summary>The theme MDSHO's own windows and menus use.</summary>
  public enum WindowThemeKind { Light, Dark }

  /// <summary>
  /// "Window theme" for MDSHO's own windows and menus (never panels or notes). App-wide, saved in AppSettings, not
  /// exported. "Same as Windows" follows Windows' app mode, also when it changes.
  /// </summary>
  public sealed class WindowThemeSetting {
    /// <summary>The AppSettings key: "SameAsWindows", "Light" or "Dark". No row means Same as Windows.</summary>
    public const string SettingKey = "WindowTheme";

    private readonly IWindowsAppMode windows;
    private WindowThemeChoice? choice;
    private WindowThemeChoice? preview;
    private bool started;

    public WindowThemeSetting(IWindowsAppMode windows) {
      this.windows = windows ?? throw new ArgumentNullException(nameof(windows));
    }

    /// <summary>The saved choice. Same as Windows unless set. Never throws.</summary>
    public WindowThemeChoice Choice {
      get {
        if (this.choice == null) {
          string text = null;
          try {
            text = DataStore.AppSettings.Get(SettingKey);
          } catch (Exception ex) {
            AppLog.Warning(ex, "The window theme setting could not be read; it is Same as Windows");
          }
          this.choice = Parse(text);
        }
        return this.choice.Value;
      }
    }

    /// <summary>The theme in use now.</summary>
    public WindowThemeKind Current => Resolve(Shown, this.windows.AppsUseLightTheme());

    // The previewed choice, else the saved one.
    private WindowThemeChoice Shown => this.preview ?? Choice;

    /// <summary>Raised when the theme may have changed: the choice changed, or Windows did while Same as Windows.</summary>
    public event Action Changed;

    /// <summary>Saves the choice.</summary>
    public void SetChoice(WindowThemeChoice value) {
      DataStore.AppSettings.Set(SettingKey, value.ToString());
      this.choice = value;
      this.preview = null;
      AppLog.Information("Window theme set to {Choice}", value);
      Changed?.Invoke();
    }

    /// <summary>Shows a choice at once without saving it, while Settings is open; null goes back to the saved one.</summary>
    public void Preview(WindowThemeChoice? value) {
      this.preview = value;
      Changed?.Invoke();
    }

    /// <summary>Starts following Windows' app mode.</summary>
    public void Start() {
      if (this.started) {
        return;
      }
      this.started = true;
      this.windows.Changed += WindowsChanged;
      this.windows.Start();
    }

    private void WindowsChanged() {
      if (Shown == WindowThemeChoice.SameAsWindows) {
        Changed?.Invoke();
      }
    }

    /// <summary>The choice in a stored value; anything unknown means Same as Windows.</summary>
    public static WindowThemeChoice Parse(string text) {
      return Enum.TryParse(text?.Trim(), true, out WindowThemeChoice parsed) && Enum.IsDefined(parsed) ? parsed : WindowThemeChoice.SameAsWindows;
    }

    /// <summary>The theme for a choice, given whether Windows apps use the light mode.</summary>
    public static WindowThemeKind Resolve(WindowThemeChoice choice, bool windowsLight) {
      switch (choice) {
        case WindowThemeChoice.Light:
          return WindowThemeKind.Light;
        case WindowThemeChoice.Dark:
          return WindowThemeKind.Dark;
        default:
          return windowsLight ? WindowThemeKind.Light : WindowThemeKind.Dark;
      }
    }
  }
}
