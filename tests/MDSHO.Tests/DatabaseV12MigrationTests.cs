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
  /// Database version 12 added MDSHOWindows.ReserveDockedSpace ("Reserve screen space when docked"). The feature was
  /// removed and version 14 drops the column again; the step stays so the version numbers keep their meaning.
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
    public void The_v12_step_adds_the_column_once_and_keeps_the_rest() {
      BuildV11Db();
      using (SqliteConnection c = db.Open()) {
        SchemaManager.Migrations.Single(m => m.Version == 12).Apply(c);
        SchemaManager.Migrations.Single(m => m.Version == 12).Apply(c);
      }
      Assert.Equal(1, Columns("MDSHOWindows", "ReserveDockedSpace"));
      Assert.Equal(0, db.Scalar("SELECT ReserveDockedSpace FROM MDSHOWindows"));
      Assert.Equal(1, db.Scalar("SELECT CommentWrapText FROM MDSHOWindows"));
    }

    [Fact]
    public void V11_database_migrates_to_the_latest_without_the_column() {
      BuildV11Db();
      DataStore.Upgrade();
      Assert.Equal(AppConstants.DatabaseVersion, db.Scalar("PRAGMA user_version"));
      Assert.Equal(0, Columns("MDSHOWindows", "ReserveDockedSpace"));
      Assert.True(DataStore.Windows.GetAll().Single().CommentWrapText);
      Assert.Contains(Directory.GetFiles(db.Folder, "test.sqlite3.bak-v*"), p => p.Contains("bak-v11-"));
    }
  }
}
