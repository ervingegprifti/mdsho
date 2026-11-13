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
  /// Database version 11 adds wrap text: NoteWindows.IsWrapText (each note window remembers it) and
  /// MDSHOWindows.CommentWrapText (the Comment windows opened from a panel share it). Both start off.
  /// </summary>
  public class DatabaseV11MigrationTests : IDisposable {
    private TempDb db;
    public void Dispose() { if (db != null) { db.Dispose(); } }

    // A version 10 database: the version 9 schema with the version 10 step applied.
    private void BuildV10Db() {
      db = new TempDb(false);
      db.Exec("PRAGMA user_version = 9;" + DatabaseV10MigrationTests.V9Sql
        + "INSERT INTO MDSHOWindows (ID,Title,ScreenDeviceName,Left,Top,Width,Height,BorderColor,BackgroundColor,TextColor,TitleBackgroundColor,TitleTextColor,Notes,SolidIcons,SingleClickToOpen) VALUES "
        + "(1,'W1','D1',0,0,300,400,'#FF000001','#FF000002','#FF000003','#FF000004','#FF000005','',1,0);"
        + "INSERT INTO Shortcuts VALUES (1,NULL,1,'N','','','g1',0,0,'',0,'#F',0,0,'','',0,'',1,'text');"
        + "INSERT INTO NoteWindows (ShortcutID,Left,Top,Width,Height,State,IsOpen,IsTopmost) VALUES (1,10,20,300,200,0,1,1);");
      using (SqliteConnection c = db.Open()) {
        SchemaManager.Migrations.Single(m => m.Version == 10).Apply(c);
      }
      db.Exec("PRAGMA user_version = 10;");
    }

    private long Columns(string table, string column) {
      return db.Scalar("SELECT count(*) FROM pragma_table_info('" + table + "') WHERE name = '" + column + "' COLLATE NOCASE");
    }

    [Fact]
    public void There_is_a_version_11_step() {
      Assert.Single(SchemaManager.Migrations, m => m.Version == 11);
    }

    [Fact]
    public void V10_database_migrates_to_11_with_wrap_text_off_and_keeps_the_rest() {
      BuildV10Db();
      DataStore.Upgrade();
      Assert.True(db.Scalar("PRAGMA user_version") >= 11);
      Assert.Equal(1, Columns("NoteWindows", "IsWrapText"));
      Assert.Equal(1, Columns("MDSHOWindows", "CommentWrapText"));
      NoteWindowPlace place = DataStore.NoteWindows.Get(1);
      Assert.Equal((10L, 20L, true, true, false), (place.Left, place.Top, place.IsOpen, place.IsTopmost, place.IsWrapText));
      DesktopWindow window = DataStore.Windows.GetAll().Single();
      Assert.False(window.CommentWrapText);
      Assert.True(window.SolidIcons);
      Assert.Contains(Directory.GetFiles(db.Folder, "test.sqlite3.bak-v*"), p => p.Contains("bak-v10-"));
    }

    [Fact]
    public void Running_the_v11_step_twice_does_nothing_the_second_time() {
      BuildV10Db();
      DataStore.Upgrade();
      using (SqliteConnection c = db.Open()) {
        SchemaManager.Migrations.Single(m => m.Version == 11).Apply(c);
      }
      Assert.Equal(1, Columns("NoteWindows", "IsWrapText"));
      Assert.Equal(1, Columns("MDSHOWindows", "CommentWrapText"));
    }

    [Fact]
    public void A_new_database_has_both_columns() {
      db = new TempDb();
      Assert.Equal(1, Columns("NoteWindows", "IsWrapText"));
      Assert.Equal(1, Columns("MDSHOWindows", "CommentWrapText"));
    }

    [Fact]
    public void Note_wrap_text_is_saved_with_the_place() {
      db = new TempDb();
      long windowId = DataStore.Windows.GetAll().First().ID;
      Shortcut note = DataStore.Shortcuts.CreateNote(null, windowId);
      DataStore.NoteWindows.Save(new NoteWindowPlace(note.ID, 1, 2, 300, 200, NoteWindowPlace.Normal, isOpen: true) { IsWrapText = true });
      Assert.True(DataStore.NoteWindows.Get(note.ID).IsWrapText);
      DataStore.NoteWindows.Save(new NoteWindowPlace(note.ID, 1, 2, 300, 200, NoteWindowPlace.Normal, isOpen: true) { IsWrapText = false });
      Assert.False(DataStore.NoteWindows.Get(note.ID).IsWrapText);
    }

    [Fact]
    public void Comment_wrap_text_is_saved_with_the_panel() {
      db = new TempDb();
      long windowId = DataStore.Windows.GetAll().First().ID;
      DataStore.Windows.UpdateCommentWrapText(windowId, true);
      Assert.True(DataStore.Windows.GetAll().Single(w => w.ID == windowId).CommentWrapText);
      DataStore.Windows.UpdateCommentWrapText(windowId, false);
      Assert.False(DataStore.Windows.GetAll().Single(w => w.ID == windowId).CommentWrapText);
    }

    [Fact]
    public void A_panel_added_or_updated_keeps_its_comment_wrap_text() {
      db = new TempDb();
      DesktopWindow window = DataStore.Windows.GetAll().First();
      window.CommentWrapText = true;
      window.ID = 0;
      long id = DataStore.Windows.Add(window);
      Assert.True(DataStore.Windows.Get(id).CommentWrapText);
      DesktopWindow read = DataStore.Windows.Get(id);
      read.CommentWrapText = false;
      DataStore.Windows.Update(read);
      Assert.False(DataStore.Windows.Get(id).CommentWrapText);
    }

    [Fact]
    public void Export_writes_comment_wrap_text_and_an_old_export_imports_with_it_off() {
      DesktopWindow window = new DesktopWindow { Title = "W", CommentWrapText = true };
      string json = System.Text.Json.JsonSerializer.Serialize(window, PortabilityJson.Options);
      Assert.Contains("\"commentWrapText\": true", json);
      DesktopWindow old = System.Text.Json.JsonSerializer.Deserialize<DesktopWindow>("{\"id\":1,\"title\":\"W\"}", PortabilityJson.Options);
      Assert.False(old.CommentWrapText);
    }
  }
}
