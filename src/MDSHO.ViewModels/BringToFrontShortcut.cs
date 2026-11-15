using System;
using MDSHO.Common;
using MDSHO.Data;

namespace MDSHO.ViewModels {

  /// <summary>
  /// The saved "Bring panels to front" shortcut, registered as a global hotkey.
  /// If another app holds the keys, it stays unregistered without an error.
  /// </summary>
  public sealed class BringToFrontShortcut : IDisposable {
    /// <summary>The AppSettings key.</summary>
    public const string SettingKey = "BringToFrontShortcut";

    private readonly IGlobalHotkeyService service;
    private bool registered;
    private bool blocked;
    private bool suspended;

    public BringToFrontShortcut(IGlobalHotkeyService service) {
      this.service = service ?? throw new ArgumentNullException(nameof(service));
      this.service.Pressed += OnPressed;
    }

    /// <summary>The saved shortcut. Null means Off.</summary>
    public Hotkey Saved { get; private set; } = Hotkey.Default;
    /// <summary>True while the shortcut is registered.</summary>
    public bool IsRegistered => registered;
    /// <summary>True when Windows refused the shortcut.</summary>
    public bool IsBlocked => Saved != null && blocked;
    /// <summary>The tray menu text. Empty when Off or blocked.</summary>
    public string MenuText => Saved != null && !blocked ? Saved.MenuText : "";

    /// <summary>Raised when the keys are pressed.</summary>
    public event Action Pressed;
    /// <summary>Raised when the shortcut or its state changes.</summary>
    public event Action Changed;

    /// <summary>At startup: reads and registers the shortcut. Never throws.</summary>
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

    /// <summary>Registers and saves the shortcut. Null means Off. False when Windows refuses; the old one keeps working.</summary>
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
        // A refused Register leaves nothing registered, so register the old keys again.
        registered = Saved != null && !blocked && TryRegister(Saved);
        return false;
      }
      Saved = hotkey;
      DataStore.AppSettings.Set(SettingKey, Hotkey.ToSetting(hotkey));
      AppLog.Information("Bring panels to front shortcut set to {Shortcut}", Hotkey.ToSetting(hotkey));
      Changed?.Invoke();
      return true;
    }

    /// <summary>Unregisters the keys while the Settings box records, so the box gets them.</summary>
    public void Suspend() {
      suspended = true;
      if (registered) {
        service.Unregister();
        registered = false;
      }
    }

    /// <summary>Registers the keys again after recording.</summary>
    public void Resume() {
      if (!suspended) {
        return;
      }
      suspended = false;
      RegisterSaved();
    }

    /// <summary>Releases the keys.</summary>
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
