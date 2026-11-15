using System;
using Microsoft.Win32;
using MDSHO.Common;

namespace MDSHO.Infrastructure {

  /// <summary>
  /// Windows' app mode (light or dark), from HKCU\...\Themes\Personalize\AppsUseLightTheme. Tells when it changes
  /// (Windows sends a user preference change).
  /// </summary>
  // https://learn.microsoft.com/en-us/dotnet/api/microsoft.win32.systemevents.userpreferencechanged
  public sealed class WindowsAppMode : IWindowsAppMode {
    /// <summary>The registry key under HKEY_CURRENT_USER.</summary>
    public const string RegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    /// <summary>The registry value: 0 = dark, 1 = light.</summary>
    public const string RegistryValue = "AppsUseLightTheme";

    private readonly Func<object> readValue;
    private bool? last;
    private bool started;

    /// <summary>Reads the real registry.</summary>
    public WindowsAppMode() : this(ReadRegistry) { }

    /// <summary>Reads the value with <paramref name="readValue"/> (tests give a fake one).</summary>
    public WindowsAppMode(Func<object> readValue) {
      this.readValue = readValue ?? throw new ArgumentNullException(nameof(readValue));
    }

    /// <summary>Raised after the mode changed. It can come on another thread.</summary>
    public event Action Changed;

    /// <summary>0 means dark; anything else, or no value (older Windows), means light.</summary>
    public static bool IsLight(object value) {
      return !(value is int number && number == 0);
    }

    /// <summary>True when Windows apps use the light mode. Never throws: a failed read means light.</summary>
    public bool AppsUseLightTheme() {
      object value;
      try {
        value = this.readValue();
      } catch (Exception ex) {
        AppLog.Warning(ex, "Windows' app mode could not be read; using light");
        value = null;
      }
      bool light = IsLight(value);
      this.last = light;
      return light;
    }

    /// <summary>Starts listening.</summary>
    public void Start() {
      if (this.started) {
        return;
      }
      this.started = true;
      AppsUseLightTheme();
      SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    /// <summary>Reads the mode again and raises <see cref="Changed"/> if it differs from the last read.</summary>
    public void CheckForChange() {
      bool? before = this.last;
      bool now = AppsUseLightTheme();
      if (before != null && before != now) {
        AppLog.Information("Windows' app mode changed to {Mode}", now ? "light" : "dark");
        Changed?.Invoke();
      }
    }

    /// <summary>Stops listening.</summary>
    public void Dispose() {
      if (!this.started) {
        return;
      }
      this.started = false;
      SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
    }

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e) {
      // The app mode comes as a General change (with "ImmersiveColorSet").
      if (e.Category == UserPreferenceCategory.General || e.Category == UserPreferenceCategory.Color) {
        CheckForChange();
      }
    }

    private static object ReadRegistry() {
      using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryKey)) {
        return key?.GetValue(RegistryValue);
      }
    }
  }
}
