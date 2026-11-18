using System;
using System.Globalization;
using System.IO;
using Microsoft.Data.Sqlite;

namespace MDSHO.Data {

  /// <summary>The database file itself, for when it can't be opened.</summary>
  public static class DatabaseFile {
    /// <summary>
    /// Renames the database file (and its -wal, -shm and -journal files) to <c>name.unreadable-yyyyMMdd-HHmmss</c> next to
    /// it, so a new database can be made and the old file is kept. Returns the new name of the database file. Throws when
    /// the file can't be renamed (another program has it open, say).
    /// </summary>
    public static string SetAside(DateTime now) {
      // Pooled connections keep the file open.
      SqliteConnection.ClearAllPools();
      string path = MDSHO.Common.Paths.GetAppDataLocalDatabaseFilePath();
      string stamp = now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
      string kept = path + ".unreadable-" + stamp;
      for (int i = 2; File.Exists(kept); i++) {
        kept = path + ".unreadable-" + stamp + "-" + i;
      }
      if (File.Exists(path)) {
        File.Move(path, kept);
      }
      foreach (string suffix in new[] { "-wal", "-shm", "-journal" }) {
        if (File.Exists(path + suffix)) {
          File.Move(path + suffix, kept + suffix);
        }
      }
      MDSHO.Common.AppLog.Information("The database could not be opened and was kept as {KeptPath}", kept);
      return kept;
    }
  }

}
