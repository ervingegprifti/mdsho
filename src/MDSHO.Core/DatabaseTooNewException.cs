using System;

namespace MDSHO.Core {

  /// <summary>
  /// The database file was written by a newer MDSHO: its schema version is higher than any version this app knows.
  /// The file has not been migrated, written or backed up. The app must not use it; the user has to update MDSHO.
  /// </summary>
  public sealed class DatabaseTooNewException : Exception {
    /// <summary>The message shown to the user.</summary>
    public const string UserMessage = "This data was created by a newer version of MDSHO. Please update MDSHO.";

    /// <summary>The schema version found in the database file (<c>PRAGMA user_version</c>).</summary>
    public int DatabaseVersion { get; }
    /// <summary>The highest schema version this app supports.</summary>
    public int SupportedVersion { get; }

    public DatabaseTooNewException(int databaseVersion, int supportedVersion)
      : base(UserMessage + " (database version " + databaseVersion + ", this version supports up to " + supportedVersion + ")") {
      DatabaseVersion = databaseVersion;
      SupportedVersion = supportedVersion;
    }
  }

}
