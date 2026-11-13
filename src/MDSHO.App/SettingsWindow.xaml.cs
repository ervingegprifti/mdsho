using System.Windows;
using MDSHO.Helpers;
using MDSHO.ViewModels;

namespace MDSHO {

  /// <summary>
  /// The Settings window of a panel (the gear in its title bar): Icons appearance and Other. Changes show live; Apply keeps
  /// them, closing without Apply puts them back. The panel's colours are in the <see cref="ColorsWindow"/>.
  /// </summary>
  public partial class SettingsWindow : Window {
    private readonly IconGlyphs icons;
    private bool closeWithoutApply = true;

    /// <summary>Creates the Settings window for a panel window and remembers the current settings so closing without Apply restores them.</summary>
    public SettingsWindow(DesktopWindowViewModel desktopWindow) {
      InitializeComponent();
      DesktopWindowAppearance a = desktopWindow.Appearance;
      this.Title = "Settings: " + a.Title;
#if DEBUG
      this.Title += " DEBUG";
#endif
      this.DataContext = desktopWindow;
      // Every outer margin of the content and of the Apply bar equals the Apply button height, like in the Note window.
      this.ButtonApply.SizeChanged += (s, e) => {
        Thickness margin = new Thickness(this.ButtonApply.ActualHeight);
        this.PanelBody.Margin = margin;
        this.FooterContent.Margin = margin;
      };
      // The window icon and the title bar's gear follow the window's Solid icons setting, also while it is changed here.
      this.icons = a.Icons;
      ShowIcons();
      System.ComponentModel.PropertyChangedEventHandler iconsChanged = (s, e) => ShowIcons();
      this.icons.PropertyChanged += iconsChanged;
      this.Closed += (s, e) => this.icons.PropertyChanged -= iconsChanged;

      // Solid icons, Single click to open and Show version in title save themselves when they change; closing without
      // Apply puts them back (which saves the old values again).
      (bool solidIcons, bool singleClickToOpen, bool showVersionInTitle) old = (a.SolidIcons, a.SingleClickToOpen, a.ShowVersionInTitle);
      this.Closing += (s, e) => {
        if (this.closeWithoutApply) {
          a.SolidIcons = old.solidIcons;
          a.SingleClickToOpen = old.singleClickToOpen;
          a.ShowVersionInTitle = old.showVersionInTitle;
        }
      };
      this.ButtonApply.Click += (s, e) => {
        this.closeWithoutApply = false;
        Close();
      };
    }

    // The window icon and the title bar's icon (the gear) and close glyph, in the window's icon set.
    private void ShowIcons() {
      this.Icon = GlyphIcon.Create(this.icons.Settings);
      this.TitleBar.Glyph = this.icons.Settings;
      this.TitleBar.CloseGlyph = this.icons.Times;
    }
  }
}
