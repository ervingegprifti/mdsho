using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MDSHO.Common;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// The paste flow of a snippet (1.19.1), with fakes for every Windows part: the window the user came from,
  /// the elevation check, the focused-field check, the clipboard, the key sender and the waits.
  /// </summary>
  public class ClipboardPasterTests {

    private static readonly IntPtr Target = new IntPtr(42);

    private sealed class Log {
      public readonly List<string> Steps = new List<string>();
      public void Add(string step) { Steps.Add(step); }
    }

    private sealed class FakeWindows : IForegroundWindows {
      private readonly Log log;
      public IntPtr Last = Target;
      public bool ActivateWorks = true;
      public FakeWindows(Log log) { this.log = log; }
      public IntPtr LastOtherWindow => Last;
      public bool Activate(IntPtr window) { log.Add("activate " + window); return ActivateWorks; }
    }

    private sealed class FakeElevation : IElevationCheck {
      public bool Blocked;
      public bool IsBlockedByElevation(IntPtr window) { return Blocked; }
    }

    private sealed class FakeFields : IFocusedFieldCheck {
      private readonly Log log;
      public FieldCheck Result = FieldCheck.Editable;
      public FakeFields(Log log) { this.log = log; }
      public Task<FieldCheck> CheckAsync(IntPtr window) { log.Add("check " + window); return Task.FromResult(Result); }
    }

    // A clipboard with a sequence number that grows on every change, like Windows'.
    private sealed class FakeClipboard : IPasteClipboard {
      private readonly Log log;
      public uint Sequence = 100;
      public string Content = "user text";
      public bool History;
      public bool Excluded;
      public object HistoryItem = "history item";
      public bool HistoryRestoreWorks = true;
      public bool CopyWorks = true;
      public bool CopyRestoreWorks = true;
      public bool SetWorks = true;
      public bool SetEmptiesFirst;
      public bool? SetAllowedInHistory;
      public FakeClipboard(Log log) { this.log = log; }

      public uint SequenceNumber => Sequence;
      public bool IsEmpty() { return Content == null; }
      public bool IsHistoryEnabled() { return History; }
      public bool IsExcludedFromHistory() { log.Add("excluded?"); return Excluded; }
      public Task<object> CurrentHistoryItemAsync() { log.Add("history item"); return Task.FromResult(HistoryItem); }
      public object CopyAll() { log.Add("copy all"); return CopyWorks ? (object)("copy of " + Content) : null; }
      public bool SetPrivateText(string text) {
        log.Add("set private");
        if (!SetWorks) {
          if (SetEmptiesFirst) { Content = null; Sequence++; }
          return false;
        }
        Content = text;
        SetAllowedInHistory = false;
        Sequence++;
        return true;
      }
      public Task<bool> RestoreHistoryItemAsync(object item) {
        log.Add("restore history " + item);
        if (HistoryRestoreWorks) { Content = "user text"; Sequence++; }
        return Task.FromResult(HistoryRestoreWorks);
      }
      public bool RestoreCopy(object copy) {
        log.Add("restore copy " + copy);
        if (CopyRestoreWorks) { Content = "user text"; Sequence++; }
        return CopyRestoreWorks;
      }
      public bool Clear() { log.Add("clear"); Content = null; Sequence++; return true; }
    }

    private sealed class FakeKeys : IKeySender {
      private readonly Log log;
      public FakeKeys(Log log) { this.log = log; }
      public void SendPaste() { log.Add("ctrl+v"); }
    }

    // The waits finish at once; an action can run during a wait (e.g. the user copies something).
    private sealed class FakeDelay : IPasteDelay {
      private readonly Log log;
      public Action DuringRestoreWait;
      public readonly List<TimeSpan> Waits = new List<TimeSpan>();
      public FakeDelay(Log log) { this.log = log; }
      public Task Delay(TimeSpan time) {
        Waits.Add(time);
        log.Add("wait " + (int)time.TotalMilliseconds);
        if (time == ClipboardPaster.RestoreWait) { DuringRestoreWait?.Invoke(); }
        return Task.CompletedTask;
      }
    }

    private sealed class Fixture {
      public readonly Log Log = new Log();
      public readonly FakeWindows Windows;
      public readonly FakeElevation Elevation = new FakeElevation();
      public readonly FakeFields Fields;
      public readonly FakeClipboard Clipboard;
      public readonly FakeKeys Keys;
      public readonly FakeDelay Delay;
      public readonly ClipboardPaster Paster;
      public Fixture() {
        Windows = new FakeWindows(Log);
        Fields = new FakeFields(Log);
        Clipboard = new FakeClipboard(Log);
        Keys = new FakeKeys(Log);
        Delay = new FakeDelay(Log);
        Paster = new ClipboardPaster(Windows, Elevation, Fields, Clipboard, Keys, Delay);
      }
      public PasteOutcome Paste(string value = "Main Street 12") {
        return Paster.PasteAsync(value).GetAwaiter().GetResult();
      }
    }

    // ---- The decision flow ----

    [Fact]
    public void With_no_window_to_go_back_to_it_says_no_place_and_touches_nothing() {
      Fixture f = new Fixture();
      f.Windows.Last = IntPtr.Zero;
      Assert.Equal(PasteOutcome.NoTarget, f.Paste());
      Assert.Empty(f.Log.Steps);
      Assert.Equal("user text", f.Clipboard.Content);
    }

    [Fact]
    public void An_elevated_window_is_never_activated_and_the_clipboard_is_untouched() {
      Fixture f = new Fixture();
      f.Elevation.Blocked = true;
      Assert.Equal(PasteOutcome.Elevated, f.Paste());
      Assert.Empty(f.Log.Steps);
      Assert.Equal(100u, f.Clipboard.Sequence);
    }

    [Fact]
    public void When_the_window_cannot_get_the_focus_back_it_says_no_place_and_sends_no_keys() {
      Fixture f = new Fixture();
      f.Windows.ActivateWorks = false;
      Assert.Equal(PasteOutcome.NoTarget, f.Paste());
      Assert.DoesNotContain("ctrl+v", f.Log.Steps);
      Assert.DoesNotContain("set private", f.Log.Steps);
      Assert.Equal(100u, f.Clipboard.Sequence);
    }

    [Fact]
    public void A_field_that_is_clearly_not_editable_says_no_place_and_the_clipboard_is_untouched() {
      Fixture f = new Fixture();
      f.Fields.Result = FieldCheck.NotEditable;
      Assert.Equal(PasteOutcome.NotEditable, f.Paste());
      Assert.Equal(new[] { "activate 42", "wait " + (int)ClipboardPaster.FocusWait.TotalMilliseconds, "check 42" }, f.Log.Steps);
      Assert.Equal(100u, f.Clipboard.Sequence);
      Assert.Equal("user text", f.Clipboard.Content);
    }

    [Fact]
    public void When_the_field_check_cannot_tell_it_pastes_anyway() {
      Fixture f = new Fixture();
      f.Fields.Result = FieldCheck.Unknown;
      Assert.Equal(PasteOutcome.Pasted, f.Paste());
      Assert.Contains("ctrl+v", f.Log.Steps);
    }

    [Fact]
    public void It_goes_back_to_the_window_checks_the_field_sets_the_value_pastes_waits_and_restores() {
      Fixture f = new Fixture();
      Assert.Equal(PasteOutcome.Pasted, f.Paste());
      Assert.Equal(new[] {
        "activate 42", "wait 100", "check 42", "copy all", "set private", "ctrl+v", "wait 500", "restore copy copy of user text"
      }, f.Log.Steps);
      Assert.Equal("user text", f.Clipboard.Content);
    }

    [Fact]
    public void The_value_is_set_excluded_from_history_and_is_on_the_clipboard_when_ctrl_v_is_sent() {
      Fixture f = new Fixture();
      string onClipboardAtPaste = null;
      FakeKeysProbe probe = new FakeKeysProbe(() => onClipboardAtPaste = f.Clipboard.Content);
      ClipboardPaster paster = new ClipboardPaster(f.Windows, f.Elevation, f.Fields, f.Clipboard, probe, f.Delay);
      Assert.Equal(PasteOutcome.Pasted, paster.PasteAsync("Main Street 12").GetAwaiter().GetResult());
      Assert.Equal("Main Street 12", onClipboardAtPaste);
      Assert.False(f.Clipboard.SetAllowedInHistory);
    }

    private sealed class FakeKeysProbe : IKeySender {
      private readonly Action onPaste;
      public FakeKeysProbe(Action onPaste) { this.onPaste = onPaste; }
      public void SendPaste() { onPaste(); }
    }

    [Fact]
    public void The_waits_are_about_100_ms_for_the_focus_and_500_ms_before_restoring() {
      Assert.Equal(TimeSpan.FromMilliseconds(100), ClipboardPaster.FocusWait);
      Assert.Equal(TimeSpan.FromMilliseconds(500), ClipboardPaster.RestoreWait);
      Fixture f = new Fixture();
      f.Paste();
      Assert.Equal(new[] { ClipboardPaster.FocusWait, ClipboardPaster.RestoreWait }, f.Delay.Waits);
    }

    // ---- Restoring the user's clipboard ----

    [Fact]
    public void An_empty_clipboard_stays_empty() {
      Fixture f = new Fixture();
      f.Clipboard.Content = null;
      Assert.Equal(PasteOutcome.Pasted, f.Paste());
      Assert.Null(f.Clipboard.Content);
      Assert.DoesNotContain("copy all", f.Log.Steps);
      Assert.DoesNotContain("history item", f.Log.Steps);
      Assert.Equal("clear", f.Log.Steps[f.Log.Steps.Count - 1]);
    }

    [Fact]
    public void An_empty_clipboard_stays_empty_with_history_on_too() {
      Fixture f = new Fixture();
      f.Clipboard.Content = null;
      f.Clipboard.History = true;
      f.Paste();
      Assert.Null(f.Clipboard.Content);
      Assert.DoesNotContain("history item", f.Log.Steps);
    }

    [Fact]
    public void With_history_on_and_content_kept_out_of_history_it_keeps_a_copy_in_memory_instead_of_an_older_history_item() {
      // A password manager's copy is not in the history: history item 0 is an older item, never the user's current content.
      Fixture f = new Fixture();
      f.Clipboard.History = true;
      f.Clipboard.Excluded = true;
      Assert.Equal(PasteOutcome.Pasted, f.Paste());
      Assert.DoesNotContain("history item", f.Log.Steps);
      Assert.Contains("copy all", f.Log.Steps);
      Assert.Equal("restore copy copy of user text", f.Log.Steps[f.Log.Steps.Count - 1]);
      Assert.DoesNotContain(f.Log.Steps, s => s.StartsWith("restore history"));
    }

    [Fact]
    public void With_history_on_and_content_in_history_it_checks_the_exclusion_and_uses_the_history_item() {
      Fixture f = new Fixture();
      f.Clipboard.History = true;
      f.Clipboard.Excluded = false;
      Assert.Equal(PasteOutcome.Pasted, f.Paste());
      Assert.Contains("excluded?", f.Log.Steps);
      Assert.True(f.Log.Steps.IndexOf("excluded?") < f.Log.Steps.IndexOf("set private"));
      Assert.DoesNotContain("copy all", f.Log.Steps);
      Assert.Equal("restore history history item", f.Log.Steps[f.Log.Steps.Count - 1]);
    }

    [Fact]
    public void With_history_on_it_puts_back_the_history_item_and_never_reads_the_clipboard() {
      Fixture f = new Fixture();
      f.Clipboard.History = true;
      Assert.Equal(PasteOutcome.Pasted, f.Paste());
      Assert.DoesNotContain("copy all", f.Log.Steps);
      Assert.Contains("history item", f.Log.Steps);
      Assert.Equal("restore history history item", f.Log.Steps[f.Log.Steps.Count - 1]);
      Assert.Equal("user text", f.Clipboard.Content);
    }

    [Fact]
    public void With_history_on_but_no_history_item_it_keeps_a_copy_in_memory_instead() {
      Fixture f = new Fixture();
      f.Clipboard.History = true;
      f.Clipboard.HistoryItem = null;
      f.Paste();
      Assert.Contains("copy all", f.Log.Steps);
      Assert.Equal("restore copy copy of user text", f.Log.Steps[f.Log.Steps.Count - 1]);
    }

    [Fact]
    public void When_the_history_item_cannot_be_put_back_the_value_is_cleared_from_the_clipboard() {
      Fixture f = new Fixture();
      f.Clipboard.History = true;
      f.Clipboard.HistoryRestoreWorks = false;
      f.Paste();
      Assert.Equal("clear", f.Log.Steps[f.Log.Steps.Count - 1]);
      Assert.Null(f.Clipboard.Content);
    }

    [Fact]
    public void When_the_copy_cannot_be_put_back_the_value_is_cleared_from_the_clipboard() {
      Fixture f = new Fixture();
      f.Clipboard.CopyRestoreWorks = false;
      f.Paste();
      Assert.Equal("clear", f.Log.Steps[f.Log.Steps.Count - 1]);
      Assert.Null(f.Clipboard.Content);
    }

    [Fact]
    public void A_newer_copy_by_the_user_is_never_overwritten() {
      Fixture f = new Fixture();
      f.Delay.DuringRestoreWait = () => { f.Clipboard.Content = "copied meanwhile"; f.Clipboard.Sequence++; };
      Assert.Equal(PasteOutcome.Pasted, f.Paste());
      Assert.Equal("copied meanwhile", f.Clipboard.Content);
      Assert.DoesNotContain(f.Log.Steps, s => s.StartsWith("restore") || s == "clear");
    }

    [Fact]
    public void A_newer_copy_is_kept_with_history_on_too() {
      Fixture f = new Fixture();
      f.Clipboard.History = true;
      f.Delay.DuringRestoreWait = () => { f.Clipboard.Content = "copied meanwhile"; f.Clipboard.Sequence++; };
      f.Paste();
      Assert.Equal("copied meanwhile", f.Clipboard.Content);
      Assert.DoesNotContain(f.Log.Steps, s => s.StartsWith("restore") || s == "clear");
    }

    [Fact]
    public void When_the_clipboard_cannot_be_saved_it_does_not_paste_and_touches_nothing() {
      Fixture f = new Fixture();
      f.Clipboard.CopyWorks = false;
      Assert.Equal(PasteOutcome.ClipboardBusy, f.Paste());
      Assert.DoesNotContain("set private", f.Log.Steps);
      Assert.DoesNotContain("ctrl+v", f.Log.Steps);
      Assert.Equal(100u, f.Clipboard.Sequence);
    }

    [Fact]
    public void When_the_value_cannot_be_set_and_nothing_changed_it_sends_no_keys_and_restores_nothing() {
      Fixture f = new Fixture();
      f.Clipboard.SetWorks = false;
      Assert.Equal(PasteOutcome.ClipboardBusy, f.Paste());
      Assert.DoesNotContain("ctrl+v", f.Log.Steps);
      Assert.DoesNotContain(f.Log.Steps, s => s.StartsWith("restore") || s == "clear");
      Assert.Equal("user text", f.Clipboard.Content);
    }

    [Fact]
    public void When_setting_the_value_failed_halfway_the_users_clipboard_is_put_back() {
      Fixture f = new Fixture();
      f.Clipboard.SetWorks = false;
      f.Clipboard.SetEmptiesFirst = true;
      Assert.Equal(PasteOutcome.ClipboardBusy, f.Paste());
      Assert.DoesNotContain("ctrl+v", f.Log.Steps);
      Assert.Equal("restore copy copy of user text", f.Log.Steps[f.Log.Steps.Count - 1]);
      Assert.Equal("user text", f.Clipboard.Content);
    }

    [Fact]
    public void The_clipboard_is_put_back_even_when_sending_the_keys_fails() {
      Fixture f = new Fixture();
      ClipboardPaster paster = new ClipboardPaster(f.Windows, f.Elevation, f.Fields, f.Clipboard,
        new FakeKeysProbe(() => throw new InvalidOperationException("SendInput failed")), f.Delay);
      Assert.Throws<InvalidOperationException>(() => paster.PasteAsync("v").GetAwaiter().GetResult());
      Assert.Equal("user text", f.Clipboard.Content);
      Assert.Equal("restore copy copy of user text", f.Log.Steps[f.Log.Steps.Count - 1]);
    }

    [Fact]
    public void A_second_paste_while_one_is_running_is_ignored_so_the_first_value_is_never_saved_as_the_users() {
      Fixture f = new Fixture();
      TaskCompletionSource<bool> hold = new TaskCompletionSource<bool>();
      HeldDelay held = new HeldDelay(hold.Task);
      ClipboardPaster paster = new ClipboardPaster(f.Windows, f.Elevation, f.Fields, f.Clipboard, f.Keys, held);
      Task<PasteOutcome> first = paster.PasteAsync("one");
      Assert.True(paster.IsPasting);
      Assert.Equal(PasteOutcome.AlreadyPasting, paster.PasteAsync("two").GetAwaiter().GetResult());
      hold.SetResult(true);
      Assert.Equal(PasteOutcome.Pasted, first.GetAwaiter().GetResult());
      Assert.False(paster.IsPasting);
      Assert.Equal("user text", f.Clipboard.Content);
    }

    private sealed class HeldDelay : IPasteDelay {
      private readonly Task hold;
      public HeldDelay(Task hold) { this.hold = hold; }
      public Task Delay(TimeSpan time) { return time == ClipboardPaster.RestoreWait ? hold : Task.CompletedTask; }
    }

    // ---- What counts as an editable text field (UI Automation facts) ----

    [Theory]
    [InlineData(FocusedControlKind.Edit, true, false, false, FieldCheck.Editable)]
    [InlineData(FocusedControlKind.Edit, true, true, false, FieldCheck.NotEditable)]
    [InlineData(FocusedControlKind.Edit, false, false, false, FieldCheck.Editable)]
    [InlineData(FocusedControlKind.Document, false, false, true, FieldCheck.Editable)]
    [InlineData(FocusedControlKind.Document, true, true, true, FieldCheck.NotEditable)]
    [InlineData(FocusedControlKind.Container, true, false, false, FieldCheck.Editable)]
    [InlineData(FocusedControlKind.Container, false, false, true, FieldCheck.Editable)]
    [InlineData(FocusedControlKind.Container, false, false, false, FieldCheck.Unknown)]
    [InlineData(FocusedControlKind.Other, false, false, false, FieldCheck.NotEditable)]
    [InlineData(FocusedControlKind.Other, true, false, false, FieldCheck.Editable)]
    [InlineData(FocusedControlKind.Other, true, true, false, FieldCheck.NotEditable)]
    [InlineData(FocusedControlKind.Other, false, false, true, FieldCheck.Editable)]
    public void The_focused_field_rules(FocusedControlKind kind, bool hasValue, bool readOnly, bool hasText, FieldCheck expected) {
      Assert.Equal(expected, FocusedFieldRules.Decide(kind, hasValue, readOnly, hasText));
    }
  }
}

