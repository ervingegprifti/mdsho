using System.Collections.Generic;
using MDSHO.Core;

namespace MDSHO.Data {

  /// <summary>The ScreenSetups and ScreenSetupPlaces tables.</summary>
  internal sealed class ScreenSetupRepository : RepositoryBase, IScreenSetupRepository {
    private const string PlaceColumns = "Kind, ItemID, ScreenDeviceName, Left, Top, Width, Height, Dock";

    public List<ScreenSetupRecord> GetAll() {
      return Many<ScreenSetupRecord>("SELECT ID, Key, Name, LastUsed FROM ScreenSetups ORDER BY ID ASC");
    }

    public ScreenSetupRecord Get(string key) {
      return One<ScreenSetupRecord>("SELECT ID, Key, Name, LastUsed FROM ScreenSetups WHERE Key = @key", new { key });
    }

    public long Ensure(string key, string name) {
      return InTransaction(() => {
        ScreenSetupRecord existing = Get(key);
        if (existing != null) {
          return existing.ID;
        }
        return Insert("INSERT INTO ScreenSetups (Key, Name) VALUES (@key, @name)", new { key, name = name ?? "" });
      });
    }

    public List<ScreenSetupPlace> GetPlaces(long setupID) {
      return Many<ScreenSetupPlace>("SELECT " + PlaceColumns + " FROM ScreenSetupPlaces WHERE SetupID = @setupID ORDER BY Kind, ItemID", new { setupID });
    }

    public void SavePlace(long setupID, ScreenSetupPlace place) {
      Run(@"
        INSERT INTO ScreenSetupPlaces (SetupID, Kind, ItemID, ScreenDeviceName, Left, Top, Width, Height, Dock)
        VALUES (@setupID, @Kind, @ItemID, @ScreenDeviceName, @Left, @Top, @Width, @Height, @Dock)
        ON CONFLICT(SetupID, Kind, ItemID) DO UPDATE SET
          ScreenDeviceName = excluded.ScreenDeviceName, Left = excluded.Left, Top = excluded.Top,
          Width = excluded.Width, Height = excluded.Height, Dock = excluded.Dock",
        new { setupID, place.Kind, place.ItemID, ScreenDeviceName = place.ScreenDeviceName ?? "", place.Left, place.Top, place.Width, place.Height, place.Dock });
    }

    public void DeleteAllExcept(long setupID) {
      Run("DELETE FROM ScreenSetups WHERE ID <> @setupID", new { setupID });
    }

    public void Touch(long setupID, string lastUsed) {
      Run("UPDATE ScreenSetups SET LastUsed = @lastUsed WHERE ID = @setupID", new { setupID, lastUsed = lastUsed ?? "" });
    }

    public void Rekey(long setupID, string key, string name) {
      Run("UPDATE ScreenSetups SET Key = @key, Name = @name WHERE ID = @setupID", new { setupID, key, name = name ?? "" });
    }

    public void Delete(long setupID) {
      Run("DELETE FROM ScreenSetups WHERE ID = @setupID", new { setupID });
    }

    public void CopyMissingPlaces(long fromSetupID, long toSetupID) {
      Run(@"
        INSERT OR IGNORE INTO ScreenSetupPlaces (SetupID, Kind, ItemID, ScreenDeviceName, Left, Top, Width, Height, Dock)
        SELECT @toSetupID, Kind, ItemID, ScreenDeviceName, Left, Top, Width, Height, Dock FROM ScreenSetupPlaces WHERE SetupID = @fromSetupID",
        new { fromSetupID, toSetupID });
    }

    public int DeletePlacesOfMissingItems() {
      return Run(@"
        DELETE FROM ScreenSetupPlaces WHERE
          (Kind = 0 AND ItemID NOT IN (SELECT ID FROM MDSHOWindows)) OR
          (Kind = 1 AND ItemID NOT IN (SELECT ID FROM MDSHOWindowClones)) OR
          (Kind = 2 AND ItemID NOT IN (SELECT ID FROM Shortcuts WHERE IsNote = 1))");
    }
  }
}
