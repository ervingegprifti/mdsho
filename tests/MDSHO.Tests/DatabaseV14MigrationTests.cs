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
  /// Database version 14 drops MDSHOWindows.ReserveDockedSpace: "Reserve screen space when docked" was removed (Windows
  /// lays out the desktop icons again on every change of a reserved strip). The rows and every other column stay.
  /// </summary>
  public class DatabaseV14MigrationTests : IDisposable {
    private TempDb db;
    public void Dispose() { if (db != null) { db.Dispose(); } }

    // A version 13 database: the version 9 schema with the version 10 to 13 steps applied, reserving on.
    private void BuildV13Db() {
      db = new TempDb(false);
      db.Exec("PRAGMA user_version = 9;" + DatabaseV10MigrationTests.V9Sql
        + "INSERT INTO MDSHOWindows (ID,Title,ScreenDeviceName,Left,Top,Width,Height,BorderColor,BackgroundColor,TextColor,TitleBackgroundColor,TitleTextColor,Notes,SolidIcons,SingleClickToOpen) VALUES "
        + "(1,'W1','D1',0,0,300,400,'#FF000001','#FF000002','#FF000003','#FF000004','#FF000005','',1,1);");
      using (SqliteConnection c = db.Open()) {
        foreach (int version in new[] { 10, 11, 12, 13 }) {
          SchemaManager.Migrations.Single(m => m.Version == version).Apply(c);
        }
      }
      db.Exec("PRAGMA user_version = 13; UPDATE MDSHOWindows SET CommentWrapText = 1, ReserveDockedSpace = 1, ShowVersionInTitle = 0;");
    }

    private long Columns(string table, string column) {
      return db.Scalar("SELECT count(*) FROM pragma_table_info('" + table + "') WHERE name = '" + column + "' COLLATE NOCASE");
    }

    [Fact]
    public void There_is_a_version_14_step() {
      Assert.Contains(SchemaManager.Migrations, m => m.Version == 14);
      Assert.True(AppConstants.DatabaseVersion >= 14);
    }

    [Fact]
    public void V13_database_migrates_to_14_without_the_column_and_keeps_the_rest() {
      BuildV13Db();
      DataStore.Upgrade();
      Assert.True(db.Scalar("PRAGMA user_version") >= 14);
      Assert.Equal(0, Columns("MDSHOWindows", "ReserveDockedSpace"));
      DesktopWindow window = DataStore.Windows.GetAll().Single();
      Assert.Equal("W1", window.Title);
      Assert.True(window.SingleClickToOpen);
      Assert.True(window.CommentWrapText);
      Assert.False(window.ShowVersionInTitle);
      Assert.Contains(Directory.GetFiles(db.Folder, "test.sqlite3.bak-v*"), p => p.Contains("bak-v13-"));
    }

    [Fact]
    public void Running_the_v14_step_twice_does_nothing_the_second_time() {
      BuildV13Db();
      DataStore.Upgrade();
      using (SqliteConnection c = db.Open()) {
        SchemaManager.Migrations.Single(m => m.Version == 14).Apply(c);
      }
      Assert.Equal(0, Columns("MDSHOWindows", "ReserveDockedSpace"));
    }

    [Fact]
    public void A_new_database_has_no_such_column() {
      db = new TempDb();
      Assert.Equal(0, Columns("MDSHOWindows", "ReserveDockedSpace"));
    }

    [Fact]
    public void Exports_leave_it_out_and_an_export_that_has_it_still_imports() {
      string json = System.Text.Json.JsonSerializer.Serialize(new DesktopWindow { Title = "W" }, PortabilityJson.Options);
      Assert.DoesNotContain("reserveDockedSpace", json);
      DesktopWindow old = System.Text.Json.JsonSerializer.Deserialize<DesktopWindow>("{\"id\":1,\"title\":\"W\",\"reserveDockedSpace\":true}", PortabilityJson.Options);
      Assert.Equal("W", old.Title);
    }

    [Fact]
    public void The_panel_has_no_reserve_setting_any_more() {
      Assert.Null(typeof(DesktopWindow).GetProperty("ReserveDockedSpace"));
      Assert.Null(typeof(MDSHO.ViewModels.DesktopWindowAppearance).GetProperty("ReserveDockedSpace"));
      Assert.Null(typeof(AppConstants).Assembly.GetType("MDSHO.Common.DockedSpace"));
      Assert.Null(typeof(AppConstants).Assembly.GetType("MDSHO.Common.IAppBarService"));
    }
  }
}