namespace MDSHO.Tests {

  /// <summary>The two small rules behind the Windows parts of the paste: which window counts as "the one the user came from", and when Windows blocks the paste.</summary>
  public class ClipboardPasteRulesTests {

    [Fact]
    public void The_last_foreground_window_of_another_process_is_remembered_and_MDSHO_windows_are_skipped() {
      ForegroundHistory history = new ForegroundHistory(ownProcessId: 10);
      Assert.Equal(IntPtr.Zero, history.Last);
      history.Observe(new IntPtr(1), 20);
      Assert.Equal(new IntPtr(1), history.Last);
      history.Observe(new IntPtr(2), 10);
      Assert.Equal(new IntPtr(1), history.Last);
      history.Observe(new IntPtr(3), 30);
      Assert.Equal(new IntPtr(3), history.Last);
      history.Observe(IntPtr.Zero, 0);
      Assert.Equal(new IntPtr(3), history.Last);
    }

    [Fact]
    public void A_window_that_is_gone_is_forgotten() {
      ForegroundHistory history = new ForegroundHistory(ownProcessId: 10);
      history.Observe(new IntPtr(5), 20);
      history.Forget(new IntPtr(6));
      Assert.Equal(new IntPtr(5), history.Last);
      history.Forget(new IntPtr(5));
      Assert.Equal(IntPtr.Zero, history.Last);
    }

    [Theory]
    [InlineData(0x2000, 0x2000, false, false)] // both medium
    [InlineData(0x3000, 0x2000, false, true)]  // target high (administrator), MDSHO medium
    [InlineData(0x3000, 0x3000, false, false)] // both administrator
    [InlineData(0x2000, 0x3000, false, false)] // MDSHO above the target
    [InlineData(0x4000, 0x3000, false, true)]  // target system
    [InlineData(0, 0x2000, true, true)]        // the target's token can't even be read: it runs above MDSHO
    [InlineData(0, 0x2000, false, false)]      // can't tell for another reason: try
    public void Windows_blocks_the_paste_only_when_the_target_runs_at_a_higher_level(int target, int own, bool accessDenied, bool blocked) {
      Assert.Equal(blocked, ElevationRules.IsBlocked(target == 0 ? null : target, own, accessDenied));
    }
  }
}
