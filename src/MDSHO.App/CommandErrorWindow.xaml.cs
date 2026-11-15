using System;
using System.Windows;

namespace MDSHO {

  /// <summary>A small MDSHO window that shows an error in red, a line below it and OK, e.g. when a run command did not run.</summary>
  public partial class CommandErrorWindow : Window {

    /// <summary>Shows the built window (modal). Tests replace it so no window opens.</summary>
    public static Action<CommandErrorWindow> ShowWindow = window => window.ShowDialog();

    /// <summary>Creates the window.</summary>
    /// <param name="title">The title, e.g. the command's name.</param>
    /// <param name="error">The error, shown in red.</param>
    /// <param name="detail">The line below the error.</param>
    public CommandErrorWindow(string title, string error, string detail) {
      InitializeComponent();
      this.Title = string.IsNullOrWhiteSpace(title) ? "MDSHO" : title;
#if DEBUG
      this.Title += " DEBUG";
#endif
      this.TextError.Text = error ?? "";
      this.TextDetail.Text = detail ?? "";
      this.ButtonOK.Click += (s, e) => Close();
      ViewModels.IconGlyphs icons = new ViewModels.IconGlyphs(true);
      this.TitleBar.Glyph = icons.Warning;
      this.TitleBar.CloseGlyph = icons.Times;
    }
  }
}
