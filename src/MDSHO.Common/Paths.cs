using System;
using System.IO;

namespace MDSHO.Common {

  /// <summary>
  /// A helper class to get diferent app data local paths.
  /// </summary>
  public static class Paths {
    /// <summary>
    /// Test hook. When not null it replaces the database file path. Null (default) = normal behaviour.
    /// </summary>
    public static string DatabaseFilePathOverride { get; set; } = null;
    /// <summary>
    /// Test hook. When not null it replaces the shortcuts folder path. Null (default) = normal behaviour.
    /// </summary>
    public static string ShortcutsPathOverride { get; set; } = null;
    /// <summary>
    /// Test hook. When not null it replaces the import backups folder path. Null (default) = normal behaviour.
    /// </summary>
    public static string ImportBackupsPathOverride { get; set; } = null;

    /// <summary>
    /// Get the user app data local path. Aapplies to current user local only.<br />
    /// No need for Administrator access to write on this folder.<br /><br />
    /// References:<br />
    /// <see href="https://www.codeproject.com/Tips/370232/Where-should-I-store-my-data">Where Should I Store My Data?</see><br />
    /// </summary>
    /// <returns>
    /// C:\Users\USER\AppData\Local
    /// </returns>
    private static string GetAppDataLocalPath() {
      string appDataLocalPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
      return appDataLocalPath;
    }

    /// <summary>
    /// Get the user app data local application folder path.
    /// </summary>
    /// <returns>
    /// C:\Users\USER\AppData\Local\ApplicationFolderName
    /// </returns>
    private static string GetAppDataLocalApplicationFolderPath() {
      string appDataLocalPath = GetAppDataLocalPath();
      string appDataLocalApplicationFolderPath = Path.Combine(appDataLocalPath, AppConstants.ApplicationFolderName);
      // Creates all directories and subdirectories in the specified path unless they already exist.
      DirectoryInfo directoryInfo = Directory.CreateDirectory(appDataLocalApplicationFolderPath);
      return directoryInfo.FullName;
    }

    /// <summary>
    /// The app data folder (created when missing): <c>%LOCALAPPDATA%\MDSHO</c>, as the app sees it.
    /// </summary>
    public static string GetAppDataFolderPath() {
      return GetAppDataLocalApplicationFolderPath();
    }

    /// <summary>
    /// Get the user app data local database file path.
    /// </summary>
    /// <returns>
    /// C:\Users\USER\AppData\Local\ApplicationFolderName\DatabaseFileName
    /// </returns>
    public static string GetAppDataLocalDatabaseFilePath() {
      if (DatabaseFilePathOverride != null) {
        return DatabaseFilePathOverride;
      }
      string appDataLocalApplicationFolderPath = GetAppDataLocalApplicationFolderPath();
#if DEBUG
      string appDataLocalDatabaseFilePath = Path.Combine(appDataLocalApplicationFolderPath, AppConstants.DatabaseDebugFileName);
#else
      string appDataLocalDatabaseFilePath = Path.Combine(appDataLocalApplicationFolderPath, AppConstants.DatabaseFileName);
#endif
      return appDataLocalDatabaseFilePath;
    }

    /// <summary>
    /// Get the user app data shortcuts folder path.
    /// </summary>
    /// <returns>C:\Users\USER\AppData\Local\ApplicationFolderName\ShortcutsFolderName</returns>
    public static string GetShortcutsPath() {
      if (ShortcutsPathOverride != null) {
        Directory.CreateDirectory(ShortcutsPathOverride);
        return ShortcutsPathOverride;
      }
#if DEBUG
      string path = Path.Combine(GetAppDataLocalApplicationFolderPath(), AppConstants.ShortcutsDebugFolderName);
#else
      string path = Path.Combine(GetAppDataLocalApplicationFolderPath(), AppConstants.ShortcutsFolderName);
#endif
      if (!Directory.Exists(path)) {
        // If directory does not exists then it is created. If it exists then nothing is done.
        DirectoryInfo directoryInfo = Directory.CreateDirectory(path);
      }
      return path;
    }
  

    /// <summary>The path of a shortcut's .lnk or .url file in the shortcuts folder: its Guid plus its extension.</summary>
    public static string GetShortcutFilePath(string guid, string ext) {
      return Path.Combine(GetShortcutsPath(), guid + ext);
    }
    /// <summary>
    /// Get the folder where the automatic "before import" backups are kept (never deleted by the application).
    /// </summary>
    /// <returns>C:\Users\USER\AppData\Local\ApplicationFolderName\ImportBackups</returns>
    public static string GetImportBackupsPath() {
      if (ImportBackupsPathOverride != null) {
        Directory.CreateDirectory(ImportBackupsPathOverride);
        return ImportBackupsPathOverride;
      }
#if DEBUG
      string path = Path.Combine(GetAppDataLocalApplicationFolderPath(), AppConstants.ImportBackupsDebugFolderName);
#else
      string path = Path.Combine(GetAppDataLocalApplicationFolderPath(), AppConstants.ImportBackupsFolderName);
#endif
      Directory.CreateDirectory(path);
      return path;
    }
  }

}
