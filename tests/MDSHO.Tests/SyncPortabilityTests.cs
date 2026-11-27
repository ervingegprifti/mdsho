using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using MDSHO.Common;
using MDSHO.Core;
using MDSHO.Data;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// 1.30.0: exports, backups and imports carry the sync data (SyncId, ChangedAt, ChangedBy, sort keys and the deleted
  /// items). A replace keeps the IDs in the file, Add as new and older files get new IDs and keys.
  /// </summary>
  public class SyncPortabilityTests : IDisposable {
    private readonly TempDb db = new TempDb();
    private readonly string work;
    private readonly long windowId;

    public SyncPortabilityTests() {
      work = Path.Combine(db.Folder, "work");
      Directory.CreateDirectory(work);
      windowId = DataStore.Windows.GetAll().Single().ID;
    }

    public void Dispose() { db.Dispose(); }

    private string Zip(string name = "export.zip") { return Path.Combine(work, name); }
    private static readonly Regex GuidText = new Regex("^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$");

    private static JsonObject ReadData(string zip) {
      using (ZipArchive z = ZipFile.OpenRead(zip))
      using (StreamReader r = new StreamReader(z.GetEntry("data.json").Open(), Encoding.UTF8)) {
        return JsonNode.Parse(r.ReadToEnd()).AsObject();
      }
    }

    private static string Rewrite(string zip, string target, Action<JsonObject> data, Action<JsonObject> manifest = null) {
      File.Copy(zip, target, true);
      using (ZipArchive z = ZipFile.Open(target, ZipArchiveMode.Update)) {
        foreach ((string name, Action<JsonObject> change) in new[] { ("data.json", data), ("manifest.json", manifest) }) {
          if (change == null) { continue; }
          ZipArchiveEntry entry = z.GetEntry(name);
          JsonObject json;
          using (StreamReader r = new StreamReader(entry.Open(), Encoding.UTF8)) { json = JsonNode.Parse(r.ReadToEnd()).AsObject(); }
          change(json);
          entry.Delete();
          using (StreamWriter w = new StreamWriter(z.CreateEntry(name).Open(), new UTF8Encoding(false))) { w.Write(json.ToJsonString()); }
        }
      }
      return target;
    }

    private (Shortcut a, Shortcut b, Shortcut c, Shortcut note) Seed() {
      Shortcut a = DataStore.Shortcuts.CreateGroup(null, windowId);
      Shortcut b = DataStore.Shortcuts.CreateSnippet(a.ID, null);
      Shortcut c = DataStore.Shortcuts.CreateProcessCommand(null, windowId);
      Shortcut note = DataStore.Shortcuts.CreateNote(null, windowId);
      DataStore.NoteWindows.Save(new NoteWindowPlace { ShortcutID = note.ID, Left = 1, Top = 2, Width = 300, Height = 200, FontSize = 18 });
      // Records from another PC, so a kept record can't be mistaken for a new one.
      db.Exec("UPDATE Shortcuts SET ChangedAt = 1000 + ID, ChangedBy = 'pc-2'; UPDATE MDSHOWindows SET ChangedAt = 7, ChangedBy = 'pc-3';");
      return (DataStore.Shortcuts.Get(a.ID), DataStore.Shortcuts.Get(b.ID), DataStore.Shortcuts.Get(c.ID), DataStore.Shortcuts.Get(note.ID));
    }

    private static List<(string SyncId, string SortKey, long ChangedAt, string ChangedBy)> ShortcutSyncData() {
      return DataStore.Shortcuts.GetAll().Select(s => (s.SyncId, s.SortKey, s.ChangedAt, s.ChangedBy)).OrderBy(x => x.SyncId).ToList();
    }

    private static List<(string SyncId, string SortKey, long ChangedAt, string ChangedBy)> WindowSyncData() {
      return DataStore.Windows.GetAll().Select(w => (w.SyncId, w.SortKey, w.ChangedAt, w.ChangedBy)).OrderBy(x => x.SyncId).ToList();
    }

    [Fact]
    public void An_export_carries_the_sync_data_and_the_deleted_items() {
      (Shortcut a, Shortcut b, Shortcut c, Shortcut note) = Seed();
      DataStore.Shortcuts.Delete(c.ID);
      DataExporter.Export(Zip());
      JsonObject data = ReadData(Zip());
      JsonObject shortcut = data["shortcuts"].AsArray().Single(s => (long)s["id"] == b.ID).AsObject();
      Assert.Equal(b.SyncId, (string)shortcut["syncId"]);
      Assert.Equal(b.SortKey, (string)shortcut["sortKey"]);
      Assert.Equal(b.ChangedAt, (long)shortcut["changedAt"]);
      Assert.Equal("pc-2", (string)shortcut["changedBy"]);
      Assert.Null(shortcut["position"]);
      JsonObject window = data["windows"].AsArray().Single().AsObject();
      Assert.Equal(DataStore.Windows.Get(windowId).SyncId, (string)window["syncId"]);
      Assert.Equal(7, (long)window["changedAt"]);
      Assert.False(string.IsNullOrEmpty((string)window["sortKey"]));
      JsonObject deleted = data["deletedItems"].AsArray().Single().AsObject();
      Assert.Equal(c.SyncId, (string)deleted["syncId"]);
      Assert.Equal("RunCommand", (string)deleted["kind"]);
      Assert.Equal(DeviceIdentity.Id, (string)deleted["deletedBy"]);
      Assert.True((long)deleted["deletedAt"] > 0);
    }

    [Fact]
    public void The_device_identity_is_never_exported() {
      DeviceIdentity.Name = "Home PC";
      string id = DeviceIdentity.Id;
      DataExporter.Export(Zip());
      string all;
      using (ZipArchive z = ZipFile.OpenRead(Zip())) {
        all = string.Join("\n", z.Entries.Select(e => { using (StreamReader r = new StreamReader(e.Open())) { return r.ReadToEnd(); } }));
      }
      // The ID is in the change records (ChangedBy), but the settings that hold the identity never go out.
      Assert.DoesNotContain("Home PC", all);
      Assert.DoesNotContain("\"" + DeviceIdentity.IdKey + "\"", all);
      Assert.DoesNotContain("\"" + DeviceIdentity.NameKey + "\"", all);
      Assert.DoesNotContain("\"" + id + "\":", all);
    }

    [Fact]
    public void A_replace_import_keeps_the_ids_keys_and_change_records_of_the_file() {
      Seed();
      var shortcuts = ShortcutSyncData();
      var windows = WindowSyncData();
      DataExporter.Export(Zip());
      DataStore.Shortcuts.UpdateName(DataStore.Shortcuts.GetAll().First().ID, "changed after the export");
      DataStore.Windows.Create("S", 0, 0);
      DataImporter.Import(Zip(), ImportMode.Replace);
      Assert.Equal(shortcuts, ShortcutSyncData());
      Assert.Equal(windows, WindowSyncData());
    }

    [Fact]
    public void A_replace_import_merges_the_deleted_items_and_forgets_the_ones_it_brings_back() {
      (Shortcut a, Shortcut b, Shortcut c, Shortcut note) = Seed();
      DataStore.Shortcuts.Delete(c.ID);       // deleted before the export: the file remembers it
      DataExporter.Export(Zip());
      DataStore.Shortcuts.Delete(note.ID);    // deleted after: the import brings it back
      Shortcut extra = DataStore.Shortcuts.CreateNote(null, windowId); // not in the file: the import deletes it
      DataImporter.Import(Zip(), ImportMode.Replace);
      Assert.Equal(new[] { c.SyncId, extra.SyncId }.OrderBy(x => x), DataStore.DeletedItems.GetAll().Select(d => d.SyncId).OrderBy(x => x));
      Assert.Contains(DataStore.Shortcuts.GetAll(), s => s.SyncId == note.SyncId);
    }

    [Fact]
    public void Add_as_new_gives_new_ids_and_change_records_puts_the_panels_last_and_ignores_the_deleted_items() {
      (Shortcut a, Shortcut b, Shortcut c, Shortcut note) = Seed();
      DataStore.Shortcuts.Delete(c.ID);
      DataExporter.Export(Zip());
      db.Exec("DELETE FROM DeletedItems");
      HashSet<string> before = new HashSet<string>(DataStore.Shortcuts.GetAll().Select(s => s.SyncId).Concat(DataStore.Windows.GetAll().Select(w => w.SyncId)));
      string lastKey = DataStore.Windows.GetAll().Last().SortKey;
      long start = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
      DataImporter.Import(Zip(), ImportMode.AddAsNew);
      List<Shortcut> added = DataStore.Shortcuts.GetAll().Where(s => !before.Contains(s.SyncId)).ToList();
      Assert.Equal(3, added.Count);
      Assert.All(added, s => {
        Assert.Matches(GuidText, s.SyncId);
        Assert.True(s.ChangedAt >= start - 1000);
        Assert.Equal(DeviceIdentity.Id, s.ChangedBy);
      });
      DesktopWindow newPanel = DataStore.Windows.GetAll().Last();
      Assert.DoesNotContain(newPanel.SyncId, before);
      Assert.True(string.CompareOrdinal(lastKey, newPanel.SortKey) < 0);
      Assert.Empty(DataStore.DeletedItems.GetAll());
      // The order inside the added panel is the file's.
      Assert.Equal(new[] { "New group", "New note" }, DataStore.Shortcuts.GetRoots(newPanel.ID).Select(s => s.Name));
    }

    // An export as MDSHO 1.29.0 wrote it: no sync data, Position numbers, database version 26.
    private string OldFile(Dictionary<string, long> positionsByName) {
      return Rewrite(Zip(), Zip("old.zip"), data => {
        foreach (JsonNode row in data["shortcuts"].AsArray().Concat(data["windows"].AsArray())) {
          JsonObject o = row.AsObject();
          foreach (string name in new[] { "syncId", "sortKey", "changedAt", "changedBy" }) { o.Remove(name); }
          if (o["name"] != null) { o["position"] = positionsByName[(string)o["name"]]; }
        }
        data.Remove("deletedItems");
      }, manifest => manifest["schemaVersion"] = 26);
    }

    [Fact]
    public void An_older_export_gets_new_ids_and_keys_in_its_old_order() {
      DataStore.Shortcuts.UpdateName(DataStore.Shortcuts.CreateNote(null, windowId).ID, "x");
      DataStore.Shortcuts.UpdateName(DataStore.Shortcuts.CreateNote(null, windowId).ID, "y");
      DataStore.Shortcuts.UpdateName(DataStore.Shortcuts.CreateNote(null, windowId).ID, "z");
      DataExporter.Export(Zip());
      string old = OldFile(new Dictionary<string, long> { ["x"] = 2, ["y"] = 0, ["z"] = 1 });
      long start = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
      DataImporter.Import(old, ImportMode.Replace);
      Assert.Equal(new[] { "y", "z", "x" }, DataStore.Shortcuts.GetRoots(windowId).Select(s => s.Name));
      List<Shortcut> rows = DataStore.Shortcuts.GetAll();
      Assert.All(rows, s => {
        Assert.Matches(GuidText, s.SyncId);
        Assert.True(SortKeys.IsValid(s.SortKey));
        Assert.True(s.ChangedAt >= start - 1000);
        Assert.Equal(DeviceIdentity.Id, s.ChangedBy);
      });
      Assert.Equal(3, rows.Select(s => s.SyncId).Distinct().Count());
      Assert.All(DataStore.Windows.GetAll(), w => Assert.True(SortKeys.IsValid(w.SortKey) && GuidText.IsMatch(w.SyncId)));
    }

    [Fact]
    public void Repeated_sync_ids_in_a_file_get_new_ones_after_the_first() {
      Shortcut a = DataStore.Shortcuts.CreateNote(null, windowId);
      DataStore.Shortcuts.CreateNote(null, windowId);
      DataExporter.Export(Zip());
      string twice = Rewrite(Zip(), Zip("twice.zip"), data => {
        foreach (JsonNode row in data["shortcuts"].AsArray()) { row["syncId"] = a.SyncId; }
      });
      DataImporter.Import(twice, ImportMode.Replace);
      List<string> ids = DataStore.Shortcuts.GetAll().Select(s => s.SyncId).ToList();
      Assert.Equal(2, ids.Distinct().Count());
      Assert.Contains(a.SyncId, ids);
    }

    [Fact]
    public void An_export_of_a_v26_database_before_its_upgrade_keeps_the_order() {
      Shortcut p = DataStore.Shortcuts.CreateNote(null, windowId);
      Shortcut q = DataStore.Shortcuts.CreateNote(null, windowId);
      Shortcut r = DataStore.Shortcuts.CreateNote(null, windowId);
      SyncFoundationTests.MakeV26(db, new Dictionary<long, long> { [p.ID] = 1, [q.ID] = 2, [r.ID] = 0 });
      DataExporter.Export(Zip(), new ExportOptions { BeforeUpgrade = true });
      DataStore.Upgrade();
      DataImporter.Import(Zip(), ImportMode.Replace);
      Assert.Equal(new[] { r.ID, p.ID, q.ID }, DataStore.Shortcuts.GetRoots(windowId).Select(s => s.ID));
      Assert.All(DataStore.Shortcuts.GetAll(), s => Assert.True(SortKeys.IsValid(s.SortKey) && GuidText.IsMatch(s.SyncId)));
    }

    [Fact]
    public void A_file_with_a_bad_sort_key_is_imported_in_its_order_with_good_keys() {
      Shortcut a = DataStore.Shortcuts.CreateNote(null, windowId);
      Shortcut b = DataStore.Shortcuts.CreateNote(null, windowId);
      DataExporter.Export(Zip());
      string bad = Rewrite(Zip(), Zip("bad.zip"), data => {
        data["shortcuts"].AsArray().Single(s => (long)s["id"] == b.ID)["sortKey"] = "not a key!";
      });
      DataImporter.Import(bad, ImportMode.Replace);
      Assert.All(DataStore.Shortcuts.GetAll(), s => Assert.True(SortKeys.IsValid(s.SortKey)));
      Assert.Equal(a.SortKey, DataStore.Shortcuts.Get(a.ID).SortKey);
    }
  }
}
