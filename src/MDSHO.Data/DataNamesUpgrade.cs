using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MDSHO.Common;
using Microsoft.Data.Sqlite;

namespace MDSHO.Data {

  /// <summary>Old (before 1.1.2) and new names of the database file and the shortcuts folder of one build.</summary>
  public sealed record DataNames(string OldDatabase, string NewDatabase, string OldShortcuts, string NewShortcuts) {
    /// <summary>Release build: MDSHO.sqlite3 → mdsho.sqlite3, MDSHOShortcuts → shortcuts.</summary>
    public static readonly DataNames Release = new DataNames(AppConstants.LegacyDatabaseFileName, AppConstants.DatabaseFileName,
      AppConstants.LegacyShortcutsFolderName, AppConstants.ShortcutsFolderName);
    /// <summary>Debug build: MDSHODebug.sqlite3 → mdsho-debug.sqlite3, MDSHOShortcutsDebug → shortcuts-debug.</summary>
    public static readonly DataNames Debug = new DataNames(AppConstants.LegacyDatabaseDebugFileName, AppConstants.DatabaseDebugFileName,
      AppConstants.LegacyShortcutsDebugFolderName, AppConstants.ShortcutsDebugFolderName);

    /// <summary>The names of the running build.</summary>
    public static DataNames Current {
      get {
#if DEBUG
        return Debug;
#else
        return Release;
#endif
      }
    }
  }

  /// <summary>
  /// What <see cref="DataNamesUpgrade.RenameOldNames"/> did. The rename runs before the logger exists, so the report is
  /// written to the log afterwards with <see cref="WriteToLog"/>.
  /// </summary>
  public sealed class DataNamesReport {
    private readonly List<Action> logActions = new List<Action>();

    /// <summary>Each rename done, as "old path -> new path".</summary>
    public List<string> Renamed { get; } = new List<string>();
    /// <summary>Each problem: something that was left under its old name.</summary>
    public List<string> Warnings { get; } = new List<string>();

    internal void AddRenamed(string oldPath, string newPath) {
      Renamed.Add(oldPath + " -> " + newPath);
      logActions.Add(() => AppLog.Information("Renamed {OldPath} to {NewPath} (new data names of MDSHO 1.1.2)", oldPath, newPath));
    }

    internal void AddWarning(Exception exception, string text, string messageTemplate, params object[] args) {
      Warnings.Add(text);
      logActions.Add(() => AppLog.Warning(exception, messageTemplate, args));
    }

    /// <summary>Writes every rename (information) and problem (warning) to the log, in order.</summary>
    public void WriteToLog() {
      foreach (Action action in logActions) {
        action();
      }
    }
  }

  /// <summary>
  /// Moves data stored under the names used before 1.1.2 (MDSHO.sqlite3, MDSHOShortcuts) to the new lowercase names.
  /// Runs at startup before anything opens the database or the shortcuts folder. Never overwrites or deletes anything:
  /// when the old and the new name both exist, the new one is used and the old one is left as it is (with a warning).
  /// Upgrade backups (<c>*.bak-v*</c>) keep their names.
  /// </summary>
  public static class DataNamesUpgrade {
    // SQLite files that belong to the database and must keep its name.
    private static readonly string[] SideFileSuffixes = { "-wal", "-shm", "-journal" };

    /// <summary>
    /// Renames the old database (with its -wal, -shm and -journal files) and the old shortcuts folder in
    /// <paramref name="folder"/> to the new names. A case-only rename (MDSHO.sqlite3 → mdsho.sqlite3) really changes the
    /// name on disk. Never throws: problems are reported as warnings.
    /// </summary>
    public static DataNamesReport RenameOldNames(string folder, DataNames names) {
      DataNamesReport report = new DataNamesReport();
      try {
        if (Directory.Exists(folder)) {
          RenameDatabase(folder, names.OldDatabase, names.NewDatabase, report);
          RenameShortcutsFolder(folder, names.OldShortcuts, names.NewShortcuts, report);
        }
      } catch (Exception ex) {
        report.AddWarning(ex, ex.Message, "Could not check the old data names in {Folder}", folder);
      }
      return report;
    }

