using System;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Data.Sqlite;
using MDSHO.Common;
using MDSHO.Core;
using MDSHO.Data;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// Database version 6 renames Shortcuts.Notes to Comment (the button comments); the name "Notes" stays free for a later
  /// feature. MDSHOWindows.Notes is not touched.
  /// </summary>
  public class DatabaseV6MigrationTests : IDisposable {
    private TempDb db;
    public void Dispose() { if (db != null) { db.Dispose(); } }

    // The version 5 schema exactly as 1.1.5 builds it.
    private const string V5Sql = @"
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
          IsGroupExpanded INTEGER NOT NULL, Notes TEXT NOT NULL, IsMarked INTEGER NOT NULL, MarkColor TEXT NOT NULL,
          Position INTEGER NOT NULL, IsProcessCommand INTEGER NOT NULL, ProcessFileName TEXT NOT NULL, ProcessArguments TEXT NOT NULL,
          ProcessRunAsAdmin INTEGER NOT NULL DEFAULT 0, ProcessWorkingFolder TEXT NOT NULL DEFAULT '',
          FOREIGN KEY(ParentShortcutID) REFERENCES Shortcuts(ID) ON DELETE CASCADE,
          FOREIGN KEY(ParentMDSHOWindowID) REFERENCES MDSHOWindows(ID) ON DELETE CASCADE);
        CREATE INDEX IX_Shortcuts_ParentShortcutID ON Shortcuts(ParentShortcutID);
        CREATE INDEX IX_Shortcuts_ParentMDSHOWindowID ON Shortcuts(ParentMDSHOWindowID);
        CREATE INDEX IX_MDSHOWindowClones_ParentMDSHOWindowID ON MDSHOWindowClones(ParentMDSHOWindowID);";

    private void BuildV5Db() {
      db = new TempDb(false);
      db.Exec("PRAGMA user_version = 5;" + V5Sql
        + "INSERT INTO MDSHOWindows (ID,Title,ScreenDeviceName,Left,Top,Width,Height,BorderColor,BackgroundColor,TextColor,TitleBackgroundColor,TitleTextColor,Notes,SolidIcons,SingleClickToOpen) VALUES "
        + "(1,'W1','D1',0,0,300,400,'#FF000001','#FF000002','#FF000003','#FF000004','#FF000005','window note',1,0);"
        + "INSERT INTO Shortcuts VALUES (1,NULL,1,'G','','','g1',1,1,'group comment',0,'#F',0,0,'','',0,'');"
        + "INSERT INTO Shortcuts VALUES (2,1,NULL,'S','t','.lnk','g2',0,0,'line 1' || char(10) || 'line 2 ü',0,'#F',0,1,'cmd.exe','/K',1,'C:\\x');");
    }

    private long Columns(string table, string column) {
      return db.Scalar("SELECT count(*) FROM pragma_table_info('" + table + "') WHERE name = '" + column + "' COLLATE NOCASE");
    }

    [Fact]
    public void Version_is_6_and_the_last_migration_is_6() {
      Assert.Equal(6, AppConstants.DatabaseVersion);
      Assert.Equal(6, SchemaManager.Migrations.Last().Version);
    }

    [Fact]
    public void V5_database_migrates_to_6_renaming_Shortcuts_Notes_to_Comment_and_keeping_the_text() {
      BuildV5Db();
      DataStore.Upgrade();
      Assert.Equal(6, db.Scalar("PRAGMA user_version"));
      Assert.Equal(0, Columns("Shortcuts", "Notes"));
      Assert.Equal(1, Columns("Shortcuts", "Comment"));
      Assert.Equal("group comment", db.ScalarText("SELECT Comment FROM Shortcuts WHERE ID = 1"));
      Assert.Equal("line 1\nline 2 ü", db.ScalarText("SELECT Comment FROM Shortcuts WHERE ID = 2"));
      // The windows keep their Notes column.
      Assert.Equal(1, Columns("MDSHOWindows", "Notes"));
      Assert.Equal("window note", db.ScalarText("SELECT Notes FROM MDSHOWindows WHERE ID = 1"));
      // Indexes and foreign keys survive.
      Assert.Equal(2, db.Scalar("SELECT count(*) FROM sqlite_master WHERE type='index' AND name IN ('IX_Shortcuts_ParentShortcutID','IX_Shortcuts_ParentMDSHOWindowID')"));
      Assert.Equal(2, db.Scalar("SELECT count(*) FROM pragma_foreign_key_list('Shortcuts') WHERE on_delete='CASCADE'"));
      // Backed up first, and the data loads through the normal code with the comment.
      Assert.Contains(Directory.GetFiles(db.Folder, "test.sqlite3.bak-v*"), p => p.Contains("bak-v5-"));
      Shortcut child = DataStore.Shortcuts.GetAll().Single(s => s.ID == 2);
      Assert.Equal("line 1\nline 2 ü", child.Comment);
    }

    [Fact]
    public void Running_the_v6_step_twice_does_nothing_the_second_time() {
      BuildV5Db();
      DataStore.Upgrade();
      using (SqliteConnection c = db.Open()) {
        SchemaManager.Migrations.Single(m => m.Version == 6).Apply(c);
      }
      Assert.Equal(1, Columns("Shortcuts", "Comment"));
      Assert.Equal("group comment", db.ScalarText("SELECT Comment FROM Shortcuts WHERE ID = 1"));
    }

    [Fact]
    public void Fresh_database_is_version_6_with_Shortcuts_Comment_and_MDSHOWindows_Notes() {
      db = new TempDb();
      Assert.Equal(6, db.Scalar("PRAGMA user_version"));
      Assert.Equal(1, Columns("Shortcuts", "Comment"));
      Assert.Equal(0, Columns("Shortcuts", "Notes"));
      Assert.Equal(1, Columns("MDSHOWindows", "Notes"));
    }

    [Fact]
    public void Export_writes_comment_and_an_old_export_with_notes_still_imports_as_the_comment() {
      Shortcut s = new Shortcut(1, null, 1, "S", "t", ".lnk", "g", 0, 0, "my comment", 0, "", 0, 0, "", "");
      string json = System.Text.Json.JsonSerializer.Serialize(s, PortabilityJson.Options);
      Assert.Contains("\"comment\": \"my comment\"", json);
      Assert.DoesNotContain("\"notes\"", json);
      Shortcut old = System.Text.Json.JsonSerializer.Deserialize<Shortcut>("{\"id\":1,\"name\":\"S\",\"notes\":\"from 1.1.5\"}", PortabilityJson.Options);
      Assert.Equal("from 1.1.5", old.Comment);
      Shortcut both = System.Text.Json.JsonSerializer.Deserialize<Shortcut>("{\"comment\":\"new\",\"notes\":\"old\"}", PortabilityJson.Options);
      Assert.Equal("new", both.Comment);
    }
  }
}
