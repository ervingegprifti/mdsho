using CommunityToolkit.Mvvm.ComponentModel;

namespace MDSHO.ViewModels {

  /// <summary>
  /// Represents the common info ViewModel of an DesktopWindow or an MDSHOClone object.
  /// </summary>
  public partial class DesktopWindowAppearance : ObservableObject {
    private DesktopWindowViewModel parentWindow = null;
    private long parentWindowID;
    /// <summary>Window title.</summary>
    [ObservableProperty]
    private string title;
    /// <summary>Border colour ("#AARRGGBB")</summary>
    [ObservableProperty]
    private string borderColor;
    /// <summary>Window background colour ("#AARRGGBB")</summary>
    [ObservableProperty]
    private string backgroundColor;
    /// <summary>Shortcut text colour ("#AARRGGBB")</summary>
    [ObservableProperty]
    private string textColor;
    /// <summary>Header background colour ("#AARRGGBB")</summary>
    [ObservableProperty]
    private string titleBackgroundColor;
    /// <summary>Header text colour ("#AARRGGBB")</summary>
    [ObservableProperty]
    private string titleTextColor;
    /// <summary>Free notes about the window.</summary>
    [ObservableProperty]
    private string notes;
    /// <summary>True to draw icons filled; changing it also switches the icon glyphs.</summary>
    [ObservableProperty]
    private bool solidIcons;
    /// <summary>True when one click opens a shortcut.</summary>
    [ObservableProperty]
    private bool singleClickToOpen;

    #region PROPERTIES
    /// <summary>The application view model this belongs to.</summary>
    public AppViewModel AppViewModel { get; private set; }
    /// <summary>The real window these settings belong to.</summary>
    public DesktopWindowViewModel ParentWindow {
      get {
        if (this.parentWindow == null) {
          foreach(DesktopWindowViewModel desktopWindow in this.AppViewModel.Windows) {
            if (desktopWindow.ID == this.parentWindowID) {
              parentWindow = desktopWindow;
            }
          }
        }
        return this.parentWindow;
      }
    }
    /// <summary>Database ID of that window.</summary>
    public long ParentWindowID { get { return parentWindowID; } set { parentWindowID = value; } }
    // The glyphs switch before PropertyChanged(SolidIcons) is raised, as before.
    partial void OnSolidIconsChanged(bool value) {
      this.Icons.SolidIcons = value;
    }
    /// <summary>The icon glyphs matching the Solid icons choice.</summary>
    public IconGlyphs Icons { get; }
    #endregion PROPERTIES


    /// <summary>Creates the settings from the stored values.</summary>
    public DesktopWindowAppearance(AppViewModel appViewModel, long parentWindowID, string title, string borderColor, string backgroundColor, string textColor, string titleBackgroundColor, string titleTextColor, string notes, bool solidIcons, bool singleClickToOpen) {
      this.AppViewModel = appViewModel;
      this.parentWindowID = parentWindowID;
      this.title = title;
      this.borderColor = borderColor;
      this.backgroundColor = backgroundColor;
      this.textColor = textColor;
      this.titleBackgroundColor = titleBackgroundColor;
      this.titleTextColor = titleTextColor;
      this.notes = notes;
      this.solidIcons = solidIcons;
      this.singleClickToOpen = singleClickToOpen;
      this.Icons = new IconGlyphs(this.solidIcons);
    }
  }

}