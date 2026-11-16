using System;
using System.Linq;
using System.Windows;
using System.Windows.Interop;
using MDSHO.Helpers;
using MDSHO.ViewModels;

namespace MDSHO {

  /// <summary>
  /// The one-time "Enjoying MDSHO?" prompt (see <see cref="ReviewPrompt"/>): Later or Rate MDSHO. Opens without taking
  /// the focus, beside the panel used last, or in the bottom-right corner of the primary screen.
  /// </summary>
  public partial class ReviewWindow : Window {

    /// <summary>The panel used last, or null. Tests replace it.</summary>
    public static Func<Window> LastUsedPanel = () => DesktopWindowView.LastUsed;

    /// <summary>Shows the placed window. Tests replace it so no window opens.</summary>
    public static Action<ReviewWindow> ShowWindow = window => window.Show();

    /// <summary>True while another MDSHO dialog is open (Settings, Run command, Colours, About, an error or a message box).</summary>
    public static Func<bool> IsDialogOpen = AnyDialogOpen;

    private readonly ReviewPrompt prompt;

    /// <summary>Creates the window.</summary>
    public ReviewWindow(ReviewPrompt prompt) {
      this.prompt = prompt ?? throw new ArgumentNullException(nameof(prompt));
      InitializeComponent();
#if DEBUG
      this.Title += " DEBUG";
#endif
      IconGlyphs icons = new IconGlyphs(true);
      this.TitleBar.Glyph = icons.Star;
      this.TitleBar.CloseGlyph = icons.Times;
      this.ButtonLater.Click += (s, e) => Close();
      // Closed first, so the Store's rating dialog opens over a panel that stays.
      this.ButtonRate.Click += (s, e) => {
        Close();
        _ = this.prompt.RateAsync();
      };
      // The body and bottom bar margins equal the button height, like Run command and Settings.
      this.ButtonRate.SizeChanged += (s, e) => {
        Thickness margin = new Thickness(this.ButtonRate.ActualHeight);
        this.BodyContent.Margin = margin;
        this.FooterContent.Margin = margin;
      };
    }

    /// <summary>Opens the prompt beside the panel used last, else bottom-right of the primary screen. Never throws.</summary>
    public static void ShowFor(ReviewPrompt prompt) {
      try {
        ReviewWindow window = new ReviewWindow(prompt);
        Window panel = LastUsedPanel();
        if (panel != null) {
          DialogPlacement.BesideWindow(window, panel, DesktopWindowView.DialogGap);
        } else {
          DialogPlacement.BottomRightOfPrimary(window, DesktopWindowView.DialogGap);
        }
        ShowWindow(window);
      } catch (Exception ex) {
        Common.AppLog.Warning(ex, "The review prompt could not be shown");
      }
    }

    // Any shown MDSHO window other than panels, the hidden main window, notes and this prompt, or a modal loop (message box).
    private static bool AnyDialogOpen() {
      if (ComponentDispatcher.IsThreadModal) {
        return true;
      }
      Application app = Application.Current;
      if (app == null) {
        return false;
      }
      return app.Windows.OfType<Window>().Any(w => w.IsVisible && !(w is MainWindow)
        && !(w is DesktopWindowView) && !(w is ReviewWindow) && !(w is TextEditWindow note && note.IsNote));
    }
  }
}
