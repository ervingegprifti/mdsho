using CommunityToolkit.Mvvm.ComponentModel;
using MDSHO.Common;

namespace MDSHO.ViewModels {

  /// <summary>The shortcut box in the Settings window. Nothing is saved until <see cref="Apply"/>.</summary>
  public sealed class ShortcutRecorder : ObservableObject {
    public const string RecordingText = "Press the new shortcut…";
    public const string OffText = "Off";
    public const string NormalHint = "For all panels, on this PC. Press it again or Esc to send them back.";
    public const string RecordingHint = "Use Ctrl, Alt or Win with a key. Esc cancels.";
    public const string NeedsModifierHint = "Add Ctrl, Alt or Win to the key.";
    public const string OffHint = "No shortcut. The tray menu item still works.";
    public const string InUseHint = "Another app already uses this shortcut. Choose another one.";

    private const int EscapeKey = 0x1B;
    private readonly BringToFrontShortcut shortcut;
    private Hotkey pending;
    // The shortcut Windows refused. Shown red while it is in the box.
    private Hotkey inUse;
    private bool needsModifier;
    private bool isRecording;

    public ShortcutRecorder(BringToFrontShortcut shortcut) {
      this.shortcut = shortcut;
      this.pending = shortcut.Saved;
      this.inUse = shortcut.IsBlocked ? shortcut.Saved : null;
    }

    public bool IsRecording => isRecording;
    public string BoxText => isRecording ? RecordingText : pending == null ? OffText : pending.DisplayText;
    public string Hint {
      get {
        if (IsInUse) { return InUseHint; }
        if (isRecording) { return needsModifier ? NeedsModifierHint : RecordingHint; }
        return pending == null ? OffHint : NormalHint;
      }
    }
    /// <summary>True when the hint should be red.</summary>
    public bool HintIsError => IsInUse || (isRecording && needsModifier);

    private bool IsInUse => !isRecording && pending != null && pending.Equals(inUse);

    /// <summary>Waits for the new shortcut.</summary>
    public void StartRecording() {
      if (isRecording) {
        return;
      }
      needsModifier = false;
      isRecording = true;
      shortcut.Suspend();
      Refresh();
    }

    /// <summary>A key pressed in the box. True when the box used it.</summary>
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

    /// <summary>Stops recording without a change.</summary>
    public void Cancel() {
      if (isRecording) {
        StopRecording();
      }
    }

    public void UseDefault() {
      SetPending(Hotkey.Default);
    }

    public void TurnOff() {
      SetPending(null);
    }

    /// <summary>Saves the box's shortcut. False when another app holds it; the window then stays open.</summary>
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
