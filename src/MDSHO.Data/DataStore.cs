using MDSHO.Core;

namespace MDSHO.Data {

  /// <summary>The SQLite implementation of <see cref="IDataStore"/>.</summary>
  internal sealed class SqliteDataStore : IDataStore {
    public IDesktopWindowRepository Windows { get; }
    public IDesktopWindowCloneRepository Clones { get; }
    public IShortcutRepository Shortcuts { get; }
    public INoteWindowRepository NoteWindows { get; }

    public SqliteDataStore() {
      Clones = new DesktopWindowCloneRepository();
      Shortcuts = new ShortcutRepository();
      NoteWindows = new NoteWindowRepository();
      Windows = new DesktopWindowRepository(Clones);
    }

    public ITransactionScope BeginTransaction() {
      return new UnitOfWork();
    }

    public void Upgrade() {
      SchemaManager.Upgrade();
    }
  }

  /// <summary>
  /// Static access to the persistence layer for the view models, which are not built by the container. The app's
  /// composition root registers <see cref="CreateSqlite"/> in the container and sets <see cref="Current"/> to that
  /// instance; tests can replace <see cref="Current"/> with another <see cref="IDataStore"/>.
  /// The default store works on the database at <c>Paths.GetAppDataLocalDatabaseFilePath()</c>, resolved on every call.
  /// </summary>
  public static class DataStore {
    /// <summary>A new SQLite store (on the database at <c>Paths.GetAppDataLocalDatabaseFilePath()</c>).</summary>
    public static IDataStore CreateSqlite() {
      return new SqliteDataStore();
    }

    /// <summary>The store used by the application. Created at first use; replace it only at startup or in tests.</summary>
    public static IDataStore Current { get; set; } = CreateSqlite();

    /// <summary>Windows repository.</summary>
    public static IDesktopWindowRepository Windows { get { return Current.Windows; } }
    /// <summary>Clones repository.</summary>
    public static IDesktopWindowCloneRepository Clones { get { return Current.Clones; } }
    /// <summary>Shortcuts repository.</summary>
    public static IShortcutRepository Shortcuts { get { return Current.Shortcuts; } }
    public static INoteWindowRepository NoteWindows { get { return Current.NoteWindows; } }

    /// <summary>Begin a transaction scope. Use: <c>using (var tx = DataStore.BeginTransaction()) { ...; tx.Commit(); }</c></summary>
    public static ITransactionScope BeginTransaction() {
      return Current.BeginTransaction();
    }

    /// <summary>Create the database if missing and apply the pending schema migrations.</summary>
    public static void Upgrade() {
      Current.Upgrade();
    }
  }

}