    /// <summary>
    /// <see cref="RenameOldNames"/> on the real app data folder with the names of the running build. Does nothing when
    /// the data paths are redirected (tests).
    /// </summary>
    public static DataNamesReport RenameOldNamesInAppDataFolder() {
      if (Paths.DatabaseFilePathOverride != null || Paths.ShortcutsPathOverride != null) {
        return new DataNamesReport();
      }
      try {
        return RenameOldNames(Paths.GetAppDataFolderPath(), DataNames.Current);
      } catch (Exception ex) {
        DataNamesReport report = new DataNamesReport();
        report.AddWarning(ex, ex.Message, "Could not check the old data names in the app data folder");
        return report;
      }
    }

    /// <summary>
    /// Dropped .lnk and .url files are copied into the shortcuts folder and their full path is stored as the button's
    /// target. Rewrites every stored target inside <paramref name="oldShortcutsFolder"/> (case-insensitive, whole folder
    /// name) to the same file in <paramref name="newShortcutsFolder"/>. Returns how many buttons changed.
    /// </summary>
    public static int FixShortcutTargets(string oldShortcutsFolder, string newShortcutsFolder) {
      string oldPrefix = oldShortcutsFolder.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
      string newPrefix = newShortcutsFolder.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
      int changed;
      using (SqliteConnection conn = ConnectionFactory.Open())
      using (SqliteCommand cmd = conn.CreateCommand()) {
        cmd.CommandText = @"UPDATE Shortcuts SET Target = @new || substr(Target, length(@old) + 1)
          WHERE length(Target) > length(@old) AND lower(substr(Target, 1, length(@old))) = lower(@old)";
        cmd.Parameters.AddWithValue("@old", oldPrefix);
        cmd.Parameters.AddWithValue("@new", newPrefix);
        changed = cmd.ExecuteNonQuery();
      }
      if (changed > 0) {
        AppLog.Information("Moved the stored paths of {Count} shortcut files from {OldFolder} to {NewFolder}", changed, oldShortcutsFolder, newShortcutsFolder);
      }
      return changed;
    }

    /// <summary>
    /// <see cref="FixShortcutTargets"/> for the real app data folder, after the database upgrade. Runs on every start
    /// (it is cheap and changes nothing the second time), so data imported from an older export is fixed too. Skipped
    /// while the old shortcuts folder still exists, because the stored paths still work then. Never throws.
    /// </summary>
    public static int FixShortcutTargetsInAppDataFolder() {
      if (Paths.DatabaseFilePathOverride != null || Paths.ShortcutsPathOverride != null) {
        return 0;
      }
      try {
        string oldFolder = Path.Combine(Paths.GetAppDataFolderPath(), DataNames.Current.OldShortcuts);
        if (Directory.Exists(oldFolder)) {
          return 0;
        }
        return FixShortcutTargets(oldFolder, Paths.GetShortcutsPath());
      } catch (Exception ex) {
        AppLog.Warning(ex, "Could not move the stored paths of the shortcut files to the new shortcuts folder");
        return 0;
      }
    }

