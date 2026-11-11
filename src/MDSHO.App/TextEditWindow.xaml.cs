using System;
using System.Windows;
using System.Windows.Input;
using MDSHO.Helpers;
using MDSHO.ViewModels;

namespace MDSHO {

  /// <summary>
  /// Edits the text of one button: its comment (any button or group) or, for a note button, the note itself. Nothing is
  /// saved until Save (or Ctrl+Enter); Cancel, the X button and Esc close it without saving. A text of only spaces and
  /// new lines is saved as empty.
  /// </summary>
  public partial class TextEditWindow : Window {
    /// <summary>What the window edits.</summary>
    public enum TextKind {
      /// <summary>The comment of a button or group (shown in its tooltip).</summary>
      Comment,
      /// <summary>The text of a note button.</summary>
      Note
    }

    private readonly ShortcutViewModel shortcut;
    private readonly TextKind kind;

    /// <summary>The keyboard modifiers held down; tests replace it, since they can't hold Ctrl.</summary>
    public static Func<ModifierKeys> KeyboardModifiers = () => Keyboard.Modifiers;

    /// <summary>Creates the window for the comment of a button or group.</summary>
    public static TextEditWindow ForComment(ShortcutViewModel shortcut) {
      return new TextEditWindow(shortcut, TextKind.Comment);
    }

    /// <summary>Creates the window for the text of a note button.</summary>
    public static TextEditWindow ForNote(ShortcutViewModel shortcut) {
      return new TextEditWindow(shortcut, TextKind.Note);
    }

    private TextEditWindow(ShortcutViewModel shortcut, TextKind kind) {
      InitializeComponent();
      // Custom title bar: ask Windows 11 for rounded corners, like the Run command window.
      Helpers.WindowCorners.UseRoundedCorners(this);
      this.shortcut = shortcut ?? throw new ArgumentNullException(nameof(shortcut));
      this.kind = kind;
      this.Title = (kind == TextKind.Note ? "Note: " : "Comment: ") + shortcut.Name;
      this.TextTitle.Text = this.Title;
      this.TextComment.Text = (kind == TextKind.Note ? shortcut.NoteText : shortcut.Comment) ?? "";
      System.Windows.Automation.AutomationProperties.SetName(this.TextComment, kind == TextKind.Note ? "Note" : "Comment");
      if (kind == TextKind.Note) {
        // A note is the main content, so its window is bigger.
        this.Width = 640;
        this.Height = 520;
      }

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

    /// <summary>Opens the comment window as a dialog, centred on the screen of the MDSHO window the button belongs to.</summary>
    public static void ShowCommentFor(ShortcutViewModel shortcut) {
      ShowDialogFor(() => ForComment(shortcut), shortcut);
    }

    /// <summary>Opens the note window as a dialog, centred on the screen of the MDSHO window the note belongs to.</summary>
    public static void ShowNoteFor(ShortcutViewModel shortcut) {
      ShowDialogFor(() => ForNote(shortcut), shortcut);
    }

    private static void ShowDialogFor(Func<TextEditWindow> create, ShortcutViewModel shortcut) {
      try {
        TextEditWindow window = create();
        Window owner = RunCommandWindow.FindOwner(shortcut);
        if (owner != null) {
          DialogPlacement.CenterOnScreenOf(window, owner);
        } else {
          window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }
        window.ShowDialog();
      } catch (Exception ex) {
        ErrorDialog.ShowDialog(ex);
      }
    }

    private void ShowIcons(IconGlyphs icons) {
      string glyph = this.kind == TextKind.Note ? icons.Rename : icons.Info;
      this.TextTitleIcon.Text = glyph;
      this.ButtonClose.Content = icons.Times;
      this.Icon = GlyphIcon.Create(glyph);
    }

    private void Save() {
      string text = this.TextComment.Text ?? "";
      string value = string.IsNullOrWhiteSpace(text) ? "" : text;
      if (this.kind == TextKind.Note) {
        this.shortcut.NoteText = value;
      } else {
        this.shortcut.Comment = value;
      }
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
