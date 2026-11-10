using System;
using System.Collections.Generic;
using System.Linq;
using Dapper;
using MDSHO.Core;

namespace MDSHO.Data {

  /// <summary>
  /// Dapper helpers shared by the repositories. Every call runs on the current transaction scope's connection
  /// (<see cref="ConnectionLease"/>), or on a short-lived connection when there is no scope.
  /// </summary>
  internal abstract class RepositoryBase {

    protected static int Run(string sql, object param = null) {
      using (ConnectionLease lease = ConnectionLease.Acquire()) {
        return lease.Connection.Execute(sql, param, lease.Transaction);
      }
    }

    protected static List<T> Many<T>(string sql, object param = null) {
      using (ConnectionLease lease = ConnectionLease.Acquire()) {
        return lease.Connection.Query<T>(sql, param, lease.Transaction).ToList();
      }
    }

    protected static T One<T>(string sql, object param = null) {
      using (ConnectionLease lease = ConnectionLease.Acquire()) {
        return lease.Connection.QueryFirstOrDefault<T>(sql, param, lease.Transaction);
      }
    }

    /// <summary>Runs the INSERT and returns the new row id (<c>last_insert_rowid()</c> of the same connection).</summary>
    protected static long Insert(string insertSql, object param) {
      using (ConnectionLease lease = ConnectionLease.Acquire()) {
        return lease.Connection.ExecuteScalar<long>(insertSql + "; SELECT last_insert_rowid();", param, lease.Transaction);
      }
    }

    /// <summary>Runs the function in a transaction scope (joins the current one if any) and commits when it returns.</summary>
    protected static T InTransaction<T>(Func<T> func) {
      using (ITransactionScope tx = new UnitOfWork()) {
        T result = func();
        tx.Commit();
        return result;
      }
    }

    protected static void InTransaction(Action action) {
      InTransaction<object>(() => { action(); return null; });
    }
  }

}
