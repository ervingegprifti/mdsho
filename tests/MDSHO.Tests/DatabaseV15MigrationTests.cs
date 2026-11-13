using System;
using System.Linq;
using Microsoft.Data.Sqlite;
using MDSHO.Common;
using MDSHO.Core;
using MDSHO.Data;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// Database version 15 adds the DefaultColors table: the colours saved with "Set as default" in the Colours window, one
  /// row for panels and one for notes. No row means MDSHO's own colours.
  /// </summary>
  public class DatabaseV15MigrationTests : IDisposable {
    private TempDb db;
    public void Dispose() { if (db != null) { db.Dispose(); } }

    // A version 14 database: the version 9 schema with the version 10 to 14 steps applied.
    private void BuildV14Db() {
      db = new TempDb(false);
      db.Exec("PRAGMA user_version = 9;" + DatabaseV10MigrationTests.V9Sql
        + "INSERT INTO MDSHOWindows (ID,Title,ScreenDeviceName,Left,Top,Width,Height,BorderColor,BackgroundColor,TextColor,TitleBackgroundColor,TitleTextColor,Notes,SolidIcons,SingleClickToOpen) VALUES "
        + "(1,'W1','D1',0,0,300,400,'#FF000001','#FF000002','#FF000003','#FF000004','#FF000005','',1,1);");
      using (SqliteConnection c = db.Open()) {
        foreach (int version in new[] { 10, 11, 12, 13, 14 }) {
          SchemaManager.Migrations.Single(m => m.Version == version).Apply(c);
        }
      }
      db.Exec("PRAGMA user_version = 14;");
    }

    private long Tables(string name) {
      return db.Scalar("SELECT count(*) FROM sqlite_master WHERE type = 'table' AND name = '" + name + "'");
    }

    private static ColorSet Set(string prefix) {
      return new ColorSet { BorderColor = prefix + "1", TitleBackgroundColor = prefix + "2", TitleTextColor = prefix + "3", BackgroundColor = prefix + "4", TextColor = prefix + "5" };
    }

    [Fact]
    public void Version_is_15_and_the_last_migration_is_15() {
      Assert.Equal(15, AppConstants.DatabaseVersion);
      Assert.Equal(15, SchemaManager.Migrations.Last().Version);
    }

    [Fact]
    public void V14_database_migrates_to_15_with_an_empty_DefaultColors_table_and_keeps_the_rest() {
      BuildV14Db();
      DataStore.Upgrade();
      Assert.Equal(15, db.Scalar("PRAGMA user_version"));
      Assert.Equal(1, Tables("DefaultColors"));
      Assert.Equal(0, db.Scalar("SELECT count(*) FROM DefaultColors"));
      Assert.Equal("W1", DataStore.Windows.GetAll().Single().Title);
      Assert.Null(DataStore.DefaultColors.Get(ColorSetKind.Window));
      Assert.Null(DataStore.DefaultColors.Get(ColorSetKind.Note));
    }

    [Fact]
    public void Running_the_v15_step_twice_does_nothing_the_second_time() {
      BuildV14Db();
      DataStore.Upgrade();
      DataStore.DefaultColors.Save(ColorSetKind.Note, Set("#FF00000"));
      using (SqliteConnection c = db.Open()) {
        SchemaManager.Migrations.Single(m => m.Version == 15).Apply(c);
      }
      Assert.Equal("#FF000001", DataStore.DefaultColors.Get(ColorSetKind.Note).BorderColor);
    }

    [Fact]
    public void A_new_database_has_the_table() {
      db = new TempDb();
      Assert.Equal(1, Tables("DefaultColors"));
    }

    [Fact]
    public void Panel_and_note_defaults_are_saved_and_read_back_apart_and_saving_again_replaces_them() {
      db = new TempDb();
      DataStore.DefaultColors.Save(ColorSetKind.Window, Set("#FF10000"));
      Assert.Null(DataStore.DefaultColors.Get(ColorSetKind.Note));
      DataStore.DefaultColors.Save(ColorSetKind.Note, Set("#FF20000"));
      DataStore.DefaultColors.Save(ColorSetKind.Window, Set("#FF30000"));
      ColorSet window = DataStore.DefaultColors.Get(ColorSetKind.Window);
      ColorSet note = DataStore.DefaultColors.Get(ColorSetKind.Note);
      Assert.Equal(new[] { "#FF300001", "#FF300002", "#FF300003", "#FF300004", "#FF300005" },
        new[] { window.BorderColor, window.TitleBackgroundColor, window.TitleTextColor, window.BackgroundColor, window.TextColor });
      Assert.Equal(new[] { "#FF200001", "#FF200002", "#FF200003", "#FF200004", "#FF200005" },
        new[] { note.BorderColor, note.TitleBackgroundColor, note.TitleTextColor, note.BackgroundColor, note.TextColor });
      Assert.Equal(2, db.Scalar("SELECT count(*) FROM DefaultColors"));
    }
  }
}
