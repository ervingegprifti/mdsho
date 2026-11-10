using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;

namespace MDSHO.Data {

  /// <summary>
  /// Creates the database schema and upgrades it with versioned migrations (<c>PRAGMA user_version</c>).
  /// </summary>
  internal static class SchemaManager {
    private static readonly object createLock = new object();

    /// <summary>
    /// Test hook. Called inside the create-database transaction, just before it commits. Null (default) = normal behaviour.
    /// </summary>
    internal static Action CreateDatabaseTestHook = null;

    /// <summary>
    /// The sql that creates the latest database schema (tables and indexes).
    /// </summary>
    private const string CreateDatabaseSql = @"
        CREATE TABLE MDSHOWindows (
          ID INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT UNIQUE,
          Title TEXT NOT NULL,
          ScreenDeviceName TEXT NOT NULL,
          Left INTEGER NOT NULL,
          Top INTEGER NOT NULL,
          Width INTEGER NOT NULL,
          Height INTEGER NOT NULL,
          BorderColor TEXT NOT NULL,
          BackgroundColor TEXT NOT NULL,
          TextColor TEXT NOT NULL,
          TitleBackgroundColor TEXT NOT NULL,
          TitleTextColor TEXT NOT NULL,
          Notes TEXT NOT NULL,
          SolidIcons INTEGER NOT NULL,
          SingleClickToOpen INTEGER NOT NULL);

        CREATE TABLE MDSHOWindowClones (
          ID INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT UNIQUE,
          ParentMDSHOWindowID INTEGER NOT NULL,
          ScreenDeviceName TEXT NOT NULL,
          Left INTEGER NOT NULL,
          Top INTEGER NOT NULL,
          Width INTEGER NOT NULL,
          Height INTEGER NOT NULL,
          FOREIGN KEY(ParentMDSHOWindowID) REFERENCES MDSHOWindows(ID) ON DELETE CASCADE);

        CREATE TABLE Shortcuts (
          ID INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT UNIQUE,
          ParentShortcutID INTEGER NULL,
          ParentMDSHOWindowID INTEGER NULL,
          Name TEXT NOT NULL,
          Target TEXT NOT NULL,
          Ext TEXT NOT NULL,
          Guid TEXT NOT NULL,
          IsGroup INTEGER NOT NULL,
          IsGroupExpanded INTEGER NOT NULL,
          Notes TEXT NOT NULL,
          IsMarked INTEGER NOT NULL,
          MarkColor TEXT NOT NULL,
          Position INTEGER NOT NULL,
          IsProcessCommand INTEGER NOT NULL,
          ProcessFileName TEXT NOT NULL,
          ProcessArguments TEXT NOT NULL,
          ProcessRunAsAdmin INTEGER NOT NULL DEFAULT 0,
          ProcessWorkingFolder TEXT NOT NULL DEFAULT '',
          FOREIGN KEY(ParentShortcutID) REFERENCES Shortcuts(ID) ON DELETE CASCADE,
          FOREIGN KEY(ParentMDSHOWindowID) REFERENCES MDSHOWindows(ID) ON DELETE CASCADE);

        CREATE INDEX IF NOT EXISTS IX_Shortcuts_ParentShortcutID ON Shortcuts(ParentShortcutID);
        CREATE INDEX IF NOT EXISTS IX_Shortcuts_ParentMDSHOWindowID ON Shortcuts(ParentMDSHOWindowID);
        CREATE INDEX IF NOT EXISTS IX_MDSHOWindowClones_ParentMDSHOWindowID ON MDSHOWindowClones(ParentMDSHOWindowID);
      ";

