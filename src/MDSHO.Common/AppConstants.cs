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
    /// <summary>The home page of the MDSHO docs site (the user guide).</summary>
    public const string DocsUrl = "https://ervingegprifti.github.io/mdsho/";
    /// <summary>The MDSHO Privacy Policy on the docs site.</summary>
    public const string PrivacyPolicyUrl = "https://ervingegprifti.github.io/mdsho/privacy/";
    /// <summary>The MDSHO Terms and Conditions on the docs site.</summary>
    public const string TermsUrl = "https://ervingegprifti.github.io/mdsho/terms/";
    /// <summary>The Support MDSHO page on the docs site (GitHub Sponsors and Ko-fi).</summary>
    public const string SupportUrl = "https://ervingegprifti.github.io/mdsho/support/";

    /// <summary>
    /// We use this to identify the current database schema version.
    /// </summary>
    public const int DatabaseVersion = 17;

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

    // Automatic backups folder name, in the user's profile folder: MSIX removes the app's own data folder on uninstall,
    // and antivirus ransomware shields and Controlled folder access block writes to Documents.
    /// <summary>Name of the default folder of the automatic backups, in the user's profile folder (Release build).</summary>
    public const string BackupsFolderName = "mdsho-backups";
    /// <summary>Name of the default folder of the automatic backups, in the user's profile folder (Debug build).</summary>
    public const string BackupsDebugFolderName = "mdsho-backups-debug";
    /// <summary>Default backup folder names of 1.12.0 and 1.12.1 (Release, Debug). A saved folder with one of these names in
    /// the profile or Documents folder counts as no folder chosen; files in it are never moved or deleted.</summary>
    public static readonly string[] OldBackupsFolderNames = { "MDSHO backups", "MDSHO backups (debug)" };

    /// <summary>Width in device independent pixels of a new window or clone.</summary>
    public const int InitialWindowWidth = 300;
    /// <summary>Height in device independent pixels of a new window or clone.</summary>
    public const int InitialWindowHeight = 500;

    // The colours of a new window (also what Default colours puts back when no defaults are saved): the colour preset
    // Autumn: Halloween (ColorPresets.Halloween in MDSHO.Core; ColorPresetsTests keeps the two equal).
    /// <summary>Border colour of a new window.</summary>
    public const string DefaultWindowBorderColor = "#FF000000";
    /// <summary>Background colour of a new window.</summary>
    public const string DefaultWindowBackgroundColor = "#CC000000";
    /// <summary>Text colour of a new window.</summary>
    public const string DefaultWindowTextColor = "#FFFFFFFF";
    /// <summary>Title bar background colour of a new window.</summary>
    public const string DefaultWindowTitleBackgroundColor = "#FF000000";
    /// <summary>Title text colour of a new window.</summary>
    public const string DefaultWindowTitleTextColor = "#FFFFFFFF";

    // File extensions.
    /// <summary>Extension of Windows shortcut files.</summary>
    public const string LnkExtension = ".lnk";
    /// <summary>Extension of Internet shortcut files.</summary>
    public const string UrlExtension = ".url";
  }

}