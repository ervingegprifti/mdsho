using System.Collections.Generic;

namespace MDSHO.Core {

  /// <summary>A row of the ScreenSetups table.</summary>
  public sealed class ScreenSetupRecord {
    public long ID { get; set; }
    /// <summary>Which screens make the setup. See ScreenSetup.Key.</summary>
    public string Key { get; set; }
    /// <summary>The name in the tray menu.</summary>
    public string Name { get; set; }
    /// <summary>When it was last the current setup, as sortable UTC text. Empty if never.</summary>
    public string LastUsed { get; set; }
  }

  /// <summary>A row of the ScreenSetupPlaces table: where one window was in one setup.</summary>
  public sealed class ScreenSetupPlace {
    /// <summary>A PlacedKind value.</summary>
    public long Kind { get; set; }
    /// <summary>The panel, clone or note button ID.</summary>
    public long ItemID { get; set; }
    public string ScreenDeviceName { get; set; }
    public long Left { get; set; }
    public long Top { get; set; }
    public long Width { get; set; }
    public long Height { get; set; }
    /// <summary>A DockSide value.</summary>
    public long Dock { get; set; }
  }

  /// <summary>Screen setups and the window places in each. On this PC only: not exported.</summary>
  public interface IScreenSetupRepository {
    /// <summary>All setups, oldest added first.</summary>
    List<ScreenSetupRecord> GetAll();
    /// <summary>The setup with this key, or null.</summary>
    ScreenSetupRecord Get(string key);
    /// <summary>Adds the setup if needed and returns its ID.</summary>
    long Ensure(string key, string name);
    List<ScreenSetupPlace> GetPlaces(long setupID);
    /// <summary>Adds or replaces one item's place in a setup.</summary>
    void SavePlace(long setupID, ScreenSetupPlace place);
    /// <summary>Deletes every other setup and its places.</summary>
    void DeleteAllExcept(long setupID);
    /// <summary>Deletes places of windows that no longer exist. Returns how many.</summary>
    int DeletePlacesOfMissingItems();
    /// <summary>Sets the last-used time.</summary>
    void Touch(long setupID, string lastUsed);
    /// <summary>Changes the setup's key and name.</summary>
    void Rekey(long setupID, string key, string name);
    /// <summary>Deletes one setup and its places.</summary>
    void Delete(long setupID);
    /// <summary>Copies places from one setup to another, only for items the target lacks.</summary>
    void CopyMissingPlaces(long fromSetupID, long toSetupID);
  }
}
