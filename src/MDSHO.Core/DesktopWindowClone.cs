using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MDSHO.Core {

  /// <summary>
  /// Represents an DesktopWindowClone database MDSHOWindowClones table record.
  /// </summary>
  public class DesktopWindowClone {
    #region PROPERTIES
    /// <summary>Database ID (primary key).</summary>
    public long ID { get; set; }
    /// <summary>
    /// The ID of the window this clone belongs to (database column ParentMDSHOWindowID, also the name used in export files).
    /// </summary>
    [JsonPropertyName("parentMDSHOWindowID")]
    public long ParentWindowID { get; set; }
    /// <summary>Name of the screen the window was last on.</summary>
    public string ScreenDeviceName { get; set; }
    /// <summary>Distance of the window's left edge from the left of the virtual screen.</summary>
    public long Left { get; set; }
    /// <summary>Distance of the window's top edge from the top of the virtual screen.</summary>
    public long Top { get; set; }
    /// <summary>Window width.</summary>
    public long Width { get; set; }
    /// <summary>Window height.</summary>
    public long Height { get; set; }
    /// <summary>The shortcuts shown by the clone: the same list as the window's root shortcuts.</summary>
    public List<Shortcut> Shortcuts { get; set; } = new List<Shortcut>();
    #endregion PROPERTIES

    /// <summary>Parameterless constructor for the data layer (row mapping) and serialization.</summary>
    public DesktopWindowClone() { }

    /// <summary>Creates a clone record (the first overload gets a new ID from the database, the second keeps the given ID).</summary>
    public DesktopWindowClone(long parentWindowID, string screenDeviceName, long left, long top, long width, long height) :
      this (id: 0, parentWindowID, screenDeviceName, left, top, width, height) { }

    /// <summary>Creates a clone record (the first overload gets a new ID from the database, the second keeps the given ID).</summary>
    public DesktopWindowClone(long id, long parentWindowID, string screenDeviceName, long left, long top, long width, long height) {
      this.ID = id;
      this.ParentWindowID = parentWindowID;
      this.ScreenDeviceName = screenDeviceName;
      this.Left = left;
      this.Top = top;
      this.Width = width;
      this.Height = height;
    }
  }

}