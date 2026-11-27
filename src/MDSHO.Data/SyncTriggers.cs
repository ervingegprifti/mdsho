using System.Collections.Generic;
using System.Linq;
using Microsoft.Data.Sqlite;

namespace MDSHO.Data {

  /// <summary>
  /// The database triggers that keep the sync data of panels and buttons, built from <see cref="SyncFields"/>, so every
  /// write (repositories, imports, cascades) is covered:
  /// <list type="bullet">
  /// <item>an insert gets a SyncId and a change record when it has none, and removes the item's deleted record;</item>
  /// <item>a change of a watched (Synced) column updates the change record, unless the writer set ChangedAt itself
  /// (a later sync applies other PCs' records that way); a local change, or saving the same value, leaves it;</item>
  /// <item>a delete, also of everything inside a deleted group or panel, adds a DeletedItems row;</item>
  /// <item>a change of a note window's Synced column is a change of its note.</item>
  /// </list>
  /// The triggers are made again at start when they differ from this code, and dropped before a migration runs.
  /// </summary>
  internal static class SyncTriggers {
    /// <summary>The name of every sync trigger starts with this.</summary>
    internal const string Prefix = "Sync_";

    /// <summary>Now as UTC milliseconds since 1970.</summary>
    internal const string NowMs = "CAST(ROUND((julianday('now') - 2440587.5) * 86400000.0) AS INTEGER)";
    /// <summary>This PC's DeviceId.</summary>
    internal const string Device = "COALESCE((SELECT Value FROM AppSettings WHERE Key = 'DeviceId'), '')";
    /// <summary>A new random GUID (version 4) as lower-case text, evaluated again for each row.</summary>
    internal const string NewGuid = "(lower(hex(randomblob(4))) || '-' || lower(hex(randomblob(2))) || '-4' || substr(lower(hex(randomblob(2))), 2) || '-' "
      + "|| substr('89ab', 1 + (abs(random()) % 4), 1) || substr(lower(hex(randomblob(2))), 2) || '-' || lower(hex(randomblob(6))))";

    // The tables whose rows are items, with the kind a deleted row records.
    private static readonly (string Table, string Kind)[] Items = {
      ("MDSHOWindows", "'Panel'"),
      ("Shortcuts", "CASE WHEN OLD.IsGroup = 1 THEN 'Group' WHEN OLD.IsNote = 1 THEN 'Note' WHEN OLD.IsSnippet = 1 THEN 'Snippet' "
        + "WHEN OLD.IsProcessCommand = 1 THEN 'RunCommand' WHEN lower(OLD.Ext) = '.url' THEN 'Link' ELSE 'Shortcut' END"),
    };

    // Tables whose Synced columns belong to an item of another table, found by a link column.
    private static readonly (string Table, string Owner, string Link)[] Parts = {
      ("NoteWindows", "Shortcuts", "ShortcutID"),
    };

