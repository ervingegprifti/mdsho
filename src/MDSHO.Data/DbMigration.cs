using System;
using Microsoft.Data.Sqlite;

namespace MDSHO.Data {

  /// <summary>
  /// One versioned database upgrade step. <see cref="Apply"/> runs inside a transaction together with the
  /// <c>PRAGMA user_version</c> update, so a step either fully applies or leaves the database untouched.
  /// </summary>
  internal sealed class DbMigration {
    public int Version { get; }
    public string Name { get; }
    public Action<SqliteConnection> Apply { get; }
    public DbMigration(int version, string name, Action<SqliteConnection> apply) {
      Version = version;
      Name = name;
      Apply = apply;
    }
  }

}
