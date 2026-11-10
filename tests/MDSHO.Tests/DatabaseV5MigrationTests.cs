using System;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using MDSHO.Common;
using MDSHO.Data;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>Database version 5 drops the RoundedCorners column from the windows and clones tables.</summary>
  public class DatabaseV5MigrationTests : IDisposable {
    private TempDb db;
    public void Dispose() { if (db != null) { db.Dispose(); } }

    // The version 4 schema exactly as 1.1.1 builds before this change.
    private const string V4Sql = @"
        CREATE TABLE MDSHOWindows (
          ID INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT UNIQUE, Title TEXT NOT NULL, ScreenDeviceName TEXT NOT NULL,
          Left INTEGER NOT NULL, Top INTEGER NOT NULL, Width INTEGER NOT NULL, Height INTEGER NOT NULL,
          BorderColor TEXT NOT NULL, BackgroundColor TEXT NOT NULL, TextColor TEXT NOT NULL, TitleBackgroundColor TEXT NOT NULL,
          TitleTextColor TEXT NOT NULL, Notes TEXT NOT NULL, SolidIcons INTEGER NOT NULL, RoundedCorners INTEGER NOT NULL,
          SingleClickToOpen INTEGER NOT NULL);
        CREATE TABLE MDSHOWindowClones (
          ID INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT UNIQUE, ParentMDSHOWindowID INTEGER NOT NULL, ScreenDeviceName TEXT NOT NULL,
          Left INTEGER NOT NULL, Top INTEGER NOT NULL, Width INTEGER NOT NULL, Height INTEGER NOT NULL, RoundedCorners INTEGER NOT NULL,
          FOREIGN KEY(ParentMDSHOWindowID) REFERENCES MDSHOWindows(ID) ON DELETE CASCADE);
        CREATE TABLE Shortcuts (
          ID INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT UNIQUE, ParentShortcutID INTEGER NULL, ParentMDSHOWindowID INTEGER NULL,
          Name TEXT NOT NULL, Target TEXT NOT NULL, Ext TEXT NOT NULL, Guid TEXT NOT NULL, IsGroup INTEGER NOT NULL,
          IsGroupExpanded INTEGER NOT NULL, Notes TEXT NOT NULL, IsMarked INTEGER NOT NULL, MarkColor TEXT NOT NULL,
          Position INTEGER NOT NULL, IsProcessCommand INTEGER NOT NULL, ProcessFileName TEXT NOT NULL, ProcessArguments TEXT NOT NULL,
          ProcessRunAsAdmin INTEGER NOT NULL DEFAULT 0, ProcessWorkingFolder TEXT NOT NULL DEFAULT '',
          FOREIGN KEY(ParentShortcutID) REFERENCES Shortcuts(ID) ON DELETE CASCADE,
          FOREIGN KEY(ParentMDSHOWindowID) REFERENCES MDSHOWindows(ID) ON DELETE CASCADE);
        CREATE INDEX IX_Shortcuts_ParentShortcutID ON Shortcuts(ParentShortcutID);
        CREATE INDEX IX_Shortcuts_ParentMDSHOWindowID ON Shortcuts(ParentMDSHOWindowID);
        CREATE INDEX IX_MDSHOWindowClones_ParentMDSHOWindowID ON MDSHOWindowClones(ParentMDSHOWindowID);";

    private void BuildV4Db(bool withRoundedCorners) {
      db = new TempDb(false);
      string sql = V4Sql;
      if (!withRoundedCorners) {
        sql = sql.Replace(" RoundedCorners INTEGER NOT NULL,", "");
      }
      string rcW1 = withRoundedCorners ? ",1" : "";
      string rcW2 = withRoundedCorners ? ",0" : "";
      db.Exec("PRAGMA user_version = 4;" + sql
        + "INSERT INTO MDSHOWindows (ID,Title,ScreenDeviceName,Left,Top,Width,Height,BorderColor,BackgroundColor,TextColor,TitleBackgroundColor,TitleTextColor,Notes,SolidIcons,SingleClickToOpen" + (withRoundedCorners ? ",RoundedCorners" : "") + ") VALUES "
        + "(1,'W1 ü','D1',-1,2,300,400,'#FF000001','#FF000002','#FF000003','#FF000004','#FF000005','n1',1,0" + rcW1 + "),"
        + "(2,'W2','D2',5,6,310,410,'#FF000011','#FF000012','#FF000013','#FF000014','#FF000015','n2',0,1" + rcW2 + ");"
        + "INSERT INTO MDSHOWindowClones (ID,ParentMDSHOWindowID,ScreenDeviceName,Left,Top,Width,Height" + (withRoundedCorners ? ",RoundedCorners" : "") + ") VALUES "
        + "(7,1,'D2',10,20,330,440" + rcW2 + "),(8,2,'D3',11,21,331,441" + rcW1 + ");"
        + "INSERT INTO Shortcuts VALUES (1,NULL,1,'G','','','g1',1,1,'',0,'#F',0,0,'','',0,'');"
        + "INSERT INTO Shortcuts VALUES (2,1,NULL,'S','t','.lnk','g2',0,0,'',0,'#F',0,1,'cmd.exe','/K',1,'C:\\x');"
        + "INSERT INTO Shortcuts VALUES (3,NULL,2,'S2','t','.lnk','g3',0,0,'',0,'#F',0,0,'','',0,'');");
    }

    private long Columns(string table, string column) {
      return db.Scalar("SELECT count(*) FROM pragma_table_info('" + table + "') WHERE name = '" + column + "' COLLATE NOCASE");
    }
    private string Text(string sql) {
      using (SqliteConnection c = db.Open()) {
        SqliteCommand cmd = c.CreateCommand();
        cmd.CommandText = sql;
        return Convert.ToString(cmd.ExecuteScalar());
      }
    }
    private string[] Baks() { return Directory.GetFiles(db.Folder, "test.sqlite3.bak-v*"); }

    [Fact]
    public void Version_is_5_and_the_last_migration_is_5() {
      Assert.Equal(5, AppConstants.DatabaseVersion);
      Assert.Equal(5, SchemaManager.Migrations.Last().Version);
    }

    [Fact]
    public void V4_database_migrates_to_5_dropping_RoundedCorners_and_keeping_everything_else() {
      BuildV4Db(true);
      string windowsBefore = Text("SELECT group_concat(ID||'|'||Title||'|'||ScreenDeviceName||'|'||Left||'|'||Top||'|'||Width||'|'||Height||'|'||BorderColor||'|'||BackgroundColor||'|'||TextColor||'|'||TitleBackgroundColor||'|'||TitleTextColor||'|'||Notes||'|'||SolidIcons||'|'||SingleClickToOpen, ';') FROM (SELECT * FROM MDSHOWindows ORDER BY ID)");
      string clonesBefore = Text("SELECT group_concat(ID||'|'||ParentMDSHOWindowID||'|'||ScreenDeviceName||'|'||Left||'|'||Top||'|'||Width||'|'||Height, ';') FROM (SELECT * FROM MDSHOWindowClones ORDER BY ID)");
      string shortcutsBefore = Text("SELECT group_concat(ID||'|'||ifnull(ParentShortcutID,'')||'|'||ifnull(ParentMDSHOWindowID,'')||'|'||Name||'|'||ProcessFileName||'|'||ProcessArguments||'|'||ProcessRunAsAdmin||'|'||ProcessWorkingFolder, ';') FROM (SELECT * FROM Shortcuts ORDER BY ID)");

      DataStore.Upgrade();

      Assert.Equal(5, db.Scalar("PRAGMA user_version"));
      Assert.Equal(0, Columns("MDSHOWindows", "RoundedCorners"));
      Assert.Equal(0, Columns("MDSHOWindowClones", "RoundedCorners"));
      Assert.Equal(windowsBefore, Text("SELECT group_concat(ID||'|'||Title||'|'||ScreenDeviceName||'|'||Left||'|'||Top||'|'||Width||'|'||Height||'|'||BorderColor||'|'||BackgroundColor||'|'||TextColor||'|'||TitleBackgroundColor||'|'||TitleTextColor||'|'||Notes||'|'||SolidIcons||'|'||SingleClickToOpen, ';') FROM (SELECT * FROM MDSHOWindows ORDER BY ID)"));
      Assert.Equal(clonesBefore, Text("SELECT group_concat(ID||'|'||ParentMDSHOWindowID||'|'||ScreenDeviceName||'|'||Left||'|'||Top||'|'||Width||'|'||Height, ';') FROM (SELECT * FROM MDSHOWindowClones ORDER BY ID)"));
      Assert.Equal(shortcutsBefore, Text("SELECT group_concat(ID||'|'||ifnull(ParentShortcutID,'')||'|'||ifnull(ParentMDSHOWindowID,'')||'|'||Name||'|'||ProcessFileName||'|'||ProcessArguments||'|'||ProcessRunAsAdmin||'|'||ProcessWorkingFolder, ';') FROM (SELECT * FROM Shortcuts ORDER BY ID)"));
      // Indexes, foreign keys and AUTOINCREMENT survive.
      Assert.Equal(3, db.Scalar("SELECT count(*) FROM sqlite_master WHERE type='index' AND name IN ('IX_Shortcuts_ParentShortcutID','IX_Shortcuts_ParentMDSHOWindowID','IX_MDSHOWindowClones_ParentMDSHOWindowID')"));
      Assert.Equal(1, db.Scalar("SELECT count(*) FROM pragma_foreign_key_list('MDSHOWindowClones') WHERE \"table\"='MDSHOWindows' AND on_delete='CASCADE'"));
      Assert.Equal(2, db.Scalar("SELECT count(*) FROM pragma_foreign_key_list('Shortcuts') WHERE on_delete='CASCADE'"));
      Assert.Equal(0, db.Scalar("SELECT count(*) FROM pragma_foreign_key_check"));
      Assert.Equal(2, db.Scalar("SELECT seq FROM sqlite_sequence WHERE name='MDSHOWindows'"));
      // The old database was backed up first, with the column still there.
      Assert.Single(Baks());
      Assert.Contains("bak-v4-", Baks()[0]);
      using (SqliteConnection c = new SqliteConnection("Data Source=" + Baks()[0] + ";Mode=ReadOnly;Pooling=False")) {
        c.Open();
        SqliteCommand cmd = c.CreateCommand();
        cmd.CommandText = "SELECT sum(RoundedCorners) FROM MDSHOWindows";
        Assert.Equal(1L, Convert.ToInt64(cmd.ExecuteScalar()));
      }
      // Deleting a window still cascades to its clone and shortcuts.
      db.Exec("PRAGMA foreign_keys = ON; DELETE FROM MDSHOWindows WHERE ID = 2;");
      Assert.Equal(0, db.Scalar("SELECT count(*) FROM MDSHOWindowClones WHERE ID = 8"));
      // The data loads through the normal code.
      Assert.Single(DataStore.Windows.LoadAll());
    }

    [Fact]
    public void Migration_tolerates_the_column_already_being_absent() {
      BuildV4Db(false);
      DataStore.Upgrade();
      Assert.Equal(5, db.Scalar("PRAGMA user_version"));
      Assert.Equal(0, Columns("MDSHOWindows", "RoundedCorners"));
      Assert.Equal(2, db.Scalar("SELECT count(*) FROM MDSHOWindowClones"));
    }

    [Fact]
    public void Running_the_v5_step_twice_does_nothing_the_second_time() {
      BuildV4Db(true);
      DataStore.Upgrade();
      using (SqliteConnection c = db.Open()) {
        SchemaManager.Migrations.Single(m => m.Version == 5).Apply(c);
      }
      Assert.Equal(0, Columns("MDSHOWindowClones", "RoundedCorners"));
      Assert.Equal(2, db.Scalar("SELECT count(*) FROM MDSHOWindows"));
    }

    [Fact]
    public void Fresh_database_is_version_5_without_RoundedCorners() {
      db = new TempDb();
      Assert.Equal(5, db.Scalar("PRAGMA user_version"));
      Assert.Equal(0, Columns("MDSHOWindows", "RoundedCorners"));
      Assert.Equal(0, Columns("MDSHOWindowClones", "RoundedCorners"));
      // New windows and clones can be saved without the column.
      long w = DataStore.Windows.Create("D", 1, 2).ID;
      DataStore.Clones.Create(w, "D", 3, 4);
      Assert.Equal(1, db.Scalar("SELECT count(*) FROM MDSHOWindowClones"));
    }

    [Fact]
    public void Bundled_SQLite_supports_DROP_COLUMN() {
      // ALTER TABLE ... DROP COLUMN needs SQLite 3.35 or later; the app ships its own SQLite, so this pins it.
      db = new TempDb();
      Version v = new Version(Text("SELECT sqlite_version()"));
      Assert.True(v >= new Version(3, 35), "bundled SQLite is " + v);
    }
  }
}
