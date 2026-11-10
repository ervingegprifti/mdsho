using System.Threading;
using Microsoft.Data.Sqlite;

namespace MDSHO.Data {

  /// <summary>
  /// Creates the connections to the SQLite database. The path is resolved on every call (so the
  /// <c>Paths.DatabaseFilePathOverride</c> test hook is respected) and every connection enforces foreign keys.<br /><br />
  /// References:<br />
  /// <see href="https://docs.microsoft.com/en-us/dotnet/standard/data/sqlite/connection-strings">Connection strings</see><br />
  /// </summary>
  internal static class ConnectionFactory {
    private static long openCount;

    /// <summary>How many connections <see cref="Open"/> has handed out (diagnostics and tests).</summary>
    public static long OpenCount { get { return Interlocked.Read(ref openCount); } }

    /// <summary>The connection string of the current database path (read/write/create, foreign keys on).</summary>
    public static string GetConnectionString() {
      SqliteConnectionStringBuilder connectionStringBuilder = new SqliteConnectionStringBuilder() {
        DataSource = MDSHO.Common.Paths.GetAppDataLocalDatabaseFilePath(),
        Mode = SqliteOpenMode.ReadWriteCreate,
        ForeignKeys = true
      };
      return connectionStringBuilder.ToString();
    }

    /// <summary>Opens a connection to the database, creating the database first if it does not exist.</summary>
    public static SqliteConnection Open() {
      SchemaManager.CreateDatabase();
      SqliteConnection conn = new SqliteConnection(GetConnectionString());
      try {
        conn.Open();
      } catch {
        conn.Dispose();
        throw;
      }
      Interlocked.Increment(ref openCount);
      return conn;
    }
  }

}
