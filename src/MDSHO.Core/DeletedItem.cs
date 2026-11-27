using System.Collections.Generic;

namespace MDSHO.Core {

  /// <summary>The kinds a deleted item can have (DeletedItems.Kind).</summary>
  public static class DeletedItemKinds {
    /// <summary>A panel.</summary>
    public const string Panel = "Panel";
    /// <summary>A group of buttons.</summary>
    public const string Group = "Group";
    /// <summary>A shortcut to a file, folder or program (.lnk).</summary>
    public const string Shortcut = "Shortcut";
    /// <summary>A web link (.url).</summary>
    public const string Link = "Link";
    /// <summary>A note.</summary>
    public const string Note = "Note";
    /// <summary>A snippet.</summary>
    public const string Snippet = "Snippet";
    /// <summary>A run command.</summary>
    public const string RunCommand = "RunCommand";
  }

  /// <summary>A deleted panel or button, remembered so a delete can reach the other PCs (a row of DeletedItems).</summary>
  public sealed class DeletedItem {
    /// <summary>The SyncId the item had.</summary>
    public string SyncId { get; set; } = "";
    /// <summary>What it was (see <see cref="DeletedItemKinds"/>).</summary>
    public string Kind { get; set; } = "";
    /// <summary>When it was deleted: UTC milliseconds since 1970.</summary>
    public long DeletedAt { get; set; }
    /// <summary>The DeviceId of the PC that deleted it.</summary>
    public string DeletedBy { get; set; } = "";
  }

  /// <summary>The DeletedItems table. Rows are added and removed by the database itself when items are deleted or added.</summary>
  public interface IDeletedItemsRepository {
    /// <summary>Every row, oldest delete first.</summary>
    List<DeletedItem> GetAll();
    /// <summary>
    /// Adds rows from an import. A row for an item that exists is skipped, and a row already here keeps the later
    /// delete.
    /// </summary>
    void Merge(IEnumerable<DeletedItem> items);
  }
}
