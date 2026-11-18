using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using MDSHO.Common;
using MDSHO.Data;
using MDSHO.ViewModels;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>A global hotkey seam that registers nothing in Windows: <see cref="Taken"/> are held by other apps.</summary>
  public sealed class FakeHotkeyService : IGlobalHotkeyService {
    public readonly HashSet<Hotkey> Taken = new HashSet<Hotkey>();
    public Hotkey Registered { get; private set; }
    public readonly List<string> Calls = new List<string>();
    public bool Disposed { get; private set; }
    public event Action Pressed;

    public bool Register(Hotkey hotkey) {
      Calls.Add("register " + hotkey.MenuText);
      Registered = null;
      if (Taken.Contains(hotkey)) {
        return false;
      }
      Registered = hotkey;
      return true;
    }
    public void Unregister() {
      Calls.Add("unregister");
      Registered = null;
    }
    public void Press() { Pressed?.Invoke(); }
    public void Dispose() { Disposed = true; Registered = null; }
  }

  /// <summary>
  /// The saved "Bring panels to front" shortcut (one for the app, on this PC) and the Settings box that records a new one.
  /// The hotkey is never really registered in Windows.
  /// </summary>
  public class BringToFrontShortcutTests : IDisposable {
    private readonly TempDb db = new TempDb();
    private readonly FakeHotkeyService service = new FakeHotkeyService();
    private static readonly Hotkey CtrlAltK = new Hotkey(HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x4B);
    private const int K = 0x4B, M = 0x4D, Ctrl = 0x11, Esc = 0x1B;

    public void Dispose() { db.Dispose(); }

    private static string Stored() { return DataStore.AppSettings.Get(BringToFrontShortcut.SettingKey); }

    private BringToFrontShortcut Started() {
      BringToFrontShortcut s = new BringToFrontShortcut(service);
      s.Start();
      return s;
    }

    // ---- The saved shortcut ----

    [Fact]
    public void Without_a_saved_shortcut_the_default_is_registered_at_startup() {
      BringToFrontShortcut s = Started();
      Assert.Equal(Hotkey.Default, s.Saved);
      Assert.Equal(Hotkey.Default, service.Registered);
      Assert.True(s.IsRegistered);
      Assert.False(s.IsBlocked);
      Assert.Equal("Ctrl+Shift+Alt+M", s.MenuText);
      Assert.Null(Stored()); // nothing is written until the user applies a change
    }

    [Fact]
    public void A_saved_Off_registers_nothing_and_the_tray_shows_no_shortcut() {
      DataStore.AppSettings.Set(BringToFrontShortcut.SettingKey, "Off");
      BringToFrontShortcut s = Started();
      Assert.Null(s.Saved);
      Assert.Null(service.Registered);
      Assert.Empty(service.Calls);
      Assert.False(s.IsRegistered);
      Assert.False(s.IsBlocked);
      Assert.Equal("", s.MenuText);
    }

    [Fact]
    public void A_saved_shortcut_another_app_holds_does_not_stop_startup_and_shows_as_blocked() {
      DataStore.AppSettings.Set(BringToFrontShortcut.SettingKey, "Ctrl+Alt+K");
      service.Taken.Add(CtrlAltK);
      BringToFrontShortcut s = Started();
      Assert.Equal(CtrlAltK, s.Saved);
      Assert.False(s.IsRegistered);
      Assert.True(s.IsBlocked);
      Assert.Equal("", s.MenuText);
    }

    [Fact]
    public void A_damaged_saved_value_falls_back_to_the_default() {
      DataStore.AppSettings.Set(BringToFrontShortcut.SettingKey, "Shift+");
      BringToFrontShortcut s = Started();
      Assert.Equal(Hotkey.Default, s.Saved);
      Assert.Equal(Hotkey.Default, service.Registered);
    }

    [Fact]
    public void Applying_a_free_shortcut_registers_and_saves_it() {
      BringToFrontShortcut s = Started();
      int changed = 0;
      s.Changed += () => changed++;
      Assert.True(s.Apply(CtrlAltK));
      Assert.Equal(CtrlAltK, s.Saved);
      Assert.Equal(CtrlAltK, service.Registered);
      Assert.Equal("Ctrl+Alt+K", Stored());
      Assert.Equal("Ctrl+Alt+K", s.MenuText);
      Assert.Equal(1, changed);
      // MDSHO started again reads it back.
      FakeHotkeyService again = new FakeHotkeyService();
      BringToFrontShortcut restarted = new BringToFrontShortcut(again);
      restarted.Start();
      Assert.Equal(CtrlAltK, again.Registered);
    }

    [Fact]
    public void Applying_a_shortcut_another_app_holds_keeps_the_old_one_working() {
      BringToFrontShortcut s = Started();
      service.Taken.Add(CtrlAltK);
      Assert.False(s.Apply(CtrlAltK));
      Assert.Equal(Hotkey.Default, s.Saved);
      Assert.Equal(Hotkey.Default, service.Registered);
      Assert.True(s.IsRegistered);
      Assert.Null(Stored());
    }

    [Fact]
    public void Applying_Off_unregisters_and_saves_Off() {
      BringToFrontShortcut s = Started();
      Assert.True(s.Apply(null));
      Assert.Null(s.Saved);
      Assert.Null(service.Registered);
      Assert.Equal("Off", Stored());
      Assert.Equal("", s.MenuText);
      // And back on.
      Assert.True(s.Apply(Hotkey.Default));
      Assert.Equal(Hotkey.Default, service.Registered);
      Assert.Equal("Ctrl+Shift+Alt+M", Stored());
    }

    [Fact]
    public void Suspend_lets_the_keys_through_while_recording_and_Resume_registers_the_saved_shortcut_again() {
      BringToFrontShortcut s = Started();
      s.Suspend();
      Assert.Null(service.Registered);
      s.Resume();
      Assert.Equal(Hotkey.Default, service.Registered);
      Assert.True(s.IsRegistered);
      s.Apply(null);
      service.Calls.Clear();
      s.Suspend();
      s.Resume();
      Assert.Null(service.Registered);
      Assert.DoesNotContain(service.Calls, c => c.StartsWith("register"));
    }

    [Fact]
    public void A_press_of_the_registered_keys_is_passed_on() {
      BringToFrontShortcut s = Started();
      int pressed = 0;
      s.Pressed += () => pressed++;
      service.Press();
      Assert.Equal(1, pressed);
    }

    [Fact]
    public void Dispose_releases_the_keys() {
      BringToFrontShortcut s = Started();
      s.Dispose();
      Assert.True(service.Disposed);
      Assert.Null(service.Registered);
    }

    [Fact]
    public void The_shortcut_is_exported_and_only_an_import_that_replaces_the_data_sets_it() {
      BringToFrontShortcut s = Started();
      s.Apply(CtrlAltK);
      new AppViewModel().AddWindow("TEST", 0, 0);
      string zip = Path.Combine(db.Folder, "export.zip");
      DataExporter.Export(zip);
      using (ZipArchive archive = ZipFile.OpenRead(zip))
      using (StreamReader reader = new StreamReader(archive.GetEntry("data.json").Open())) {
        Assert.Equal("Ctrl+Alt+K", (string)System.Text.Json.Nodes.JsonNode.Parse(reader.ReadToEnd())["settings"][BringToFrontShortcut.SettingKey]);
      }
      DataStore.AppSettings.Set(BringToFrontShortcut.SettingKey, "Alt+Win+F1");
      DataImporter.Import(zip, ImportMode.AddAsNew);
      Assert.Equal("Alt+Win+F1", Stored());
      DataImporter.Import(zip, ImportMode.Replace);
      Assert.Equal("Ctrl+Alt+K", Stored());
    }

    // ---- The Settings box ----

    private ShortcutRecorder Recorder(BringToFrontShortcut s) { return new ShortcutRecorder(s); }

    [Fact]
    public void The_box_shows_the_saved_shortcut_with_the_normal_hint() {
      ShortcutRecorder r = Recorder(Started());
      Assert.Equal("Ctrl + Shift + Alt + M", r.BoxText);
      Assert.Equal("For all panels, on this PC. Press it again or Esc to send them back.", r.Hint);
      Assert.False(r.HintIsError);
      Assert.False(r.IsRecording);
    }

    [Fact]
    public void The_box_shows_Off_with_its_hint() {
      DataStore.AppSettings.Set(BringToFrontShortcut.SettingKey, "Off");
      ShortcutRecorder r = Recorder(Started());
      Assert.Equal("Off", r.BoxText);
      Assert.Equal("No shortcut. The tray menu item still works.", r.Hint);
      Assert.False(r.HintIsError);
    }

    [Fact]
    public void A_saved_shortcut_that_could_not_be_registered_shows_the_red_already_used_hint() {
      DataStore.AppSettings.Set(BringToFrontShortcut.SettingKey, "Ctrl+Alt+K");
      service.Taken.Add(CtrlAltK);
      ShortcutRecorder r = Recorder(Started());
      Assert.Equal("Ctrl + Alt + K", r.BoxText);
      Assert.Equal("Another app already uses this shortcut. Choose another one.", r.Hint);
      Assert.True(r.HintIsError);
    }

    [Fact]
    public void Recording_asks_for_the_new_shortcut_and_lets_the_old_keys_through() {
      ShortcutRecorder r = Recorder(Started());
      r.StartRecording();
      Assert.True(r.IsRecording);
      Assert.Equal("Press the new shortcut…", r.BoxText);
      Assert.Equal("Use Ctrl, Alt or Win with a key. Esc cancels.", r.Hint);
      Assert.False(r.HintIsError);
      Assert.Null(service.Registered); // so Ctrl+Shift+Alt+M itself can be recorded
    }

    [Fact]
    public void A_key_without_Ctrl_Alt_or_Win_shows_the_red_hint_and_keeps_recording() {
      ShortcutRecorder r = Recorder(Started());
      r.StartRecording();
      Assert.True(r.KeyPressed(HotkeyModifiers.Shift, K));
      Assert.True(r.IsRecording);
      Assert.Equal("Press the new shortcut…", r.BoxText);
      Assert.Equal("Add Ctrl, Alt or Win to the key.", r.Hint);
      Assert.True(r.HintIsError);
    }

    [Fact]
    public void Holding_only_modifier_keys_keeps_waiting() {
      ShortcutRecorder r = Recorder(Started());
      r.StartRecording();
      Assert.True(r.KeyPressed(HotkeyModifiers.Control, Ctrl));
      Assert.True(r.IsRecording);
      Assert.Equal("Use Ctrl, Alt or Win with a key. Esc cancels.", r.Hint);
    }

    [Fact]
    public void A_valid_shortcut_ends_recording_but_takes_effect_only_on_Apply() {
      BringToFrontShortcut s = Started();
      ShortcutRecorder r = Recorder(s);
      r.StartRecording();
      Assert.True(r.KeyPressed(HotkeyModifiers.Control | HotkeyModifiers.Alt, K));
      Assert.False(r.IsRecording);
      Assert.Equal("Ctrl + Alt + K", r.BoxText);
      Assert.False(r.HintIsError);
      Assert.Equal(Hotkey.Default, s.Saved);
      Assert.Equal(Hotkey.Default, service.Registered); // the old one works again until Apply
      Assert.True(r.Apply());
      Assert.Equal(CtrlAltK, s.Saved);
      Assert.Equal(CtrlAltK, service.Registered);
    }

    [Fact]
    public void Esc_cancels_recording() {
      ShortcutRecorder r = Recorder(Started());
      r.StartRecording();
      r.KeyPressed(HotkeyModifiers.Shift, K);
      Assert.True(r.KeyPressed(HotkeyModifiers.None, Esc));
      Assert.False(r.IsRecording);
      Assert.Equal("Ctrl + Shift + Alt + M", r.BoxText);
      Assert.False(r.HintIsError);
      Assert.Equal(Hotkey.Default, service.Registered);
    }

    [Fact]
    public void Keys_are_not_taken_while_not_recording() {
      ShortcutRecorder r = Recorder(Started());
      Assert.False(r.KeyPressed(HotkeyModifiers.Control | HotkeyModifiers.Alt, K));
      Assert.False(r.KeyPressed(HotkeyModifiers.None, Esc));
      Assert.Equal("Ctrl + Shift + Alt + M", r.BoxText);
    }

    [Fact]
    public void Off_and_Default_change_the_box_and_take_effect_on_Apply() {
      BringToFrontShortcut s = Started();
      ShortcutRecorder r = Recorder(s);
      r.TurnOff();
      Assert.Equal("Off", r.BoxText);
      Assert.Equal("No shortcut. The tray menu item still works.", r.Hint);
      Assert.Equal(Hotkey.Default, service.Registered);
      Assert.True(r.Apply());
      Assert.Null(service.Registered);
      Assert.Equal("Off", Stored());
      ShortcutRecorder again = Recorder(s);
      again.UseDefault();
      Assert.Equal("Ctrl + Shift + Alt + M", again.BoxText);
      Assert.Equal("For all panels, on this PC. Press it again or Esc to send them back.", again.Hint);
      Assert.True(again.Apply());
      Assert.Equal(Hotkey.Default, service.Registered);
    }

    [Fact]
    public void Off_or_Default_while_recording_stop_recording() {
      ShortcutRecorder r = Recorder(Started());
      r.StartRecording();
      r.TurnOff();
      Assert.False(r.IsRecording);
      Assert.Equal("Off", r.BoxText);
      Assert.Equal(Hotkey.Default, service.Registered);
    }

    [Fact]
    public void Apply_with_a_shortcut_another_app_holds_shows_the_red_hint_and_keeps_the_old_one() {
      BringToFrontShortcut s = Started();
      service.Taken.Add(CtrlAltK);
      ShortcutRecorder r = Recorder(s);
      r.StartRecording();
      r.KeyPressed(HotkeyModifiers.Control | HotkeyModifiers.Alt, K);
      Assert.False(r.Apply());
      Assert.Equal("Ctrl + Alt + K", r.BoxText);
      Assert.Equal("Another app already uses this shortcut. Choose another one.", r.Hint);
      Assert.True(r.HintIsError);
      Assert.Equal(Hotkey.Default, s.Saved);
      Assert.Equal(Hotkey.Default, service.Registered);
    }

    [Fact]
    public void Apply_without_a_change_on_a_blocked_shortcut_tries_again_and_never_keeps_the_window_open() {
      DataStore.AppSettings.Set(BringToFrontShortcut.SettingKey, "Ctrl+Alt+K");
      service.Taken.Add(CtrlAltK);
      BringToFrontShortcut s = Started();
      Assert.True(Recorder(s).Apply());
      Assert.False(s.IsRegistered);
      // The other app let go of the keys: Apply registers them.
      service.Taken.Clear();
      Assert.True(Recorder(s).Apply());
      Assert.True(s.IsRegistered);
      Assert.Equal(CtrlAltK, service.Registered);
    }

    [Fact]
    public void Apply_without_a_change_does_not_register_again() {
      BringToFrontShortcut s = Started();
      service.Calls.Clear();
      Assert.True(Recorder(s).Apply());
      Assert.Empty(service.Calls);
    }

    [Fact]
    public void Closing_while_recording_lets_the_saved_shortcut_work_again() {
      ShortcutRecorder r = Recorder(Started());
      r.StartRecording();
      r.Cancel();
      Assert.False(r.IsRecording);
      Assert.Equal(Hotkey.Default, service.Registered);
    }

    [Fact]
    public void The_box_raises_property_changes_for_the_view() {
      ShortcutRecorder r = Recorder(Started());
      List<string> changed = new List<string>();
      r.PropertyChanged += (o, e) => changed.Add(e.PropertyName);
      r.StartRecording();
      Assert.Contains(nameof(ShortcutRecorder.BoxText), changed);
      Assert.Contains(nameof(ShortcutRecorder.Hint), changed);
      Assert.Contains(nameof(ShortcutRecorder.IsRecording), changed);
      changed.Clear();
      r.KeyPressed(HotkeyModifiers.Shift, M);
      Assert.Contains(nameof(ShortcutRecorder.HintIsError), changed);
    }
  }
}
