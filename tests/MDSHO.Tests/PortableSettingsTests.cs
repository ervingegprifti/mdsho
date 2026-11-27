using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;
using MDSHO.Common;
using MDSHO.Core;
using MDSHO.Data;
using MDSHO.ViewModels;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// Export format 3 also carries the default colours and the portable settings: Window theme, the Bring panels to front
  /// shortcut, Remember places for each screen setup and (from 1.22.0) how many earlier texts the text history keeps. Only Replace applies them, and Replace also clears the
  /// remembered screen setup places. Formats 1 and 2 have none of them, so the settings on this PC stay as they are.
  /// </summary>
  public class PortableSettingsTests : IDisposable {
    private TempDb db = new TempDb();
    private readonly string work = Path.Combine(Path.GetTempPath(), "MDSHO.Tests", "settings-" + Guid.NewGuid().ToString("N"));

    private static readonly ColorSet Blue = new ColorSet { BorderColor = "#FF000080", TitleBackgroundColor = "#FF0000A0", TitleTextColor = "#FFFFFFFF", BackgroundColor = "#CC000040", TextColor = "#FFEEEEFF" };
    private static readonly ColorSet Yellow = new ColorSet { BorderColor = "#FF808000", TitleBackgroundColor = "#FFFFE080", TitleTextColor = "#FF000000", BackgroundColor = "#FFFFF8C0", TextColor = "#FF202020" };

    private const string Theme = WindowThemeSetting.SettingKey;
    private const string Shortcut = BringToFrontShortcut.SettingKey;
    private const string Remember = ScreenSetupTracker.SettingKey;

    public PortableSettingsTests() {
      Directory.CreateDirectory(work);
    }

    public void Dispose() {
      if (db != null) { db.Dispose(); }
      try { Directory.Delete(work, true); } catch (IOException) { }
    }

    private string Zip(string name = "export.zip") { return Path.Combine(work, name); }

    private static string Colors(ColorSet c) {
      return c == null ? "none" : string.Join(",", c.BorderColor, c.TitleBackgroundColor, c.TitleTextColor, c.BackgroundColor, c.TextColor);
    }

    // What the user set on the PC the file comes from.
    private static void SeedSettings() {
      DataStore.DefaultColors.Save(ColorSetKind.Window, Blue);
      DataStore.AppSettings.Set(Theme, "Dark");
      DataStore.AppSettings.Set(Shortcut, "Ctrl+Alt+K");
      DataStore.AppSettings.Set(Remember, "Off");
    }

    // Settings tied to this PC, never exported or imported.
    private static readonly string[] PcOnlyKeys = {
      BackupService.OnKey, BackupService.FolderKey, BackupService.KeepKey, BackupService.LastTimeKey, BackupService.LastHashKey,
      BackupService.LastErrorKey, ReviewPrompt.ShownKey, ReviewPrompt.RatedKey, ReviewPrompt.FirstStartKey, "SettingsPage"
    };

    private static JsonNode ReadEntry(string zip, string name) {
      using (ZipArchive z = ZipFile.OpenRead(zip))
      using (StreamReader r = new StreamReader(z.GetEntry(name).Open(), Encoding.UTF8)) {
        return JsonNode.Parse(r.ReadToEnd());
      }
    }

    private string MutateJson(string source, string entryName, Action<JsonNode> change, string target) {
      string dest = Zip(target);
      if (File.Exists(dest)) { File.Delete(dest); }
      using (ZipArchive src = ZipFile.OpenRead(source))
      using (ZipArchive dst = ZipFile.Open(dest, ZipArchiveMode.Create)) {
        foreach (ZipArchiveEntry e in src.Entries) {
          byte[] bytes;
          using (Stream s = e.Open()) using (MemoryStream ms = new MemoryStream()) { s.CopyTo(ms); bytes = ms.ToArray(); }
          if (e.FullName == entryName) {
            JsonNode n = JsonNode.Parse(Encoding.UTF8.GetString(bytes));
            change(n);
            bytes = new UTF8Encoding(false).GetBytes(n.ToJsonString());
          }
          using (Stream w = dst.CreateEntry(e.FullName).Open()) { w.Write(bytes, 0, bytes.Length); }
        }
      }
      return dest;
    }

    // An older file: format 1 has neither note windows nor settings, format 2 has note windows but no settings.
    private string OlderFormat(int format) {
      string zip = MutateJson(Zip(), "data.json", n => {
        n.AsObject().Remove("defaultColors");
        n.AsObject().Remove("settings");
        if (format == 1) { n.AsObject().Remove("noteWindows"); }
      }, "older-data.zip");
      return MutateJson(zip, "manifest.json", n => {
        n["formatVersion"] = format;
        if (format == 1) { n["counts"].AsObject().Remove("noteWindows"); }
      }, "format" + format + ".zip");
    }

    // What this PC has before the import (different from the file).
    private static void SetThisPc() {
      DataStore.DefaultColors.Save(ColorSetKind.Window, Yellow);
      DataStore.DefaultColors.Save(ColorSetKind.Note, Yellow);
      DataStore.AppSettings.Set(Theme, "Light");
      DataStore.AppSettings.Set(Shortcut, "Off");
      DataStore.AppSettings.Set(Remember, "On");
    }

    private static void AssertThisPcUnchanged() {
      Assert.Equal(Colors(Yellow), Colors(DataStore.DefaultColors.Get(ColorSetKind.Window)));
      Assert.Equal(Colors(Yellow), Colors(DataStore.DefaultColors.Get(ColorSetKind.Note)));
      Assert.Equal("Light", DataStore.AppSettings.Get(Theme));
      Assert.Equal("Off", DataStore.AppSettings.Get(Shortcut));
      Assert.Equal("On", DataStore.AppSettings.Get(Remember));
    }

    private static long AddScreenPlaces() {
      long setup = DataStore.ScreenSetups.Ensure("1920x1080*", "1 screen");
      long windowId = DataStore.Windows.GetAll().First().ID;
      DataStore.ScreenSetups.SavePlace(setup, new ScreenSetupPlace { Kind = 0, ItemID = windowId, ScreenDeviceName = @"\\.\DISPLAY1", Left = 5, Top = 6, Width = 300, Height = 400 });
      return setup;
    }

    // ---- Export ----

    [Fact]
    public void Export_format_is_3() {
      ExportManifest m = DataExporter.Export(Zip());
      Assert.Equal(3, ExportManifest.CurrentFormatVersion);
      Assert.Equal(3, m.FormatVersion);
      Assert.Equal(3, (int)ReadEntry(Zip(), "manifest.json")["formatVersion"]);
    }

    [Fact]
    public void The_portable_setting_keys_are_the_ones_the_app_uses() {
      Assert.Equal(new[] { Theme, Shortcut, Remember, TextHistorySetting.SettingKey }, PortableSettings.Keys);
    }

    [Fact]
    public void Export_writes_the_default_colours_and_the_portable_settings_and_nothing_tied_to_this_pc() {
      SeedSettings();
      DataStore.AppSettings.Set(Remember, "On");
      foreach (string key in PcOnlyKeys) { DataStore.AppSettings.Set(key, "x"); }

      DataExporter.Export(Zip());

      JsonNode data = ReadEntry(Zip(), "data.json");
      JsonObject colors = data["defaultColors"].AsObject();
      Assert.Equal("#FF000080", (string)colors["Window"]["borderColor"]);
      Assert.Equal("#FFEEEEFF", (string)colors["Window"]["textColor"]);
      Assert.True(colors.ContainsKey("Note"));
      Assert.Null(colors["Note"]); // never set: none
      JsonObject settings = data["settings"].AsObject();
      Assert.Equal(new[] { Shortcut, Remember, TextHistorySetting.SettingKey, Theme }, settings.Select(kv => kv.Key).OrderBy(k => k, StringComparer.Ordinal).ToArray());
      Assert.Equal("Dark", (string)settings[Theme]);
      Assert.Equal("Ctrl+Alt+K", (string)settings[Shortcut]);
      Assert.Equal("On", (string)settings[Remember]);
    }

    [Fact]
    public void A_setting_never_saved_is_written_as_null() {
      DataExporter.Export(Zip());
      JsonObject settings = ReadEntry(Zip(), "data.json")["settings"].AsObject();
      Assert.True(settings.ContainsKey(Theme));
      Assert.Null(settings[Theme]);
    }

    [Fact]
    public void The_data_hash_changes_with_the_theme_but_not_with_a_backup_setting() {
      string before = DataExporter.DataHash();
      DataStore.AppSettings.Set(BackupService.LastErrorKey, "disk full");
      Assert.Equal(before, DataExporter.DataHash());
      DataStore.AppSettings.Set(Theme, "Dark");
      Assert.NotEqual(before, DataExporter.DataHash());
    }

    // ---- Import ----

    [Fact]
    public void Round_trip_into_an_empty_database_gives_the_same_colours_and_settings() {
      SeedSettings();
      DataStore.DefaultColors.Save(ColorSetKind.Note, Yellow);
      DataExporter.Export(Zip());
      db.Dispose();
      db = new TempDb();

      DataImporter.Import(Zip(), ImportMode.Replace);

      Assert.Equal(Colors(Blue), Colors(DataStore.DefaultColors.Get(ColorSetKind.Window)));
      Assert.Equal(Colors(Yellow), Colors(DataStore.DefaultColors.Get(ColorSetKind.Note)));
      Assert.Equal("Dark", DataStore.AppSettings.Get(Theme));
      Assert.Equal("Ctrl+Alt+K", DataStore.AppSettings.Get(Shortcut));
      Assert.Equal("Off", DataStore.AppSettings.Get(Remember));
    }

    [Fact]
    public void Replace_sets_what_the_file_has_and_clears_what_the_file_never_set() {
      SeedSettings(); // Window colours, theme, hotkey, remember; no Note colours
      DataStore.AppSettings.Set(Remember, "Off");
      DataExporter.Export(Zip());
      string zip = MutateJson(Zip(), "data.json", n => n["settings"][Remember] = null, "no-remember.zip");
      SetThisPc();

      DataImporter.Import(zip, ImportMode.Replace);

      Assert.Equal(Colors(Blue), Colors(DataStore.DefaultColors.Get(ColorSetKind.Window)));
      Assert.Null(DataStore.DefaultColors.Get(ColorSetKind.Note));
      Assert.Equal("Dark", DataStore.AppSettings.Get(Theme));
      Assert.Equal("Ctrl+Alt+K", DataStore.AppSettings.Get(Shortcut));
      Assert.Null(DataStore.AppSettings.Get(Remember));
    }

    [Fact]
    public void Replace_never_touches_settings_tied_to_this_pc_even_when_the_file_has_them() {
      foreach (string key in PcOnlyKeys) { DataStore.AppSettings.Set(key, "here"); }
      DataExporter.Export(Zip());
      string zip = MutateJson(Zip(), "data.json", n => {
        foreach (string key in PcOnlyKeys) { n["settings"][key] = "from the file"; }
      }, "extra-keys.zip");

      DataImporter.Import(zip, ImportMode.Replace);

      foreach (string key in PcOnlyKeys) { Assert.Equal("here", DataStore.AppSettings.Get(key)); }
    }

    [Fact]
    public void Add_as_new_leaves_the_colours_and_settings_of_this_pc_alone() {
      SeedSettings();
      DataExporter.Export(Zip());
      SetThisPc();

      DataImporter.Import(Zip(), ImportMode.AddAsNew);

      AssertThisPcUnchanged();
    }

    [Fact]
    public void Replace_clears_the_remembered_screen_setup_places_but_keeps_the_setups() {
      long setup = AddScreenPlaces();
      DataExporter.Export(Zip());

      DataImporter.Import(Zip(), ImportMode.Replace);

      Assert.Empty(DataStore.ScreenSetups.GetPlaces(setup));
      Assert.NotNull(DataStore.ScreenSetups.Get("1920x1080*"));
    }

    [Fact]
    public void Add_as_new_keeps_the_remembered_screen_setup_places() {
      long setup = AddScreenPlaces();
      DataExporter.Export(Zip());

      DataImporter.Import(Zip(), ImportMode.AddAsNew);

      Assert.Single(DataStore.ScreenSetups.GetPlaces(setup));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void A_format_1_or_2_file_imports_and_keeps_the_colours_and_settings_of_this_pc(int format) {
      SeedSettings();
      DataExporter.Export(Zip());
      string older = OlderFormat(format);
      SetThisPc();

      DataImporter.Import(older, ImportMode.Replace);

      AssertThisPcUnchanged();
      Assert.Equal(format, DataImporter.Inspect(older).FormatVersion);
    }

    [Fact]
    public void A_format_2_file_still_brings_back_the_note_window_places() {
      long windowId = DataStore.Windows.GetAll().First().ID;
      Shortcut note = DataStore.Shortcuts.CreateNote(null, windowId);
      DataStore.NoteWindows.Save(new NoteWindowPlace(note.ID, 70, 80, 500, 400, NoteWindowPlace.Normal, isOpen: true) { FontSize = 18 });
      DataExporter.Export(Zip());
      string older = OlderFormat(2);
      DataStore.NoteWindows.Save(new NoteWindowPlace(note.ID, 1, 1, 300, 200, NoteWindowPlace.Normal, isOpen: false));

      DataImporter.Import(older, ImportMode.Replace);

      NoteWindowPlace place = DataStore.NoteWindows.Get(note.ID);
      Assert.Equal((70L, 80L, true, 18L), (place.Left, place.Top, place.IsOpen, place.FontSize));
    }

    [Fact]
    public void A_default_colour_that_is_not_a_colour_is_rejected() {
      SeedSettings();
      DataExporter.Export(Zip());
      string zip = MutateJson(Zip(), "data.json", n => n["defaultColors"]["Window"]["borderColor"] = "red", "bad-colour.zip");
      SetThisPc();
      DataPortabilityException ex = Assert.Throws<DataPortabilityException>(() => DataImporter.Import(zip, ImportMode.Replace));
      Assert.Contains("colour", ex.Message, StringComparison.OrdinalIgnoreCase);
      AssertThisPcUnchanged();
    }

    // ---- Backups made before a database upgrade read the old tables ----

    [Fact]
    public void A_backup_before_the_upgrade_of_a_database_without_these_tables_has_no_settings() {
      db.Dispose();
      db = new TempDb(false);
      db.Exec("PRAGMA user_version = 9;" + DatabaseV10MigrationTests.V9Sql
        + "INSERT INTO MDSHOWindows (ID,Title,ScreenDeviceName,Left,Top,Width,Height,BorderColor,BackgroundColor,TextColor,TitleBackgroundColor,TitleTextColor,Notes,SolidIcons,SingleClickToOpen) VALUES "
        + "(1,'W1','D1',0,0,300,400,'#FF000001','#FF000002','#FF000003','#FF000004','#FF000005','',1,1);");

      DataExporter.Export(Zip(), new ExportOptions { BeforeUpgrade = true });

      JsonObject data = ReadEntry(Zip(), "data.json").AsObject();
      Assert.Null(data["defaultColors"]);
      Assert.Null(data["settings"]);
    }

    [Fact]
    public void A_backup_before_the_upgrade_of_a_version_17_database_has_its_colours_and_settings() {
      db.Dispose();
      db = new TempDb(false);
      db.Exec("PRAGMA user_version = 9;" + DatabaseV10MigrationTests.V9Sql
        + "INSERT INTO MDSHOWindows (ID,Title,ScreenDeviceName,Left,Top,Width,Height,BorderColor,BackgroundColor,TextColor,TitleBackgroundColor,TitleTextColor,Notes,SolidIcons,SingleClickToOpen) VALUES "
        + "(1,'W1','D1',0,0,300,400,'#FF000001','#FF000002','#FF000003','#FF000004','#FF000005','',1,1);");
      using (SqliteConnection c = db.Open()) {
        foreach (int version in new[] { 10, 11, 12, 13, 14, 15, 16, 17 }) {
          SchemaManager.Migrations.Single(m => m.Version == version).Apply(c);
        }
      }
      db.Exec("PRAGMA user_version = 17;"
        + "INSERT INTO DefaultColors VALUES ('Note','#FF808000','#FFFFE080','#FF000000','#FFFFF8C0','#FF202020');"
        + "INSERT INTO AppSettings VALUES ('WindowTheme','Dark'),('BackupFolder','D:\\b');");

      DataExporter.Export(Zip(), new ExportOptions { BeforeUpgrade = true });

      JsonObject data = ReadEntry(Zip(), "data.json").AsObject();
      Assert.Equal("#FF808000", (string)data["defaultColors"]["Note"]["borderColor"]);
      Assert.Null(data["defaultColors"]["Window"]);
      Assert.Equal("Dark", (string)data["settings"][Theme]);
      Assert.False(data["settings"].AsObject().ContainsKey("BackupFolder"));
    }
  }

  /// <summary>After a Replace import the imported Bring panels to front shortcut is registered; keys another app holds are turned off.</summary>
  public class ImportedShortcutTests : IDisposable {
    private readonly TempDb db = new TempDb();
    private readonly FakeHotkeyService service = new FakeHotkeyService();
    private static readonly Hotkey CtrlAltK = new Hotkey(HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x4B);

    public void Dispose() { db.Dispose(); }

    private BringToFrontShortcut Started() {
      BringToFrontShortcut s = new BringToFrontShortcut(service);
      s.Start();
      return s;
    }

    [Fact]
    public void A_free_imported_shortcut_is_registered_and_kept() {
      BringToFrontShortcut s = Started();
      DataStore.AppSettings.Set(BringToFrontShortcut.SettingKey, "Ctrl+Alt+K"); // what the import wrote
      Assert.Null(s.ApplyImported());
      Assert.Equal(CtrlAltK, s.Saved);
      Assert.Equal(CtrlAltK, service.Registered);
      Assert.Equal("Ctrl+Alt+K", DataStore.AppSettings.Get(BringToFrontShortcut.SettingKey));
    }

    [Fact]
    public void An_imported_shortcut_another_app_uses_is_turned_off_and_reported() {
      BringToFrontShortcut s = Started();
      service.Taken.Add(CtrlAltK);
      DataStore.AppSettings.Set(BringToFrontShortcut.SettingKey, "Ctrl+Alt+K");

      Assert.Equal(CtrlAltK, s.ApplyImported());

      Assert.Null(s.Saved);
      Assert.Null(service.Registered);
      Assert.Equal("Off", DataStore.AppSettings.Get(BringToFrontShortcut.SettingKey));
      string message = BringToFrontShortcut.ImportedInUseMessage(CtrlAltK);
      Assert.Contains(ShortcutRecorder.InUseHint.Split('.')[0], message); // "Another app already uses this shortcut"
      Assert.Contains(CtrlAltK.DisplayText, message);
      Assert.Contains("turned off", message);
    }

    [Fact]
    public void An_imported_Off_unregisters_the_shortcut() {
      BringToFrontShortcut s = Started();
      DataStore.AppSettings.Set(BringToFrontShortcut.SettingKey, "Off");
      Assert.Null(s.ApplyImported());
      Assert.Null(s.Saved);
      Assert.Null(service.Registered);
    }

    [Fact]
    public void Without_a_change_the_shortcut_stays_registered() {
      BringToFrontShortcut s = Started();
      Assert.Null(s.ApplyImported());
      Assert.Equal(Hotkey.Default, service.Registered);
    }
  }
}
