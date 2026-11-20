using System;
using System.Threading.Tasks;

namespace MDSHO.Common {

  /// <summary>What a snippet's paste did.</summary>
  public enum PasteOutcome {
    /// <summary>The value was pasted.</summary>
    Pasted,
    /// <summary>No window to paste into.</summary>
    NoTarget,
    /// <summary>The window runs as administrator and MDSHO doesn't.</summary>
    Elevated,
    /// <summary>The focused part of the window is clearly not a text field.</summary>
    NotEditable,
    /// <summary>The clipboard could not be saved or set.</summary>
    ClipboardBusy,
    /// <summary>Another paste is still running.</summary>
    AlreadyPasting
  }

  /// <summary>Whether the focused part of a window takes typed text.</summary>
  public enum FieldCheck {
    /// <summary>A text field.</summary>
    Editable,
    /// <summary>Clearly not a text field.</summary>
    NotEditable,
    /// <summary>Can't tell.</summary>
    Unknown
  }

  /// <summary>The kind of the focused UI Automation element.</summary>
  public enum FocusedControlKind {
    /// <summary>An edit box.</summary>
    Edit,
    /// <summary>A document.</summary>
    Document,
    /// <summary>A window, pane, group, custom or unknown control: can hold anything.</summary>
    Container,
    /// <summary>A control that is not for text (button, list, tree, menu...).</summary>
    Other
  }

  /// <summary>The window the user was in before MDSHO.</summary>
  public interface IForegroundWindows {
    /// <summary>The last foreground window that isn't MDSHO; zero when none.</summary>
    IntPtr LastOtherWindow { get; }
    /// <summary>Gives the window the focus; true when it is the foreground window then.</summary>
    bool Activate(IntPtr window);
  }

  /// <summary>Tells when Windows blocks input to a window because it runs at a higher level (UIPI).</summary>
  public interface IElevationCheck {
    /// <summary>True when the window's process runs above MDSHO (as administrator while MDSHO doesn't).</summary>
    bool IsBlockedByElevation(IntPtr window);
  }

  /// <summary>Looks at the focused part of a window.</summary>
  public interface IFocusedFieldCheck {
    /// <summary>Whether the focused element takes text. Never throws: Unknown when it can't tell.</summary>
    Task<FieldCheck> CheckAsync(IntPtr window);
  }

  /// <summary>The clipboard, as the paste needs it. Never logs or shows its content.</summary>
  public interface IPasteClipboard {
    /// <summary>Windows' clipboard sequence number; it changes with every change.</summary>
    uint SequenceNumber { get; }
    /// <summary>True when the clipboard has nothing on it.</summary>
    bool IsEmpty();
    /// <summary>True when Windows clipboard history is on.</summary>
    bool IsHistoryEnabled();
    /// <summary>The newest clipboard history item (the current content), or null.</summary>
    Task<object> CurrentHistoryItemAsync();
    /// <summary>A copy in memory of every format it can copy, or null when that failed.</summary>
    object CopyAll();
    /// <summary>Puts text on the clipboard, kept out of clipboard history and cloud sync. False when it failed.</summary>
    bool SetPrivateText(string text);
    /// <summary>Makes a history item the current content again. False when it failed.</summary>
    Task<bool> RestoreHistoryItemAsync(object item);
    /// <summary>Puts back a copy made by <see cref="CopyAll"/>. False when it failed.</summary>
    bool RestoreCopy(object copy);
    /// <summary>Empties the clipboard. False when it failed.</summary>
    bool Clear();
  }

  /// <summary>Sends keys to the foreground window.</summary>
  public interface IKeySender {
    /// <summary>Sends Ctrl+V.</summary>
    void SendPaste();
  }

  /// <summary>Waits.</summary>
  public interface IPasteDelay {
    /// <summary>A task that ends after the time.</summary>
    Task Delay(TimeSpan time);
  }

  /// <summary>Decides from UI Automation facts whether the focused element takes text.</summary>
  public static class FocusedFieldRules {
    /// <summary>
    /// A Value pattern decides when there is one (read-only = not a text field). Otherwise a Text pattern, an edit box or
    /// a document counts as a text field. A container can't tell; any other control is clearly not a text field.
    /// </summary>
    public static FieldCheck Decide(FocusedControlKind kind, bool hasValuePattern, bool valueIsReadOnly, bool hasTextPattern) {
      if (hasValuePattern) {
        return valueIsReadOnly ? FieldCheck.NotEditable : FieldCheck.Editable;
      }
      if (hasTextPattern || kind == FocusedControlKind.Edit || kind == FocusedControlKind.Document) {
        return FieldCheck.Editable;
      }
      return kind == FocusedControlKind.Container ? FieldCheck.Unknown : FieldCheck.NotEditable;
    }
  }

  /// <summary>
  /// Pastes a snippet's value into the window the user came from: gives that window the focus back, checks the
  /// focused field, saves the user's clipboard, puts the value on it (kept out of history and cloud sync), sends Ctrl+V,
  /// waits, then puts the user's clipboard back unless the user copied something newer. Never logs the value or the
  /// clipboard content.
  /// </summary>
  public sealed class ClipboardPaster {
    /// <summary>The wait after giving the window the focus back, so the focus has arrived before the check.</summary>
    public static readonly TimeSpan FocusWait = TimeSpan.FromMilliseconds(100);
    /// <summary>The wait after Ctrl+V before the clipboard is put back, so the window has read the value.</summary>
    public static readonly TimeSpan RestoreWait = TimeSpan.FromMilliseconds(500);

