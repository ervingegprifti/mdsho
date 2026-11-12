using System.Windows;
using System.Windows.Controls;

namespace MDSHO.Controls {

  /// <summary>
  /// MDSHO's own title bar for the windows that used to have the Windows one (Settings, About, Error), so they can have
  /// the rounded frame: the window's icon and title on the left and a close button on the right. The window gives it a
  /// WindowChrome with a 32 px caption, so the bar drags the window.
  /// </summary>
  public partial class DialogTitleBar : UserControl {
    /// <summary>The icon glyph shown before the title.</summary>
    public static readonly DependencyProperty GlyphProperty = DependencyProperty.Register(nameof(Glyph), typeof(string), typeof(DialogTitleBar));
    /// <summary>The glyph of the close button.</summary>
    public static readonly DependencyProperty CloseGlyphProperty = DependencyProperty.Register(nameof(CloseGlyph), typeof(string), typeof(DialogTitleBar));
    /// <summary>The title text.</summary>
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(nameof(Text), typeof(string), typeof(DialogTitleBar));

    /// <summary>The icon glyph shown before the title.</summary>
    public string Glyph { get { return (string)GetValue(GlyphProperty); } set { SetValue(GlyphProperty, value); } }
    /// <summary>The glyph of the close button.</summary>
    public string CloseGlyph { get { return (string)GetValue(CloseGlyphProperty); } set { SetValue(CloseGlyphProperty, value); } }
    /// <summary>The title text.</summary>
    public string Text { get { return (string)GetValue(TextProperty); } set { SetValue(TextProperty, value); } }

    /// <summary>Creates the title bar; its close button closes the window it is in.</summary>
    public DialogTitleBar() {
      InitializeComponent();
      this.ButtonClose.Click += (s, e) => Window.GetWindow(this)?.Close();
    }
  }
}
