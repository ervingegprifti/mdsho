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

    public static List<DesktopWindow> Windows() {
      return Filled(Many<DesktopWindow>("SELECT * FROM MDSHOWindows ORDER BY ID ASC"));
    }

    public static List<DesktopWindowClone> Clones() {
      return Filled(Many<DesktopWindowClone>("SELECT *, ParentMDSHOWindowID AS ParentWindowID FROM MDSHOWindowClones ORDER BY ID ASC"));
    }

    public static List<Shortcut> Shortcuts() {
      return Filled(Many<Shortcut>("SELECT *, ParentMDSHOWindowID AS ParentWindowID FROM Shortcuts ORDER BY ID ASC"));
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
