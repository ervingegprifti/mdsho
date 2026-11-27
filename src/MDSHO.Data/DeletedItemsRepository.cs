using System.Collections.Generic;
using MDSHO.Core;

namespace MDSHO.Data {

  /// <summary>The DeletedItems table. The sync triggers add a row for each deleted panel or button and remove it when the item is added again.</summary>
  internal sealed class DeletedItemsRepository : RepositoryBase, IDeletedItemsRepository {

    public List<DeletedItem> GetAll() {
      return Many<DeletedItem>("SELECT SyncId, Kind, DeletedAt, DeletedBy FROM DeletedItems ORDER BY DeletedAt ASC, SyncId ASC");
    }

    public void Merge(IEnumerable<DeletedItem> items) {
      InTransaction(() => {
        foreach (DeletedItem item in items) {
          if (string.IsNullOrEmpty(item?.SyncId)) {
            continue;
          }
          Run(@"INSERT INTO DeletedItems (SyncId, Kind, DeletedAt, DeletedBy)
            SELECT @SyncId, @Kind, @DeletedAt, @DeletedBy
            WHERE NOT EXISTS (SELECT 1 FROM Shortcuts WHERE SyncId = @SyncId) AND NOT EXISTS (SELECT 1 FROM MDSHOWindows WHERE SyncId = @SyncId)
            ON CONFLICT(SyncId) DO UPDATE SET Kind = excluded.Kind, DeletedAt = excluded.DeletedAt, DeletedBy = excluded.DeletedBy
            WHERE excluded.DeletedAt > DeletedItems.DeletedAt",
            new { item.SyncId, Kind = item.Kind ?? "", item.DeletedAt, DeletedBy = item.DeletedBy ?? "" });
        }
      });
    }
  }
}
