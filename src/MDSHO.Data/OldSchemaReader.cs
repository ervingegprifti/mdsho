using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MDSHO.Core;

namespace MDSHO.Data {

  /// <summary>
  /// Reads the exported tables from a database an older MDSHO made, before it is upgraded. Every column is read as it
  /// is (SELECT *), so columns added later are simply missing; a missing text column becomes "" because the importer
  /// refuses empty text values.
  /// </summary>
  internal sealed class OldSchemaReader : RepositoryBase {

    // Before version 27 panels had no sort key: they get keys in ID order, so the file keeps their order.
    public static List<DesktopWindow> Windows() {
      List<DesktopWindow> windows = Filled(Many<DesktopWindow>("SELECT * FROM MDSHOWindows ORDER BY ID ASC"));
      if (!ColumnExists("MDSHOWindows", "SortKey")) {
        List<string> keys = MDSHO.Common.SortKeys.Sequence(windows.Count);
        for (int i = 0; i < windows.Count; i++) {
          windows[i].SortKey = keys[i];
        }
      }
      return windows;
    }

    public static List<DesktopWindowClone> Clones() {
      return Filled(Many<DesktopWindowClone>("SELECT *, ParentMDSHOWindowID AS ParentWindowID FROM MDSHOWindowClones ORDER BY ID ASC"));
    }

    // Before version 27 buttons were ordered by Position, then ID: they get sort keys in that order.
    public static List<Shortcut> Shortcuts() {
      List<Shortcut> shortcuts = Filled(Many<Shortcut>("SELECT *, ParentMDSHOWindowID AS ParentWindowID FROM Shortcuts ORDER BY ID ASC"));
      if (!ColumnExists("Shortcuts", "SortKey") && ColumnExists("Shortcuts", "Position")) {
        Dictionary<long, long> positions = Many<(long ID, long Position)>("SELECT ID, Position FROM Shortcuts").ToDictionary(r => r.ID, r => r.Position);
        foreach (IGrouping<(long?, long?), Shortcut> siblings in shortcuts.GroupBy(s => (s.ParentShortcutID, s.ParentWindowID))) {
          List<Shortcut> ordered = siblings.OrderBy(s => positions[s.ID]).ThenBy(s => s.ID).ToList();
          List<string> keys = MDSHO.Common.SortKeys.Sequence(ordered.Count);
          for (int i = 0; i < ordered.Count; i++) {
            ordered[i].SortKey = keys[i];
          }
        }
      }
      return shortcuts;
    }

    // DeletedItems came in version 27; older databases have none.
    public static List<DeletedItem> DeletedItems() {
      return TableExists("DeletedItems") ? Many<DeletedItem>("SELECT SyncId, Kind, DeletedAt, DeletedBy FROM DeletedItems ORDER BY DeletedAt ASC") : new List<DeletedItem>();
    }

    // The NoteWindows table came in version 8; IsTopmost, IsWrapText and FontSize later (missing ones read as 0).
    public static List<NoteWindowPlace> NoteWindows() {
      return TableExists("NoteWindows") ? Many<NoteWindowPlace>("SELECT * FROM NoteWindows ORDER BY ShortcutID ASC") : new List<NoteWindowPlace>();
    }

    // DefaultColors came in version 15. Null without the table, so an import keeps the colours on that PC.
    public static Dictionary<string, ColorSet> DefaultColors() {
      if (!TableExists("DefaultColors")) {
        return null;
      }
      Dictionary<string, ColorSet> colors = new Dictionary<string, ColorSet>();
      foreach (ColorSetKind kind in System.Enum.GetValues<ColorSetKind>()) {
        colors[kind.ToString()] = One<ColorSet>(
          "SELECT BorderColor, TitleBackgroundColor, TitleTextColor, BackgroundColor, TextColor FROM DefaultColors WHERE Kind = @kind",
          new { kind = kind.ToString() });
      }
      return colors;
    }

    // AppSettings came in version 16. Null without the table, so an import keeps the settings on that PC.
    public static Dictionary<string, string> Settings() {
      if (!TableExists("AppSettings")) {
        return null;
      }
      Dictionary<string, string> values = new Dictionary<string, string>();
      foreach (string key in PortableSettings.Keys) {
        values[key] = One<string>("SELECT Value FROM AppSettings WHERE Key = @key", new { key });
      }
      return values;
    }

    // The History table came in version 24. Versions 22 and 23 kept two earlier copies on each shortcut: read them as
    // rows, the older copy first, skipping empty ones. Older databases have no history.
    public static List<HistoryEntry> History() {
      if (TableExists("History")) {
        return Filled(Many<HistoryEntry>("SELECT ID, ShortcutID, Field, Text, KeptAt FROM History ORDER BY ID ASC"));
      }
      List<HistoryEntry> rows = new List<HistoryEntry>();
      if (!ColumnExists("Shortcuts", "TextPrevious1")) {
        return rows;
      }
      foreach ((string field, string column) in new[] { ("Text", "TextPrevious2"), ("Text", "TextPrevious1"), ("Comment", "CommentPrevious2"), ("Comment", "CommentPrevious1") }) {
        if (ColumnExists("Shortcuts", column) && ColumnExists("Shortcuts", column + "Time")) {
          rows.AddRange(Many<HistoryEntry>("SELECT ID AS ShortcutID, '" + field + "' AS Field, " + column + " AS Text, " + column + "Time AS KeptAt "
            + "FROM Shortcuts WHERE " + column + " <> '' ORDER BY ID ASC"));
        }
      }
      return rows;
    }

    private static bool ColumnExists(string table, string column) {
      return One<long>("SELECT count(*) FROM pragma_table_info(@table) WHERE name = @column COLLATE NOCASE", new { table, column }) > 0;
    }

    private static bool TableExists(string table) {
      return One<long>("SELECT count(*) FROM sqlite_master WHERE type = 'table' AND name = @table", new { table }) > 0;
    }

    private static List<T> Filled<T>(List<T> rows) {
      PropertyInfo[] texts = typeof(T).GetProperties().Where(p => p.PropertyType == typeof(string) && p.CanWrite && p.CanRead).ToArray();
      foreach (T row in rows) {
        foreach (PropertyInfo p in texts) {
          if (p.GetValue(row) == null) {
            p.SetValue(row, "");
          }
        }
      }
      return rows;
    }
  }
}
