using System;
using System.Threading;
using Microsoft.Data.Sqlite;
using MDSHO.Core;

namespace MDSHO.Data {

  /// <summary>
  /// The transaction scope (unit of work). Every repository call made on this thread while the scope is open uses the
  /// scope's connection, so several writes for one user action commit or roll back together.
  /// Dispose without <see cref="Commit"/> rolls everything back. Scopes can be nested: the outermost one decides.
  /// </summary>
  internal sealed class UnitOfWork : ITransactionScope {
    private static readonly ThreadLocal<UnitOfWork> current = new ThreadLocal<UnitOfWork>();

    private readonly UnitOfWork parent;
    private readonly SqliteTransaction sqliteTransaction;
    private bool committed;
    private bool failed;
    private bool disposed;

    /// <summary>The connection of the scope (the outermost scope's connection when nested).</summary>
    public SqliteConnection Connection { get; }
    /// <summary>The transaction of the scope (the outermost scope's transaction when nested).</summary>
    public SqliteTransaction Transaction { get { return parent != null ? parent.Transaction : sqliteTransaction; } }

    /// <summary>The scope open on the calling thread or null.</summary>
    public static UnitOfWork Current { get { return current.Value; } }

    public UnitOfWork() {
      parent = current.Value;
      if (parent != null) {
        Connection = parent.Connection;
      } else {
        Connection = ConnectionFactory.Open();
        try {
          sqliteTransaction = Connection.BeginTransaction();
        } catch {
          Connection.Dispose();
          throw;
        }
      }
      current.Value = this;
    }

    public void Commit() {
      if (disposed || committed) {
        return;
      }
      if (failed) {
        throw new InvalidOperationException("A nested transaction scope was not committed, the transaction cannot be committed.");
      }
      if (parent == null) {
        sqliteTransaction.Commit();
      }
      committed = true;
    }

    public void Dispose() {
      if (disposed) {
        return;
      }
      disposed = true;
      current.Value = parent;
      if (parent != null) {
        if (!committed) {
          parent.failed = true;
        }
        return;
      }
      try {
        sqliteTransaction.Dispose(); // rolls back when not committed
      } finally {
        Connection.Dispose();
      }
    }
  }

  /// <summary>
  /// The connection a repository call runs on: the current scope's connection (not closed on dispose) or, outside any
  /// scope, a new connection that is closed on dispose.
  /// </summary>
  internal sealed class ConnectionLease : IDisposable {
    private readonly bool owned;
    public SqliteConnection Connection { get; }
    public SqliteTransaction Transaction { get; }

    private ConnectionLease(SqliteConnection connection, SqliteTransaction transaction, bool owned) {
      Connection = connection;
      Transaction = transaction;
      this.owned = owned;
    }

    public static ConnectionLease Acquire() {
      UnitOfWork scope = UnitOfWork.Current;
      if (scope != null) {
        return new ConnectionLease(scope.Connection, scope.Transaction, false);
      }
      return new ConnectionLease(ConnectionFactory.Open(), null, true);
    }

    public void Dispose() {
      if (owned) {
        Connection.Dispose();
      }
    }
  }

}