    private readonly IForegroundWindows windows;
    private readonly IElevationCheck elevation;
    private readonly IFocusedFieldCheck fields;
    private readonly IPasteClipboard clipboard;
    private readonly IKeySender keys;
    private readonly IPasteDelay delay;
    private bool pasting;

    /// <summary>Creates the paster from its Windows parts.</summary>
    public ClipboardPaster(IForegroundWindows windows, IElevationCheck elevation, IFocusedFieldCheck fields, IPasteClipboard clipboard, IKeySender keys, IPasteDelay delay) {
      this.windows = windows ?? throw new ArgumentNullException(nameof(windows));
      this.elevation = elevation ?? throw new ArgumentNullException(nameof(elevation));
      this.fields = fields ?? throw new ArgumentNullException(nameof(fields));
      this.clipboard = clipboard ?? throw new ArgumentNullException(nameof(clipboard));
      this.keys = keys ?? throw new ArgumentNullException(nameof(keys));
      this.delay = delay ?? throw new ArgumentNullException(nameof(delay));
    }

    /// <summary>True while a paste runs; another paste is ignored then, so our value is never saved as the user's.</summary>
    public bool IsPasting => this.pasting;

    /// <summary>Pastes the value. Call it on the UI thread.</summary>
    public async Task<PasteOutcome> PasteAsync(string value) {
      if (this.pasting) {
        return PasteOutcome.AlreadyPasting;
      }
      this.pasting = true;
      try {
        return await PasteNowAsync(value ?? "");
      } finally {
        this.pasting = false;
      }
    }

    private async Task<PasteOutcome> PasteNowAsync(string value) {
      IntPtr target = this.windows.LastOtherWindow;
      if (target == IntPtr.Zero) {
        return PasteOutcome.NoTarget;
      }
      // Windows drops input sent to a window that runs above us (UIPI): don't touch anything.
      if (this.elevation.IsBlockedByElevation(target)) {
        return PasteOutcome.Elevated;
      }
      if (!this.windows.Activate(target)) {
        return PasteOutcome.NoTarget;
      }
      await this.delay.Delay(FocusWait);
      if (await this.fields.CheckAsync(target) == FieldCheck.NotEditable) {
        return PasteOutcome.NotEditable;
      }

      // Save the user's clipboard: the history item when history is on (nothing is read), else a copy in memory.
      bool wasEmpty = this.clipboard.IsEmpty();
      object historyItem = null;
      object copy = null;
      if (!wasEmpty) {
        if (this.clipboard.IsHistoryEnabled()) {
          historyItem = await this.clipboard.CurrentHistoryItemAsync();
        }
        if (historyItem == null) {
          copy = this.clipboard.CopyAll();
          if (copy == null) {
            return PasteOutcome.ClipboardBusy;
          }
        }
      }

      uint before = this.clipboard.SequenceNumber;
      if (!this.clipboard.SetPrivateText(value)) {
        // Put it back only if the failed set changed something.
        if (this.clipboard.SequenceNumber != before) {
          await RestoreAsync(wasEmpty, historyItem, copy);
        }
        return PasteOutcome.ClipboardBusy;
      }
      uint ours = this.clipboard.SequenceNumber;
      try {
        this.keys.SendPaste();
        await this.delay.Delay(RestoreWait);
      } finally {
        // A newer copy by the user is never overwritten.
        if (this.clipboard.SequenceNumber == ours) {
          await RestoreAsync(wasEmpty, historyItem, copy);
        }
      }
      return PasteOutcome.Pasted;
    }

    // Empty stays empty. When the content can't be put back, the value is cleared so it doesn't stay on the clipboard.
    private async Task RestoreAsync(bool wasEmpty, object historyItem, object copy) {
      bool restored;
      if (wasEmpty) {
        restored = false;
      } else if (historyItem != null) {
        restored = await this.clipboard.RestoreHistoryItemAsync(historyItem);
      } else {
        restored = this.clipboard.RestoreCopy(copy);
      }
      if (!restored) {
        this.clipboard.Clear();
      }
    }
  }

  /// <summary>Remembers the last foreground window that belongs to another process than MDSHO.</summary>
  public sealed class ForegroundHistory {
    private readonly int ownProcessId;

    /// <summary>Creates the history for MDSHO's own process ID.</summary>
    public ForegroundHistory(int ownProcessId) {
      this.ownProcessId = ownProcessId;
    }

    /// <summary>The last window of another process; zero when none.</summary>
    public IntPtr Last { get; private set; }

    /// <summary>A window became the foreground window.</summary>
    public void Observe(IntPtr window, int processId) {
      if (window != IntPtr.Zero && processId != 0 && processId != this.ownProcessId) {
        Last = window;
      }
    }

    /// <summary>The window is gone: it is not remembered any more.</summary>
    public void Forget(IntPtr window) {
      if (Last == window) {
        Last = IntPtr.Zero;
      }
    }
  }

  /// <summary>When Windows blocks input from MDSHO to another process (User Interface Privilege Isolation).</summary>
  public static class ElevationRules {
    /// <summary>
    /// True when the target's integrity level is above MDSHO's, or its token can't even be read (access denied, which a
    /// process running as administrator gives a normal one). Any other unknown is not blocked.
    /// </summary>
    public static bool IsBlocked(int? targetIntegrity, int ownIntegrity, bool tokenAccessDenied) {
      if (targetIntegrity == null) {
        return tokenAccessDenied;
      }
      return targetIntegrity.Value > ownIntegrity;
    }
  }
}
