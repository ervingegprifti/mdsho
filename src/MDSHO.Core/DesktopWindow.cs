using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MDSHO.Core {

  /// <summary>
  /// Represents an DesktopWindow database MDSHOWindows table record.
  /// </summary>
  public class DesktopWindow {
    #region PROPERTIES
    /// <summary>Database ID (primary key).</summary>
    public long ID { get; set; }
    /// <summary>The title shown in the window header.</summary>
    public string Title { get; set; }
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
    /// <summary>Border colour as #AARRGGBB text.</summary>
    public string BorderColor { get; set; }
    /// <summary>Window background colour as #AARRGGBB text.</summary>
    public string BackgroundColor { get; set; }
    /// <summary>Shortcut text colour as #AARRGGBB text.</summary>
    public string TextColor { get; set; }
    /// <summary>Header background colour as #AARRGGBB text.</summary>
    public string TitleBackgroundColor { get; set; }
    /// <summary>Header text colour as #AARRGGBB text.</summary>
    public string TitleTextColor { get; set; }
    /// <summary>Free notes about the window.</summary>
    public string Notes { get; set; }
    /// <summary>True to draw the icons filled, false for outlines.</summary>
    public bool SolidIcons { get; set; }
    /// <summary>True when one click opens a shortcut, false when it takes a double click.</summary>
    public bool SingleClickToOpen { get; set; }
    /// <summary>True when the Comment windows opened from this window wrap their text (off for new windows).</summary>
    public bool CommentWrapText { get; set; }
    /// <summary>True when this window and its clones, docked to a screen side, reserve that strip of the screen (off for new windows).</summary>
    public bool ReserveDockedSpace { get; set; }
    /// <summary>True when the title bar shows the app version after the title (on for new windows and older exports).</summary>
    public bool ShowVersionInTitle { get; set; } = true;
    /// <summary>
    /// Children RootShortcuts.
    /// </summary>
    public List<Shortcut> RootShortcuts { get; set; } = new List<Shortcut>();
    /// <summary>
    /// Children clones. Not a column and not part of export files (the clones are exported separately).
    /// </summary>
    [JsonIgnore]
    public List<DesktopWindowClone> Clones { get; set; } = new List<DesktopWindowClone>();
    #endregion PROPERTIES

    /// <summary>Parameterless constructor for the data layer (row mapping) and serialization.</summary>
    public DesktopWindow() { }

    /// <summary>Creates a window record (the first overload gets a new ID from the database, the second keeps the given ID).</summary>
    public DesktopWindow(
      string title,
      string screenDeviceName,
      long left,
      long top,
      long width,
      long height,
      bool solidIcons,
      string borderColor,
      string backgroundColor,
      string textColor,
      string titleBackgroundColor,
      string titleTextColor,
      string notes,
      bool singleClickToOpen) :
      this(
        id: 0,
        title,
        screenDeviceName,
        left,
        top,
        width,
        height,
        borderColor,
        backgroundColor,
        textColor,
        titleBackgroundColor,
        titleTextColor,
        notes,
        solidIcons,
        singleClickToOpen) { }

    /// <summary>Creates a window record (the first overload gets a new ID from the database, the second keeps the given ID).</summary>
    public DesktopWindow(
      long id,
      string title,
      string screenDeviceName,
      long left,
      long top,
      long width,
      long height,
      string borderColor,
      string backgroundColor,
      string textColor,
      string titleBackgroundColor,
      string titleTextColor,
      string notes,
      bool solidIcons,
      bool singleClickToOpen) {
      this.ID = id;
      this.Title = title;
      this.ScreenDeviceName = screenDeviceName;
      this.Left = left;
      this.Top = top;
      this.Width = width;
      this.Height = height;
      this.BorderColor = borderColor;
      this.BackgroundColor = backgroundColor;
      this.TextColor = textColor;
      this.TitleBackgroundColor = titleBackgroundColor;
      this.TitleTextColor = titleTextColor;
      this.Notes = notes;
      this.SolidIcons = solidIcons;
      this.SingleClickToOpen = singleClickToOpen;
    }
  }

}