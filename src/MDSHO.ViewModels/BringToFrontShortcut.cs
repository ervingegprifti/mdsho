using System;
using MDSHO.Common;
using MDSHO.Data;

namespace MDSHO.ViewModels {

  /// <summary>
  /// The "Bring panels to front" keyboard shortcut: one for the whole app, saved on this PC in the AppSettings table (not
  /// exported). Without a saved value it is <see cref="Hotkey.Default"/>. It is registered as a global hotkey through
  /// <see cref="IGlobalHotkeyService"/>; when another app holds the keys it stays unregistered (<see cref="IsBlocked"/>),
  /// without an error, and the Settings window says so.
  /// </summary>
  public sealed class BringToFrontShortcut : IDisposable {
    /// <summary>The key of the setting in the AppSettings table.</summary>
    public const string SettingKey = "BringToFrontShortcut";

    private readonly IGlobalHotkeyService service;
    private bool registered;
    private bool blocked;
    private bool suspended;

    /// <summary>Uses this hotkey service; nothing is registered until <see cref="Start"/>.</summary>
    public BringToFrontShortcut(IGlobalHotkeyService service) {
      this.service = service ?? throw new ArgumentNullException(nameof(service));
      this.service.Pressed += OnPressed;
    }

    /// <summary>The saved shortcut; null = Off.</summary>
    public Hotkey Saved { get; private set; } = Hotkey.Default;
    /// <summary>True while the saved shortcut is registered with Windows.</summary>
    public bool IsRegistered => registered;
    /// <summary>True when Windows refused the saved shortcut, usually because another app holds the keys.</summary>
    public bool IsBlocked => Saved != null && blocked;
    /// <summary>The shortcut for the tray menu: empty when Off or when it doesn't work.</summary>
    public string MenuText => Saved != null && !blocked ? Saved.MenuText : "";

    /// <summary>Raised when the saved keys are pressed.</summary>
    public event Action Pressed;
    /// <summary>Raised when the saved shortcut or whether it works changed (the tray menu shows it).</summary>
    public event Action Changed;

    /// <summary>At startup: reads the saved shortcut and registers it. Never throws and never asks anything.</summary>
    public void Start() {
      string text = null;
      try {
        text = DataStore.AppSettings.Get(SettingKey);
      } catch (Exception ex) {
        AppLog.Warning(ex, "The Bring panels to front shortcut could not be read; the default is used");
      }
      if (text == null) {
        Saved = Hotkey.Default;
      } else if (Hotkey.TryParseSetting(text, out Hotkey saved)) {
        Saved = saved;
      } else {
        AppLog.Warning(null, "The saved Bring panels to front shortcut {Setting} is not valid; the default is used", text);
        Saved = Hotkey.Default;
      }
      RegisterSaved();
      if (IsBlocked) {
        AppLog.Information("The Bring panels to front shortcut {Shortcut} is used by another app", Saved.MenuText);
      }
    }

    /// <summary>
    /// Settings, Apply: registers and saves <paramref name="hotkey"/> (null = Off). False when Windows refuses the new keys;
    /// then the old shortcut stays saved and working. Applying the saved shortcut again tries again to register it when
    /// it was blocked.
    /// </summary>
    public bool Apply(Hotkey hotkey) {
      if (Equals(hotkey, Saved)) {
        if (hotkey != null && !registered) {
          RegisterSaved();
        }
        return true;
      }
      if (hotkey == null) {
        service.Unregister();
        registered = false;
        blocked = false;
      } else if (TryRegister(hotkey)) {
        registered = true;
        blocked = false;
      } else {
        // Nothing is registered after a refused Register: the old keys come back.
        registered = Saved != null && !blocked && TryRegister(Saved);
        return false;
      }
      Saved = hotkey;
      DataStore.AppSettings.Set(SettingKey, Hotkey.ToSetting(hotkey));
      AppLog.Information("Bring panels to front shortcut set to {Shortcut}", Hotkey.ToSetting(hotkey));
      Changed?.Invoke();
      return true;
    }

    /// <summary>While the Settings box records a new shortcut: the saved keys reach the box instead of bringing the panels up.</summary>
    public void Suspend() {
      suspended = true;
      if (registered) {
        service.Unregister();
        registered = false;
      }
    }

    /// <summary>Recording ended: the saved shortcut works again.</summary>
    public void Resume() {
      if (!suspended) {
        return;
      }
      suspended = false;
      RegisterSaved();
    }

    /// <summary>Releases the keys (at exit).</summary>
    public void Dispose() {
      service.Pressed -= OnPressed;
      registered = false;
      service.Dispose();
    }

    private void RegisterSaved() {
      if (Saved == null) {
        if (registered) {
          service.Unregister();
        }
        registered = false;
        blocked = false;
      } else {
        registered = TryRegister(Saved);
        blocked = !registered;
      }
      Changed?.Invoke();
    }

    private bool TryRegister(Hotkey hotkey) {
      try {
        return service.Register(hotkey);
      } catch (Exception ex) {
        AppLog.Warning(ex, "The Bring panels to front shortcut {Shortcut} could not be registered", hotkey.MenuText);
        return false;
      }
    }

    private void OnPressed() {
      Pressed?.Invoke();
    }
  }
}
