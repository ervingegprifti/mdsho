using System.Collections.Generic;

namespace MDSHO.Core {

  /// <summary>A screen setup MDSHO has seen on this PC (a row of the ScreenSetups table).</summary>
  public sealed class ScreenSetupRecord {
    /// <summary>Database ID.</summary>
    public long ID { get; set; }
    /// <summary>Which screens make the setup: device names and bounds (see MDSHO.Common.ScreenSetup.Key).</summary>
    public string Key { get; set; }
    /// <summary>The name shown in the tray menu, for example "2 screens: 1920×1080, 2560×1440".</summary>
    public string Name { get; set; }
    /// <summary>When the setup was last the current one, as UTC round-trip text (sorts by time); empty when never recorded.</summary>
    public string LastUsed { get; set; }
  }

  /// <summary>Where a panel, clone or note window was last in one screen setup (a row of the ScreenSetupPlaces table).</summary>
  public sealed class ScreenSetupPlace {
    /// <summary>0 = panel, 1 = clone, 2 = note window (MDSHO.Common.PlacedKind).</summary>
    public long Kind { get; set; }
    /// <summary>The panel's MDSHOWindows.ID, the clone's MDSHOWindowClones.ID or the note's Shortcuts.ID.</summary>
    public long ItemID { get; set; }
    /// <summary>The screen it was mostly on.</summary>
    public string ScreenDeviceName { get; set; }
    /// <summary>Left edge (DIPs).</summary>
    public long Left { get; set; }
    /// <summary>Top edge (DIPs).</summary>
    public long Top { get; set; }
    /// <summary>Width (DIPs).</summary>
    public long Width { get; set; }
    /// <summary>Height (DIPs).</summary>
    public long Height { get; set; }
    /// <summary>0 = not docked, 1 = docked left, 2 = docked right (MDSHO.Common.DockSide).</summary>
    public long Dock { get; set; }
  }

  /// <summary>
  /// The screen setups seen on this PC and, for each, the place of every panel, clone and note window. Not part of exports,
  /// and an import that replaces the data keeps them.
  /// </summary>
  public interface IScreenSetupRepository {
    /// <summary>Every known setup, in the order they were added.</summary>
    List<ScreenSetupRecord> GetAll();
    /// <summary>The setup with this key, or null when it isn't known.</summary>
    ScreenSetupRecord Get(string key);
    /// <summary>Adds the setup if it isn't known yet and returns its ID.</summary>
    long Ensure(string key, string name);
    /// <summary>The saved places of one setup.</summary>
    List<ScreenSetupPlace> GetPlaces(long setupID);
    /// <summary>Saves the place of one item in one setup (adds the row the first time, replaces it after that).</summary>
    void SavePlace(long setupID, ScreenSetupPlace place);
    /// <summary>Forgets every setup but this one, with their places.</summary>
    void DeleteAllExcept(long setupID);
    /// <summary>Deletes the places of panels, clones and notes that no longer exist; returns how many.</summary>
    int DeletePlacesOfMissingItems();
    /// <summary>Records when the setup was last the current one.</summary>
    void Touch(long setupID, string lastUsed);
    /// <summary>Gives the setup a new key and name (when merging setups under a new key).</summary>
    void Rekey(long setupID, string key, string name);
    /// <summary>Forgets one setup, with its places.</summary>
    void Delete(long setupID);
    /// <summary>Copies to one setup the places of another for the items it has no place for.</summary>
    void CopyMissingPlaces(long fromSetupID, long toSetupID);
  }
}
