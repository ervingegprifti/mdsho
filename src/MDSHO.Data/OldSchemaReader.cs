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