    private static void RenameDatabase(string folder, string oldName, string newName, DataNamesReport report) {
      if (!ExactExists(folder, oldName)) {
        return;
      }
      List<(string From, string To)> moves = new List<(string From, string To)> { (oldName, newName) };
      foreach (string suffix in SideFileSuffixes) {
        if (ExactExists(folder, oldName + suffix)) {
          moves.Add((oldName + suffix, newName + suffix));
        }
      }
      foreach ((string from, string to) in moves) {
        if (Occupied(folder, from, to)) {
          WarnBothExist(folder, from, to, report);
          return;
        }
      }
      List<(string From, string To)> done = new List<(string From, string To)>();
      try {
        foreach ((string From, string To) move in moves) {
          Move(folder, move.From, move.To, false);
          done.Add(move);
        }
      } catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) {
        // Put back what was moved, so the database and its side files keep one name.
        done.Reverse();
        foreach ((string from, string to) in done) {
          try {
            Move(folder, to, from, false);
          } catch (Exception undo) when (undo is IOException || undo is UnauthorizedAccessException) {
            report.AddWarning(undo, undo.Message, "Could not rename {NewPath} back to {OldPath}", Path.Combine(folder, to), Path.Combine(folder, from));
          }
        }
        report.AddWarning(ex, ex.Message, "Could not rename the database {OldPath} to {NewPath}; it keeps its old name",
          Path.Combine(folder, oldName), Path.Combine(folder, newName));
        return;
      }
      foreach ((string from, string to) in done) {
        report.AddRenamed(Path.Combine(folder, from), Path.Combine(folder, to));
      }
    }

    private static void RenameShortcutsFolder(string folder, string oldName, string newName, DataNamesReport report) {
      if (!ExactExists(folder, oldName)) {
        return;
      }
      if (Occupied(folder, oldName, newName)) {
        WarnBothExist(folder, oldName, newName, report);
        return;
      }
      try {
        Move(folder, oldName, newName, true);
      } catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) {
        report.AddWarning(ex, ex.Message, "Could not rename the shortcuts folder {OldPath} to {NewPath}; it keeps its old name",
          Path.Combine(folder, oldName), Path.Combine(folder, newName));
        return;
      }
      report.AddRenamed(Path.Combine(folder, oldName), Path.Combine(folder, newName));
    }

    private static void WarnBothExist(string folder, string oldName, string newName, DataNamesReport report) {
      string oldPath = Path.Combine(folder, oldName);
      string newPath = Path.Combine(folder, newName);
      report.AddWarning(null, "Both " + oldPath + " and " + newPath + " exist",
        "Both {OldPath} and {NewPath} exist: MDSHO uses {NewPath} and leaves {OldPath} untouched", oldPath, newPath, newPath, oldPath);
    }

    // True when a file or folder with exactly this name (same case) is in the folder.
    private static bool ExactExists(string folder, string name) {
      return Directory.EnumerateFileSystemEntries(folder, name)
        .Any(path => string.Equals(Path.GetFileName(path), name, StringComparison.Ordinal));
    }

    // True when the new name is already taken. For a case-only rename the old entry itself answers to the new name on a
    // case-insensitive disk, so only an entry with exactly the new name counts.
    private static bool Occupied(string folder, string oldName, string newName) {
      if (string.Equals(oldName, newName, StringComparison.OrdinalIgnoreCase)) {
        return ExactExists(folder, newName);
      }
      string newPath = Path.Combine(folder, newName);
      return File.Exists(newPath) || Directory.Exists(newPath);
    }

    // Renames one entry. A case-only rename goes through a temporary name, so the new case is really written on disk.
    private static void Move(string folder, string from, string to, bool isFolder) {
      string source = Path.Combine(folder, from);
      string target = Path.Combine(folder, to);
      if (!string.Equals(from, to, StringComparison.OrdinalIgnoreCase)) {
        MoveEntry(source, target, isFolder);
        return;
      }
      string temp = Path.Combine(folder, to + ".rename-" + Guid.NewGuid().ToString("N"));
      MoveEntry(source, temp, isFolder);
      try {
        MoveEntry(temp, target, isFolder);
      } catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) {
        MoveEntry(temp, source, isFolder);
        throw;
      }
    }

    private static void MoveEntry(string source, string target, bool isFolder) {
      if (isFolder) {
        Directory.Move(source, target);
      } else {
        File.Move(source, target);
      }
    }
  }
}
