using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using MDSHO.Common;
using MDSHO.Core;
using MDSHO.Data;
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
      // Just the button's name: the icon already says whether it is a note or a comment.
      this.Title = shortcut.Name;
      this.TextTitle.Text = this.Title;
      this.TextComment.Text = (kind == TextKind.Note ? shortcut.NoteText : shortcut.Comment) ?? "";
      System.Windows.Automation.AutomationProperties.SetName(this.TextComment, kind == TextKind.Note ? "Note" : "Comment");
      if (kind == TextKind.Note) {
        // A note is the main content, so its window is bigger; a comment keeps the smaller size.
        this.Width = 640;
        this.Height = 520;
      }
      UseEditorLayout();
      if (kind == TextKind.Note) {
        UseNoteTitleBar();
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
      this.ButtonTitleSave.Click += (s, e) => SaveNoteKeepOpen();
      this.ButtonPin.Click += (s, e) => {
        SetPinned(!this.Topmost);
        SaveNotePlace();
      };
      this.PreviewKeyDown += Window_PreviewKeyDown;
      this.Loaded += (s, e) => {
        this.TextComment.Focus();
        this.TextComment.CaretIndex = this.TextComment.Text.Length;
      };
    }

    /// <summary>True when the user saved; false after Cancel, X or Esc.</summary>
    public bool Saved { get; private set; }

    /// <summary>The button whose comment or note this window edits.</summary>
    public ShortcutViewModel Shortcut => this.shortcut;

    private static readonly List<TextEditWindow> openNoteWindows = new List<TextEditWindow>();

    /// <summary>The note windows open now. Several notes can be open at once, but each note in one window only.</summary>
    public static IReadOnlyCollection<TextEditWindow> OpenNoteWindows => openNoteWindows.AsReadOnly();

    /// <summary>Places a new note window before it shows: centred on the screen of the MDSHO window the note belongs to. Tests replace it.</summary>
    public static Action<TextEditWindow> PlaceNoteWindow = window => {
      Window owner = RunCommandWindow.FindOwner(window.shortcut);
      if (owner != null) {
        DialogPlacement.CenterOnScreenOf(window, owner);
      } else {
        window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
      }
    };

    /// <summary>Opens the comment window as a dialog, centred on the screen of the MDSHO window the button belongs to.</summary>
    public static void ShowCommentFor(ShortcutViewModel shortcut) {
      ShowDialogFor(() => ForComment(shortcut), shortcut);
    }

    /// <summary>
    /// Opens the note window without blocking, so several notes can be open side by side while MDSHO stays usable. A note
    /// that is already open is brought to the front instead of opening a second window that could overwrite it.
    /// </summary>
    public static void ShowNoteFor(ShortcutViewModel shortcut) {
      try {
        TextEditWindow open = openNoteWindows.Find(w => ReferenceEquals(w.shortcut, shortcut) || (shortcut != null && w.shortcut.ID == shortcut.ID && shortcut.ID != 0));
        if (open != null) {
          if (open.WindowState == WindowState.Minimized) {
            open.WindowState = WindowState.Normal;
          }
          open.Activate();
          return;
        }
        TextEditWindow window = ForNote(shortcut);
        NoteWindowPlace place = shortcut.ID != 0 ? DataStore.NoteWindows.Get(shortcut.ID) : null;
        if (place != null) {
          window.UseSavedPlace(place);
        } else {
          PlaceNoteWindow(window);
        }
        openNoteWindows.Add(window);
        window.Closed += (s, e) => openNoteWindows.Remove(window);
        window.TrackNotePlace();
        window.Show();
        window.SaveNotePlace();
      } catch (Exception ex) {
        ErrorDialog.ShowDialog(ex);
      }
    }

    // ---- Remembering the place of note windows ----

    /// <summary>The work areas of all screens, in physical pixels. Tests replace it.</summary>
    public static Func<IList<ScreenRect>> WorkAreas = () => AppServices.Current.Screens.AllScreens().Select(s => s.WorkingArea).ToList();

    /// <summary>The work area of the main screen, in physical pixels; null = the primary screen. Tests replace it.</summary>
    public static Func<ScreenRect> MainWorkArea;

    /// <summary>True while MDSHO exits: open note windows then stay marked as open, so they come back at the next start.</summary>
    public static bool IsExiting { get; set; }

    private readonly DispatcherTimer placeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
    private WindowState savedState = WindowState.Normal;

    /// <summary>Opens the window at a saved place and size, in its saved state; a place off every screen moves to the main screen.</summary>
    private void UseSavedPlace(NoteWindowPlace place) {
      this.WindowStartupLocation = WindowStartupLocation.Manual;
      this.Left = place.Left;
      this.Top = place.Top;
      this.Width = Math.Max(this.MinWidth, place.Width);
      this.Height = Math.Max(this.MinHeight, place.Height);
      SetPinned(place.IsTopmost);
      this.savedState = place.State == NoteWindowPlace.Maximized ? WindowState.Maximized
        : place.State == NoteWindowPlace.Minimized ? WindowState.Minimized : WindowState.Normal;
      EventHandler placeOnHandle = null;
      placeOnHandle = (s, e) => {
        this.SourceInitialized -= placeOnHandle;
        // The normal place is checked in physical pixels before the window shows, then the state is applied.
        RescueIfLost();
        this.WindowState = this.savedState;
      };
      this.SourceInitialized += placeOnHandle;
    }

    // Moves the window to the middle of the main screen when not enough of its title bar is on any screen.
    private void RescueIfLost() {
      Rect bounds = DialogPlacement.PhysicalBoundsOf(this);
      if (bounds.IsEmpty) {
        return;
      }
      if (!LostWindows.IsLost(new ScreenRect(bounds.X, bounds.Y, bounds.Width, bounds.Height), WorkAreas())) {
        return;
      }
      ScreenRect main = MainWorkArea != null ? MainWorkArea() : AppServices.Current.Screens.PrimaryScreen().WorkingArea;
      DialogPlacement.MoveToPhysical(this, new Point(Math.Round(main.X + (main.Width - bounds.Width) / 2), Math.Round(main.Y + (main.Height - bounds.Height) / 2)));
    }

    // Saves the place shortly after the window stops moving or resizing, and marks it closed when the user closes it.
    private void TrackNotePlace() {
      this.placeTimer.Tick += (s, e) => {
        this.placeTimer.Stop();
        SaveNotePlace();
      };
      EventHandler later = (s, e) => {
        this.placeTimer.Stop();
        this.placeTimer.Start();
      };
      this.LocationChanged += later;
      this.SizeChanged += (s, e) => later(s, e);
      this.StateChanged += later;
      this.Closing += (s, e) => {
        this.placeTimer.Stop();
        // Closed by the user: remember the place for next time, but not as open. At exit it stays open.
        SaveNotePlace(isOpen: IsExiting);
      };
    }

    /// <summary>Saves this note window's normal place, size and state, marked as open.</summary>
    public void SaveNotePlace() {
      SaveNotePlace(isOpen: true);
    }

    private void SaveNotePlace(bool isOpen) {
      if (this.kind != TextKind.Note || this.shortcut.ID == 0) {
        return;
      }
      Rect normal = this.WindowState == WindowState.Normal
        ? new Rect(this.Left, this.Top, double.IsNaN(this.Width) ? this.ActualWidth : this.Width, double.IsNaN(this.Height) ? this.ActualHeight : this.Height)
        : this.RestoreBounds;
      if (normal.IsEmpty || double.IsNaN(normal.X) || double.IsInfinity(normal.Width)) {
        return;
      }
      long state = this.WindowState == WindowState.Maximized ? NoteWindowPlace.Maximized
        : this.WindowState == WindowState.Minimized ? NoteWindowPlace.Minimized : NoteWindowPlace.Normal;
      try {
        DataStore.NoteWindows.Save(new NoteWindowPlace(this.shortcut.ID, (long)Math.Round(normal.X), (long)Math.Round(normal.Y),
          (long)Math.Round(normal.Width), (long)Math.Round(normal.Height), state, isOpen) { IsTopmost = this.Topmost });
      } catch (Exception ex) {
        // A place that can't be saved must never break the note window.
        AppLog.Warning(ex, "Could not save the place of a note window");
      }
    }

    /// <summary>
    /// At exit (from the tray, or when Windows shuts down or restarts): saves unsaved typing in every open note window and
    /// keeps the windows marked as open, so they come back where they were at the next start.
    /// </summary>
    public static void SaveOpenNotesForExit() {
      IsExiting = true;
      foreach (TextEditWindow window in openNoteWindows.ToList()) {
        string text = window.TextComment.Text ?? "";
        string value = string.IsNullOrWhiteSpace(text) ? "" : text;
        if (value != (window.shortcut.NoteText ?? "")) {
          window.shortcut.NoteText = value;
        }
        window.SaveNotePlace(isOpen: true);
      }
    }

    /// <summary>At start: opens every note window that was open, where it was; a saved window of a note that is gone is marked closed.</summary>
    public static void ReopenOpenNotes(AppViewModel app) {
      List<ShortcutViewModel> all = new List<ShortcutViewModel>();
      foreach (DesktopWindowViewModel panel in app.Windows) {
        AddWithChildren(panel.RootShortcuts, all);
      }
      foreach (NoteWindowPlace place in DataStore.NoteWindows.GetOpen()) {
        ShortcutViewModel note = all.FirstOrDefault(s => s.ID == place.ShortcutID && s.IsNote);
        if (note != null) {
          ShowNoteFor(note);
        } else {
          DataStore.NoteWindows.SetOpen(place.ShortcutID, false);
        }
      }
    }

    private static void AddWithChildren(IEnumerable<ShortcutViewModel> shortcuts, List<ShortcutViewModel> into) {
      foreach (ShortcutViewModel s in shortcuts) {
        into.Add(s);
        AddWithChildren(s.Children, into);
      }
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

    /// <summary>
    /// A note has pin (keep on top), save and close in its title bar and no bottom bar, so the text gets the whole window.
    /// Save is greyed out while there is nothing to save and keeps the window open; X and Esc close without saving.
    /// </summary>
    private void UseNoteTitleBar() {
      this.Footer.Visibility = Visibility.Collapsed;
      this.ButtonColors.Visibility = Visibility.Visible;
      this.ButtonColors.Click += (s, e) => ShowNoteColours();
      this.ButtonPin.Visibility = Visibility.Visible;
      this.ButtonTitleSave.Visibility = Visibility.Visible;
      this.ButtonClose.ToolTip = "Close without saving (Esc)";
      System.Windows.Automation.AutomationProperties.SetName(this.ButtonClose, "Close without saving");
      SetPinned(false);
      this.TextComment.TextChanged += (s, e) => UpdateTitleSave();
      UpdateTitleSave();
      UseNoteColours();
    }

    /// <summary>The window follows the note's five colours live: border, title bar, title text and buttons, background, text.</summary>
    private void UseNoteColours() {
      NoteColors colors = this.shortcut.NoteColors;
      Converters.HexToBrushConverter toBrush = new Converters.HexToBrushConverter();
      System.Windows.Data.Binding Bind(string path) {
        return new System.Windows.Data.Binding(path) { Source = colors, Converter = toBrush };
      }
      SetBinding(BorderBrushProperty, Bind(nameof(NoteColors.BorderColor)));
      this.TitleBar.SetBinding(System.Windows.Controls.Border.BackgroundProperty, Bind(nameof(NoteColors.TitleBackgroundColor)));
      this.TextTitle.SetBinding(System.Windows.Controls.TextBlock.ForegroundProperty, Bind(nameof(NoteColors.TitleTextColor)));
      this.TextTitleIcon.SetBinding(System.Windows.Controls.TextBlock.ForegroundProperty, Bind(nameof(NoteColors.TitleTextColor)));
      foreach (System.Windows.Controls.Button button in new[] { this.ButtonColors, this.ButtonPin, this.ButtonTitleSave, this.ButtonClose }) {
        button.SetBinding(ForegroundProperty, Bind(nameof(NoteColors.TitleTextColor)));
      }
      SetBinding(BackgroundProperty, Bind(nameof(NoteColors.BackgroundColor)));
      this.Body.SetBinding(System.Windows.Controls.Panel.BackgroundProperty, Bind(nameof(NoteColors.BackgroundColor)));
      this.TextComment.SetBinding(System.Windows.Controls.Control.BackgroundProperty, Bind(nameof(NoteColors.BackgroundColor)));
      this.TextComment.SetBinding(System.Windows.Controls.Control.ForegroundProperty, Bind(nameof(NoteColors.TextColor)));
      this.TextComment.SetBinding(System.Windows.Controls.Primitives.TextBoxBase.CaretBrushProperty, Bind(nameof(NoteColors.TextColor)));
    }

    /// <summary>Shows the Note colours window modally. Tests replace it so no modal window blocks them.</summary>
    public static Func<Window, bool?> ShowNoteColoursDialog = w => w.ShowDialog();

    /// <summary>The gap between a note and its Note colours window (DIPs).</summary>
    public const double NoteColoursGap = 20;

    // Opens the Settings window as "Note colours" beside this note (right or left, where there is room); the note
    // follows the colours while they change.
    private void ShowNoteColours() {
      try {
        SettingsWindow colours = new SettingsWindow(this.shortcut);
        DialogPlacement.BesideWindow(colours, this, NoteColoursGap);
        ShowNoteColoursDialog(colours);
      } catch (Exception ex) {
        ErrorDialog.ShowDialog(ex);
      }
    }

    /// <summary>Pins the window on top of other apps, or not; the pin button shows a filled pin while pinned.</summary>
    private void SetPinned(bool pinned) {
      this.Topmost = pinned;
      this.ButtonPin.Content = pinned ? "\uE841" : "\uE718";
      if (pinned) {
        // A shade, so the pinned look works on any title bar colour.
        this.ButtonPin.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x33, 0, 0, 0));
      } else {
        this.ButtonPin.ClearValue(BackgroundProperty);
      }
    }

    // Save is active only when the text differs from the saved note.
    private void UpdateTitleSave() {
      this.ButtonTitleSave.IsEnabled = TextToSave() != (this.shortcut.NoteText ?? "");
    }

    private string TextToSave() {
      string text = this.TextComment.Text ?? "";
      return string.IsNullOrWhiteSpace(text) ? "" : text;
    }

    // Saves the note and keeps its window open (the title-bar Save, Ctrl+Enter and Ctrl+S).
    private void SaveNoteKeepOpen() {
      this.shortcut.NoteText = TextToSave();
      this.Saved = true;
      UpdateTitleSave();
    }

    /// <summary>
    /// The look of the Note and Comment windows: the editor fills the body (window colour, no border, 10 px padding, no
    /// word wrap, each scroll bar only when it is needed), with the hint in the bottom bar on the left. Every outer margin of the bottom bar
    /// equals the height of the Save button.
    /// </summary>
    private void UseEditorLayout() {
      this.BodyContent.Margin = new Thickness(0);
      this.TextComment.Background = this.Background;
      this.TextComment.BorderThickness = new Thickness(0);
      this.TextComment.Padding = new Thickness(10);
      this.TextComment.TextWrapping = TextWrapping.NoWrap;
      this.TextComment.HorizontalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto;
      this.TextComment.VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto;
      this.TextHint.Visibility = Visibility.Collapsed;
      this.TextFooterHint.Visibility = Visibility.Visible;
      this.TextFooterHint.Margin = new Thickness(0, 0, 16, 0);
      this.ButtonSave.SizeChanged += (s, e) => this.FooterContent.Margin = new Thickness(this.ButtonSave.ActualHeight);
    }

    private void ShowIcons(IconGlyphs icons) {
      string glyph = this.kind == TextKind.Note ? icons.Rename : icons.Info;
      this.TextTitleIcon.Text = glyph;
      this.ButtonClose.Content = icons.Times;
      this.ButtonColors.Content = icons.Brush;
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
      } else if (this.kind == TextKind.Note && (e.Key == Key.Enter || e.Key == Key.S) && (KeyboardModifiers() & ModifierKeys.Control) == ModifierKeys.Control) {
        // A note is saved and stays open.
        e.Handled = true;
        SaveNoteKeepOpen();
      } else if (e.Key == Key.Enter && (KeyboardModifiers() & ModifierKeys.Control) == ModifierKeys.Control) {
        e.Handled = true;
        Save();
      }
    }
  }
}
