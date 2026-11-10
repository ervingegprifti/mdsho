namespace MDSHO.Core {

  /// <summary>
  /// Entry point to the persistence layer: the repositories, transaction scopes and the schema upgrade.
  /// </summary>
  public interface IDataStore {
    /// <summary>Windows repository.</summary>
    IDesktopWindowRepository Windows { get; }
    /// <summary>Clones repository.</summary>
    IDesktopWindowCloneRepository Clones { get; }
    /// <summary>Shortcuts repository.</summary>
    IShortcutRepository Shortcuts { get; }
    /// <summary>Begin a transaction scope (joins the current one on this thread, if any).</summary>
    ITransactionScope BeginTransaction();
    /// <summary>Create the database if missing and apply every pending schema migration (with a backup first).
    /// Throws <see cref="DatabaseTooNewException"/>, without touching the file, when the database is newer than this app.</summary>
    void Upgrade();
  }

}
