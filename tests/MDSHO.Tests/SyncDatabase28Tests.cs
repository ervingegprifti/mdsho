using System;
using System.IO;
using System.Linq;
using MDSHO.Common;
using MDSHO.Core;
using MDSHO.Data;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// 2.0.0, database version 28: a text that lost a sync clash goes into Text history marked with the PC it came from
  /// (History.FromPc). The new sync settings are this PC's own.
  /// </summary>
  public class SyncDatabase28Tests : IDisposable {
    private readonly TempDb db = new TempDb();
    private readonly long windowId;

    public SyncDatabase28Tests() {
      windowId = DataStore.Windows.GetAll().Single().ID;
    }

    public void Dispose() { db.Dispose(); }

    [Fact]
    public void Version_28_is_the_database_version() {
      Assert.Equal(28, AppConstants.DatabaseVersion);
      Assert.Single(SchemaManager.Migrations, m => m.Version == 28);
    }

    [Fact]
    public void A_new_database_has_History_FromPc() {
      Assert.Equal(1, db.Scalar("SELECT count(*) FROM pragma_table_info('History') WHERE name = 'FromPc'"));
      Assert.Equal(28, db.Scalar("PRAGMA user_version"));
    }

    [Fact]
    public void A_v27_database_gets_History_FromPc_and_keeps_its_rows() {
      Shortcut note = DataStore.Shortcuts.CreateNote(null, windowId);
      DataStore.History.Add(new HistoryEntry { ShortcutID = note.ID, Field = HistoryFields.Text, Text = "old", KeptAt = "2026-11-01T10:00:00" });
      db.Exec("ALTER TABLE History DROP COLUMN FromPc; PRAGMA user_version = 27;");
      DataStore.Upgrade();
      Assert.Equal(28, db.Scalar("PRAGMA user_version"));
      Assert.Single(Directory.GetFiles(db.Folder, "test.sqlite3.bak-v27-*"));
      HistoryEntry row = DataStore.History.Get(note.ID, HistoryFields.Text).Single();
      Assert.Equal(("old", ""), (row.Text, row.FromPc));
    }

    [Fact]
    public void Running_the_v28_step_again_changes_nothing() {
      using (Microsoft.Data.Sqlite.SqliteConnection c = db.Open()) {
        SchemaManager.Migrations.Single(m => m.Version == 28).Apply(c);
      }
      Assert.Equal(1, db.Scalar("SELECT count(*) FROM pragma_table_info('History') WHERE name = 'FromPc'"));
    }

    [Fact]
    public void A_history_row_keeps_the_PC_it_came_from() {
      Shortcut note = DataStore.Shortcuts.CreateNote(null, windowId);
      DataStore.History.Add(new HistoryEntry { ShortcutID = note.ID, Field = HistoryFields.Text, Text = "theirs", KeptAt = "2026-11-01T10:00:00", FromPc = "Work PC" });
      Assert.Equal("Work PC", DataStore.History.Get(note.ID, HistoryFields.Text).Single().FromPc);
      Assert.Equal("Work PC", DataStore.History.GetAll().Single().FromPc);
    }

    [Fact]
    public void Export_and_import_keep_the_PC_of_a_history_row() {
      Shortcut note = DataStore.Shortcuts.CreateNote(null, windowId);
      DataStore.History.Add(new HistoryEntry { ShortcutID = note.ID, Field = HistoryFields.Text, Text = "theirs", KeptAt = "2026-11-01T10:00:00", FromPc = "Work PC" });
      string zip = Path.Combine(db.Folder, "e.zip");
      DataExporter.Export(zip);
      DataImporter.Import(zip, ImportMode.Replace);
      Assert.Equal("Work PC", DataStore.History.GetAll().Single().FromPc);
    }

    [Fact]
    public void FromPc_and_the_sync_settings_are_this_PCs_own() {
      Assert.Equal(SyncScope.Local, SyncFields.Of("History", "FromPc").Scope);
      foreach (string key in new[] { SyncSettings.OnKey, SyncSettings.FolderKey, SyncSettings.SeqKey, SyncSettings.LastTimeKey, SyncSettings.BackupNoticeKey }) {
        Assert.Equal(SyncScope.Local, SyncFields.Settings.Single(s => s.Key == key).Scope);
      }
    }

    [Fact]
    public void The_history_menu_says_which_PC_a_text_came_from() {
      DateTime now = new DateTime(2026, 11, 21, 15, 0, 0);
      Assert.Equal("Today 14:05:00  ·  from Work PC  ·  Buy milk", TextHistory.MenuLine("2026-11-21T14:05:00", "Buy milk", now, "Work PC"));
      Assert.Equal("Today 14:05:00  ·  Buy milk", TextHistory.MenuLine("2026-11-21T14:05:00", "Buy milk", now, ""));
    }
  }
}
