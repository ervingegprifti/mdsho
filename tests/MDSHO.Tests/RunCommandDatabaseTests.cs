using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using MDSHO.Common;
using MDSHO.Core;
using MDSHO.Data;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// Database version 4: run commands get "run as administrator" and a working folder (Shortcuts.ProcessRunAsAdmin,
  /// Shortcuts.ProcessWorkingFolder). They are stored, copied, exported and imported like the other columns; older
  /// databases are upgraded and older export files still import with the defaults.
  /// </summary>
  public class RunCommandDatabaseTests : IDisposable {
    private TempDb db;
    public void Dispose() { if (db != null) { db.Dispose(); } }

    private static IShortcutRepository Shortcuts { get { return DataStore.Shortcuts; } }

    private long Column(string name) {
      return db.Scalar("SELECT count(*) FROM pragma_table_info('Shortcuts') WHERE name = '" + name + "'");
    }

    // ---- Schema and migration ----

    [Fact]
    public void Database_version_is_at_least_4() {
      Assert.True(AppConstants.DatabaseVersion >= 4);
    }

    [Fact]
    public void New_database_has_the_two_columns_at_the_latest_version() {
      db = new TempDb();
      Assert.Equal(AppConstants.DatabaseVersion, db.Scalar("PRAGMA user_version"));
      Assert.Equal(1, Column("ProcessRunAsAdmin"));
      Assert.Equal(1, Column("ProcessWorkingFolder"));
      Assert.Equal(4, SchemaManager.Migrations.Single(m => m.Version == 4).Version);
    }

    [Fact]
    public void Version_3_database_is_upgraded_with_defaults_and_a_backup() {
      db = new TempDb();
      long win = DataStore.Windows.LoadAll()[0].ID;
      long id = Shortcuts.CreateProcessCommand(null, win).ID;
      // Turn it back into a version 3 database (as 1.1.0 wrote it).
      db.Exec("ALTER TABLE Shortcuts DROP COLUMN ProcessRunAsAdmin; ALTER TABLE Shortcuts DROP COLUMN ProcessWorkingFolder; PRAGMA user_version = 3;");
      Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
      Assert.Equal(0, Column("ProcessRunAsAdmin"));

      DataStore.Upgrade();

      Assert.Equal(AppConstants.DatabaseVersion, db.Scalar("PRAGMA user_version"));
      Assert.Equal(0, db.Scalar("SELECT ProcessRunAsAdmin FROM Shortcuts WHERE ID=" + id));
      Assert.Equal(0, db.Scalar("SELECT length(ProcessWorkingFolder) FROM Shortcuts WHERE ID=" + id));
      Assert.Equal(1, db.Scalar("SELECT \"notnull\" FROM pragma_table_info('Shortcuts') WHERE name='ProcessRunAsAdmin'"));
      Assert.Equal(1, db.Scalar("SELECT \"notnull\" FROM pragma_table_info('Shortcuts') WHERE name='ProcessWorkingFolder'"));
      string bak = Assert.Single(Directory.GetFiles(db.Folder, "test.sqlite3.bak-v*"));
      Assert.Contains("bak-v3-", bak);
      Shortcut row = Shortcuts.Get(id);
      Assert.Equal(0, row.ProcessRunAsAdmin);
      Assert.Equal("", row.ProcessWorkingFolder);
    }

    [Fact]
    public void Database_newer_than_the_latest_version_is_still_refused() {
      db = new TempDb();
      db.Exec("PRAGMA user_version = " + (AppConstants.DatabaseVersion + 1) + ";");
      Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
      DatabaseTooNewException ex = Assert.Throws<DatabaseTooNewException>(() => DataStore.Upgrade());
      Assert.Equal(AppConstants.DatabaseVersion, ex.SupportedVersion);
    }

    // ---- Repository ----

    [Fact]
    public void New_rows_default_to_not_admin_and_no_folder() {
      db = new TempDb();
      long win = DataStore.Windows.LoadAll()[0].ID;
      foreach (Shortcut s in new[] { Shortcuts.CreateProcessCommand(null, win), Shortcuts.CreateGroup(null, win), Shortcuts.Create(null, win, "n", "t", ".lnk", "g") }) {
        Assert.Equal(0, s.ProcessRunAsAdmin);
        Assert.Equal("", s.ProcessWorkingFolder);
      }
    }

    [Fact]
    public void Shortcut_record_defaults_are_safe_for_json_without_the_fields() {
      Shortcut s = new Shortcut();
      Assert.Equal(0, s.ProcessRunAsAdmin);
      Assert.Equal("", s.ProcessWorkingFolder);
    }

    [Fact]
    public void Add_Update_and_Append_store_both_fields() {
      db = new TempDb();
      long win = DataStore.Windows.LoadAll()[0].ID;
      Shortcut s = Shortcuts.CreateProcessCommand(null, win);
      s.ProcessRunAsAdmin = 1;
      s.ProcessWorkingFolder = @"C:\Work";
      Shortcuts.Update(s);
      Shortcut read = Shortcuts.Get(s.ID);
      Assert.Equal((1L, @"C:\Work"), (read.ProcessRunAsAdmin, read.ProcessWorkingFolder));

      Shortcut copy = Shortcuts.Append(read);
      Assert.NotEqual(read.ID, copy.ID);
      Assert.Equal((1L, @"C:\Work"), (copy.ProcessRunAsAdmin, copy.ProcessWorkingFolder));

      Shortcut added = new Shortcut(null, win, "x", "", "", "gx", 0, 0, "", 0, "#FFFFFFFF", 9, 1, "cmd.exe", "/K x",
        processRunAsAdmin: 1, processWorkingFolder: @"D:\Logs");
      Shortcuts.Add(added);
      Assert.Equal((1L, @"D:\Logs"), (Shortcuts.Get(added.ID).ProcessRunAsAdmin, Shortcuts.Get(added.ID).ProcessWorkingFolder));
      Assert.Contains(Shortcuts.GetRoots(win), r => r.ID == added.ID && r.ProcessWorkingFolder == @"D:\Logs");
    }

    [Fact]
    public void Single_field_updates_save_admin_and_folder() {
      db = new TempDb();
      long win = DataStore.Windows.LoadAll()[0].ID;
      Shortcut s = Shortcuts.CreateProcessCommand(null, win);
      Shortcuts.UpdateProcessRunAsAdmin(s.ID, true);
      Shortcuts.UpdateProcessWorkingFolder(s.ID, @"C:\Temp\{}");
      Shortcut read = Shortcuts.Get(s.ID);
      Assert.Equal((1L, @"C:\Temp\{}"), (read.ProcessRunAsAdmin, read.ProcessWorkingFolder));
      Shortcuts.UpdateProcessRunAsAdmin(s.ID, false);
      Assert.Equal(0, Shortcuts.Get(s.ID).ProcessRunAsAdmin);
    }

    [Fact]
    public void LoadAll_tree_carries_both_fields() {
      db = new TempDb();
      long win = DataStore.Windows.LoadAll()[0].ID;
      Shortcut g = Shortcuts.CreateGroup(null, win);
      Shortcut c = Shortcuts.CreateProcessCommand(g.ID, null);
      Shortcuts.UpdateProcessRunAsAdmin(c.ID, true);
      Shortcuts.UpdateProcessWorkingFolder(c.ID, @"C:\W");
      Shortcut loaded = DataStore.Windows.LoadAll()[0].RootShortcuts.Single(r => r.ID == g.ID).Shortcuts.Single();
      Assert.Equal((1L, @"C:\W"), (loaded.ProcessRunAsAdmin, loaded.ProcessWorkingFolder));
    }

    // ---- Export / import ----

    private static string ReadEntry(string zip, string name) {
      using (ZipArchive z = ZipFile.OpenRead(zip))
      using (StreamReader r = new StreamReader(z.GetEntry(name).Open(), Encoding.UTF8)) {
        return r.ReadToEnd();
      }
    }

    /// <summary>Copies the zip with data.json and manifest.json changed.</summary>
    private static string Rewrite(string source, string dest, Action<JsonNode> changeData, Action<JsonNode> changeManifest) {
      using (ZipArchive src = ZipFile.OpenRead(source))
      using (ZipArchive dst = ZipFile.Open(dest, ZipArchiveMode.Create)) {
        foreach (ZipArchiveEntry e in src.Entries) {
          byte[] bytes;
          using (Stream s = e.Open()) using (MemoryStream ms = new MemoryStream()) { s.CopyTo(ms); bytes = ms.ToArray(); }
          if (e.FullName == "data.json" || e.FullName == "manifest.json") {
            JsonNode n = JsonNode.Parse(Encoding.UTF8.GetString(bytes));
            (e.FullName == "data.json" ? changeData : changeManifest)(n);
            bytes = new UTF8Encoding(false).GetBytes(n.ToJsonString());
          }
          using (Stream w = dst.CreateEntry(e.FullName).Open()) { w.Write(bytes, 0, bytes.Length); }
        }
      }
      return dest;
    }

    [Fact]
    public void Export_writes_both_fields_and_import_restores_them() {
      db = new TempDb();
      long win = DataStore.Windows.LoadAll()[0].ID;
      Shortcut c = Shortcuts.CreateProcessCommand(null, win);
      Shortcuts.UpdateProcessRunAsAdmin(c.ID, true);
      Shortcuts.UpdateProcessWorkingFolder(c.ID, @"C:\Work");
      string zip = Path.Combine(db.Folder, "export.zip");
      ExportManifest manifest = DataExporter.Export(zip);
      Assert.Equal(AppConstants.DatabaseVersion, manifest.SchemaVersion);
      string data = ReadEntry(zip, "data.json");
      Assert.Contains("\"processRunAsAdmin\"", data);
      Assert.Contains("\"processWorkingFolder\"", data);

      foreach (ImportMode mode in new[] { ImportMode.Replace, ImportMode.AddAsNew }) {
        DataImporter.Import(zip, mode);
        Assert.All(Shortcuts.GetAll().Where(s => s.IsProcessCommand == 1), s => {
          Assert.Equal(1, s.ProcessRunAsAdmin);
          Assert.Equal(@"C:\Work", s.ProcessWorkingFolder);
        });
      }
      Assert.Equal(2, Shortcuts.GetAll().Count(s => s.IsProcessCommand == 1));
    }

    [Fact]
    public void Older_export_without_the_fields_imports_with_defaults() {
      db = new TempDb();
      long win = DataStore.Windows.LoadAll()[0].ID;
      Shortcut c = Shortcuts.CreateProcessCommand(null, win);
      Shortcuts.UpdateProcessFileName(c.ID, "cmd.exe");
      Shortcuts.UpdateProcessArguments(c.ID, "/K ping localhost");
      Shortcuts.UpdateProcessRunAsAdmin(c.ID, true);
      Shortcuts.UpdateProcessWorkingFolder(c.ID, @"C:\Work");
      string zip = Path.Combine(db.Folder, "export.zip");
      DataExporter.Export(zip);
      string old = Rewrite(zip, Path.Combine(db.Folder, "old.zip"),
        data => {
          foreach (JsonNode s in data["shortcuts"].AsArray()) {
            s.AsObject().Remove("processRunAsAdmin");
            s.AsObject().Remove("processWorkingFolder");
          }
        },
        manifest => manifest["schemaVersion"] = 3);
      Assert.DoesNotContain("processWorkingFolder", ReadEntry(old, "data.json"));

      DataImporter.Inspect(old);
      DataImporter.Import(old, ImportMode.Replace);

      Shortcut imported = Assert.Single(Shortcuts.GetAll(), s => s.IsProcessCommand == 1);
      Assert.Equal("/K ping localhost", imported.ProcessArguments);
      Assert.Equal(0, imported.ProcessRunAsAdmin);
      Assert.Equal("", imported.ProcessWorkingFolder);
    }
  }
}
