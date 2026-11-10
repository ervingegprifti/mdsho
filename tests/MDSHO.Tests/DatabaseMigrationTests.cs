using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Microsoft.Data.Sqlite;
using MDSHO.Common;
using MDSHO.Core;
using MDSHO.Data;
using Xunit;
using Xunit.Abstractions;

namespace MDSHO.Tests {

  public class DatabaseMigrationTests : IDisposable {
    private readonly ITestOutputHelper output;
    private TempDb db;

    public DatabaseMigrationTests(ITestOutputHelper output) { this.output = output; }
    public void Dispose() {
      SchemaManager.CreateDatabaseTestHook = null;
      if (db != null) { db.Dispose(); }
    }

    private const string OldWindowsSql = @"CREATE TABLE MDSHOWindows (
      ID INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT UNIQUE, Title TEXT NOT NULL, ScreenDeviceName TEXT NOT NULL,
      Left INTEGER NOT NULL, Top INTEGER NOT NULL, Width INTEGER NOT NULL, Height INTEGER NOT NULL,
      BorderColor TEXT NOT NULL, BackgroundColor TEXT NOT NULL, TextColor TEXT NOT NULL, TitleBackgroundColor TEXT NOT NULL,
      TitleTextColor TEXT NOT NULL, Notes TEXT NOT NULL, SolidIcons INTEGER NOT NULL, RoundedCorners INTEGER NOT NULL,
      SingleClickToOpen INTEGER NOT NULL);";
    private const string OldShortcutsSql = @"CREATE TABLE Shortcuts (
      ID INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT UNIQUE, ParentShortcutID INTEGER NULL, ParentMDSHOWindowID INTEGER NULL,
      Name TEXT NOT NULL, Target TEXT NOT NULL, Ext TEXT NOT NULL, Guid TEXT NOT NULL, IsGroup INTEGER NOT NULL,
      IsGroupExpanded INTEGER NOT NULL, Notes TEXT NOT NULL, IsMarked INTEGER NOT NULL, MarkColor TEXT NOT NULL,
      Position INTEGER NOT NULL, IsProcessCommand INTEGER NOT NULL, ProcessFileName TEXT NOT NULL, ProcessArguments TEXT NOT NULL,
      FOREIGN KEY(ParentShortcutID) REFERENCES Shortcuts(ID) ON DELETE CASCADE,
      FOREIGN KEY(ParentMDSHOWindowID) REFERENCES MDSHOWindows(ID) ON DELETE CASCADE);";

    /// <summary>Builds an old-shape database (the original CREATE statements) with sample rows.</summary>
    private void BuildOldDb(int userVersion, bool withRoundedCornersOnClones) {
      db = new TempDb(false);
      string cloneRc = withRoundedCornersOnClones ? ", RoundedCorners INTEGER NOT NULL" : "";
      string cloneRcValue = withRoundedCornersOnClones ? ", 1" : "";
      db.Exec("PRAGMA user_version = " + userVersion + ";" + OldWindowsSql
        + @"CREATE TABLE MDSHOWindowClones (ID INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT UNIQUE, ParentMDSHOWindowID INTEGER NOT NULL,
          ScreenDeviceName TEXT NOT NULL, Left INTEGER NOT NULL, Top INTEGER NOT NULL, Width INTEGER NOT NULL, Height INTEGER NOT NULL" + cloneRc
        + ", FOREIGN KEY(ParentMDSHOWindowID) REFERENCES MDSHOWindows(ID) ON DELETE CASCADE);" + OldShortcutsSql
        + "INSERT INTO MDSHOWindows VALUES (1,'W1','D1',1,2,300,400,'#1','#2','#3','#4','#5','n',1,1,0);"
        + "INSERT INTO MDSHOWindows VALUES (2,'W2','D2',5,6,300,400,'#1','#2','#3','#4','#5','n',1,1,0);"
        + "INSERT INTO MDSHOWindowClones VALUES (1,1,'D2',10,20,300,400" + cloneRcValue + ");"
        + "INSERT INTO Shortcuts VALUES (1,NULL,1,'G','','','g1',1,1,'',0,'#F',0,0,'','');"
        + "INSERT INTO Shortcuts VALUES (2,1,NULL,'S','t','.lnk','g2',0,0,'',0,'#F',0,0,'','');"
        + "INSERT INTO Shortcuts VALUES (3,NULL,2,'S2','t','.lnk','g3',0,0,'',0,'#F',0,0,'','');");
    }

    private long Version() { return db.Scalar("PRAGMA user_version"); }
    private long Indexes() {
      return db.Scalar("SELECT count(*) FROM sqlite_master WHERE type='index' AND name IN ('IX_Shortcuts_ParentShortcutID','IX_Shortcuts_ParentMDSHOWindowID','IX_MDSHOWindowClones_ParentMDSHOWindowID')");
    }
    private string[] Baks() { return Directory.GetFiles(db.Folder, "test.sqlite3.bak-v*"); }
    private long Rows() {
      return db.Scalar("SELECT (SELECT count(*) FROM MDSHOWindows)*10000 + (SELECT count(*) FROM MDSHOWindowClones)*100 + (SELECT count(*) FROM Shortcuts)");
    }

    [Fact]
    public void New_database_has_latest_version_and_indexes() {
      db = new TempDb();
      Assert.Equal(AppConstants.DatabaseVersion, (int)Version());
      Assert.Equal(AppConstants.DatabaseVersion, SchemaManager.Migrations.Last().Version);
      Assert.Equal(3, Indexes());
      DataStore.Upgrade();
      Assert.Empty(Baks()); // already latest: nothing to migrate, no backup
    }

    [Fact]
    public void Old_database_upgrades_with_rows_intact_and_backup() {
      BuildOldDb(1, true);
      long before = Rows();
      DataStore.Upgrade();
      Assert.Equal(AppConstants.DatabaseVersion, (int)Version());
      Assert.Equal(3, Indexes());
      Assert.Equal(before, Rows());
      Assert.Single(Baks());
      Assert.Matches(@"test\.sqlite3\.bak-v1-\d{14}$", Baks()[0]);
      // The backup is the untouched old database.
      using (SqliteConnection c = new SqliteConnection("Data Source=" + Baks()[0] + ";Mode=ReadOnly;Pooling=False")) {
        c.Open();
        SqliteCommand cmd = c.CreateCommand();
        cmd.CommandText = "SELECT (SELECT count(*) FROM Shortcuts), (SELECT count(*) FROM sqlite_master WHERE type='index' AND name LIKE 'IX_%')";
        using (SqliteDataReader r = cmd.ExecuteReader()) {
          Assert.True(r.Read());
          Assert.Equal(3, r.GetInt64(0));
          Assert.Equal(0, r.GetInt64(1));
        }
      }
      // The data is still usable through the DAL.
      Assert.Equal(2, DataStore.Windows.LoadAll().Count);
    }

    [Fact]
    public void Version_zero_database_upgrades_to_latest() {
      BuildOldDb(0, true);
      DataStore.Upgrade();
      Assert.Equal(AppConstants.DatabaseVersion, (int)Version());
      Assert.Equal(3, Indexes());
      Assert.Single(Baks());
      Assert.Contains("bak-v0-", Baks()[0]);
    }

    [Fact]
    public void Pre_1_0_25_clones_table_gets_RoundedCorners_with_default_true_at_step_2() {
      BuildOldDb(1, false);
      // Step 2 alone (step 5 drops the column again later).
      SchemaManager.ApplyMigrations(SchemaManager.Migrations.Where(m => m.Version <= 2).ToList());
      Assert.Equal(2, (int)Version());
      Assert.Equal(1, db.Scalar("SELECT RoundedCorners FROM MDSHOWindowClones WHERE ID=1"));
      Assert.Equal(1, db.Scalar("SELECT count(*) FROM MDSHOWindowClones"));
      DataStore.Upgrade();
      Assert.Equal(0, db.Scalar("SELECT count(*) FROM pragma_table_info('MDSHOWindowClones') WHERE name='RoundedCorners'"));
      Assert.Equal(1, db.Scalar("SELECT count(*) FROM MDSHOWindowClones"));
      Assert.Equal(AppConstants.DatabaseVersion, (int)Version());
    }

    [Fact]
    public void Running_migrations_twice_is_a_noop() {
      BuildOldDb(1, true);
      DataStore.Upgrade();
      long rows = Rows();
      DataStore.Upgrade();
      DataStore.Upgrade();
      Assert.Equal(rows, Rows());
      Assert.Equal(AppConstants.DatabaseVersion, (int)Version());
      Assert.Single(Baks());
    }

    [Fact]
    public void Failing_migration_rolls_back_and_keeps_previous_version() {
      BuildOldDb(1, true);
      long before = Rows();
      List<DbMigration> list = SchemaManager.Migrations.ToList();
      list.Add(new DbMigration(AppConstants.DatabaseVersion + 1, "bad", c => {
        using (SqliteCommand cmd = c.CreateCommand()) {
          cmd.CommandText = "CREATE TABLE Zed (x INTEGER); DELETE FROM Shortcuts; CREATE INDEX IX_Zed ON Zed(x);";
          cmd.ExecuteNonQuery();
        }
        throw new InvalidOperationException("boom");
      }));
      Assert.ThrowsAny<Exception>(() => SchemaManager.ApplyMigrations(list));
      // Earlier good steps were committed, the failing one left no trace.
      Assert.Equal(AppConstants.DatabaseVersion, (int)Version());
      Assert.Equal(0, db.Scalar("SELECT count(*) FROM sqlite_master WHERE name IN ('Zed','IX_Zed')"));
      Assert.Equal(before, Rows());
      Assert.Single(Baks());
    }

    [Fact]
    public void Failing_first_step_leaves_database_at_old_version() {
      BuildOldDb(1, true);
      long before = Rows();
      List<DbMigration> list = new List<DbMigration> {
        new DbMigration(2, "bad", c => {
          using (SqliteCommand cmd = c.CreateCommand()) {
            cmd.CommandText = "DELETE FROM Shortcuts;";
            cmd.ExecuteNonQuery();
          }
          throw new InvalidOperationException("boom");
        })
      };
      Assert.ThrowsAny<Exception>(() => SchemaManager.ApplyMigrations(list));
      Assert.Equal(1, (int)Version());
      Assert.Equal(before, Rows());
    }

    [Fact]
    public void Failed_creation_leaves_no_database_file() {
      db = new TempDb(false);
      SchemaManager.CreateDatabaseTestHook = () => throw new InvalidOperationException("boom");
      Assert.ThrowsAny<Exception>(() => DataStore.Windows.LoadAll());
      SqliteConnection.ClearAllPools();
      Assert.False(File.Exists(db.DbPath));
      // After the failure a normal creation still works.
      SchemaManager.CreateDatabaseTestHook = null;
      Assert.Single(DataStore.Windows.LoadAll());
      Assert.Equal(AppConstants.DatabaseVersion, (int)Version());
    }

    [Fact]
    public void Empty_leftover_file_is_treated_as_missing_database() {
      db = new TempDb(false);
      File.WriteAllBytes(db.DbPath, new byte[0]);
      Assert.Single(DataStore.Windows.LoadAll());
      Assert.Equal(3, Indexes());
    }

    [Fact]
    public void Transaction_scope_rolls_back_all_writes_on_exception() {
      db = new TempDb();
      long win = DataStore.Windows.LoadAll()[0].ID;
      Assert.Throws<InvalidOperationException>((Action)(() => {
        using (DataStore.BeginTransaction()) {
          DataStore.Shortcuts.CreateGroup(null, win);
          DataStore.Shortcuts.CreateGroup(null, win);
          Assert.Equal(2, DataStore.Shortcuts.CreateGroup(null, win).Position); // sees its own uncommitted rows
          throw new InvalidOperationException("boom");
        }
      }));
      Assert.Equal(0, db.Scalar("SELECT count(*) FROM Shortcuts"));
    }

    [Fact]
    public void Transaction_scope_without_commit_rolls_back_and_with_commit_persists() {
      db = new TempDb();
      long win = DataStore.Windows.LoadAll()[0].ID;
      using (DataStore.BeginTransaction()) {
        DataStore.Shortcuts.CreateGroup(null, win);
      }
      Assert.Equal(0, db.Scalar("SELECT count(*) FROM Shortcuts"));
      using (ITransactionScope tx = DataStore.BeginTransaction()) {
        DataStore.Shortcuts.CreateGroup(null, win);
        DataStore.Shortcuts.CreateGroup(null, win);
        tx.Commit();
      }
      Assert.Equal(2, db.Scalar("SELECT count(*) FROM Shortcuts"));
    }

    [Fact]
    public void UpdateShortcutPositions_is_atomic() {
      db = new TempDb();
      long win = DataStore.Windows.LoadAll()[0].ID;
      long a = DataStore.Shortcuts.CreateGroup(null, win).ID;
      long b = DataStore.Shortcuts.CreateGroup(null, win).ID;
      DataStore.Shortcuts.UpdatePositions(new[] { b, a });
      Assert.Equal(0, db.Scalar("SELECT Position FROM Shortcuts WHERE ID=" + b));
      Assert.Equal(1, db.Scalar("SELECT Position FROM Shortcuts WHERE ID=" + a));
      // Make the second update fail via a trigger: nothing may change.
      db.Exec("CREATE TRIGGER t BEFORE UPDATE OF Position ON Shortcuts WHEN NEW.Position = 1 AND OLD.ID = " + b + " BEGIN SELECT RAISE(ABORT,'no'); END;");
      Assert.ThrowsAny<Exception>(() => DataStore.Shortcuts.UpdatePositions(new[] { a, b }));
      Assert.Equal(0, db.Scalar("SELECT Position FROM Shortcuts WHERE ID=" + b));
      Assert.Equal(1, db.Scalar("SELECT Position FROM Shortcuts WHERE ID=" + a));
    }

    [Fact]
    public void New_window_and_clone_in_one_transaction_roll_back_together() {
      db = new TempDb();
      long windows = db.Scalar("SELECT count(*) FROM MDSHOWindows");
      Assert.ThrowsAny<Exception>(() => {
        using (DataStore.BeginTransaction()) {
          DataStore.Windows.Create("D", 0, 0);
          DataStore.Clones.Create(99999, "D", 0, 0); // FK violation -> exception
        }
      });
      Assert.Equal(windows, db.Scalar("SELECT count(*) FROM MDSHOWindows"));
    }

    /// <summary>
    /// One-off check against copies of the user's real databases from a backup zip. Does nothing unless
    /// the MDSHO_BACKUP_ZIP environment variable points at the zip. Never touches the live files.
    /// </summary>
    [Theory]
    [InlineData("MDSHO.sqlite3")]
    [InlineData("MDSHODebug.sqlite3")]
    public void Real_database_copy_upgrades_without_data_loss(string name) {
      string zip = Environment.GetEnvironmentVariable("MDSHO_BACKUP_ZIP");
      if (string.IsNullOrEmpty(zip) || !File.Exists(zip)) { return; }
      db = new TempDb(false);
      using (var za = System.IO.Compression.ZipFile.OpenRead(zip)) {
        var entry = za.GetEntry("appdata-LocalAppData-MDSHO/" + name);
        Assert.NotNull(entry);
        entry.ExtractToFile(db.DbPath, true);
      }
      string counts = "SELECT (SELECT count(*) FROM MDSHOWindows)||'/'||(SELECT count(*) FROM MDSHOWindowClones)||'/'||(SELECT count(*) FROM Shortcuts)";
      string before = Describe(counts);
      long vBefore = Version();
      DataStore.Upgrade();
      string after = Describe(counts);
      string line = $"{name}: user_version {vBefore} -> {Version()}; windows/clones/shortcuts {before} -> {after}; indexes {Indexes()}; backups {Baks().Length}";
      output.WriteLine(line);
      File.AppendAllText(Path.Combine(Path.GetTempPath(), "MDSHO.Tests", "real-db-upgrade-report.txt"), line + Environment.NewLine);
      Assert.Equal(before, after);
      Assert.Equal(AppConstants.DatabaseVersion, (int)Version());
      Assert.Equal(3, Indexes());
      Assert.Equal(vBefore < AppConstants.DatabaseVersion ? 1 : 0, Baks().Length); // a database that is already at the latest version is not backed up
      Assert.Equal(0, db.Scalar("SELECT count(*) FROM pragma_foreign_key_check"));
      Assert.NotEmpty(DataStore.Windows.LoadAll());
    }
    private string Describe(string sql) {
      using (SqliteConnection c = db.Open()) {
        SqliteCommand cmd = c.CreateCommand();
        cmd.CommandText = sql;
        return (string)cmd.ExecuteScalar();
      }
    }
  }
}
