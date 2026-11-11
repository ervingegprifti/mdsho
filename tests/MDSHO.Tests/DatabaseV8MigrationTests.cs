using System;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using MDSHO.Common;
using MDSHO.Core;
using MDSHO.Data;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// Database version 8 adds NoteWindows: one row per note window with its last place, size and state and whether it was
  /// open, so open notes come back where they were after a restart. Deleting a note deletes its row.
  /// </summary>
  public class DatabaseV8MigrationTests : IDisposable {
    private TempDb db;
    public void Dispose() { if (db != null) { db.Dispose(); } }

    // The version 7 schema (note buttons).
    private const string V7Sql = @"
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
        CREATE INDEX IX_MDSHOWindowClones_ParentMDSHOWindowID ON MDSHOWindowClones(ParentMDSHOWindowID);";

    private void BuildV7Db() {
      db = new TempDb(false);
      db.Exec("PRAGMA user_version = 7;" + V7Sql
        + "INSERT INTO MDSHOWindows (ID,Title,ScreenDeviceName,Left,Top,Width,Height,BorderColor,BackgroundColor,TextColor,TitleBackgroundColor,TitleTextColor,Notes,SolidIcons,SingleClickToOpen) VALUES "
        + "(1,'W1','D1',0,0,300,400,'#FF000001','#FF000002','#FF000003','#FF000004','#FF000005','',1,0);"
        + "INSERT INTO Shortcuts VALUES (1,NULL,1,'N','','','g1',0,0,'',0,'#F',0,0,'','',0,'',1,'note text');");
    }

    private long Tables(string name) {
      return db.Scalar("SELECT count(*) FROM sqlite_master WHERE type='table' AND name='" + name + "'");
    }

    [Fact]
    public void Version_is_8_and_the_last_migration_is_8() {
      Assert.Equal(8, AppConstants.DatabaseVersion);
      Assert.Equal(8, SchemaManager.Migrations.Last().Version);
    }

    [Fact]
    public void V7_database_migrates_to_8_adding_an_empty_NoteWindows_table() {
      BuildV7Db();
      DataStore.Upgrade();
      Assert.Equal(8, db.Scalar("PRAGMA user_version"));
      Assert.Equal(1, Tables("NoteWindows"));
      Assert.Equal(0, db.Scalar("SELECT count(*) FROM NoteWindows"));
      Assert.Equal("note text", db.ScalarText("SELECT NoteText FROM Shortcuts WHERE ID = 1"));
      Assert.Contains(Directory.GetFiles(db.Folder, "test.sqlite3.bak-v*"), p => p.Contains("bak-v7-"));
    }

    [Fact]
    public void Running_the_v8_step_twice_does_nothing_the_second_time() {
      BuildV7Db();
      DataStore.Upgrade();
      using (SqliteConnection c = db.Open()) {
        SchemaManager.Migrations.Single(m => m.Version == 8).Apply(c);
      }
      Assert.Equal(1, Tables("NoteWindows"));
    }

    [Fact]
    public void A_note_window_place_is_saved_updated_and_read_back() {
      db = new TempDb();
      Assert.Equal(8, db.Scalar("PRAGMA user_version"));
      long windowId = DataStore.Windows.GetAll().First().ID;
      Shortcut note = DataStore.Shortcuts.CreateNote(null, windowId);
      Assert.Null(DataStore.NoteWindows.Get(note.ID));

      DataStore.NoteWindows.Save(new NoteWindowPlace(note.ID, -1500, 120, 640, 520, NoteWindowPlace.Maximized, isOpen: true));
      NoteWindowPlace read = DataStore.NoteWindows.Get(note.ID);
      Assert.Equal((-1500L, 120L, 640L, 520L, NoteWindowPlace.Maximized, true), (read.Left, read.Top, read.Width, read.Height, read.State, read.IsOpen));

      // Saving again updates the same row.
      DataStore.NoteWindows.Save(new NoteWindowPlace(note.ID, 10, 20, 300, 200, NoteWindowPlace.Normal, isOpen: true));
      Assert.Equal(1, db.Scalar("SELECT count(*) FROM NoteWindows"));
      Assert.Equal(10L, DataStore.NoteWindows.Get(note.ID).Left);

      // Closing keeps the place, so the note reopens where it was.
      DataStore.NoteWindows.SetOpen(note.ID, false);
      read = DataStore.NoteWindows.Get(note.ID);
      Assert.False(read.IsOpen);
      Assert.Equal((10L, 20L, 300L, 200L), (read.Left, read.Top, read.Width, read.Height));
    }

    [Fact]
    public void GetOpen_lists_only_open_note_windows() {
      db = new TempDb();
      long windowId = DataStore.Windows.GetAll().First().ID;
      Shortcut open = DataStore.Shortcuts.CreateNote(null, windowId);
      Shortcut closed = DataStore.Shortcuts.CreateNote(null, windowId);
      DataStore.NoteWindows.Save(new NoteWindowPlace(open.ID, 1, 2, 300, 200, NoteWindowPlace.Normal, isOpen: true));
      DataStore.NoteWindows.Save(new NoteWindowPlace(closed.ID, 3, 4, 300, 200, NoteWindowPlace.Normal, isOpen: false));
      Assert.Equal(new[] { open.ID }, DataStore.NoteWindows.GetOpen().Select(p => p.ShortcutID));
    }

    [Fact]
    public void Deleting_a_note_deletes_its_window_place() {
      db = new TempDb();
      long windowId = DataStore.Windows.GetAll().First().ID;
      Shortcut note = DataStore.Shortcuts.CreateNote(null, windowId);
      DataStore.NoteWindows.Save(new NoteWindowPlace(note.ID, 1, 2, 300, 200, NoteWindowPlace.Normal, isOpen: true));
      DataStore.Shortcuts.Delete(note.ID);
      Assert.Equal(0, db.Scalar("SELECT count(*) FROM NoteWindows"));
    }
  }
}
