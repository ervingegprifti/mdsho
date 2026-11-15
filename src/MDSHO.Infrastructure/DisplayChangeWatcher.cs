using System;
using Microsoft.Win32;
using MDSHO.Common;

namespace MDSHO.Infrastructure {

  /// <summary>Tells when screens are connected or disconnected, or a resolution changes.</summary>
  // https://learn.microsoft.com/en-us/dotnet/api/microsoft.win32.systemevents.displaysettingschanged
  public sealed class DisplayChangeWatcher : IDisplayChangeWatcher {
    private bool started;

    /// <summary>Raised after a change. It can come on another thread.</summary>
    public event Action Changed;

    /// <summary>Starts listening.</summary>
    public void Start() {
      if (this.started) {
        return;
      }
      this.started = true;
      SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
    }

    /// <summary>Stops listening.</summary>
    public void Dispose() {
      if (!this.started) {
        return;
      }
      this.started = false;
      SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
    }

    private void OnDisplaySettingsChanged(object sender, EventArgs e) {
      Changed?.Invoke();
    }
  }
}