    /// <summary>
    /// Create the database with tables, relations and indexes at the latest version.<br />
    /// Everything runs in one transaction. If anything fails nothing is left behind: the half-created file is deleted.
    /// A missing or empty (0 bytes) database file counts as "no database".
    /// </summary>
    internal static void CreateDatabase() {
      string databaseFilePath = MDSHO.Common.Paths.GetAppDataLocalDatabaseFilePath();
      if (File.Exists(databaseFilePath) && new FileInfo(databaseFilePath).Length > 0) {
        // The database is already created. Stop here.
        return;
      }
      lock (createLock) {
        if (File.Exists(databaseFilePath) && new FileInfo(databaseFilePath).Length > 0) {
          return;
        }
        try {
          using (SqliteConnection conn = new SqliteConnection(ConnectionFactory.GetConnectionString())) {
            conn.Open();
            using (SqliteTransaction tx = conn.BeginTransaction()) {
              using (SqliteCommand cmd = new SqliteCommand(CreateDatabaseSql, conn, tx)) {
                cmd.ExecuteNonQuery();
              }
              CreateDatabaseTestHook?.Invoke();
              using (SqliteCommand cmd = new SqliteCommand($"PRAGMA user_version = {MDSHO.Common.AppConstants.DatabaseVersion};", conn, tx)) {
                cmd.ExecuteNonQuery();
              }
              tx.Commit();
            }
          }
        } catch {
          SqliteConnection.ClearAllPools();
          try { File.Delete(databaseFilePath); } catch (IOException) { } catch (UnauthorizedAccessException) { }
          throw;
        }
      }
    }

    
    private static void Execute(SqliteConnection conn, string sql) {
      using (SqliteCommand cmd = conn.CreateCommand()) {
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
      }
    }

    /// <summary>
    /// Ordered list of database migrations. The database <c>user_version</c> is the version of the last applied step.<br />
    /// Version 1 is the original schema (before versioned migrations existed). Databases created by older app versions
    /// have user_version 0 or 1 and may still lack the clones RoundedCorners column, so step 2 adds it when missing
    /// (step 5 later drops it from both tables again).<br />
    /// <strong>Never edit or reorder an existing step, only append new ones</strong> (and raise AppConstants.DatabaseVersion
    /// and CreateDatabaseSql to match).
    /// </summary>
    internal static readonly IReadOnlyList<DbMigration> Migrations = new List<DbMigration> {
      new DbMigration(2, "MDSHOWindowClones.RoundedCorners (added in app 1.0.25.0)", conn => {
        bool exists = false;
        using (SqliteCommand cmd = conn.CreateCommand()) {
          cmd.CommandText = "SELECT count(*) FROM pragma_table_info('MDSHOWindowClones') WHERE name = 'RoundedCorners' COLLATE NOCASE";
          exists = Convert.ToInt64(cmd.ExecuteScalar()) > 0;
        }
        if (!exists) {
          Execute(conn, "ALTER TABLE MDSHOWindowClones ADD COLUMN RoundedCorners INTEGER DEFAULT 1 NOT NULL;");
        }
      }),
      new DbMigration(3, "Indexes on the foreign key columns", conn => {
        Execute(conn, "CREATE INDEX IF NOT EXISTS IX_Shortcuts_ParentShortcutID ON Shortcuts(ParentShortcutID);");
        Execute(conn, "CREATE INDEX IF NOT EXISTS IX_Shortcuts_ParentMDSHOWindowID ON Shortcuts(ParentMDSHOWindowID);");
        Execute(conn, "CREATE INDEX IF NOT EXISTS IX_MDSHOWindowClones_ParentMDSHOWindowID ON MDSHOWindowClones(ParentMDSHOWindowID);");
      }),
      new DbMigration(4, "Run commands: Shortcuts.ProcessRunAsAdmin and Shortcuts.ProcessWorkingFolder (Run Command window)", conn => {
        if (!ColumnExists(conn, "Shortcuts", "ProcessRunAsAdmin")) {
          Execute(conn, "ALTER TABLE Shortcuts ADD COLUMN ProcessRunAsAdmin INTEGER NOT NULL DEFAULT 0;");
        }
        if (!ColumnExists(conn, "Shortcuts", "ProcessWorkingFolder")) {
          Execute(conn, "ALTER TABLE Shortcuts ADD COLUMN ProcessWorkingFolder TEXT NOT NULL DEFAULT '';");
        }
      }),
      new DbMigration(5, "Drop MDSHOWindows.RoundedCorners and MDSHOWindowClones.RoundedCorners (corners now follow the docked state)", conn => {
        // DROP COLUMN needs SQLite 3.35+, which the app bundles (pinned by a test). No index, trigger or view uses the
        // column, and DROP COLUMN keeps the rows, other columns, indexes, foreign keys and AUTOINCREMENT counters.
        // Unlike a table rebuild, it never drops MDSHOWindows, so ON DELETE CASCADE cannot remove clones or shortcuts.
        if (ColumnExists(conn, "MDSHOWindows", "RoundedCorners")) {
          Execute(conn, "ALTER TABLE MDSHOWindows DROP COLUMN RoundedCorners;");
        }
        if (ColumnExists(conn, "MDSHOWindowClones", "RoundedCorners")) {
          Execute(conn, "ALTER TABLE MDSHOWindowClones DROP COLUMN RoundedCorners;");
        }
      })
    };

