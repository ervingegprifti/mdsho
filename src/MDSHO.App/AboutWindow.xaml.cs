using System;
using System.Reflection;
using System.Windows;

namespace MDSHO {
  /// <summary>
  /// Interaction logic for AboutWindow.xaml
  /// </summary>
  public partial class AboutWindow : Window {
    /// <summary>The outer margin around the content (26 px): the button height the other windows use as their margin, though About has no buttons.</summary>
    public const double OuterMargin = 26;

    /// <summary>Icon glyphs for the Rate / Share links: always solid, the About window has no per-window Solid icons setting.</summary>
    public static ViewModels.IconGlyphs Icons { get; } = new ViewModels.IconGlyphs(true);

    private readonly Common.StoreActions storeActions;
    private readonly Common.IShellLauncher shell;

    /// <summary>Creates the About dialog over the current app services.</summary>
    public AboutWindow() : this(Helpers.AppServices.Current.StoreActions, Helpers.AppServices.Current.Shell) { }

    /// <summary>
    /// Draws the window icon (the info glyph) from <paramref name="icons"/>: the set of the window whose title menu opened
    /// About, or the first window's set when it was opened from the tray. Until then the icon is solid.
    /// </summary>
    public void UseIcons(ViewModels.IconGlyphs icons) {
      ViewModels.IconGlyphs set = icons ?? new ViewModels.IconGlyphs(true);
      this.Icon = Helpers.GlyphIcon.Create(set.Info);
      this.TitleBar.Glyph = set.Info;
      this.TitleBar.CloseGlyph = set.Times;
    }

    /// <summary>Creates the About dialog and fills in the version number.</summary>
    public AboutWindow(Common.StoreActions storeActions, Common.IShellLauncher shell) {
      this.storeActions = storeActions;
      this.shell = shell;
      InitializeComponent();
      // Opens just tall enough to show every third-party notice without scrolling, but never taller than the screen.
      // After that the size is the user's: the window can be resized, and a smaller window scrolls the notices.
      this.MaxHeight = SystemParameters.WorkArea.Height;
      this.ContentRendered += (s, e) => {
        // Rounded up by two pixels: layout rounding can leave the list a fraction taller than its area, which would show
        // a scroll bar with nothing to scroll.
        double height = Math.Min(Math.Ceiling(this.ActualHeight) + 2, SystemParameters.WorkArea.Height);
        this.SizeToContent = SizeToContent.Manual;
        this.MaxHeight = double.PositiveInfinity;
        this.Height = height;
      };
      SetTitle();
      UseIcons(null);

      try {
        string version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
        LabelVersion.Content = version;
      } catch (Exception ex) {
        ErrorDialog.ShowDialog(ex);
      }

      // Link addresses live in one place (AppConstants).
      HyperlinkProject.NavigateUri = new Uri(Common.AppConstants.ProjectUrl);
      HyperlinkPrivacyPolicy.NavigateUri = new Uri(Common.AppConstants.PrivacyPolicyUrl);
      HyperlinkTermsAndConditions.NavigateUri = new Uri(Common.AppConstants.TermsUrl);

      // Events
      HyperlinkProject.RequestNavigate += Hyperlink_RequestNavigate;
      HyperlinkPrivacyPolicy.RequestNavigate += Hyperlink_RequestNavigate;
      HyperlinkTermsAndConditions.RequestNavigate += Hyperlink_RequestNavigate;
      HyperlinkRate.Click += (s, e) => _ = this.storeActions.RateAsync();
      HyperlinkShare.Click += (s, e) => _ = this.storeActions.ShareAsync();
      // Third-party notice links are wired in the ItemsControl template (AboutWindow.xaml).
    }

    private void SetTitle() {
#if DEBUG
      this.Title += " DEBUG";
#endif
    }

    private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e) {
      try {
        // https://github.com/dotnet/runtime/issues/17938
        this.shell.OpenUrl(e.Uri.AbsoluteUri);
        e.Handled = true;
      } catch (Exception ex) {
        ErrorDialog.ShowDialog(ex);
      }
    }
  }
}
