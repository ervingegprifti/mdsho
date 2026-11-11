using System;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using MDSHO.Common;
using MDSHO.Core;
using MDSHO.Data;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>Database version 9 adds NoteWindows.IsTopmost: whether a note window is pinned to stay on top of other apps.</summary>
  public class DatabaseV9MigrationTests : IDisposable {
    private TempDb db;
    public void Dispose() { if (db != null) { db.Dispose(); } }

    // The version 8 schema (NoteWindows without IsTopmost).
    private const string V8Sql = @"
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
          ProcessRunAsAdmin INTEGER NOT NULL DEFAULT 0, ProcessWorkingFolder TEXT NOT NULL DEFAULT '', IsNote INTEGER NOT NULL DEFAULT 0, NoteText TEXT NOT NULL DEFAULT '',
          FOREIGN KEY(ParentShortcutID) REFERENCES Shortcuts(ID) ON DELETE CASCADE,
          FOREIGN KEY(ParentMDSHOWindowID) REFERENCES MDSHOWindows(ID) ON DELETE CASCADE);
        CREATE INDEX IX_Shortcuts_ParentShortcutID ON Shortcuts(ParentShortcutID);
        CREATE INDEX IX_Shortcuts_ParentMDSHOWindowID ON Shortcuts(ParentMDSHOWindowID);
        CREATE INDEX IX_MDSHOWindowClones_ParentMDSHOWindowID ON MDSHOWindowClones(ParentMDSHOWindowID);
        CREATE TABLE NoteWindows (
          ID INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT UNIQUE, ShortcutID INTEGER NOT NULL UNIQUE, Left INTEGER NOT NULL,
          Top INTEGER NOT NULL, Width INTEGER NOT NULL, Height INTEGER NOT NULL, State INTEGER NOT NULL DEFAULT 0,
          IsOpen INTEGER NOT NULL DEFAULT 0, FOREIGN KEY(ShortcutID) REFERENCES Shortcuts(ID) ON DELETE CASCADE);";

    private void BuildV8Db() {
      db = new TempDb(false);
      db.Exec("PRAGMA user_version = 8;" + V8Sql
        + "INSERT INTO MDSHOWindows (ID,Title,ScreenDeviceName,Left,Top,Width,Height,BorderColor,BackgroundColor,TextColor,TitleBackgroundColor,TitleTextColor,Notes,SolidIcons,SingleClickToOpen) VALUES "
        + "(1,'W1','D1',0,0,300,400,'#FF000001','#FF000002','#FF000003','#FF000004','#FF000005','',1,0);"
        + "INSERT INTO Shortcuts VALUES (1,NULL,1,'N','','','g1',0,0,'',0,'#F',0,0,'','',0,'',1,'text');"
        + "INSERT INTO NoteWindows (ShortcutID,Left,Top,Width,Height,State,IsOpen) VALUES (1,10,20,300,200,2,1);");
    }

    private long Columns(string table, string column) {
      return db.Scalar("SELECT count(*) FROM pragma_table_info('" + table + "') WHERE name = '" + column + "' COLLATE NOCASE");
    }

    [Fact]
    public void V8_database_migrates_past_9_adding_IsTopmost_off_and_keeping_the_places() {
      BuildV8Db();
      DataStore.Upgrade();
      Assert.Equal(AppConstants.DatabaseVersion, db.Scalar("PRAGMA user_version"));
      Assert.Equal(1, Columns("NoteWindows", "IsTopmost"));
      NoteWindowPlace place = DataStore.NoteWindows.Get(1);
      Assert.Equal((10L, 20L, 300L, 200L, NoteWindowPlace.Maximized, true, false), (place.Left, place.Top, place.Width, place.Height, place.State, place.IsOpen, place.IsTopmost));
      Assert.Contains(Directory.GetFiles(db.Folder, "test.sqlite3.bak-v*"), p => p.Contains("bak-v8-"));
    }

    [Fact]
    public void Running_the_v9_step_twice_does_nothing_the_second_time() {
      BuildV8Db();
      DataStore.Upgrade();
      using (SqliteConnection c = db.Open()) {
        SchemaManager.Migrations.Single(m => m.Version == 9).Apply(c);
      }
      Assert.Equal(1, Columns("NoteWindows", "IsTopmost"));
    }

    [Fact]
    public void The_pin_is_saved_with_the_place() {
      db = new TempDb();
      long windowId = DataStore.Windows.GetAll().First().ID;
      Shortcut note = DataStore.Shortcuts.CreateNote(null, windowId);
      DataStore.NoteWindows.Save(new NoteWindowPlace(note.ID, 1, 2, 300, 200, NoteWindowPlace.Normal, isOpen: true) { IsTopmost = true });
      Assert.True(DataStore.NoteWindows.Get(note.ID).IsTopmost);
      DataStore.NoteWindows.Save(new NoteWindowPlace(note.ID, 1, 2, 300, 200, NoteWindowPlace.Normal, isOpen: true) { IsTopmost = false });
      Assert.False(DataStore.NoteWindows.Get(note.ID).IsTopmost);
    }
  }
}
