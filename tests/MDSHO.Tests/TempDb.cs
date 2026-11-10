using System;
using System.IO;
using Microsoft.Data.Sqlite;
using MDSHO.Common;
using MDSHO.Core;
using MDSHO.Data;

namespace MDSHO.Tests {

  /// <summary>
  /// Points MDSHO.Common.Paths at a fresh temp database (and shortcuts folder) for one test and deletes it afterwards.
  /// Paths is static, so tests must not run in parallel (see AssemblyInfo.cs).
  /// </summary>
  public sealed class TempDb : IDisposable {
    public string Folder { get; }
    public string DbPath { get; }
    private readonly Func<string, string, bool> originalConfirm = MDSHO.ViewModels.Dialogs.Confirm;
    private readonly Action<string, string> originalShowMessage = MDSHO.ViewModels.Dialogs.ShowMessage;

    public TempDb() : this(true) { }

    /// <param name="create">false = only set up the temp paths; the database file is not created.</param>
    public TempDb(bool create) {
      MDSHO.ViewModels.Dialogs.Confirm = (message, caption) => true; // never show a real MessageBox in tests
      // A message box would block the test run forever: fail instead.
      MDSHO.ViewModels.Dialogs.ShowMessage = (message, caption) => throw new InvalidOperationException("A message box was shown in a test: " + caption + ": " + message);
      Folder = Path.Combine(Path.GetTempPath(), "MDSHO.Tests", Guid.NewGuid().ToString("N"));
      Directory.CreateDirectory(Folder);
      DbPath = Path.Combine(Folder, "test.sqlite3");
      Paths.DatabaseFilePathOverride = DbPath;
      Paths.ShortcutsPathOverride = Path.Combine(Folder, "Shortcuts");
      Paths.ImportBackupsPathOverride = Path.Combine(Folder, "ImportBackups");
      AssertNotRealPath();
      if (!create) {
        return;
      }
      // Creates the database through the normal create-database code path.
      DataStore.Windows.LoadAll();
      if (!File.Exists(DbPath)) {
        throw new InvalidOperationException("Temp database was not created at " + DbPath);
      }
    }

    /// <summary>Fails loudly if the data paths could ever resolve to the real %LOCALAPPDATA%\MDSHO folder.</summary>
    public static void AssertNotRealPath() {
      string real = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppConstants.ApplicationFolderName);
      string tempRoot = Path.Combine(Path.GetTempPath(), "MDSHO.Tests");
      string db = Path.GetFullPath(Paths.GetAppDataLocalDatabaseFilePath());
      string sc = Path.GetFullPath(Paths.GetShortcutsPath());
      string bk = Path.GetFullPath(Paths.GetImportBackupsPath());
      foreach (string p in new[] { db, sc, bk }) {
        if (p.StartsWith(real, StringComparison.OrdinalIgnoreCase) || !p.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase)) {
          throw new InvalidOperationException("Test data path is not an isolated temp path (refusing to touch real data): " + p);
        }
      }
    }

    public SqliteConnection Open() {
      AssertNotRealPath();
      SqliteConnection c = new SqliteConnection("Data Source=" + DbPath + ";Mode=ReadWriteCreate;Pooling=False");
      c.Open();
      return c;
    }

    public long Scalar(string sql) {
      using (SqliteConnection c = Open())
      using (SqliteCommand cmd = c.CreateCommand()) {
        cmd.CommandText = sql;
        object o = cmd.ExecuteScalar();
        return (o == null || o is DBNull) ? -1 : Convert.ToInt64(o);
      }
    }

    public string ScalarText(string sql) {
      using (SqliteConnection c = Open())
      using (SqliteCommand cmd = c.CreateCommand()) {
        cmd.CommandText = sql;
        object o = cmd.ExecuteScalar();
        return (o == null || o is DBNull) ? null : Convert.ToString(o);
      }
    }

    public void Exec(string sql) {
      using (SqliteConnection c = Open())
      using (SqliteCommand cmd = c.CreateCommand()) {
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
      }
    }

    public void Dispose() {
      MDSHO.ViewModels.Dialogs.Confirm = originalConfirm;
      MDSHO.ViewModels.Dialogs.ShowMessage = originalShowMessage;
      Paths.DatabaseFilePathOverride = null;
      Paths.ShortcutsPathOverride = null;
      Paths.ImportBackupsPathOverride = null;
      SqliteConnection.ClearAllPools();
      try { Directory.Delete(Folder, true); } catch (IOException) { }
    }
  }
}
