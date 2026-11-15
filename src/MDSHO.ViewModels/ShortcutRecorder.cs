using CommunityToolkit.Mvvm.ComponentModel;
using MDSHO.Common;

namespace MDSHO.ViewModels {

  /// <summary>
  /// The shortcut box of "Bring panels to front" in the Settings window. Click the box to record a new shortcut; Default
  /// and Off set one directly. Nothing changes until <see cref="Apply"/>, like the other settings. While recording, the
  /// saved shortcut is suspended so its own keys can be recorded.
  /// </summary>
  public sealed class ShortcutRecorder : ObservableObject {
    /// <summary>The box while recording.</summary>
    public const string RecordingText = "Press the new shortcut…";
    /// <summary>The box when there is no shortcut.</summary>
    public const string OffText = "Off";
    /// <summary>The hint under a working shortcut.</summary>
    public const string NormalHint = "For all panels, on this PC. Press it again or Esc to send them back.";
    /// <summary>The hint while recording.</summary>
    public const string RecordingHint = "Use Ctrl, Alt or Win with a key. Esc cancels.";
    /// <summary>The red hint for a key without Ctrl, Alt or Win.</summary>
    public const string NeedsModifierHint = "Add Ctrl, Alt or Win to the key.";
    /// <summary>The hint when the shortcut is Off.</summary>
    public const string OffHint = "No shortcut. The tray menu item still works.";
    /// <summary>The red hint when another app holds the keys.</summary>
    public const string InUseHint = "Another app already uses this shortcut. Choose another one.";

    private const int EscapeKey = 0x1B;
    private readonly BringToFrontShortcut shortcut;
    private Hotkey pending;
    // The shortcut Windows refused (another app holds it); shown red while it is in the box.
    private Hotkey inUse;
    private bool needsModifier;
    private bool isRecording;

    /// <summary>Starts with the saved shortcut, red when it could not be registered.</summary>
    public ShortcutRecorder(BringToFrontShortcut shortcut) {
      this.shortcut = shortcut;
      this.pending = shortcut.Saved;
      this.inUse = shortcut.IsBlocked ? shortcut.Saved : null;
    }

    /// <summary>True while the box waits for the new shortcut.</summary>
    public bool IsRecording => isRecording;
    /// <summary>What the box shows.</summary>
    public string BoxText => isRecording ? RecordingText : pending == null ? OffText : pending.DisplayText;
    /// <summary>The line under the box.</summary>
    public string Hint {
      get {
        if (IsInUse) { return InUseHint; }
        if (isRecording) { return needsModifier ? NeedsModifierHint : RecordingHint; }
        return pending == null ? OffHint : NormalHint;
      }
    }
    /// <summary>True when the hint is a problem (shown red).</summary>
    public bool HintIsError => IsInUse || (isRecording && needsModifier);

    private bool IsInUse => !isRecording && pending != null && pending.Equals(inUse);

    /// <summary>Click on the box: waits for the new shortcut.</summary>
    public void StartRecording() {
      if (isRecording) {
        return;
      }
      needsModifier = false;
      isRecording = true;
      shortcut.Suspend();
      Refresh();
    }

    /// <summary>A key pressed in the box (Win32 virtual key). True = the box used it.</summary>
    public bool KeyPressed(HotkeyModifiers modifiers, int key) {
      if (!isRecording) {
        return false;
      }
      if (key == EscapeKey) {
        Cancel();
        return true;
      }
      switch (Hotkey.Check(modifiers, key)) {
        case HotkeyCheck.Valid:
          pending = new Hotkey(modifiers, key);
          StopRecording();
          break;
        case HotkeyCheck.NeedsModifier:
          needsModifier = true;
          Refresh();
          break;
      }
      return true;
    }

    /// <summary>Esc, a click elsewhere or closing the window: stops recording and keeps the shortcut the box had.</summary>
    public void Cancel() {
      if (isRecording) {
        StopRecording();
      }
    }

    /// <summary>Default: Ctrl+Shift+Alt+M.</summary>
    public void UseDefault() {
      SetPending(Hotkey.Default);
    }

    /// <summary>Off: no shortcut (the tray menu item still works).</summary>
    public void TurnOff() {
      SetPending(null);
    }

    /// <summary>
    /// Apply: registers and saves the box's shortcut. False (the window stays open) when another app holds a new
    /// shortcut; the old one then keeps working. An unchanged shortcut that is still blocked never keeps the window open.
    /// </summary>
    public bool Apply() {
      Cancel();
      if (shortcut.Apply(pending)) {
        return true;
      }
      inUse = pending;
      Refresh();
      return false;
    }

    private void SetPending(Hotkey hotkey) {
      pending = hotkey;
      if (isRecording) {
        StopRecording();
      } else {
        Refresh();
      }
    }

    private void StopRecording() {
      isRecording = false;
      needsModifier = false;
      shortcut.Resume();
      Refresh();
    }

    private void Refresh() {
      OnPropertyChanged(nameof(IsRecording));
      OnPropertyChanged(nameof(BoxText));
      OnPropertyChanged(nameof(Hint));
      OnPropertyChanged(nameof(HintIsError));
    }
  }
}
