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
    /// <summary>1 when the button is a note (its text is <see cref="NoteText"/>), otherwise 0 (also 0 when an older export file has no value).</summary>
    public long IsNote { get; set; }
    /// <summary>The text of a note button; empty for other buttons (also empty when an older export file has no value).</summary>
    public string NoteText { get; set; } = "";
    /// <summary>Border colour of the note's window (#AARRGGBB); empty = the default colour. Also empty in older export files.</summary>
    public string NoteBorderColor { get; set; } = "";
    /// <summary>Title bar background colour of the note's window; empty = the default colour.</summary>
    public string NoteTitleBackgroundColor { get; set; } = "";
    /// <summary>Title text colour of the note's window; empty = the default colour.</summary>
    public string NoteTitleTextColor { get; set; } = "";
    /// <summary>Background colour of the note's window; empty = the default colour.</summary>
    public string NoteBackgroundColor { get; set; } = "";
    /// <summary>Text colour of the note's window; empty = the default colour.</summary>
    public string NoteTextColor { get; set; } = "";
    /// <summary>1 when the button is a snippet (its text is <see cref="SnippetText"/>), otherwise 0 (also 0 when an older export file has no value).</summary>
    public long IsSnippet { get; set; }
    /// <summary>The text a snippet pastes; empty for other buttons (also empty when an older export file has no value).</summary>
    public string SnippetText { get; set; } = "";
    /// <summary>The font of a note's or snippet's text (see <see cref="NoteFonts"/>); Courier New also when an older export file has no value.</summary>
    public string TextFont { get; set; } = NoteFonts.Default;
    /// <summary>The font of the comment's text (Courier New by default).</summary>
    public string CommentFont { get; set; } = NoteFonts.Default;
    /// <summary>The size of the comment's text in whole points; 0 = 12 pt.</summary>
    public long CommentFontSize { get; set; }
    /// <summary>The size of a snippet's text in whole points; 0 = 12 pt (a note keeps its size with its window).</summary>
    public long TextFontSize { get; set; }
    /// <summary>A note's saved colour highlights: terms and colours as JSON, "" for none (see NoteHighlights in MDSHO.Common).</summary>
    public string NoteHighlights { get; set; } = "";
    /// <summary>1 when the note's or snippet's window shades the caret's line (on for new items and older export files).</summary>
    public long TextCurrentLine { get; set; } = 1;
    /// <summary>1 when the note's or snippet's highlights match whole words only.</summary>
    public long TextWholeWords { get; set; }
    /// <summary>1 when the note's or snippet's highlights match the case.</summary>
    public long TextMatchCase { get; set; }
    /// <summary>1 when the snippet's text wraps (a note keeps its wrap with its window place).</summary>
    public long TextWrapText { get; set; }
    /// <summary>1 when the Comment window shades the caret's line (on for new items and older export files).</summary>
    public long CommentCurrentLine { get; set; } = 1;
    /// <summary>1 when the comment's highlights match whole words only.</summary>
    public long CommentWholeWords { get; set; }
    /// <summary>1 when the comment's highlights match the case.</summary>
    public long CommentMatchCase { get; set; }
    /// <summary>1 when the comment's text wraps.</summary>
    public long CommentWrapText { get; set; }
    /// <summary>
    /// Children shortcuts.
    /// </summary>
    public List<Shortcut> Shortcuts { get; set; } = new List<Shortcut>();
    #endregion PROPERTIES

    /// <summary>Parameterless constructor for the data layer (row mapping) and serialization.</summary>
    public Shortcut() { }

    /// <summary>Creates a shortcut record (the first overload gets a new ID from the database, the second keeps the given ID).</summary>
    public Shortcut(long? parentShortcutID, long? parentWindowID, string name, string target, string ext, string guid, long isGroup, long isGroupExpanded, string comment, long isMarked, string markColor, long position, long isProcessCommand, string processFileName, string processArguments, long processRunAsAdmin = 0, string processWorkingFolder = "", long isNote = 0, string noteText = "") :
      this (id: 0, parentShortcutID, parentWindowID, name, target, ext, guid, isGroup, isGroupExpanded, comment, isMarked, markColor, position, isProcessCommand, processFileName, processArguments, processRunAsAdmin, processWorkingFolder, isNote, noteText) { }

    /// <summary>Creates a shortcut record (the first overload gets a new ID from the database, the second keeps the given ID).</summary>
    public Shortcut(long id, long? parentShortcutID, long? parentWindowID, string name, string target, string ext, string guid, long isGroup, long isGroupExpanded, string comment, long isMarked, string markColor, long position, long isProcessCommand, string processFileName, string processArguments, long processRunAsAdmin = 0, string processWorkingFolder = "", long isNote = 0, string noteText = "") {
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
      this.IsNote = isNote;
      this.NoteText = noteText ?? "";
    }
  }

}