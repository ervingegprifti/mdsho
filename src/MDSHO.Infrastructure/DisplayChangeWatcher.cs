using System;
using Microsoft.Win32;
using MDSHO.Common;

namespace MDSHO.Infrastructure {

  /// <summary>
  /// Tells when screens are connected or disconnected or a resolution changes, through
  /// <see cref="SystemEvents.DisplaySettingsChanged"/> (Windows' WM_DISPLAYCHANGE). The event can come on another thread.
  /// https://learn.microsoft.com/en-us/dotnet/api/microsoft.win32.systemevents.displaysettingschanged
  /// </summary>
  public sealed class DisplayChangeWatcher : IDisplayChangeWatcher {
    private bool started;

    /// <summary>Raised after the display settings changed, possibly on another thread.</summary>
    public event Action Changed;

    /// <summary>Starts listening.</summary>
    public void Start() {
      if (this.started) {
        return;
      }
      this.started = true;
      SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
    }

    /// <summary>Stops listening (at exit).</summary>
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
