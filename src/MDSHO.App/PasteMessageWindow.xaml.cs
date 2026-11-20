using System;
using System.Windows;

namespace MDSHO {

  /// <summary>A small MDSHO window that says why a clipboard button could not paste: an info icon, a bold heading, the text and OK.</summary>
  public partial class PasteMessageWindow : Window {

    /// <summary>Shows the built window (modal). Tests replace it so no window opens.</summary>
    public static Action<PasteMessageWindow> ShowWindow = window => window.ShowDialog();

    /// <summary>Creates the window.</summary>
    /// <param name="caption">The title, "MDSHO".</param>
    /// <param name="heading">The bold first line.</param>
    /// <param name="text">The text below it.</param>
    public PasteMessageWindow(string caption, string heading, string text) {
      InitializeComponent();
      this.Title = string.IsNullOrWhiteSpace(caption) ? "MDSHO" : caption;
#if DEBUG
      this.Title += " DEBUG";
#endif
      this.TextHeading.Text = heading ?? "";
      this.TextMessage.Text = text ?? "";
      this.ButtonOK.Click += (s, e) => Close();
      ViewModels.IconGlyphs icons = new ViewModels.IconGlyphs(true);
      this.TextIcon.Text = icons.Info;
      this.TitleBar.Glyph = icons.MDSHO;
      this.TitleBar.CloseGlyph = icons.Times;
    }
  }
}