    /// <summary>The CREATE TRIGGER statements by name, for the current schema.</summary>
    internal static SortedDictionary<string, string> Build(SqliteConnection conn) {
      SortedDictionary<string, string> triggers = new SortedDictionary<string, string>();
      foreach ((string table, string kind) in Items) {
        string changed = string.Join(" OR ", SyncFields.WatchedColumns(table).Select(c => "NEW." + c + " IS NOT OLD." + c));
        triggers[Prefix + table + "_Insert"] = "CREATE TRIGGER " + Prefix + table + "_Insert AFTER INSERT ON " + table + " BEGIN "
          + "UPDATE " + table + " SET SyncId = CASE WHEN NEW.SyncId = '' THEN " + NewGuid + " ELSE SyncId END, "
          + "ChangedAt = CASE WHEN NEW.ChangedAt = 0 THEN " + NowMs + " ELSE ChangedAt END, "
          + "ChangedBy = CASE WHEN NEW.ChangedAt = 0 THEN " + Device + " ELSE ChangedBy END "
          + "WHERE ID = NEW.ID AND (NEW.SyncId = '' OR NEW.ChangedAt = 0); "
          + "DELETE FROM DeletedItems WHERE SyncId = (SELECT SyncId FROM " + table + " WHERE ID = NEW.ID); END";
        triggers[Prefix + table + "_Update"] = "CREATE TRIGGER " + Prefix + table + "_Update AFTER UPDATE ON " + table
          + " WHEN NEW.ChangedAt IS OLD.ChangedAt AND (" + changed + ") BEGIN "
          + "UPDATE " + table + " SET ChangedAt = MAX(" + NowMs + ", OLD.ChangedAt + 1), ChangedBy = " + Device + " WHERE ID = NEW.ID; END";
        triggers[Prefix + table + "_Delete"] = "CREATE TRIGGER " + Prefix + table + "_Delete AFTER DELETE ON " + table
          + " WHEN OLD.SyncId <> '' BEGIN "
          + "INSERT OR REPLACE INTO DeletedItems (SyncId, Kind, DeletedAt, DeletedBy) VALUES (OLD.SyncId, " + kind + ", " + NowMs + ", " + Device + "); END";
      }
      foreach ((string table, string owner, string link) in Parts) {
        List<string> watched = SyncFields.WatchedColumns(table).ToList();
        Dictionary<string, string> defaults = Defaults(conn, table);
        string differs = string.Join(" OR ", watched.Select(c => "NEW." + c + " IS NOT " + defaults[c]));
        string changed = string.Join(" OR ", watched.Select(c => "NEW." + c + " IS NOT OLD." + c).Append("NEW." + link + " IS NOT OLD." + link));
        string touch = "UPDATE " + owner + " SET ChangedAt = MAX(" + NowMs + ", ChangedAt + 1), ChangedBy = " + Device + " WHERE ID = NEW." + link + "; END";
        triggers[Prefix + table + "_Insert"] = "CREATE TRIGGER " + Prefix + table + "_Insert AFTER INSERT ON " + table + " WHEN " + differs + " BEGIN " + touch;
        triggers[Prefix + table + "_Update"] = "CREATE TRIGGER " + Prefix + table + "_Update AFTER UPDATE ON " + table + " WHEN " + changed + " BEGIN " + touch;
      }
      return triggers;
    }

    /// <summary>Makes the triggers again when they differ from <see cref="Build"/> (none, older or extra ones).</summary>
    internal static void Ensure(SqliteConnection conn) {
      SortedDictionary<string, string> wanted = Build(conn);
      SortedDictionary<string, string> existing = Existing(conn);
      if (wanted.Count == existing.Count && wanted.All(t => existing.TryGetValue(t.Key, out string sql) && sql == t.Value)) {
        return;
      }
      Drop(conn);
      foreach (string sql in wanted.Values) {
        Execute(conn, sql);
      }
      MDSHO.Common.AppLog.Information("Sync triggers made: {Count}", wanted.Count);
    }

    /// <summary>Drops every sync trigger.</summary>
    internal static void Drop(SqliteConnection conn) {
      foreach (string name in Existing(conn).Keys) {
        Execute(conn, "DROP TRIGGER IF EXISTS " + name + ";");
      }
    }

    private static SortedDictionary<string, string> Existing(SqliteConnection conn) {
      SortedDictionary<string, string> triggers = new SortedDictionary<string, string>();
      using (SqliteCommand cmd = conn.CreateCommand()) {
        cmd.CommandText = "SELECT name, sql FROM sqlite_master WHERE type = 'trigger' AND substr(name, 1, " + Prefix.Length + ") = '" + Prefix + "'";
        using (SqliteDataReader r = cmd.ExecuteReader()) {
          while (r.Read()) {
            triggers[r.GetString(0)] = r.GetString(1);
          }
        }
      }
      return triggers;
    }

    // The default of each column as SQL ("NULL" when it has none).
    private static Dictionary<string, string> Defaults(SqliteConnection conn, string table) {
      Dictionary<string, string> defaults = new Dictionary<string, string>();
      using (SqliteCommand cmd = conn.CreateCommand()) {
        cmd.CommandText = "SELECT name, dflt_value FROM pragma_table_info('" + table + "')";
        using (SqliteDataReader r = cmd.ExecuteReader()) {
          while (r.Read()) {
            defaults[r.GetString(0)] = r.IsDBNull(1) ? "NULL" : r.GetString(1);
          }
        }
      }
      return defaults;
    }

    private static void Execute(SqliteConnection conn, string sql) {
      using (SqliteCommand cmd = conn.CreateCommand()) {
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
      }
    }
  }
}
