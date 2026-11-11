namespace MDSHO.Common {

  /// <summary>Values shared by the whole application: names, file extensions and default sizes.</summary>
  public static class AppConstants {
    /// <summary>
    /// This is the application name.
    /// </summary>
    public const string ApplicationName = "MDSHO";

    /// <summary>
    /// This is the application folder name created in C:\Users\USER\AppData\Local.
    /// </summary>
    public const string ApplicationFolderName = ApplicationName;

    /// <summary>The MDSHO project on GitHub (source code, issues).</summary>
    public const string ProjectUrl = "https://github.com/ervingegprifti/mdsho";
    /// <summary>The MDSHO Privacy Policy on the docs site.</summary>
    public const string PrivacyPolicyUrl = "https://ervingegprifti.github.io/mdsho/privacy/";
    /// <summary>The MDSHO Terms and Conditions on the docs site.</summary>
    public const string TermsUrl = "https://ervingegprifti.github.io/mdsho/terms/";

    /// <summary>
    /// We use this to identify the current database schema version.
    /// </summary>
    public const int DatabaseVersion = 8;

    // Database file name.
    /// <summary>File name of the database of the Release build.</summary>
    public const string DatabaseFileName = "mdsho.sqlite3";
    /// <summary>File name of the database of the Debug build (kept apart from the real data).</summary>
    public const string DatabaseDebugFileName = "mdsho-debug.sqlite3";

    // Shortcuts folder name.
    /// <summary>Name of the folder holding the shortcut files of the Release build.</summary>
    public const string ShortcutsFolderName = "shortcuts";
    /// <summary>Name of the folder holding the shortcut files of the Debug build.</summary>
    public const string ShortcutsDebugFolderName = "shortcuts-debug";

    // Names used before 1.1.2. Data under these names is renamed at startup (see MDSHO.Data.DataNamesUpgrade).
    /// <summary>Database file name of the Release build before 1.1.2.</summary>
    public const string LegacyDatabaseFileName = "MDSHO.sqlite3";
    /// <summary>Database file name of the Debug build before 1.1.2.</summary>
    public const string LegacyDatabaseDebugFileName = "MDSHODebug.sqlite3";
    /// <summary>Shortcuts folder name of the Release build before 1.1.2.</summary>
    public const string LegacyShortcutsFolderName = "MDSHOShortcuts";
    /// <summary>Shortcuts folder name of the Debug build before 1.1.2.</summary>
    public const string LegacyShortcutsDebugFolderName = "MDSHOShortcutsDebug";

    // Import backups folder name.
    /// <summary>Name of the folder where the automatic backup before an import is kept (Release build).</summary>
    public const string ImportBackupsFolderName = "ImportBackups";
    /// <summary>Name of the folder where the automatic backup before an import is kept (Debug build).</summary>
    public const string ImportBackupsDebugFolderName = "ImportBackupsDebug";

    /// <summary>Width in device independent pixels of a new window or clone.</summary>
    public const int InitialWindowWidth = 300;
    /// <summary>Height in device independent pixels of a new window or clone.</summary>
    public const int InitialWindowHeight = 500;

    // File extensions.
    /// <summary>Extension of Windows shortcut files.</summary>
    public const string LnkExtension = ".lnk";
    /// <summary>Extension of Internet shortcut files.</summary>
    public const string UrlExtension = ".url";
  }

}