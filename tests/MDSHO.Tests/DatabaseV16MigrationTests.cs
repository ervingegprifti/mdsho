using System;
using System.Linq;
using Microsoft.Data.Sqlite;
using MDSHO.Common;
using MDSHO.Data;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// Database version 16 adds the AppSettings table: settings of the whole app on this PC, one row per setting (the first
  /// is the "Bring panels to front" shortcut). No row means the setting's default.
  /// </summary>
  public class DatabaseV16MigrationTests : IDisposable {
    private TempDb db;
    public void Dispose() { if (db != null) { db.Dispose(); } }

    // A version 15 database: the version 9 schema with the version 10 to 15 steps applied.
    private void BuildV15Db() {
      db = new TempDb(false);
      db.Exec("PRAGMA user_version = 9;" + DatabaseV10MigrationTests.V9Sql
        + "INSERT INTO MDSHOWindows (ID,Title,ScreenDeviceName,Left,Top,Width,Height,BorderColor,BackgroundColor,TextColor,TitleBackgroundColor,TitleTextColor,Notes,SolidIcons,SingleClickToOpen) VALUES "
        + "(1,'W1','D1',0,0,300,400,'#FF000001','#FF000002','#FF000003','#FF000004','#FF000005','',1,1);");
      using (SqliteConnection c = db.Open()) {
        foreach (int version in new[] { 10, 11, 12, 13, 14, 15 }) {
          SchemaManager.Migrations.Single(m => m.Version == version).Apply(c);
        }
      }
      db.Exec("PRAGMA user_version = 15;");
    }

    private long Tables(string name) {
      return db.Scalar("SELECT count(*) FROM sqlite_master WHERE type = 'table' AND name = '" + name + "'");
    }

    [Fact]
    public void Version_16_is_a_known_step() {
      Assert.True(AppConstants.DatabaseVersion >= 16);
      Assert.Single(SchemaManager.Migrations, m => m.Version == 16);
    }

    [Fact]
    public void V15_database_migrates_to_16_with_an_empty_AppSettings_table_and_keeps_the_rest() {
      BuildV15Db();
      DataStore.Upgrade();
      Assert.Equal(AppConstants.DatabaseVersion, db.Scalar("PRAGMA user_version"));
      Assert.Equal(1, Tables("AppSettings"));
      Assert.Equal(0, db.Scalar("SELECT count(*) FROM AppSettings WHERE Key <> 'DeviceId'")); // the DeviceId comes with version 27
      Assert.Equal("W1", DataStore.Windows.GetAll().Single().Title);
      Assert.Null(DataStore.AppSettings.Get("BringToFrontShortcut"));
    }

    [Fact]
    public void Running_the_v16_step_twice_does_nothing_the_second_time() {
      BuildV15Db();
      DataStore.Upgrade();
      DataStore.AppSettings.Set("BringToFrontShortcut", "Off");
      using (SqliteConnection c = db.Open()) {
        SchemaManager.Migrations.Single(m => m.Version == 16).Apply(c);
      }
      Assert.Equal("Off", DataStore.AppSettings.Get("BringToFrontShortcut"));
    }

    [Fact]
    public void A_new_database_has_the_table() {
      db = new TempDb();
      Assert.Equal(1, Tables("AppSettings"));
    }

    [Fact]
    public void A_setting_is_saved_read_back_and_replaced_by_saving_again() {
      db = new TempDb();
      Assert.Null(DataStore.AppSettings.Get("A"));
      DataStore.AppSettings.Set("A", "1");
      DataStore.AppSettings.Set("B", "2");
      DataStore.AppSettings.Set("A", "3");
      Assert.Equal("3", DataStore.AppSettings.Get("A"));
      Assert.Equal("2", DataStore.AppSettings.Get("B"));
      Assert.Equal(2, db.Scalar("SELECT count(*) FROM AppSettings WHERE Key <> 'DeviceId'"));
    }
  }
}
