using System;
using System.IO;
using MDSHO.Core;
using MDSHO.Data;

namespace MDSHO.ViewModels {

  /// <summary>What to do when the database can't be opened at startup.</summary>
  public enum DatabaseRecoveryChoice {
    /// <summary>Keep the old file, make a new database and import the latest backup into it.</summary>
    RestoreBackup,
    /// <summary>Keep the old file and start with a new, empty database.</summary>
    StartEmpty,
    /// <summary>Exit MDSHO and change nothing.</summary>
    Exit
  }

  /// <summary>The database step of app startup, kept free of WPF so it can be tested.</summary>
  public static class DatabaseStartup {
    /// <summary>Caption of the message shown when the database is newer than the app.</summary>
    public const string TooNewCaption = "MDSHO Update Needed";

    /// <summary>Caption of the question and messages when the database can't be opened.</summary>
    public const string RecoveryCaption = "MDSHO could not open your data";

    /// <summary>
    /// Runs <paramref name="upgrade"/>. Returns true when startup may continue. When the database was created by a newer
    /// MDSHO, shows the update message through <paramref name="showMessage"/> (message, caption) and returns false: the
    /// caller must exit without touching the data. Any other exception is not handled here.
    /// </summary>
    public static bool TryUpgrade(Action upgrade, Action<string, string> showMessage) {
      try {
        upgrade();
        return true;
      } catch (DatabaseTooNewException ex) {
        Common.AppLog.Warning(ex, "The database was created by a newer MDSHO (database version {DatabaseVersion}, supported {SupportedVersion}); MDSHO will exit", ex.DatabaseVersion, ex.SupportedVersion);
        showMessage(DatabaseTooNewException.UserMessage, TooNewCaption);
        return false;
      }
    }

    /// <summary>
    /// Like <see cref="TryUpgrade(Action, Action{string, string})"/>, but when the database can't be opened or upgraded
    /// for any other reason, asks the user through <paramref name="ask"/> (message, is there a backup) to restore the
    /// latest backup, start with empty data, or exit. Restore and Start empty keep the old file (renamed) and make a new
    /// database. Returns true when startup may continue.
    /// </summary>
    /// <param name="findLatestBackup">The newest backup file, or null.</param>
    public static bool TryUpgrade(Action upgrade, Action<string, string> showMessage, Func<string, bool, DatabaseRecoveryChoice> ask, Func<string> findLatestBackup) {
      try {
        return TryUpgrade(upgrade, showMessage);
      } catch (Exception ex) {
        Common.AppLog.Error(ex, "The database could not be opened or upgraded");
        return Recover(ex, upgrade, showMessage, ask, findLatestBackup);
      }
    }

    private static bool Recover(Exception error, Action upgrade, Action<string, string> showMessage, Func<string, bool, DatabaseRecoveryChoice> ask, Func<string> findLatestBackup) {
      string backup = null;
      try {
        backup = findLatestBackup();
      } catch (Exception ex) {
        Common.AppLog.Warning(ex, "The backup folder could not be searched");
      }
      DatabaseRecoveryChoice choice = ask(RecoveryQuestion(error, backup), backup != null);
      Common.AppLog.Information("Database recovery chosen: {Choice}", choice);
      if (choice == DatabaseRecoveryChoice.Exit) {
        return false;
      }
      string kept;
      try {
        kept = DatabaseFile.SetAside(DateTime.Now);
        upgrade();
      } catch (Exception ex) {
        Common.AppLog.Error(ex, "A new database could not be made");
        showMessage("MDSHO could not make a new data file: " + ex.Message + "\n\nMDSHO will exit. If another program is using the data file, close it and start MDSHO again.", RecoveryCaption);
        return false;
      }
      string keptLine = "Your old data file was kept as:\n" + kept;
      if (choice == DatabaseRecoveryChoice.RestoreBackup && backup != null) {
        try {
          DataImporter.Import(backup, ImportMode.Replace);
        } catch (Exception ex) {
          // The new database is there: start with empty data rather than not at all.
          showMessage("The backup " + Path.GetFileName(backup) + " could not be restored: " + ex.Message + "\n\nMDSHO starts with empty data.\n\n" + keptLine, RecoveryCaption);
          return true;
        }
        showMessage("Your data was restored from the backup " + Path.GetFileName(backup) + ".\n\n" + keptLine, RecoveryCaption);
        return true;
      }
      showMessage("MDSHO starts with empty data.\n\n" + keptLine, RecoveryCaption);
      return true;
    }

    /// <summary>The question asked when the database can't be opened, with the answers the buttons give.</summary>
    public static string RecoveryQuestion(Exception error, string backup) {
      string text = "MDSHO could not open your data.\n\n" + error.Message.Trim() + "\n\n";
      if (backup != null) {
        return text + "Yes: restore the latest backup, " + Path.GetFileName(backup) + ".\nNo: start with empty data.\nCancel: exit MDSHO.\n\n" +
          "Yes and No keep your old data file.";
      }
      return text + "No backup was found.\n\nOK: start with empty data. Your old data file is kept.\nCancel: exit MDSHO.";
    }
  }

}
