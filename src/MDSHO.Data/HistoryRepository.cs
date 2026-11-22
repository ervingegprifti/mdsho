using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MDSHO.Common;
using MDSHO.Core;

namespace MDSHO.Data {

  /// <summary>The History table. Newest means the latest KeptAt, then the highest ID.</summary>
  internal sealed class HistoryRepository : RepositoryBase, IHistoryRepository {
    private const string Columns = "ID, ShortcutID, Field, Text, KeptAt";
    // The rows of one button and field beyond the newest @keep.
    private const string Extra = "SELECT ID FROM History WHERE ShortcutID = @shortcutId AND Field = @field ORDER BY KeptAt DESC, ID DESC LIMIT -1 OFFSET @keep";

    public List<HistoryEntry> Get(long shortcutId, string field) {
      return Many<HistoryEntry>("SELECT " + Columns + " FROM History WHERE ShortcutID = @shortcutId AND Field = @field ORDER BY KeptAt DESC, ID DESC",
        new { shortcutId, field });
    }

    public List<HistoryEntry> GetAll() {
      return Many<HistoryEntry>("SELECT " + Columns + " FROM History ORDER BY ID ASC");
    }

    public void Add(HistoryEntry entry) {
      entry.ID = Insert("INSERT INTO History (ShortcutID, Field, Text, KeptAt) VALUES (@ShortcutID, @Field, COALESCE(@Text, ''), COALESCE(@KeptAt, ''))", entry);
    }

    public void Delete(long id) {
      Run("DELETE FROM History WHERE ID = @id", new { id });
    }

    public void Clear(long shortcutId, string field) {
      Run("DELETE FROM History WHERE ShortcutID = @shortcutId AND Field = @field", new { shortcutId, field });
    }

    public void Trim(long shortcutId, string field, int keep) {
      Run("DELETE FROM History WHERE ID IN (" + Extra + ")", new { shortcutId, field, keep = Math.Max(0, keep) });
    }

    public void TrimAll(int keep) {
      keep = Math.Max(0, keep);
      if (keep == 0) {
        Run("DELETE FROM History");
        return;
      }
      foreach (var group in Many<(long ShortcutID, string Field)>("SELECT DISTINCT ShortcutID, Field FROM History")) {
        Trim(group.ShortcutID, group.Field, keep);
      }
    }
  }

  /// <summary>
  /// "Text history: keep N earlier texts" in Settings, app-wide: 10 by default, 0 to 50, 0 = off. Lowering it trims the
  /// oldest copies of every button at once. Exports carry it (see <see cref="PortableSettings"/>).
  /// </summary>
  public static class TextHistorySetting {
    /// <summary>The AppSettings key; no row means <see cref="DefaultKeep"/>.</summary>
    public const string SettingKey = "TextHistoryKeep";
    /// <summary>How many earlier texts are kept when never set.</summary>
    public const int DefaultKeep = 10;
    /// <summary>The most that can be kept.</summary>
    public const int MaxKeep = 50;

    /// <summary>0 to 50, for the Settings list.</summary>
    public static IEnumerable<int> Choices => Enumerable.Range(0, MaxKeep + 1);

    /// <summary>The saved number within 0 to 50; the default when never set or not a number. Never throws.</summary>
    public static int Keep {
      get {
        string text = null;
        try {
          text = DataStore.AppSettings.Get(SettingKey);
        } catch (Exception ex) {
          AppLog.Warning(ex, "The text history setting could not be read; it is {Default}", DefaultKeep);
        }
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int keep) ? Clamp(keep) : DefaultKeep;
      }
    }

    /// <summary>Saves the number (kept within 0 to 50) and trims every button's history to it.</summary>
    public static void SetKeep(int keep) {
      keep = Clamp(keep);
      DataStore.AppSettings.Set(SettingKey, keep.ToString(CultureInfo.InvariantCulture));
      DataStore.History.TrimAll(keep);
    }

    private static int Clamp(int keep) => Math.Min(MaxKeep, Math.Max(0, keep));
  }
}
