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
  /// Database version 12 adds MDSHOWindows.ReserveDockedSpace: a panel (and its clones) docked to a screen side reserves
  /// that strip of the screen, so desktop icons and maximised programs stay out of it. Off for every panel.
  /// </summary>
  public class DatabaseV12MigrationTests : IDisposable {
    private TempDb db;
    public void Dispose() { if (db != null) { db.Dispose(); } }

    // A version 11 database: the version 9 schema with the version 10 and 11 steps applied.
    private void BuildV11Db() {
      db = new TempDb(false);
      db.Exec("PRAGMA user_version = 9;" + DatabaseV10MigrationTests.V9Sql
        + "INSERT INTO MDSHOWindows (ID,Title,ScreenDeviceName,Left,Top,Width,Height,BorderColor,BackgroundColor,TextColor,TitleBackgroundColor,TitleTextColor,Notes,SolidIcons,SingleClickToOpen) VALUES "
        + "(1,'W1','D1',0,0,300,400,'#FF000001','#FF000002','#FF000003','#FF000004','#FF000005','',1,1);");
      using (SqliteConnection c = db.Open()) {
        SchemaManager.Migrations.Single(m => m.Version == 10).Apply(c);
        SchemaManager.Migrations.Single(m => m.Version == 11).Apply(c);
      }
      db.Exec("PRAGMA user_version = 11; UPDATE MDSHOWindows SET CommentWrapText = 1;");
    }

    private long Columns(string table, string column) {
      return db.Scalar("SELECT count(*) FROM pragma_table_info('" + table + "') WHERE name = '" + column + "' COLLATE NOCASE");
    }

    [Fact]
    public void There_is_a_version_12_step() {
      Assert.Single(SchemaManager.Migrations, m => m.Version == 12);
    }

    [Fact]
    public void V11_database_migrates_to_12_with_reserving_off_and_keeps_the_rest() {
      BuildV11Db();
      DataStore.Upgrade();
      Assert.True(db.Scalar("PRAGMA user_version") >= 12);
      Assert.Equal(1, Columns("MDSHOWindows", "ReserveDockedSpace"));
      DesktopWindow window = DataStore.Windows.GetAll().Single();
      Assert.False(window.ReserveDockedSpace);
      Assert.True(window.SingleClickToOpen);
      Assert.True(window.CommentWrapText);
      Assert.Contains(Directory.GetFiles(db.Folder, "test.sqlite3.bak-v*"), p => p.Contains("bak-v11-"));
    }

    [Fact]
    public void Running_the_v12_step_twice_does_nothing_the_second_time() {
      BuildV11Db();
      DataStore.Upgrade();
      using (SqliteConnection c = db.Open()) {
        SchemaManager.Migrations.Single(m => m.Version == 12).Apply(c);
      }
      Assert.Equal(1, Columns("MDSHOWindows", "ReserveDockedSpace"));
    }

    [Fact]
    public void A_new_database_has_the_column_and_new_panels_do_not_reserve() {
      db = new TempDb();
      Assert.Equal(1, Columns("MDSHOWindows", "ReserveDockedSpace"));
      Assert.All(DataStore.Windows.GetAll(), w => Assert.False(w.ReserveDockedSpace));
    }

    [Fact]
    public void Reserving_is_saved_with_the_panel() {
      db = new TempDb();
      long windowId = DataStore.Windows.GetAll().First().ID;
      DataStore.Windows.UpdateReserveDockedSpace(windowId, true);
      Assert.True(DataStore.Windows.GetAll().Single(w => w.ID == windowId).ReserveDockedSpace);
      DataStore.Windows.UpdateReserveDockedSpace(windowId, false);
      Assert.False(DataStore.Windows.GetAll().Single(w => w.ID == windowId).ReserveDockedSpace);
    }

    [Fact]
    public void A_panel_added_or_updated_keeps_its_reserving() {
      db = new TempDb();
      DesktopWindow window = DataStore.Windows.GetAll().First();
      window.ReserveDockedSpace = true;
      window.ID = 0;
      long id = DataStore.Windows.Add(window);
      Assert.True(DataStore.Windows.Get(id).ReserveDockedSpace);
      DesktopWindow read = DataStore.Windows.Get(id);
      read.ReserveDockedSpace = false;
      DataStore.Windows.Update(read);
      Assert.False(DataStore.Windows.Get(id).ReserveDockedSpace);
    }

    [Fact]
    public void Export_writes_reserving_and_an_old_export_imports_with_it_off() {
      DesktopWindow window = new DesktopWindow { Title = "W", ReserveDockedSpace = true };
      string json = System.Text.Json.JsonSerializer.Serialize(window, PortabilityJson.Options);
      Assert.Contains("\"reserveDockedSpace\": true", json);
      DesktopWindow old = System.Text.Json.JsonSerializer.Deserialize<DesktopWindow>("{\"id\":1,\"title\":\"W\"}", PortabilityJson.Options);
      Assert.False(old.ReserveDockedSpace);
    }
  }
}