    private static bool ColumnExists(SqliteConnection conn, string table, string column) {
      using (SqliteCommand cmd = conn.CreateCommand()) {
        cmd.CommandText = "SELECT count(*) FROM pragma_table_info(@table) WHERE name = @column COLLATE NOCASE";
        cmd.Parameters.AddWithValue("@table", table);
        cmd.Parameters.AddWithValue("@column", column);
        return Convert.ToInt64(cmd.ExecuteScalar()) > 0;
      }
    }

    /// <summary>
    /// Copy the database next to itself as <c>name.bak-v{oldVersion}-{yyyyMMddHHmmss}</c> using the SQLite online backup.
    /// </summary>
    private static void BackupDatabase(int oldVersion) {
      string databaseFilePath = MDSHO.Common.Paths.GetAppDataLocalDatabaseFilePath();
      string backupPath = databaseFilePath + ".bak-v" + oldVersion + "-" + DateTime.Now.ToString("yyyyMMddHHmmss");
      using (SqliteConnection source = new SqliteConnection(ConnectionFactory.GetConnectionString()))
      using (SqliteConnection destination = new SqliteConnection("Data Source=" + backupPath + ";Pooling=False")) {
        source.Open();
        destination.Open();
        source.BackupDatabase(destination);
      }
      MDSHO.Common.AppLog.Information("Database backed up to {BackupPath} before upgrading from version {OldVersion}", backupPath, oldVersion);
    }

    /// <summary>
    /// Read the database <c>user_version</c> over a read-only, unpooled connection (reading never changes the file).
    /// </summary>
    private static int ReadUserVersion() {
      SqliteConnectionStringBuilder builder = new SqliteConnectionStringBuilder(ConnectionFactory.GetConnectionString()) {
        Mode = SqliteOpenMode.ReadOnly,
        Pooling = false
      };
      using (SqliteConnection conn = new SqliteConnection(builder.ToString())) {
        conn.Open();
        using (SqliteCommand cmd = new SqliteCommand("PRAGMA user_version;", conn)) {
          return Convert.ToInt32(cmd.ExecuteScalar());
        }
      }
    }

    /// <summary>
    /// Apply, in order, every migration newer than the database <c>user_version</c>. A database whose version is higher
    /// than every version this app knows throws <see cref="MDSHO.Core.DatabaseTooNewException"/> and is left untouched. The database is backed up first
    /// when there is anything to apply. Each step has its own transaction; a failing step is rolled back, the
    /// exception is rethrown and the database stays at the last successfully applied version.
    /// </summary>
    internal static void ApplyMigrations(IReadOnlyList<DbMigration> migrations) {
      CreateDatabase();
      int currentVersion = ReadUserVersion();
      // A database from a newer MDSHO: never migrate, write or back it up.
      int supportedVersion = MDSHO.Common.AppConstants.DatabaseVersion;
      foreach (DbMigration migration in migrations) {
        supportedVersion = Math.Max(supportedVersion, migration.Version);
      }
      if (currentVersion > supportedVersion) {
        throw new MDSHO.Core.DatabaseTooNewException(currentVersion, supportedVersion);
      }
      List<DbMigration> pending = new List<DbMigration>();
      foreach (DbMigration migration in migrations) {
        if (migration.Version > currentVersion) {
          pending.Add(migration);
        }
      }
      if (pending.Count == 0) {
        MDSHO.Common.AppLog.Information("Database version {DatabaseVersion}", currentVersion);
        return;
      }
      pending.Sort((a, b) => a.Version.CompareTo(b.Version));
      BackupDatabase(currentVersion);
      foreach (DbMigration migration in pending) {
        using (SqliteConnection conn = new SqliteConnection(ConnectionFactory.GetConnectionString())) {
          conn.Open();
          using (SqliteTransaction tx = conn.BeginTransaction()) {
            migration.Apply(conn);
            Execute(conn, $"PRAGMA user_version = {migration.Version};");
            tx.Commit();
          }
        }
        MDSHO.Common.AppLog.Information("Database migration {Version} applied: {Name}", migration.Version, migration.Name);
      }
      MDSHO.Common.AppLog.Information("Database version {DatabaseVersion}", pending[pending.Count - 1].Version);
    }

    /// <summary>
    /// Update the database with the latest changes.
    /// </summary>
    public static void Upgrade() {
      ApplyMigrations(Migrations);
    }
    

  }

}
