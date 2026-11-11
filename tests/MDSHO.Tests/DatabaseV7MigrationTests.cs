using System;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using MDSHO.Common;
using MDSHO.Core;
using MDSHO.Data;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>Database version 7 adds note buttons: Shortcuts.IsNote (0 or 1) and Shortcuts.NoteText (the note).</summary>
  public class DatabaseV7MigrationTests : IDisposable {
    private TempDb db;
    public void Dispose() { if (db != null) { db.Dispose(); } }

    // The version 6 schema (Shortcuts.Notes renamed to Comment).
    private const string V6Sql = @"
        CREATE TABLE MDSHOWindows (
          ID INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT UNIQUE, Title TEXT NOT NULL, ScreenDeviceName TEXT NOT NULL,
          Left INTEGER NOT NULL, Top INTEGER NOT NULL, Width INTEGER NOT NULL, Height INTEGER NOT NULL,
          BorderColor TEXT NOT NULL, BackgroundColor TEXT NOT NULL, TextColor TEXT NOT NULL, TitleBackgroundColor TEXT NOT NULL,
          TitleTextColor TEXT NOT NULL, Notes TEXT NOT NULL, SolidIcons INTEGER NOT NULL,
          SingleClickToOpen INTEGER NOT NULL);
        CREATE TABLE MDSHOWindowClones (
          ID INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT UNIQUE, ParentMDSHOWindowID INTEGER NOT NULL, ScreenDeviceName TEXT NOT NULL,
          Left INTEGER NOT NULL, Top INTEGER NOT NULL, Width INTEGER NOT NULL, Height INTEGER NOT NULL,
          FOREIGN KEY(ParentMDSHOWindowID) REFERENCES MDSHOWindows(ID) ON DELETE CASCADE);
        CREATE TABLE Shortcuts (
          ID INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT UNIQUE, ParentShortcutID INTEGER NULL, ParentMDSHOWindowID INTEGER NULL,
          Name TEXT NOT NULL, Target TEXT NOT NULL, Ext TEXT NOT NULL, Guid TEXT NOT NULL, IsGroup INTEGER NOT NULL,
          IsGroupExpanded INTEGER NOT NULL, Comment TEXT NOT NULL, IsMarked INTEGER NOT NULL, MarkColor TEXT NOT NULL,
          Position INTEGER NOT NULL, IsProcessCommand INTEGER NOT NULL, ProcessFileName TEXT NOT NULL, ProcessArguments TEXT NOT NULL,
          ProcessRunAsAdmin INTEGER NOT NULL DEFAULT 0, ProcessWorkingFolder TEXT NOT NULL DEFAULT '',
          FOREIGN KEY(ParentShortcutID) REFERENCES Shortcuts(ID) ON DELETE CASCADE,
          FOREIGN KEY(ParentMDSHOWindowID) REFERENCES MDSHOWindows(ID) ON DELETE CASCADE);
        CREATE INDEX IX_Shortcuts_ParentShortcutID ON Shortcuts(ParentShortcutID);
        CREATE INDEX IX_Shortcuts_ParentMDSHOWindowID ON Shortcuts(ParentMDSHOWindowID);
        CREATE INDEX IX_MDSHOWindowClones_ParentMDSHOWindowID ON MDSHOWindowClones(ParentMDSHOWindowID);";

    private void BuildV6Db() {
      db = new TempDb(false);
      db.Exec("PRAGMA user_version = 6;" + V6Sql
        + "INSERT INTO MDSHOWindows (ID,Title,ScreenDeviceName,Left,Top,Width,Height,BorderColor,BackgroundColor,TextColor,TitleBackgroundColor,TitleTextColor,Notes,SolidIcons,SingleClickToOpen) VALUES "
        + "(1,'W1','D1',0,0,300,400,'#FF000001','#FF000002','#FF000003','#FF000004','#FF000005','',1,0);"
        + "INSERT INTO Shortcuts VALUES (1,NULL,1,'G','','','g1',1,1,'c',0,'#F',0,0,'','',0,'');"
        + "INSERT INTO Shortcuts VALUES (2,1,NULL,'S','t','.lnk','g2',0,0,'',0,'#F',0,1,'cmd.exe','/K',1,'');");
    }

    private long Columns(string table, string column) {
      return db.Scalar("SELECT count(*) FROM pragma_table_info('" + table + "') WHERE name = '" + column + "' COLLATE NOCASE");
    }

    [Fact]
    public void Version_is_7_and_the_last_migration_is_7() {
      Assert.Equal(7, AppConstants.DatabaseVersion);
      Assert.Equal(7, SchemaManager.Migrations.Last().Version);
    }

    [Fact]
    public void V6_database_migrates_to_7_adding_IsNote_and_NoteText_with_defaults() {
      BuildV6Db();
      DataStore.Upgrade();
      Assert.Equal(7, db.Scalar("PRAGMA user_version"));
      Assert.Equal(1, Columns("Shortcuts", "IsNote"));
      Assert.Equal(1, Columns("Shortcuts", "NoteText"));
      Assert.Equal(0, db.Scalar("SELECT count(*) FROM Shortcuts WHERE IsNote <> 0 OR NoteText <> ''"));
      Assert.Equal(2, db.Scalar("SELECT count(*) FROM Shortcuts"));
      Assert.Contains(Directory.GetFiles(db.Folder, "test.sqlite3.bak-v*"), p => p.Contains("bak-v6-"));
      Assert.All(DataStore.Shortcuts.GetAll(), s => Assert.Equal((0L, ""), (s.IsNote, s.NoteText)));
    }

    [Fact]
    public void Running_the_v7_step_twice_does_nothing_the_second_time() {
      BuildV6Db();
      DataStore.Upgrade();
      using (SqliteConnection c = db.Open()) {
        SchemaManager.Migrations.Single(m => m.Version == 7).Apply(c);
      }
      Assert.Equal(1, Columns("Shortcuts", "IsNote"));
    }

    [Fact]
    public void Fresh_database_is_version_7_and_a_note_round_trips() {
      db = new TempDb();
      Assert.Equal(7, db.Scalar("PRAGMA user_version"));
      long windowId = DataStore.Windows.GetAll().First().ID;
      Shortcut note = DataStore.Shortcuts.Append(new Shortcut(null, windowId, "Checklist", "", "", "gN", 0, 0, "", 0, "", 0, 0, "", "",
        isNote: 1, noteText: "1. Tell the help desk.\r\n2. Restart SRV01."));
      Shortcut read = DataStore.Shortcuts.GetAll().Single(s => s.ID == note.ID);
      Assert.Equal((1L, "1. Tell the help desk.\r\n2. Restart SRV01."), (read.IsNote, read.NoteText));
      read.NoteText = "changed";
      DataStore.Shortcuts.Update(read);
      Assert.Equal("changed", DataStore.Shortcuts.GetAll().Single(s => s.ID == note.ID).NoteText);
    }

    [Fact]
    public void Export_writes_isNote_and_noteText_and_an_old_export_imports_as_a_normal_button() {
      Shortcut s = new Shortcut(1, null, 1, "N", "", "", "g", 0, 0, "", 0, "", 0, 0, "", "", 0, "", isNote: 1, noteText: "text");
      string json = System.Text.Json.JsonSerializer.Serialize(s, PortabilityJson.Options);
      Assert.Contains("\"isNote\": 1", json);
      Assert.Contains("\"noteText\": \"text\"", json);
      Shortcut old = System.Text.Json.JsonSerializer.Deserialize<Shortcut>("{\"id\":1,\"name\":\"S\"}", PortabilityJson.Options);
      Assert.Equal(0L, old.IsNote);
      Assert.Equal("", old.NoteText ?? "");
    }
  }
}
