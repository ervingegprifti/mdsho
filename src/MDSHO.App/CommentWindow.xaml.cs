using System;
using System.Windows;
using System.Windows.Input;
using MDSHO.Helpers;
using MDSHO.ViewModels;

namespace MDSHO {

  /// <summary>
  /// The Comment window: edits the comment of one button or group. Nothing is saved until Save (or Ctrl+Enter); Cancel,
  /// the X button and Esc close it without saving. A comment of only spaces and new lines is saved as no comment.
  /// </summary>
  public partial class CommentWindow : Window {
    private readonly ShortcutViewModel shortcut;

    /// <summary>The keyboard modifiers held down; tests replace it, since they can't hold Ctrl.</summary>
    public static Func<ModifierKeys> KeyboardModifiers = () => Keyboard.Modifiers;

    /// <summary>Creates the window for a button or group.</summary>
    public CommentWindow(ShortcutViewModel shortcut) {
      InitializeComponent();
      // Custom title bar: ask Windows 11 for rounded corners, like the Run command window.
      Helpers.WindowCorners.UseRoundedCorners(this);
      this.shortcut = shortcut ?? throw new ArgumentNullException(nameof(shortcut));
      this.Title = "Comment: " + shortcut.Name;
      this.TextTitle.Text = this.Title;
      this.TextComment.Text = shortcut.Comment ?? "";

      // The title icon, the close button and the window icon follow the window's solid or outline setting while open.
      IconGlyphs icons = shortcut.OwningWindow?.Appearance?.Icons ?? new IconGlyphs(true);
      ShowIcons(icons);
      System.ComponentModel.PropertyChangedEventHandler iconsChanged = (s, e) => ShowIcons(icons);
      icons.PropertyChanged += iconsChanged;
      this.Closed += (s, e) => icons.PropertyChanged -= iconsChanged;

      this.ButtonClose.Click += (s, e) => Close();
      this.ButtonCancel.Click += (s, e) => Close();
      this.ButtonSave.Click += (s, e) => Save();
      this.PreviewKeyDown += Window_PreviewKeyDown;
      this.Loaded += (s, e) => {
        this.TextComment.Focus();
        this.TextComment.CaretIndex = this.TextComment.Text.Length;
      };
    }

    /// <summary>True when the user saved; false after Cancel, X or Esc.</summary>
    public bool Saved { get; private set; }

    /// <summary>Opens the window as a dialog for a button or group, centred on the screen of the MDSHO window it belongs to.</summary>
    public static void ShowFor(ShortcutViewModel shortcut) {
      try {
        Create(shortcut, RunCommandWindow.FindOwner(shortcut)).ShowDialog();
      } catch (Exception ex) {
        ErrorDialog.ShowDialog(ex);
      }
    }

    /// <summary>Creates the window centred on the screen of <paramref name="owner"/> (or the middle of the screen), without showing it.</summary>
    public static CommentWindow Create(ShortcutViewModel shortcut, Window owner) {
      CommentWindow window = new CommentWindow(shortcut);
      if (owner != null) {
        DialogPlacement.CenterOnScreenOf(window, owner);
      } else {
        window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
      }
      return window;
    }

    private void ShowIcons(IconGlyphs icons) {
      this.TextTitleIcon.Text = icons.Info;
      this.ButtonClose.Content = icons.Times;
      this.Icon = GlyphIcon.Create(icons.Info);
    }

    private void Save() {
      string text = this.TextComment.Text ?? "";
      this.shortcut.Comment = string.IsNullOrWhiteSpace(text) ? "" : text;
      this.Saved = true;
      Close();
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e) {
      if (e.Key == Key.Escape) {
        e.Handled = true;
        Close();
      } else if (e.Key == Key.Enter && (KeyboardModifiers() & ModifierKeys.Control) == ModifierKeys.Control) {
        e.Handled = true;
        Save();
      }
    }
  }
}
