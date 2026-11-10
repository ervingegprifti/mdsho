using System;
using MDSHO.Core;

namespace MDSHO.ViewModels {

  /// <summary>The database step of app startup, kept free of WPF so it can be tested.</summary>
  public static class DatabaseStartup {
    /// <summary>Caption of the message shown when the database is newer than the app.</summary>
    public const string TooNewCaption = "MDSHO Update Needed";

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
  }

}
