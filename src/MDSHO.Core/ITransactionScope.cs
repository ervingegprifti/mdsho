using System;

namespace MDSHO.Core {

  /// <summary>
  /// A database transaction scope (unit of work). Every repository call made on the same thread while the scope is open
  /// takes part in it, so several writes for one user action commit or roll back together.<br />
  /// Dispose without <see cref="Commit"/> rolls everything back. Scopes can be nested: an inner scope joins the outer one,
  /// the outermost scope decides. An inner scope that is disposed without commit makes the outer commit fail.
  /// <code>using (ITransactionScope tx = store.BeginTransaction()) { ...; tx.Commit(); }</code>
  /// </summary>
  public interface ITransactionScope : IDisposable {
    /// <summary>Makes all changes of the scope permanent; without it they are rolled back on Dispose.</summary>
    void Commit();
  }

}
