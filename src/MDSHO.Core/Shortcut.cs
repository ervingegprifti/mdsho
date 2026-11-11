using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MDSHO.Core {

  /// <summary>
  /// Represents an Shortcut database Shortcuts table record.
  /// </summary>
  public class Shortcut {
    #region PROPERTIES
    /// <summary>Database ID (primary key).</summary>
    public long ID { get; set; }
    /// <summary>
    /// The parent Shortcut ID of this Shortcut. If NULL then this is a Root Shortcut.
    /// </summary>
    public long? ParentShortcutID { get; set; }
    /// <summary>
    /// The window this Shortcut sits in directly (a root shortcut), otherwise NULL (database column ParentMDSHOWindowID, also the name used in export files).
    /// </summary>
    [JsonPropertyName("parentMDSHOWindowID")]
    public long? ParentWindowID { get; set; }
    /// <summary>The name shown for the shortcut.</summary>
    public string Name { get; set; }
    /// <summary>The file, folder, web address or command the shortcut opens.</summary>
    public string Target { get; set; }
    /// <summary>Extension of the shortcut file in the shortcuts folder (.lnk or .url).</summary>
    public string Ext { get; set; }
    /// <summary>Unique name (without extension) of the shortcut file in the shortcuts folder.</summary>
    public string Guid { get; set; }
    /// <summary>1 for a group (folder of shortcuts), otherwise 0.</summary>
    public long IsGroup { get; set; }
    /// <summary>1 when the group is open in the tree.</summary>
    public long IsGroupExpanded { get; set; }
    /// <summary>The comment on the shortcut, shown in its tooltip and edited in the Comment window.</summary>
    public string Comment { get; set; }
    /// <summary>1 when the shortcut is marked with a colour.</summary>
    public long IsMarked { get; set; }
    /// <summary>The mark colour as #AARRGGBB text.</summary>
    public string MarkColor { get; set; }
    /// <summary>Order among the siblings, counting from 0.</summary>
    public long Position { get; set; }
    /// <summary>1 when the shortcut runs a command instead of opening a file.</summary>
    public long IsProcessCommand { get; set; }
    /// <summary>Program to run for a command shortcut.</summary>
    public string ProcessFileName { get; set; }
    /// <summary>Arguments for the program of a command shortcut.</summary>
    public string ProcessArguments { get; set; }
    /// <summary>1 when the run command starts as administrator, otherwise 0 (also 0 when an older export file has no value).</summary>
    public long ProcessRunAsAdmin { get; set; }
    /// <summary>Folder the run command starts in; empty = none (also empty when an older export file has no value).</summary>
    public string ProcessWorkingFolder { get; set; } = "";
    /// <summary>
    /// Children shortcuts.
    /// </summary>
    public List<Shortcut> Shortcuts { get; set; } = new List<Shortcut>();
    #endregion PROPERTIES

    /// <summary>Parameterless constructor for the data layer (row mapping) and serialization.</summary>
    public Shortcut() { }

    /// <summary>Creates a shortcut record (the first overload gets a new ID from the database, the second keeps the given ID).</summary>
    public Shortcut(long? parentShortcutID, long? parentWindowID, string name, string target, string ext, string guid, long isGroup, long isGroupExpanded, string comment, long isMarked, string markColor, long position, long isProcessCommand, string processFileName, string processArguments, long processRunAsAdmin = 0, string processWorkingFolder = "") :
      this (id: 0, parentShortcutID, parentWindowID, name, target, ext, guid, isGroup, isGroupExpanded, comment, isMarked, markColor, position, isProcessCommand, processFileName, processArguments, processRunAsAdmin, processWorkingFolder) { }

    /// <summary>Creates a shortcut record (the first overload gets a new ID from the database, the second keeps the given ID).</summary>
    public Shortcut(long id, long? parentShortcutID, long? parentWindowID, string name, string target, string ext, string guid, long isGroup, long isGroupExpanded, string comment, long isMarked, string markColor, long position, long isProcessCommand, string processFileName, string processArguments, long processRunAsAdmin = 0, string processWorkingFolder = "") {
      this.ID = id;
      this.ParentShortcutID = parentShortcutID;
      this.ParentWindowID = parentWindowID;
      this.Name = name;
      this.Target = target;
      this.Ext = ext;
      this.Guid = guid;
      this.IsGroup = isGroup;
      this.IsGroupExpanded = isGroupExpanded;
      this.Comment = comment;
      this.IsMarked = isMarked;
      this.MarkColor = markColor;
      this.Position = position;
      this.IsProcessCommand = isProcessCommand;
      this.ProcessFileName = processFileName;
      this.ProcessArguments = processArguments;
      this.ProcessRunAsAdmin = processRunAsAdmin;
      this.ProcessWorkingFolder = processWorkingFolder;
    }
  }

}