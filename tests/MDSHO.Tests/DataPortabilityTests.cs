using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using MDSHO.Common;
using MDSHO.Core;
using MDSHO.Data;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>m2: export and import of all MDSHO data (zip with manifest.json, data.json and the shortcut files).</summary>
  public class DataPortabilityTests : IDisposable {
    private readonly TempDb db = new TempDb();
    private readonly string work;

    public DataPortabilityTests() {
      work = Path.Combine(db.Folder, "work");
      Directory.CreateDirectory(work);
    }

    public void Dispose() { db.Dispose(); }

    private string Zip(string name = "export.zip") { return Path.Combine(work, name); }
    private static string ShortcutsDir { get { return Paths.GetShortcutsPath(); } }

    #region HELPERS
    private Shortcut FileShortcut(long? parentShortcut, long? parentWindow, string name) {
      string guid = Identifiers.NewGuid();
      Shortcut s = DataStore.Shortcuts.Create(parentShortcut, parentWindow, name, @"C:\Target\" + name + ".exe", ".lnk", guid);
      File.WriteAllText(Path.Combine(ShortcutsDir, guid + ".lnk"), "content of " + name);
      return s;
    }

    /// <summary>W1 (clone, roots: g[c, d], a, b — reordered), W2 (root e, root m with a missing file).</summary>
    private void Seed() {
      foreach (DesktopWindow existing in DataStore.Windows.GetAll()) { DataStore.Windows.Delete(existing.ID); } // the new database has one default window
      DesktopWindow w1 = DataStore.Windows.Create("SCREEN1", 10, 20);
      DataStore.Windows.UpdateTitle(w1.ID, "W1");
      DesktopWindow w2 = DataStore.Windows.Create("SCREEN2", 30, 40);
      DataStore.Windows.UpdateTitle(w2.ID, "W2");
      DataStore.Clones.Create(w1.ID, "SCREEN2", 5, 6);
      Shortcut g = DataStore.Shortcuts.CreateGroup(null, w1.ID);
      Shortcut a = FileShortcut(null, w1.ID, "a");
      Shortcut b = FileShortcut(null, w1.ID, "b");
      FileShortcut(g.ID, null, "c");
      FileShortcut(g.ID, null, "d");
      DataStore.Shortcuts.UpdatePositions(new[] { b.ID, a.ID, g.ID });
      FileShortcut(null, w2.ID, "e");
      Shortcut m = FileShortcut(null, w2.ID, "m");
      File.Delete(Path.Combine(ShortcutsDir, m.Guid + m.Ext));
    }

    private static readonly JsonSerializerOptions Json = new JsonSerializerOptions { WriteIndented = false };

    private static List<string> Snapshot() {
      List<string> rows = new List<string>();
      foreach (DesktopWindow w in DataStore.Windows.GetAll().OrderBy(x => x.ID)) { rows.Add("W" + JsonSerializer.Serialize(new { w.ID, w.Title, w.ScreenDeviceName, w.Left, w.Top, w.Width, w.Height, w.BorderColor, w.BackgroundColor, w.TextColor, w.TitleBackgroundColor, w.TitleTextColor, w.Notes, w.SolidIcons, w.SingleClickToOpen }, Json)); }
      foreach (DesktopWindowClone c in DataStore.Clones.GetAll().OrderBy(x => x.ID)) { rows.Add("C" + JsonSerializer.Serialize(new { c.ID, c.ParentWindowID, c.ScreenDeviceName, c.Left, c.Top, c.Width, c.Height }, Json)); }
      foreach (Shortcut s in DataStore.Shortcuts.GetAll().OrderBy(x => x.ID)) { rows.Add("S" + JsonSerializer.Serialize(new { s.ID, s.ParentShortcutID, s.ParentWindowID, s.Name, s.Target, s.Ext, s.Guid, s.IsGroup, s.IsGroupExpanded, s.Comment, s.IsMarked, s.MarkColor, s.Position, s.IsProcessCommand, s.ProcessFileName, s.ProcessArguments }, Json)); }
      return rows;
    }

    private static Dictionary<string, string> Files() {
      return Directory.GetFiles(ShortcutsDir).ToDictionary(Path.GetFileName, File.ReadAllText);
    }

    private string[] TempFolders() { return Directory.GetDirectories(db.Folder, "ImportTemp*"); }

    private static string ReadEntry(string zip, string name) {
      using (ZipArchive z = ZipFile.OpenRead(zip)) {
        using (StreamReader r = new StreamReader(z.GetEntry(name).Open(), Encoding.UTF8)) { return r.ReadToEnd(); }
      }
    }

    /// <summary>Copies the zip with the entry's text replaced (or removed when the function returns null).</summary>
    private string Mutate(string source, string entryName, Func<string, string> change, string target = "mutated.zip") {
      string dest = Zip(target);
      if (File.Exists(dest)) { File.Delete(dest); }
      using (ZipArchive src = ZipFile.OpenRead(source))
      using (ZipArchive dst = ZipFile.Open(dest, ZipArchiveMode.Create)) {
        foreach (ZipArchiveEntry e in src.Entries) {
          string text = null;
          using (Stream s = e.Open()) using (MemoryStream ms = new MemoryStream()) { s.CopyTo(ms); text = Encoding.UTF8.GetString(ms.ToArray()); }
          if (e.FullName == entryName) {
            text = change(text);
            if (text == null) { continue; }
          }
          ZipArchiveEntry n = dst.CreateEntry(e.FullName);
          using (StreamWriter w = new StreamWriter(n.Open(), new UTF8Encoding(false))) { w.Write(text); }
        }
      }
      return dest;
    }

    private string MutateJson(string source, string entryName, Action<JsonNode> change, string target = "mutated.zip") {
      return Mutate(source, entryName, text => { JsonNode n = JsonNode.Parse(text); change(n); return n.ToJsonString(); }, target);
    }

    private string AddEntry(string source, string entryName, string content) {
      string dest = Zip("added.zip");
      File.Copy(source, dest, true);
      using (ZipArchive z = ZipFile.Open(dest, ZipArchiveMode.Update)) {
        using (StreamWriter w = new StreamWriter(z.CreateEntry(entryName).Open())) { w.Write(content); }
      }
      return dest;
    }

    /// <summary>The import must throw a DataPortabilityException, in both modes, and change nothing.</summary>
    private DataPortabilityException AssertRejected(string zip, string messagePart = null) {
      List<string> rows = Snapshot();
      Dictionary<string, string> files = Files();
      DataPortabilityException first = null;
      foreach (ImportMode mode in new[] { ImportMode.Replace, ImportMode.AddAsNew }) {
        DataPortabilityException ex = Assert.Throws<DataPortabilityException>(() => DataImporter.Import(zip, mode));
        Assert.False(string.IsNullOrWhiteSpace(ex.Message));
        if (messagePart != null) { Assert.Contains(messagePart, ex.Message, StringComparison.OrdinalIgnoreCase); }
        first = first ?? ex;
      }
      Assert.Throws<DataPortabilityException>(() => DataImporter.Inspect(zip));
      Assert.Equal(rows, Snapshot());
      Assert.Equal(files, Files());
      Assert.Empty(TempFolders());
      Assert.False(Directory.Exists(Paths.GetImportBackupsPath()) && Directory.GetFiles(Paths.GetImportBackupsPath()).Length > 0, "no backup for a rejected file");
      return first;
    }
    #endregion HELPERS

    #region EXPORT
    [Fact]
    public void Default_file_name_has_timestamp() {
      Assert.Equal("MDSHO-export-2026-03-09-0705.zip", DataExporter.DefaultFileName(new DateTime(2026, 3, 9, 7, 5, 59)));
    }

    [Fact]
    public void Export_writes_manifest_data_and_files() {
      Seed();
      ExportManifest m = DataExporter.Export(Zip());
      Assert.False(File.Exists(Zip() + ".tmp"));
      Assert.Equal("MDSHO-export", m.Format);
      Assert.Equal(1, m.FormatVersion);
      Assert.Equal(AppConstants.DatabaseVersion, m.SchemaVersion);
      Assert.Equal(2, m.Counts.Windows);
      Assert.Equal(1, m.Counts.Clones);
      Assert.Equal(7, m.Counts.Shortcuts);
      Assert.Equal(5, m.Counts.Files);
      Assert.Single(m.MissingFiles);

      using (ZipArchive z = ZipFile.OpenRead(Zip())) {
        List<string> names = z.Entries.Select(e => e.FullName).ToList();
        Assert.Contains("manifest.json", names);
        Assert.Contains("data.json", names);
        Assert.Equal(5, names.Count(n => n.StartsWith("shortcuts/")));
      }
      JsonNode manifest = JsonNode.Parse(ReadEntry(Zip(), "manifest.json"));
      Assert.Equal("MDSHO-export", (string)manifest["format"]);
      Assert.Equal(1, (int)manifest["formatVersion"]);
      Assert.Equal(AppConstants.DatabaseVersion, (int)manifest["schemaVersion"]);
      Assert.Equal(7, (int)manifest["counts"]["shortcuts"]);
      Assert.Equal(1, manifest["missingFiles"].AsArray().Count);
      Assert.False(string.IsNullOrEmpty((string)manifest["appVersion"]));
      JsonNode data = JsonNode.Parse(ReadEntry(Zip(), "data.json"));
      Assert.Equal(2, data["windows"].AsArray().Count);
      Assert.Equal(1, data["clones"].AsArray().Count);
      Assert.Equal(7, data["shortcuts"].AsArray().Count);
      Assert.NotNull(data["shortcuts"][0]["parentMDSHOWindowID"] ?? data["shortcuts"][0]["parentShortcutID"]);
      Assert.Contains("\n", ReadEntry(Zip(), "data.json")); // indented
      Assert.DoesNotContain(db.Folder, ReadEntry(Zip(), "data.json")); // no machine paths
    }

    [Fact]
    public void Export_overwrites_existing_file_and_failure_leaves_it_alone() {
      Seed();
      File.WriteAllText(Zip(), "old");
      DataExporter.Export(Zip());
      Assert.True(new FileInfo(Zip()).Length > 10);
      Assert.Throws<DirectoryNotFoundException>(() => DataExporter.Export(Path.Combine(work, "nodir", "x.zip")));
    }

    [Fact]
    public void Export_of_empty_database_works() {
      ExportManifest m = DataExporter.Export(Zip());
      Assert.Equal(0, m.Counts.Shortcuts);
      Assert.Equal(m.Counts.Windows, DataImporter.Inspect(Zip()).Counts.Windows);
    }
    #endregion EXPORT

    #region REPLACE
    [Fact]
    public void Replace_roundtrip_reproduces_rows_ids_positions_and_files() {
      Seed();
      List<string> rows = Snapshot();
      Dictionary<string, string> files = Files();
      DataExporter.Export(Zip());

      // Change everything afterwards.
      foreach (DesktopWindow w in DataStore.Windows.GetAll()) { DataStore.Windows.Delete(w.ID); }
      DataStore.Windows.Create("SCREEN9", 1, 1);
      foreach (string f in Directory.GetFiles(ShortcutsDir)) { File.Delete(f); }
      File.WriteAllText(Path.Combine(ShortcutsDir, "stray.lnk"), "stray");

      ImportResult r = DataImporter.Import(Zip(), ImportMode.Replace);

      Assert.Equal(rows, Snapshot());
      Assert.Equal(files, Files());
      Assert.Empty(TempFolders());
      // Tree order (Position) and clone link.
      DesktopWindow w1 = DataStore.Windows.LoadAll().Single(w => w.Title == "W1");
      Assert.Equal(new[] { "b", "a", "New group" }, w1.RootShortcuts.Select(s => s.Name).ToArray());
      Assert.Single(w1.Clones);
      Assert.Equal(new[] { "c", "d" }, w1.RootShortcuts[2].Shortcuts.Select(s => s.Name).ToArray());
      Assert.NotNull(r);
    }

    [Fact]
    public void Replace_creates_backup_of_previous_data_first() {
      Seed();
      DataExporter.Export(Zip());
      List<string> before = Snapshot();
      DataStore.Windows.Create("SCREEN9", 1, 1);
      List<string> beforeImport = Snapshot();
      ImportResult r = DataImporter.Import(Zip(), ImportMode.Replace);
      Assert.NotNull(r.BackupPath);
      Assert.True(File.Exists(r.BackupPath));
      Assert.StartsWith(Paths.GetImportBackupsPath(), r.BackupPath, StringComparison.OrdinalIgnoreCase);
      Assert.StartsWith("MDSHO-before-import-", Path.GetFileName(r.BackupPath));
      Assert.EndsWith(".zip", r.BackupPath);
      Assert.Equal(DataStore.Windows.GetAll().Count + 1, DataImporter.Inspect(r.BackupPath).Counts.Windows);
      // Restoring the backup brings back the state before the import.
      DataImporter.Import(r.BackupPath, ImportMode.Replace);
      Assert.Equal(beforeImport, Snapshot());
      Assert.NotEqual(before, beforeImport);
    }

    [Fact]
    public void Replace_into_a_fresh_empty_install_works() {
      Seed();
      List<string> rows = Snapshot();
      Dictionary<string, string> files = Files();
      DataExporter.Export(Zip());
      foreach (DesktopWindow w in DataStore.Windows.GetAll()) { DataStore.Windows.Delete(w.ID); }
      foreach (string f in Directory.GetFiles(ShortcutsDir)) { File.Delete(f); }
      DataImporter.Import(Zip(), ImportMode.Replace);
      Assert.Equal(rows, Snapshot());
      Assert.Equal(files, Files());
    }

    [Fact]
    public void Missing_shortcut_file_is_recorded_not_fatal_and_stays_missing() {
      Seed();
      ExportManifest m = DataExporter.Export(Zip());
      Assert.Single(m.MissingFiles);
      DataImporter.Import(Zip(), ImportMode.Replace);
      Assert.False(File.Exists(Path.Combine(ShortcutsDir, m.MissingFiles[0])));
      Assert.Equal(7, DataStore.Shortcuts.GetAll().Count);
    }
    #endregion REPLACE

    #region MERGE
    [Fact]
    public void Merge_adds_with_new_ids_and_guids_remaps_links_and_keeps_originals() {
      Seed();
      List<string> originalRows = Snapshot();
      Dictionary<string, string> originalFiles = Files();
      List<long> oldWindowIds = DataStore.Windows.GetAll().Select(w => w.ID).ToList();
      List<long> oldShortcutIds = DataStore.Shortcuts.GetAll().Select(s => s.ID).ToList();
      DataExporter.Export(Zip());

      ImportResult r = DataImporter.Import(Zip(), ImportMode.AddAsNew);

      Assert.Null(r.BackupPath);
      List<string> after = Snapshot();
      Assert.Equal(originalRows, after.Where(x => originalRows.Contains(x)).ToList()); // originals untouched
      Assert.Equal(4, DataStore.Windows.GetAll().Count);
      Assert.Equal(2, DataStore.Clones.GetAll().Count);
      Assert.Equal(14, DataStore.Shortcuts.GetAll().Count);
      List<DesktopWindow> windows = DataStore.Windows.LoadAll();
      List<DesktopWindow> added = windows.Where(w => !oldWindowIds.Contains(w.ID)).ToList();
      Assert.Equal(2, added.Count);
      DesktopWindow nw1 = added.Single(w => w.Title == "W1");
      Assert.Single(nw1.Clones);
      Assert.Equal(nw1.ID, nw1.Clones[0].ParentWindowID);
      Assert.Equal(new[] { "b", "a", "New group" }, nw1.RootShortcuts.Select(s => s.Name).ToArray());
      Assert.Equal(new[] { "c", "d" }, nw1.RootShortcuts[2].Shortcuts.Select(s => s.Name).ToArray());
      Assert.Equal(nw1.RootShortcuts[2].ID, nw1.RootShortcuts[2].Shortcuts[0].ParentShortcutID);

      List<Shortcut> newShortcuts = DataStore.Shortcuts.GetAll().Where(s => !oldShortcutIds.Contains(s.ID)).ToList();
      Assert.Equal(7, newShortcuts.Count);
      HashSet<string> oldGuids = new HashSet<string>(DataStore.Shortcuts.GetAll().Where(s => oldShortcutIds.Contains(s.ID)).Select(s => s.Guid));
      foreach (Shortcut s in newShortcuts) {
        Assert.DoesNotContain(s.Guid, oldGuids);
        Assert.True(s.ParentShortcutID != null ^ s.ParentWindowID != null);
        if (s.ParentWindowID != null) { Assert.Contains(added, w => w.ID == s.ParentWindowID); }
        if (s.ParentShortcutID != null) { Assert.Contains(newShortcuts, p => p.ID == s.ParentShortcutID); }
        if (s.Name == "m") { Assert.False(File.Exists(Path.Combine(ShortcutsDir, s.Guid + s.Ext))); }
        else if (s.IsGroup == 0) { Assert.Equal("content of " + s.Name, File.ReadAllText(Path.Combine(ShortcutsDir, s.Guid + s.Ext))); }
      }
      // Original files untouched, 5 new ones added.
      Dictionary<string, string> filesAfter = Files();
      Assert.All(originalFiles, kv => Assert.Equal(kv.Value, filesAfter[kv.Key]));
      Assert.Equal(originalFiles.Count + 5, filesAfter.Count);
      Assert.Empty(TempFolders());
    }

    [Theory]
    [InlineData(ImportMode.Replace)]
    [InlineData(ImportMode.AddAsNew)]
    public void Import_gives_shortcuts_that_share_a_guid_their_own_guid_and_shortcut_file(ImportMode mode) {
      foreach (DesktopWindow existing in DataStore.Windows.GetAll()) { DataStore.Windows.Delete(existing.ID); }
      DesktopWindow w = DataStore.Windows.Create("SCREEN1", 0, 0);
      Shortcut a = FileShortcut(null, w.ID, "a");
      FileShortcut(null, w.ID, "b");
      DataExporter.Export(Zip());
      // b points at a's GUID: both rows share one shortcut file.
      string zip = MutateJson(Zip(), "data.json", n => n["shortcuts"].AsArray().First(x => (string)x["name"] == "b")["guid"] = a.Guid);
      HashSet<long> before = new HashSet<long>(mode == ImportMode.AddAsNew ? DataStore.Shortcuts.GetAll().Select(s => s.ID) : Enumerable.Empty<long>());

      DataImporter.Import(zip, mode);

      List<Shortcut> imported = DataStore.Shortcuts.GetAll().Where(s => !before.Contains(s.ID)).OrderBy(s => s.Name).ToList();
      Assert.Equal(new[] { "a", "b" }, imported.Select(s => s.Name).ToArray());
      Assert.NotEqual(imported[0].Guid, imported[1].Guid, StringComparer.OrdinalIgnoreCase);
      string fileA = Path.Combine(ShortcutsDir, imported[0].Guid + imported[0].Ext);
      string fileB = Path.Combine(ShortcutsDir, imported[1].Guid + imported[1].Ext);
      Assert.Equal("content of a", File.ReadAllText(fileA));
      Assert.Equal("content of a", File.ReadAllText(fileB));
      // Independent: deleting one shortcut's file leaves the other working.
      File.Delete(fileA);
      Assert.True(File.Exists(fileB));
      Assert.Empty(TempFolders());
    }

    [Fact]
    public void Merge_twice_keeps_adding() {
      Seed();
      DataExporter.Export(Zip());
      DataImporter.Import(Zip(), ImportMode.AddAsNew);
      DataImporter.Import(Zip(), ImportMode.AddAsNew);
      Assert.Equal(6, DataStore.Windows.GetAll().Count);
      Assert.Equal(21, DataStore.Shortcuts.GetAll().Count);
      Assert.Equal(21, DataStore.Shortcuts.GetAll().Select(s => s.Guid).Distinct().Count());
    }
    #endregion MERGE

    #region VALIDATION
    [Fact]
    public void Inspect_returns_summary() {
      Seed();
      DataExporter.Export(Zip());
      ExportManifest m = DataImporter.Inspect(Zip());
      Assert.Equal(2, m.Counts.Windows);
      Assert.Equal(1, m.Counts.Clones);
      Assert.Equal(7, m.Counts.Shortcuts);
      Assert.Equal("1.2.4", m.AppVersion);
      Assert.Equal(DateTimeKind.Utc, m.ExportedAtUtc.Kind);
    }

    [Fact]
    public void Not_a_zip_is_rejected() {
      Seed();
      File.WriteAllText(Zip("bad.zip"), "this is not a zip");
      AssertRejected(Zip("bad.zip"));
    }

    [Fact]
    public void Missing_file_is_rejected() {
      Seed();
      AssertRejected(Path.Combine(work, "nope.zip"));
    }

    [Fact]
    public void Missing_manifest_is_rejected() {
      Seed();
      DataExporter.Export(Zip());
      AssertRejected(Mutate(Zip(), "manifest.json", t => null), "manifest");
    }

    [Fact]
    public void Missing_data_is_rejected() {
      Seed();
      DataExporter.Export(Zip());
      AssertRejected(Mutate(Zip(), "data.json", t => null), "data.json");
    }

    [Fact]
    public void Wrong_format_is_rejected() {
      Seed();
      DataExporter.Export(Zip());
      AssertRejected(MutateJson(Zip(), "manifest.json", n => n["format"] = "Other"), "format");
    }

    [Fact]
    public void Unsupported_format_version_is_rejected() {
      Seed();
      DataExporter.Export(Zip());
      AssertRejected(MutateJson(Zip(), "manifest.json", n => n["formatVersion"] = 2), "formatVersion");
    }

    [Fact]
    public void Newer_schema_is_rejected_with_update_hint() {
      Seed();
      DataExporter.Export(Zip());
      AssertRejected(MutateJson(Zip(), "manifest.json", n => n["schemaVersion"] = AppConstants.DatabaseVersion + 1), "newer");
    }

    [Fact]
    public void Older_schema_is_accepted() {
      Seed();
      DataExporter.Export(Zip());
      string older = MutateJson(Zip(), "manifest.json", n => n["schemaVersion"] = 2);
      DataImporter.Import(older, ImportMode.Replace);
      Assert.Equal(2, DataStore.Windows.GetAll().Count);
    }

    [Fact]
    public void Invalid_json_is_rejected() {
      Seed();
      DataExporter.Export(Zip());
      AssertRejected(Mutate(Zip(), "data.json", t => "{ not json"));
      AssertRejected(Mutate(Zip(), "manifest.json", t => "{ not json", "m2.zip"));
    }

    [Fact]
    public void Broken_shortcut_parent_link_is_rejected() {
      Seed();
      DataExporter.Export(Zip());
      AssertRejected(MutateJson(Zip(), "data.json", n => {
        JsonNode s = n["shortcuts"].AsArray().First(x => x["parentShortcutID"] != null);
        s["parentShortcutID"] = 99999;
      }), "parent");
    }

    [Fact]
    public void Broken_shortcut_window_link_is_rejected() {
      Seed();
      DataExporter.Export(Zip());
      AssertRejected(MutateJson(Zip(), "data.json", n => {
        JsonNode s = n["shortcuts"].AsArray().First(x => x["parentMDSHOWindowID"] != null);
        s["parentMDSHOWindowID"] = 99999;
      }), "parent");
    }

    [Fact]
    public void Shortcut_with_two_parents_or_none_is_rejected() {
      Seed();
      DataExporter.Export(Zip());
      AssertRejected(MutateJson(Zip(), "data.json", n => {
        JsonNode child = n["shortcuts"].AsArray().First(x => x["parentShortcutID"] != null);
        child["parentMDSHOWindowID"] = n["windows"][0]["id"].GetValue<long>();
      }), "parent");
      AssertRejected(MutateJson(Zip(), "data.json", n => {
        JsonNode root = n["shortcuts"].AsArray().First(x => x["parentMDSHOWindowID"] != null);
        root["parentMDSHOWindowID"] = null;
      }, "m2.zip"), "parent");
    }

    [Fact]
    public void Broken_clone_link_is_rejected() {
      Seed();
      DataExporter.Export(Zip());
      AssertRejected(MutateJson(Zip(), "data.json", n => n["clones"][0]["parentMDSHOWindowID"] = 99999), "clone");
    }

    [Fact]
    public void Duplicate_ids_are_rejected() {
      Seed();
      DataExporter.Export(Zip());
      AssertRejected(MutateJson(Zip(), "data.json", n => n["windows"][1]["id"] = n["windows"][0]["id"].GetValue<long>()), "duplicate");
    }

    [Fact]
    public void Shortcut_cycle_is_rejected() {
      Seed();
      DataExporter.Export(Zip());
      AssertRejected(MutateJson(Zip(), "data.json", n => {
        JsonArray all = n["shortcuts"].AsArray();
        JsonNode group = all.First(x => (long)x["isGroup"] == 1);
        JsonNode child = all.First(x => x["parentShortcutID"] != null && x["parentShortcutID"].GetValue<long>() == group["id"].GetValue<long>());
        group["parentMDSHOWindowID"] = null;
        group["parentShortcutID"] = child["id"].GetValue<long>();
      }), "cycle");
    }

    [Fact]
    public void Zip_slip_entries_are_rejected() {
      Seed();
      DataExporter.Export(Zip());
      foreach (string evil in new[] { "../evil.txt", "shortcuts/../../evil.lnk", "/abs.lnk", "C:/abs.lnk", "shortcuts\\..\\..\\evil.lnk" }) {
        AssertRejected(AddEntry(Zip(), evil, "x"), "unsafe");
      }
      Assert.False(File.Exists(Path.Combine(db.Folder, "evil.txt")));
      Assert.False(File.Exists(Path.Combine(db.Folder, "evil.lnk")));
    }

    [Fact]
    public void Unsafe_guid_or_extension_in_data_is_rejected() {
      Seed();
      DataExporter.Export(Zip());
      AssertRejected(MutateJson(Zip(), "data.json", n => n["shortcuts"].AsArray().First(x => (long)x["isGroup"] == 0)["guid"] = @"..\..\evil"), "unsafe");
      AssertRejected(MutateJson(Zip(), "data.json", n => n["shortcuts"].AsArray().First(x => (long)x["isGroup"] == 0)["ext"] = @"\..\x.lnk", "m2.zip"), "unsafe");
    }

    [Fact]
    public void Oversized_import_is_rejected() {
      Seed();
      DataExporter.Export(Zip());
      long old = DataImporter.MaxTotalBytes;
      DataImporter.MaxTotalBytes = 200;
      try {
        AssertRejected(Zip(), "large");
      } finally {
        DataImporter.MaxTotalBytes = old;
      }
    }
    #endregion VALIDATION

    #region FAILURE
    [Fact]
    public void Failure_during_replace_rolls_back_database_files_and_temp() {
      Seed();
      DataExporter.Export(Zip());
      DataStore.Windows.Create("SCREEN9", 1, 1); // so rollback differs from "replaced"
      List<string> rows = Snapshot();
      Dictionary<string, string> files = Files();
      db.Exec("CREATE TRIGGER boom BEFORE INSERT ON Shortcuts WHEN NEW.Name = 'e' BEGIN SELECT RAISE(ABORT, 'boom'); END;");

      Assert.ThrowsAny<Exception>(() => DataImporter.Import(Zip(), ImportMode.Replace));

      Assert.Equal(rows, Snapshot());
      Assert.Equal(files, Files());
      Assert.Empty(TempFolders());
    }

    [Fact]
    public void Failure_during_merge_rolls_back_database_files_and_temp() {
      Seed();
      DataExporter.Export(Zip());
      List<string> rows = Snapshot();
      Dictionary<string, string> files = Files();
      db.Exec("CREATE TRIGGER boom BEFORE INSERT ON Shortcuts WHEN NEW.Name = 'e' BEGIN SELECT RAISE(ABORT, 'boom'); END;");

      Assert.ThrowsAny<Exception>(() => DataImporter.Import(Zip(), ImportMode.AddAsNew));

      Assert.Equal(rows, Snapshot());
      Assert.Equal(files, Files());
      Assert.Empty(TempFolders());
    }
    #endregion FAILURE

    #region REPOSITORY ID INSERT
    [Fact]
    public void Add_with_explicit_id_keeps_the_id_and_zero_means_auto() {
      DesktopWindow w = DataStore.Windows.Get(DataStore.Windows.GetAll()[0].ID);
      w.ID = 500;
      Assert.Equal(500, DataStore.Windows.Add(w));
      Assert.NotNull(DataStore.Windows.Get(500));
      w.ID = 0;
      long auto = DataStore.Windows.Add(w);
      Assert.True(auto > 500);
    }
    #endregion REPOSITORY ID INSERT
  }
}
