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
  /// Edits the text of one button: its comment (any button or group) or, for a note button, the note itself. A comment is
  /// saved only by OK in the title bar (or Ctrl+Enter), which is greyed out while nothing changed; the X button and Esc
  /// close it without saving. A note saves itself shortly
  /// after each change and when its window closes, so it has no Save button. A text of only spaces and new lines is saved
  /// as empty.
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
    private readonly IconGlyphs icons;

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
      this.shortcut = shortcut ?? throw new ArgumentNullException(nameof(shortcut));
      this.kind = kind;
      this.icons = shortcut.OwningWindow?.Appearance?.Icons ?? new IconGlyphs(true);
      // Just the button's name: the icon already says whether it is a note or a comment. It follows renames while open.
      ShowTitle();
      System.ComponentModel.PropertyChangedEventHandler nameChanged = (s, e) => {
        if (e.PropertyName == nameof(ShortcutViewModel.Name)) { ShowTitle(); }
      };
      shortcut.PropertyChanged += nameChanged;
      this.Closed += (s, e) => shortcut.PropertyChanged -= nameChanged;
      this.TextComment.Text = (kind == TextKind.Note ? shortcut.NoteText : shortcut.Comment) ?? "";
      System.Windows.Automation.AutomationProperties.SetName(this.TextComment, kind == TextKind.Note ? "Note" : "Comment");
      if (kind == TextKind.Note) {
        // A note is the main content, so its window is bigger; a comment keeps the smaller size.
        this.Width = 640;
        this.Height = 520;
        this.MinWidth = 380;
        this.MinHeight = 300;
      }
      // The editor has the window's colour (a note then follows its own colours).
      this.TextComment.Background = this.Background;
      if (kind == TextKind.Note) {
        UseNoteTitleBar();
        UseRenamableTitle();
      } else {
        UseCommentTitleBar();
      }

      // The title icon, the title bar buttons and the window icon follow the window's solid or outline setting while open.
      IconGlyphs icons = this.icons;
      ShowIcons(icons);
      System.ComponentModel.PropertyChangedEventHandler iconsChanged = (s, e) => ShowIcons(icons);
      icons.PropertyChanged += iconsChanged;
      this.Closed += (s, e) => icons.PropertyChanged -= iconsChanged;

      this.ButtonClose.Click += (s, e) => Close();
      this.ButtonOk.Click += (s, e) => Save();
      this.ButtonPin.Click += (s, e) => {
        SetPinned(!this.Topmost);
        SaveNotePlace();
      };
      // Wrap text: a note remembers it with its window, the Comment windows of a panel share it.
      SetWrapText(kind == TextKind.Comment && (shortcut.OwningWindow?.Appearance?.CommentWrapText ?? false));
      this.ButtonWrap.Click += (s, e) => {
        SetWrapText(!this.IsWrapText);
        if (this.kind == TextKind.Note) {
          SaveNotePlace();
        } else if (shortcut.OwningWindow?.Appearance != null) {
          shortcut.OwningWindow.Appearance.CommentWrapText = this.IsWrapText;
        }
      };
      this.PreviewKeyDown += Window_PreviewKeyDown;
      this.TextComment.PreviewMouseLeftButtonDown += TextComment_PreviewMouseLeftButtonDown;
      this.Loaded += (s, e) => {
        this.TextComment.Focus();
        this.TextComment.CaretIndex = this.TextComment.Text.Length;
      };
    }

    private void ShowTitle() {
      this.Title = this.shortcut.Name;
      this.TextTitle.Text = this.Title;
    }

    // ---- Renaming a note from its title ----

    /// <summary>The tooltip of a note's title.</summary>
    public const string RenameTitleToolTip = "Double-click to rename the note";

    private TitleRename titleRename;

    /// <summary>A note is renamed from its title (see <see cref="TitleRename"/>); the new name is saved at once.</summary>
    private void UseRenamableTitle() {
      this.titleRename = new TitleRename(this, this.TextTitle, this.TextTitleEdit, RenameTitleToolTip,
        () => this.shortcut.Name, name => this.shortcut.Name = name,
        () => { if (this.IsActive) { this.TextComment.Focus(); } });
    }

    /// <summary>True while the note's title is being renamed.</summary>
    public bool IsRenamingTitle => this.titleRename?.IsRenaming ?? false;

    /// <summary>Opens the edit box over a note's title, with the name selected.</summary>
    public void BeginRenameTitle() {
      this.titleRename?.Begin();
    }

    /// <summary>Closes the title's edit box; with <paramref name="save"/> a changed, non-blank name renames the note.</summary>
    public void EndRenameTitle(bool save) {
      this.titleRename?.End(save);
    }

    /// <summary>F2 on a note in its panel: opens (or brings back) its Note window with the title ready to rename.</summary>
    public static void RenameNoteFor(ShortcutViewModel shortcut) {
      ShowNoteFor(shortcut);
      FindOpenNoteWindow(shortcut)?.BeginRenameTitle();
    }

    /// <summary>True when the text was saved; for a comment false after Cancel, X or Esc.</summary>
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

    /// <summary>Opens the comment window as a dialog beside the MDSHO window the button belongs to, level with the button (see <see cref="PlaceComment"/>).</summary>
    public static void ShowCommentFor(ShortcutViewModel shortcut) {
      try {
        TextEditWindow window = ForComment(shortcut);
        Window owner = RunCommandWindow.FindOwner(shortcut);
        if (owner != null) {
          PlaceComment(window, owner);
        } else {
          window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }
        window.ShowDialog();
      } catch (Exception ex) {
        ErrorDialog.ShowDialog(ex);
      }
    }

    /// <summary>
    /// Opens the note window without blocking, so several notes can be open side by side while MDSHO stays usable. A note
    /// that is already open is brought to the front instead of opening a second window that could overwrite it.
    /// </summary>
    /// <summary>The character under the mouse in the editor, or -1. Tests replace it.</summary>
    public static Func<System.Windows.Controls.TextBox, MouseButtonEventArgs, int> CharacterIndexAt =
      (box, e) => box.GetCharacterIndexFromPoint(e.GetPosition(box), true);

    /// <summary>
    /// Triggering a note button: opens its note window, or closes it when it is open (closing saves the text). A minimized
    /// note window is brought back instead, since it can't be seen.
    /// </summary>
    public static void ToggleNoteFor(ShortcutViewModel shortcut) {
      try {
        TextEditWindow open = FindOpenNoteWindow(shortcut);
        if (open != null && open.WindowState != WindowState.Minimized) {
          open.Close();
          return;
        }
      } catch (Exception ex) {
        ErrorDialog.ShowDialog(ex);
        return;
      }
      ShowNoteFor(shortcut);
    }

    private static TextEditWindow FindOpenNoteWindow(ShortcutViewModel shortcut) {
      return openNoteWindows.Find(w => ReferenceEquals(w.shortcut, shortcut) || (shortcut != null && w.shortcut.ID == shortcut.ID && shortcut.ID != 0));
    }

    public static void ShowNoteFor(ShortcutViewModel shortcut) {
      try {
        TextEditWindow open = FindOpenNoteWindow(shortcut);
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
      SetWrapText(place.IsWrapText);
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
          (long)Math.Round(normal.Width), (long)Math.Round(normal.Height), state, isOpen) { IsTopmost = this.Topmost, IsWrapText = this.IsWrapText });
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

    /// <summary>The gap between the Comment window and its panel (DIPs).</summary>
    public const double CommentGap = 20;

    /// <summary>
    /// Places the Comment window beside its panel, <see cref="CommentGap"/> away, with its title bar level with the button the
    /// comment belongs to; centred on the panel's screen when there is no room beside it or the button can't be found.
    /// </summary>
    public static void PlaceComment(TextEditWindow window, Window owner) {
      FrameworkElement button = FindButtonOf(owner, window.shortcut);
      if (button == null) {
        DialogPlacement.CenterOnScreenOf(window, owner);
        return;
      }
      // The middle of the title bar: below the window's top border, half the caption height down.
      double caption = System.Windows.Shell.WindowChrome.GetWindowChrome(window)?.CaptionHeight ?? 32;
      DialogPlacement.BesideButton(window, owner, button, CommentGap, window.BorderThickness.Top + caption / 2);
    }

    // The shown button of the shortcut in an MDSHO window, or null (for example inside a collapsed group).
    private static FrameworkElement FindButtonOf(DependencyObject root, ShortcutViewModel shortcut) {
      int count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);
      for (int i = 0; i < count; i++) {
        DependencyObject child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
        if (child is System.Windows.Controls.Button button && ReferenceEquals(button.DataContext, shortcut) && button.IsVisible) {
          return button;
        }
        FrameworkElement found = FindButtonOf(child, shortcut);
        if (found != null) {
          return found;
        }
      }
      return null;
    }

    /// <summary>How long after the last change a note saves itself.</summary>
    public static readonly TimeSpan NoteAutoSaveDelay = TimeSpan.FromMilliseconds(500);

    private readonly DispatcherTimer noteSaveTimer = new DispatcherTimer { Interval = NoteAutoSaveDelay };

    /// <summary>
    /// A note has colours, pin (keep on top) and close in its title bar and no bottom bar, so the text gets the whole window.
    /// It saves itself shortly after each change (not on every key, to keep database writes few) and at once when the
    /// window closes (X or Esc), so nothing typed is lost.
    /// </summary>
    private void UseNoteTitleBar() {
      this.ButtonColors.Visibility = Visibility.Visible;
      this.ButtonColors.Click += (s, e) => ShowNoteColours();
      this.ButtonPin.Visibility = Visibility.Visible;
      this.ButtonClose.ToolTip = "Close (Esc)";
      SetPinned(false);
      this.noteSaveTimer.Tick += (s, e) => SaveNoteNow();
      this.TextComment.TextChanged += (s, e) => {
        this.noteSaveTimer.Stop();
        this.noteSaveTimer.Start();
      };
      this.Closing += (s, e) => SaveNoteNow();
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
      foreach (System.Windows.Controls.Button button in new[] { this.ButtonColors, this.ButtonWrap, this.ButtonPin, this.ButtonClose }) {
        button.SetBinding(ForegroundProperty, Bind(nameof(NoteColors.TitleTextColor)));
      }
      SetBinding(BackgroundProperty, Bind(nameof(NoteColors.BackgroundColor)));
      this.Body.SetBinding(System.Windows.Controls.Panel.BackgroundProperty, Bind(nameof(NoteColors.BackgroundColor)));
      this.TextComment.SetBinding(System.Windows.Controls.Control.BackgroundProperty, Bind(nameof(NoteColors.BackgroundColor)));
      this.TextComment.SetBinding(System.Windows.Controls.Control.ForegroundProperty, Bind(nameof(NoteColors.TextColor)));
      this.TextComment.SetBinding(System.Windows.Controls.Primitives.TextBoxBase.CaretBrushProperty, Bind(nameof(NoteColors.TextColor)));
    }

    /// <summary>A comment has OK and close in its title bar: OK saves and closes, X and Esc close without saving.</summary>
    private void UseCommentTitleBar() {
      this.ButtonOk.Visibility = Visibility.Visible;
      this.ButtonClose.ToolTip = "Close without saving (Esc)";
      System.Windows.Automation.AutomationProperties.SetName(this.ButtonClose, "Close without saving");
      this.TextComment.TextChanged += (s, e) => UpdateOk();
      UpdateOk();
    }

    // OK is active only when the text differs from the saved comment (only spaces counts as no comment).
    private void UpdateOk() {
      this.ButtonOk.IsEnabled = HasCommentChange();
    }

    private bool HasCommentChange() {
      return TextToSave() != (this.shortcut.Comment ?? "");
    }

    /// <summary>Shows the Note colours window modally. Tests replace it so no modal window blocks them.</summary>
    public static Func<Window, bool?> ShowNoteColoursDialog = w => w.ShowDialog();

    /// <summary>The gap between a note and its Note colours window (DIPs).</summary>
    public const double NoteColoursGap = 20;

    // Opens the Colours window as "Note colours" beside this note (right or left, where there is room); the note
    // follows the colours while they change.
    private void ShowNoteColours() {
      try {
        ColorsWindow colours = new ColorsWindow(this.shortcut);
        DialogPlacement.BesideWindow(colours, this, NoteColoursGap);
        ShowNoteColoursDialog(colours);
      } catch (Exception ex) {
        ErrorDialog.ShowDialog(ex);
      }
    }

    /// <summary>Pins the window on top of other apps, or not; the thumbtack points down while pinned and left while not.</summary>
    private void SetPinned(bool pinned) {
      this.Topmost = pinned;
      this.ButtonPin.Content = pinned ? this.icons.PinOn : this.icons.PinOff;
      string tip = pinned ? "Stop keeping on top" : "Keep on top";
      this.ButtonPin.ToolTip = tip;
      System.Windows.Automation.AutomationProperties.SetName(this.ButtonPin, tip);
      if (pinned) {
        // A shade, so the pinned look works on any title bar colour.
        this.ButtonPin.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x33, 0, 0, 0));
      } else {
        this.ButtonPin.ClearValue(BackgroundProperty);
      }
    }

    /// <summary>True while the text wraps at the window's edge.</summary>
    public bool IsWrapText => this.TextComment.TextWrapping == TextWrapping.Wrap;

    /// <summary>Wraps the text at the window's edge (no sideways scrolling then), or not.</summary>
    private void SetWrapText(bool wrap) {
      this.TextComment.TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap;
      this.TextComment.HorizontalScrollBarVisibility = wrap ? System.Windows.Controls.ScrollBarVisibility.Disabled : System.Windows.Controls.ScrollBarVisibility.Auto;
      string tip = wrap ? "Stop wrapping text" : "Wrap text";
      this.ButtonWrap.ToolTip = tip;
      System.Windows.Automation.AutomationProperties.SetName(this.ButtonWrap, tip);
      if (wrap) {
        // The same shade as the pin while on.
        this.ButtonWrap.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x33, 0, 0, 0));
      } else {
        this.ButtonWrap.ClearValue(BackgroundProperty);
      }
    }

    // A double click selects the word without the space after it, a triple click the whole paragraph.
    private void TextComment_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
      if (e.ClickCount != 2 && e.ClickCount != 3) {
        return;
      }
      int index = CharacterIndexAt(this.TextComment, e);
      if (index < 0) {
        return;
      }
      string text = this.TextComment.Text ?? "";
      (int start, int length) = e.ClickCount == 2 ? TextClickSelection.WordAt(text, index) : TextClickSelection.ParagraphAt(text, index);
      this.TextComment.Focus();
      this.TextComment.Select(start, length);
      e.Handled = true;
    }

    private string TextToSave() {
      string text = this.TextComment.Text ?? "";
      return string.IsNullOrWhiteSpace(text) ? "" : text;
    }

    // Saves the note now if its text changed, and keeps the window open (autosave, closing, Ctrl+Enter and Ctrl+S).
    private void SaveNoteNow() {
      this.noteSaveTimer.Stop();
      string value = TextToSave();
      if (value == (this.shortcut.NoteText ?? "")) {
        return;
      }
      try {
        this.shortcut.NoteText = value;
        this.Saved = true;
      } catch (Exception ex) {
        ErrorDialog.ShowDialog(ex);
      }
    }

    private void ShowIcons(IconGlyphs icons) {
      string glyph = this.kind == TextKind.Note ? icons.Note : icons.Comment;
      this.TextTitleIcon.Text = glyph;
      this.ButtonClose.Content = icons.Times;
      this.ButtonColors.Content = icons.Brush;
      this.ButtonOk.Content = icons.Ok;
      this.ButtonWrap.Content = icons.WrapText;
      this.ButtonPin.Content = this.Topmost ? icons.PinOn : icons.PinOff;
      this.Icon = GlyphIcon.Create(glyph);
    }

    // Saves the comment and closes; does nothing while there is no change (OK is greyed out then).
    private void Save() {
      if (!HasCommentChange()) {
        return;
      }
      this.shortcut.Comment = TextToSave();
      this.Saved = true;
      Close();
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e) {
      if (this.titleRename != null && this.titleRename.HandleKey(e)) {
        // Enter saved the new name, Esc kept the old one; neither closes or saves the window.
      } else if (e.Key == Key.Escape) {
        e.Handled = true;
        Close();
      } else if (this.kind == TextKind.Note && (e.Key == Key.Enter || e.Key == Key.S) && (KeyboardModifiers() & ModifierKeys.Control) == ModifierKeys.Control) {
        // A note is saved now and stays open.
        e.Handled = true;
        SaveNoteNow();
      } else if (e.Key == Key.Enter && (KeyboardModifiers() & ModifierKeys.Control) == ModifierKeys.Control) {
        e.Handled = true;
        Save();
      }
    }
  }
}
