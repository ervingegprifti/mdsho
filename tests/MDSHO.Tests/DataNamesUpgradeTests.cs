using System;
using System.IO;
using System.Linq;
using MDSHO.Common;
using MDSHO.Data;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// Lowercase data names (1.1.2): the database becomes mdsho.sqlite3 and the shortcuts folder "shortcuts". Existing data
  /// under the old names (MDSHO.sqlite3, MDSHOShortcuts) is renamed at startup, before the database is opened; nothing
  /// is ever overwritten or deleted, and the stored paths of dropped .lnk/.url files move to the new folder.
  /// </summary>
  public class DataNamesUpgradeTests : IDisposable {
    private readonly string folder = Path.Combine(Path.GetTempPath(), "MDSHO.Tests", "names-" + Guid.NewGuid().ToString("N"));
    private readonly LogCapture log = new LogCapture();

    public DataNamesUpgradeTests() {
      Directory.CreateDirectory(folder);
    }

    public void Dispose() {
      log.Dispose();
      try { Directory.Delete(folder, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string P(string name) { return Path.Combine(folder, name); }
    // Names exactly as stored on disk (with their real case).
    private string[] FileNames() { return Directory.GetFiles(folder).Select(Path.GetFileName).OrderBy(n => n, StringComparer.Ordinal).ToArray(); }
    private string[] FolderNames() { return Directory.GetDirectories(folder).Select(Path.GetFileName).OrderBy(n => n, StringComparer.Ordinal).ToArray(); }

    [Fact]
    public void New_names_are_lowercase_and_debug_names_stay_apart() {
      Assert.Equal("mdsho.sqlite3", AppConstants.DatabaseFileName);
      Assert.Equal("mdsho-debug.sqlite3", AppConstants.DatabaseDebugFileName);
      Assert.Equal("shortcuts", AppConstants.ShortcutsFolderName);
      Assert.Equal("shortcuts-debug", AppConstants.ShortcutsDebugFolderName);
      Assert.Equal(new DataNames("MDSHO.sqlite3", "mdsho.sqlite3", "MDSHOShortcuts", "shortcuts"), DataNames.Release);
      Assert.Equal(new DataNames("MDSHODebug.sqlite3", "mdsho-debug.sqlite3", "MDSHOShortcutsDebug", "shortcuts-debug"), DataNames.Debug);
    }

    [Fact]
    public void Old_database_is_renamed_to_lowercase_on_disk_with_its_content() {
      File.WriteAllText(P("MDSHO.sqlite3"), "db");

      DataNamesReport report = DataNamesUpgrade.RenameOldNames(folder, DataNames.Release);

      Assert.Equal(new[] { "mdsho.sqlite3" }, FileNames());
      Assert.Equal("db", File.ReadAllText(P("mdsho.sqlite3")));
      Assert.Single(report.Renamed);
      Assert.Empty(report.Warnings);
    }

    [Fact]
    public void Wal_shm_and_journal_side_files_move_with_the_database() {
      File.WriteAllText(P("MDSHO.sqlite3"), "db");
      File.WriteAllText(P("MDSHO.sqlite3-wal"), "wal");
      File.WriteAllText(P("MDSHO.sqlite3-shm"), "shm");
      File.WriteAllText(P("MDSHO.sqlite3-journal"), "journal");

      DataNamesUpgrade.RenameOldNames(folder, DataNames.Release);

      Assert.Equal(new[] { "mdsho.sqlite3", "mdsho.sqlite3-journal", "mdsho.sqlite3-shm", "mdsho.sqlite3-wal" }, FileNames());
      Assert.Equal("wal", File.ReadAllText(P("mdsho.sqlite3-wal")));
      Assert.Equal("shm", File.ReadAllText(P("mdsho.sqlite3-shm")));
    }

    [Fact]
    public void Old_shortcuts_folder_is_renamed_with_its_files() {
      Directory.CreateDirectory(P("MDSHOShortcuts"));
      File.WriteAllText(P(@"MDSHOShortcuts\a.lnk"), "lnk");
      File.WriteAllText(P(@"MDSHOShortcuts\b.url"), "url");

      DataNamesReport report = DataNamesUpgrade.RenameOldNames(folder, DataNames.Release);

      Assert.Equal(new[] { "shortcuts" }, FolderNames());
      Assert.Equal("lnk", File.ReadAllText(P(@"shortcuts\a.lnk")));
      Assert.Equal("url", File.ReadAllText(P(@"shortcuts\b.url")));
      Assert.Single(report.Renamed);
    }

    [Fact]
    public void Debug_names_are_renamed_and_release_data_is_left_alone() {
      File.WriteAllText(P("MDSHODebug.sqlite3"), "debug");
      Directory.CreateDirectory(P("MDSHOShortcutsDebug"));
      File.WriteAllText(P("MDSHO.sqlite3"), "release");
      Directory.CreateDirectory(P("MDSHOShortcuts"));

      DataNamesUpgrade.RenameOldNames(folder, DataNames.Debug);

      Assert.Equal(new[] { "MDSHO.sqlite3", "mdsho-debug.sqlite3" }, FileNames());
      Assert.Equal(new[] { "MDSHOShortcuts", "shortcuts-debug" }, FolderNames());
      Assert.Equal("debug", File.ReadAllText(P("mdsho-debug.sqlite3")));
    }

    [Fact]
    public void When_old_and_new_both_exist_neither_is_touched_and_a_warning_is_logged() {
      File.WriteAllText(P("MDSHODebug.sqlite3"), "old");
      File.WriteAllText(P("MDSHODebug.sqlite3-wal"), "old-wal");
      File.WriteAllText(P("mdsho-debug.sqlite3"), "new");
      Directory.CreateDirectory(P("MDSHOShortcutsDebug"));
      File.WriteAllText(P(@"MDSHOShortcutsDebug\old.lnk"), "old");
      Directory.CreateDirectory(P("shortcuts-debug"));
      File.WriteAllText(P(@"shortcuts-debug\new.lnk"), "new");

      DataNamesReport report = DataNamesUpgrade.RenameOldNames(folder, DataNames.Debug);
      report.WriteToLog();

      Assert.Equal(new[] { "MDSHODebug.sqlite3", "MDSHODebug.sqlite3-wal", "mdsho-debug.sqlite3" }, FileNames());
      Assert.Equal("old", File.ReadAllText(P("MDSHODebug.sqlite3")));
      Assert.Equal("new", File.ReadAllText(P("mdsho-debug.sqlite3")));
      Assert.Equal(new[] { "old.lnk" }, Directory.GetFiles(P("MDSHOShortcutsDebug")).Select(Path.GetFileName).ToArray());
      Assert.Equal(new[] { "new.lnk" }, Directory.GetFiles(P("shortcuts-debug")).Select(Path.GetFileName).ToArray());
      Assert.Empty(report.Renamed);
      Assert.Equal(2, report.Warnings.Count);
      Assert.Equal(2, log.Events.Count(e => e.Level == Serilog.Events.LogEventLevel.Warning));
    }

    [Fact]
    public void A_side_file_already_at_the_new_name_stops_the_database_rename() {
      File.WriteAllText(P("MDSHODebug.sqlite3"), "old");
      File.WriteAllText(P("MDSHODebug.sqlite3-wal"), "old-wal");
      File.WriteAllText(P("mdsho-debug.sqlite3-wal"), "stray");

      DataNamesReport report = DataNamesUpgrade.RenameOldNames(folder, DataNames.Debug);

      Assert.Equal(new[] { "MDSHODebug.sqlite3", "MDSHODebug.sqlite3-wal", "mdsho-debug.sqlite3-wal" }, FileNames());
      Assert.Single(report.Warnings);
    }

    [Fact]
    public void A_failed_side_file_move_puts_the_database_back_under_its_old_name() {
      File.WriteAllText(P("MDSHO.sqlite3"), "db");
      File.WriteAllText(P("MDSHO.sqlite3-wal"), "wal");

      DataNamesReport report;
      using (new FileStream(P("MDSHO.sqlite3-wal"), FileMode.Open, FileAccess.Read, FileShare.None)) {
        report = DataNamesUpgrade.RenameOldNames(folder, DataNames.Release);
      }

      Assert.Equal(new[] { "MDSHO.sqlite3", "MDSHO.sqlite3-wal" }, FileNames());
      Assert.Equal("db", File.ReadAllText(P("MDSHO.sqlite3")));
      Assert.Empty(report.Renamed);
      Assert.Single(report.Warnings);
    }

    [Fact]
    public void Upgrade_backups_are_never_renamed_or_deleted() {
      File.WriteAllText(P("MDSHO.sqlite3"), "db");
      File.WriteAllText(P("MDSHO.sqlite3.bak-v3-20261109143000"), "bak");

      DataNamesUpgrade.RenameOldNames(folder, DataNames.Release);

      Assert.Equal(new[] { "MDSHO.sqlite3.bak-v3-20261109143000", "mdsho.sqlite3" }, FileNames());
      Assert.Equal("bak", File.ReadAllText(P("MDSHO.sqlite3.bak-v3-20261109143000")));
    }

    [Fact]
    public void Nothing_happens_when_only_the_new_names_exist() {
      File.WriteAllText(P("mdsho.sqlite3"), "db");
      Directory.CreateDirectory(P("shortcuts"));
      Directory.CreateDirectory(P("logs"));

      DataNamesReport report = DataNamesUpgrade.RenameOldNames(folder, DataNames.Release);
      report.WriteToLog();

      Assert.Equal(new[] { "mdsho.sqlite3" }, FileNames());
      Assert.Equal(new[] { "logs", "shortcuts" }, FolderNames());
      Assert.Empty(report.Renamed);
      Assert.Empty(report.Warnings);
      Assert.Empty(log.Events);
    }

    [Fact]
    public void Renames_are_logged_with_old_and_new_paths() {
      File.WriteAllText(P("MDSHO.sqlite3"), "db");
      Directory.CreateDirectory(P("MDSHOShortcuts"));

      DataNamesUpgrade.RenameOldNames(folder, DataNames.Release).WriteToLog();

      Assert.Contains(log.Messages, m => m.Contains(P("MDSHO.sqlite3")) && m.Contains(P("mdsho.sqlite3")));
      Assert.Contains(log.Messages, m => m.Contains(P("MDSHOShortcuts")) && m.Contains(P("shortcuts")));
    }

    [Fact]
    public void Stored_paths_of_files_in_the_old_shortcuts_folder_move_to_the_new_folder() {
      using (TempDb db = new TempDb()) {
        string oldFolder = Path.Combine(db.Folder, "MDSHOShortcuts");
        string newFolder = Path.Combine(db.Folder, "shortcuts");
        long window = DataStore.Windows.Create("W", 0, 0).ID;
        long dropped = DataStore.Shortcuts.Create(null, window, "lnk", oldFolder + @"\g1.lnk", ".lnk", "g1").ID;
        long otherCase = DataStore.Shortcuts.Create(null, window, "url", oldFolder.ToUpperInvariant() + @"\g2.url", ".url", "g2").ID;
        long file = DataStore.Shortcuts.Create(null, window, "file", @"C:\Tools\MDSHOShortcuts\x.exe", ".lnk", "g3").ID;
        long similar = DataStore.Shortcuts.Create(null, window, "similar", oldFolder + @"Debug\g4.lnk", ".lnk", "g4").ID;

        int changed = DataNamesUpgrade.FixShortcutTargets(oldFolder, newFolder);

        Assert.Equal(2, changed);
        Assert.Equal(newFolder + @"\g1.lnk", db.ScalarText("SELECT Target FROM Shortcuts WHERE ID = " + dropped));
        Assert.Equal(newFolder + @"\g2.url", db.ScalarText("SELECT Target FROM Shortcuts WHERE ID = " + otherCase));
        Assert.Equal(@"C:\Tools\MDSHOShortcuts\x.exe", db.ScalarText("SELECT Target FROM Shortcuts WHERE ID = " + file));
        Assert.Equal(oldFolder + @"Debug\g4.lnk", db.ScalarText("SELECT Target FROM Shortcuts WHERE ID = " + similar));
        Assert.Equal(0, DataNamesUpgrade.FixShortcutTargets(oldFolder, newFolder)); // running again changes nothing
        Assert.Contains(log.Messages, m => m.Contains("2") && m.Contains(newFolder));
      }
    }

    [Fact]
    public void App_renames_old_names_before_anything_opens_the_app_data_folder() {
      DirectoryInfo dir = new DirectoryInfo(AppContext.BaseDirectory);
      while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Directory.Packages.props"))) {
        dir = dir.Parent;
      }
      Assert.NotNull(dir);
      string app = File.ReadAllText(Path.Combine(dir.FullName, "src", "MDSHO.App", "App.xaml.cs"));
      int startup = app.IndexOf("protected override void OnStartup", StringComparison.Ordinal);
      int mutex = app.IndexOf("AcquireSingleInstanceMutex()", startup, StringComparison.Ordinal);
      int rename = app.IndexOf("DataNamesUpgrade.RenameOldNamesInAppDataFolder()", startup, StringComparison.Ordinal);
      // The logger finds the app data folder through Paths.GetShortcutsPath(), which creates the new shortcuts folder.
      int logger = app.IndexOf("CreateFileLogger()", startup, StringComparison.Ordinal);
      int upgrade = app.IndexOf("DatabaseUpdate()", startup, StringComparison.Ordinal);
      Assert.True(mutex > 0 && rename > mutex && logger > rename && upgrade > logger, "Order must be: single instance check, rename, logger, database upgrade.");
      Assert.Contains("DataNamesUpgrade.FixShortcutTargetsInAppDataFolder()", app);
    }
  }
}
