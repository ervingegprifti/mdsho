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
  /// Database version 10 adds the colours of a note's window to its button row (empty = the default colours), so they
  /// are exported, imported and copied with the note.
  /// </summary>
  public class DatabaseV10MigrationTests : IDisposable {
    private TempDb db;
    public void Dispose() { if (db != null) { db.Dispose(); } }
    private static readonly string[] ColourColumns = { "NoteBorderColor", "NoteTitleBackgroundColor", "NoteTitleTextColor", "NoteBackgroundColor", "NoteTextColor" };

    // The version 9 schema.
    internal const string V9Sql = @"
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
          IsOpen INTEGER NOT NULL DEFAULT 0, IsTopmost INTEGER NOT NULL DEFAULT 0, FOREIGN KEY(ShortcutID) REFERENCES Shortcuts(ID) ON DELETE CASCADE);";

    private void BuildV9Db() {
      db = new TempDb(false);
      db.Exec("PRAGMA user_version = 9;" + V9Sql
        + "INSERT INTO MDSHOWindows (ID,Title,ScreenDeviceName,Left,Top,Width,Height,BorderColor,BackgroundColor,TextColor,TitleBackgroundColor,TitleTextColor,Notes,SolidIcons,SingleClickToOpen) VALUES "
        + "(1,'W1','D1',0,0,300,400,'#FF000001','#FF000002','#FF000003','#FF000004','#FF000005','',1,0);"
        + "INSERT INTO Shortcuts VALUES (1,NULL,1,'N','','','g1',0,0,'',0,'#F',0,0,'','',0,'',1,'text');");
    }

    private long Columns(string table, string column) {
      return db.Scalar("SELECT count(*) FROM pragma_table_info('" + table + "') WHERE name = '" + column + "' COLLATE NOCASE");
    }

    [Fact]
    public void V9_database_migrates_past_10_adding_empty_note_colours() {
      BuildV9Db();
      DataStore.Upgrade();
      Assert.Equal(AppConstants.DatabaseVersion, db.Scalar("PRAGMA user_version"));
      foreach (string column in ColourColumns) {
        Assert.Equal(1, Columns("Shortcuts", column));
      }
      Shortcut note = DataStore.Shortcuts.GetAll().Single();
      Assert.Equal(("", "", "", "", ""), (note.NoteBorderColor, note.NoteTitleBackgroundColor, note.NoteTitleTextColor, note.NoteBackgroundColor, note.NoteTextColor));
      Assert.Equal("text", note.NoteText);
      Assert.Contains(Directory.GetFiles(db.Folder, "test.sqlite3.bak-v*"), p => p.Contains("bak-v9-"));
    }

    [Fact]
    public void Running_the_v10_step_twice_does_nothing_the_second_time() {
      BuildV9Db();
      DataStore.Upgrade();
      using (SqliteConnection c = db.Open()) {
        SchemaManager.Migrations.Single(m => m.Version == 10).Apply(c);
      }
      Assert.Equal(1, Columns("Shortcuts", "NoteTextColor"));
    }

    [Fact]
    public void Note_colours_are_saved_read_and_copied_with_the_note() {
      db = new TempDb();
      long windowId = DataStore.Windows.GetAll().First().ID;
      Shortcut note = DataStore.Shortcuts.CreateNote(null, windowId);
      DataStore.Shortcuts.UpdateNoteColors(note.ID, "#FF2F4F6F", "#FF2F4F6F", "#FFFFFFFF", "#FFFFF8DC", "#FF1F2F3F");
      Shortcut read = DataStore.Shortcuts.GetAll().Single(s => s.ID == note.ID);
      Assert.Equal(("#FF2F4F6F", "#FF2F4F6F", "#FFFFFFFF", "#FFFFF8DC", "#FF1F2F3F"),
        (read.NoteBorderColor, read.NoteTitleBackgroundColor, read.NoteTitleTextColor, read.NoteBackgroundColor, read.NoteTextColor));
      Shortcut copy = DataStore.Shortcuts.Append(read);
      Assert.Equal("#FFFFF8DC", DataStore.Shortcuts.GetAll().Single(s => s.ID == copy.ID).NoteBackgroundColor);
      DataStore.Shortcuts.UpdateNoteColors(note.ID, "", "", "", "", "");
      Assert.Equal("", DataStore.Shortcuts.GetAll().Single(s => s.ID == note.ID).NoteBorderColor);
    }

    [Fact]
    public void Export_writes_the_note_colours_and_an_old_export_imports_with_default_colours() {
      Shortcut s = new Shortcut(1, null, 1, "N", "", "", "g", 0, 0, "", 0, "", 0, 0, "", "", 0, "", isNote: 1, noteText: "t") { NoteBackgroundColor = "#FFFFF8DC" };
      string json = System.Text.Json.JsonSerializer.Serialize(s, PortabilityJson.Options);
      Assert.Contains("\"noteBackgroundColor\": \"#FFFFF8DC\"", json);
      Shortcut old = System.Text.Json.JsonSerializer.Deserialize<Shortcut>("{\"id\":1,\"name\":\"N\",\"isNote\":1}", PortabilityJson.Options);
      Assert.Equal("", old.NoteBackgroundColor ?? "");
    }
  }
}
