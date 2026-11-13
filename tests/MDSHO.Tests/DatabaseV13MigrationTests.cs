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
  /// Database version 13 adds MDSHOWindows.ShowVersionInTitle: the title bar of a panel (and its clones) shows the MDSHO
  /// version after the title. On for every panel, existing ones included.
  /// </summary>
  public class DatabaseV13MigrationTests : IDisposable {
    private TempDb db;
    public void Dispose() { if (db != null) { db.Dispose(); } }

    // A version 12 database: the version 9 schema with the version 10 to 12 steps applied.
    private void BuildV12Db() {
      db = new TempDb(false);
      db.Exec("PRAGMA user_version = 9;" + DatabaseV10MigrationTests.V9Sql
        + "INSERT INTO MDSHOWindows (ID,Title,ScreenDeviceName,Left,Top,Width,Height,BorderColor,BackgroundColor,TextColor,TitleBackgroundColor,TitleTextColor,Notes,SolidIcons,SingleClickToOpen) VALUES "
        + "(1,'W1','D1',0,0,300,400,'#FF000001','#FF000002','#FF000003','#FF000004','#FF000005','',1,1);");
      using (SqliteConnection c = db.Open()) {
        SchemaManager.Migrations.Single(m => m.Version == 10).Apply(c);
        SchemaManager.Migrations.Single(m => m.Version == 11).Apply(c);
        SchemaManager.Migrations.Single(m => m.Version == 12).Apply(c);
      }
      db.Exec("PRAGMA user_version = 12; UPDATE MDSHOWindows SET CommentWrapText = 1, ReserveDockedSpace = 1;");
    }

    private long Columns(string table, string column) {
      return db.Scalar("SELECT count(*) FROM pragma_table_info('" + table + "') WHERE name = '" + column + "' COLLATE NOCASE");
    }

    [Fact]
    public void Version_is_13_and_the_last_migration_is_13() {
      Assert.Equal(13, AppConstants.DatabaseVersion);
      Assert.Equal(13, SchemaManager.Migrations.Last().Version);
    }

    [Fact]
    public void V12_database_migrates_to_13_with_the_version_shown_and_keeps_the_rest() {
      BuildV12Db();
      DataStore.Upgrade();
      Assert.Equal(13, db.Scalar("PRAGMA user_version"));
      Assert.Equal(1, Columns("MDSHOWindows", "ShowVersionInTitle"));
      DesktopWindow window = DataStore.Windows.GetAll().Single();
      Assert.True(window.ShowVersionInTitle);
      Assert.True(window.SingleClickToOpen);
      Assert.True(window.CommentWrapText);
      Assert.True(window.ReserveDockedSpace);
      Assert.Contains(Directory.GetFiles(db.Folder, "test.sqlite3.bak-v*"), p => p.Contains("bak-v12-"));
    }

    [Fact]
    public void Running_the_v13_step_twice_does_nothing_the_second_time() {
      BuildV12Db();
      DataStore.Upgrade();
      using (SqliteConnection c = db.Open()) {
        SchemaManager.Migrations.Single(m => m.Version == 13).Apply(c);
      }
      Assert.Equal(1, Columns("MDSHOWindows", "ShowVersionInTitle"));
    }

    [Fact]
    public void A_new_database_has_the_column_and_new_panels_show_the_version() {
      db = new TempDb();
      Assert.Equal(1, Columns("MDSHOWindows", "ShowVersionInTitle"));
      Assert.All(DataStore.Windows.GetAll(), w => Assert.True(w.ShowVersionInTitle));
      Assert.True(DataStore.Windows.Create("D1", 0, 0).ShowVersionInTitle);
    }

    [Fact]
    public void The_setting_is_saved_with_the_panel() {
      db = new TempDb();
      long windowId = DataStore.Windows.GetAll().First().ID;
      DataStore.Windows.UpdateShowVersionInTitle(windowId, false);
      Assert.False(DataStore.Windows.GetAll().Single(w => w.ID == windowId).ShowVersionInTitle);
      DataStore.Windows.UpdateShowVersionInTitle(windowId, true);
      Assert.True(DataStore.Windows.GetAll().Single(w => w.ID == windowId).ShowVersionInTitle);
    }

    [Fact]
    public void A_panel_added_or_updated_keeps_the_setting() {
      db = new TempDb();
      DesktopWindow window = DataStore.Windows.GetAll().First();
      window.ShowVersionInTitle = false;
      window.ID = 0;
      long id = DataStore.Windows.Add(window);
      Assert.False(DataStore.Windows.Get(id).ShowVersionInTitle);
      DesktopWindow read = DataStore.Windows.Get(id);
      read.ShowVersionInTitle = true;
      DataStore.Windows.Update(read);
      Assert.True(DataStore.Windows.Get(id).ShowVersionInTitle);
    }

    [Fact]
    public void Export_writes_the_setting_and_an_old_export_imports_with_it_on() {
      DesktopWindow window = new DesktopWindow { Title = "W", ShowVersionInTitle = false };
      string json = System.Text.Json.JsonSerializer.Serialize(window, PortabilityJson.Options);
      Assert.Contains("\"showVersionInTitle\": false", json);
      DesktopWindow old = System.Text.Json.JsonSerializer.Deserialize<DesktopWindow>("{\"id\":1,\"title\":\"W\"}", PortabilityJson.Options);
      Assert.True(old.ShowVersionInTitle);
    }
  }
}
