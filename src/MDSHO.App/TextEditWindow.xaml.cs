using System;
using System.Collections.Generic;
using System.ComponentModel;
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
  /// Edits the text of one button: its comment (any button or group), the note of a note button, or the value of a
  /// snippet. A comment or snippet is saved only by OK in the title bar (or Ctrl+Enter), which is greyed out while nothing changed; the X button and Esc
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
      Note,
      /// <summary>The text of a snippet.</summary>
      Snippet
    }

    private readonly ShortcutViewModel shortcut;
    private readonly TextKind kind;
    private readonly IconGlyphs icons;

    /// <summary>True for a note window, false for a comment window.</summary>
    public bool IsNote => this.kind == TextKind.Note;

    /// <summary>The keyboard modifiers held down; tests replace it, since they can't hold Ctrl.</summary>
    public static Func<ModifierKeys> KeyboardModifiers = () => Keyboard.Modifiers;

    /// <summary>Creates the window for the comment of a button or group.</summary>
    public static TextEditWindow ForComment(ShortcutViewModel shortcut) {
      return new TextEditWindow(shortcut, TextKind.Comment);
    }

    /// <summary>Creates the window for the text of a snippet.</summary>
    public static TextEditWindow ForSnippet(ShortcutViewModel shortcut) {
      return new TextEditWindow(shortcut, TextKind.Snippet);
    }

    /// <summary>Shows a Comment window or snippet editor as a dialog. Tests replace it so no modal window blocks them.</summary>
    public static Func<TextEditWindow, bool?> ShowTextDialog = w => w.ShowDialog();

    /// <summary>True for an Edit snippet window.</summary>
    public bool IsSnippetWindow => this.kind == TextKind.Snippet;

    /// <summary>The tooltip of an Edit snippet window's title.</summary>
    public const string RenameSnippetTitleToolTip = "Click twice slowly to rename the snippet";

    /// <summary>Opens the Edit snippet window as a dialog beside the MDSHO window the button belongs to, like the Comment window.</summary>
    public static void ShowSnippetFor(ShortcutViewModel shortcut) {
      ShowBesidePanel(shortcut, ForSnippet);
    }

    // Comment... and Edit snippet... open the same way: a dialog beside the panel, level with the button
    // (see PlaceComment), or centred on the screen when the panel can't be found.
    private static void ShowBesidePanel(ShortcutViewModel shortcut, Func<ShortcutViewModel, TextEditWindow> create) {
      try {
        TextEditWindow window = create(shortcut);
        Window owner = RunCommandWindow.FindOwner(shortcut);
        if (owner != null) {
          PlaceComment(window, owner);
        } else {
          window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }
        ShowTextDialog(window);
      } catch (Exception ex) {
        ErrorDialog.ShowDialog(ex);
      }
    }

    /// <summary>Creates the window for the text of a note button.</summary>
    public static TextEditWindow ForNote(ShortcutViewModel shortcut) {
      return new TextEditWindow(shortcut, TextKind.Note);
    }

    /// <summary>The smallest width of a Note window.</summary>
    public const double NoteMinWidth = 460;
    /// <summary>The smallest width of the Comment window and the snippet editor.</summary>
    public const double TextMinWidth = 420;

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
      this.TextComment.Text = StoredText() ?? "";
      System.Windows.Automation.AutomationProperties.SetName(this.TextComment,
        kind == TextKind.Note ? "Note" : kind == TextKind.Snippet ? "Snippet" : "Comment");
      if (kind == TextKind.Note) {
        // A note is the main content, so its window is bigger; a comment keeps the smaller size.
        this.Width = 640;
        this.Height = 520;
        this.MinWidth = NoteMinWidth;
        this.MinHeight = 300;
      } else {
        // The Comment window and the snippet editor share one smallest width, wide enough for their title bar buttons.
        this.Width = TextMinWidth;
        this.MinWidth = TextMinWidth;
      }
      // The editor has the window's colour (a note then follows its own colours).
      this.TextComment.Background = this.Background;
      if (kind == TextKind.Note) {
        UseNoteTitleBar();
        UseRenamableTitle(RenameTitleToolTip);
      } else {
        UseCommentTitleBar();
        if (kind == TextKind.Snippet) {
          UseRenamableTitle(RenameSnippetTitleToolTip);
        }
      }
      UseSizeButton();
      UseFontButton();
      UseAutoSave();

      // The title icon, the title bar buttons and the window icon follow the window's solid or outline setting while open.
      IconGlyphs icons = this.icons;
      ShowIcons(icons);
      System.ComponentModel.PropertyChangedEventHandler iconsChanged = (s, e) => ShowIcons(icons);
      icons.PropertyChanged += iconsChanged;
      this.Closed += (s, e) => icons.PropertyChanged -= iconsChanged;

      this.ButtonClose.Click += (s, e) => Close();
      this.ButtonPin.Click += (s, e) => {
        SetPinned(!this.Topmost);
        SaveNotePlace();
      };
      // Wrap text: a note remembers it with its window, the Comment windows of a panel share it.
      SetWrapText(kind != TextKind.Note && (shortcut.OwningWindow?.Appearance?.CommentWrapText ?? false));
      this.ButtonWrap.Click += (s, e) => {
        SetWrapText(!this.IsWrapText);
        if (this.kind == TextKind.Note) {
          SaveNotePlace();
        } else if (shortcut.OwningWindow?.Appearance != null) {
          shortcut.OwningWindow.Appearance.CommentWrapText = this.IsWrapText;
        }
      };
      // Ctrl + mouse wheel sizes the text; a note remembers the size with its window, a comment with its button, and the
      // snippet editor starts at 12 pt each time.
      SetTextSize(kind == TextKind.Comment ? NoteWindowPlace.FontSizeOrDefault(shortcut.CommentFontSize) : NoteWindowPlace.DefaultFontSize);
      this.TextComment.PreviewMouseWheel += TextComment_PreviewMouseWheel;
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
    public const string RenameTitleToolTip = "Click twice slowly to rename the note";

    private TitleRename titleRename;

    /// <summary>What a double click on the title text does: maximise or restore, like the title bar. Tests replace it.</summary>
    public Action TitleDoubleClick { get; set; }

    /// <summary>A note or snippet is renamed from its title (see <see cref="TitleRename"/>); the new name is saved at once.</summary>
    private void UseRenamableTitle(string toolTip) {
      this.titleRename = new TitleRename(this, this.TextTitle, this.TextTitleEdit, toolTip,
        () => this.shortcut.Name, name => this.shortcut.Name = name,
        () => { if (this.IsActive) { this.TextComment.Focus(); } },
        () => (this.TitleDoubleClick ?? (() => TitleRename.ToggleMaximize(this)))());
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
      ShowBesidePanel(shortcut, ForComment);
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
      SetTextSize(NoteWindowPlace.FontSizeOrDefault(place.FontSize));
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
        // Not closing after all (the note could not be saved and the user kept it open).
        if (e.Cancel) {
          return;
        }
        // Closed by the user: remember the place for next time, but not as open. At exit it stays open.
        SaveNotePlace(isOpen: IsExiting);
      };
    }

    /// <summary>Saves this note window's normal place, size and state, marked as open.</summary>
    public void SaveNotePlace() {
      SaveNotePlace(isOpen: true);
    }

    /// <summary>The normal (not maximized) place and size. Empty if not known yet.</summary>
    public Rect NormalBounds {
      get {
        Rect normal = this.WindowState == WindowState.Normal
          ? new Rect(this.Left, this.Top, double.IsNaN(this.Width) ? this.ActualWidth : this.Width, double.IsNaN(this.Height) ? this.ActualHeight : this.Height)
          : this.RestoreBounds;
        return normal.IsEmpty || double.IsNaN(normal.X) || double.IsNaN(normal.Y) || double.IsInfinity(normal.Width) || double.IsNaN(normal.Width) ? Rect.Empty : normal;
      }
    }

    /// <summary>Moves the normal place. A maximized window is maximized again on its new screen.</summary>
    public void MoveNormalBoundsTo(Rect bounds) {
      bool maximized = this.WindowState == WindowState.Maximized;
      if (maximized) {
        this.WindowState = WindowState.Normal;
      }
      this.Left = bounds.X;
      this.Top = bounds.Y;
      this.Width = Math.Max(this.MinWidth, bounds.Width);
      this.Height = Math.Max(this.MinHeight, bounds.Height);
      if (maximized) {
        this.WindowState = WindowState.Maximized;
      }
    }

    private void SaveNotePlace(bool isOpen) {
      if (this.kind != TextKind.Note || this.shortcut.ID == 0 || forgetting || this.discarded) {
        return;
      }
      Rect normal = NormalBounds;
      if (normal.IsEmpty) {
        return;
      }
      long state = this.WindowState == WindowState.Maximized ? NoteWindowPlace.Maximized
        : this.WindowState == WindowState.Minimized ? NoteWindowPlace.Minimized : NoteWindowPlace.Normal;
      try {
        DataStore.NoteWindows.Save(new NoteWindowPlace(this.shortcut.ID, (long)Math.Round(normal.X), (long)Math.Round(normal.Y),
          (long)Math.Round(normal.Width), (long)Math.Round(normal.Height), state, isOpen) { IsTopmost = this.Topmost, IsWrapText = this.IsWrapText,
          FontSize = TextSize });
      } catch (Exception ex) {
        // A place that can't be saved must never break the note window.
        AppLog.Warning(ex, "Could not save the place of a note window");
      }
      // Also save it for the current screen setup.
      AppServices.Current.ScreenSetups.Remember(new NoteWindowPlacedItem(this));
    }

    /// <summary>
    /// At exit (from the tray, or when Windows shuts down or restarts): saves unsaved typing in every open note window and
    /// keeps the windows marked as open, so they come back where they were at the next start.
    /// </summary>
    public static IReadOnlyList<string> SaveOpenNotesForExit() {
      IsExiting = true;
      return SaveOpenNotes();
    }

    /// <summary>"This note could not be saved: Shopping." or "These notes could not be saved: Shopping, Ideas."</summary>
    public static string UnsavedNotesMessage(IReadOnlyList<string> names) {
      return (names.Count == 1 ? "This note could not be saved: " : "These notes could not be saved: ") + string.Join(", ", names) + ".";
    }

    /// <summary>
    /// After a note, or a group or panel holding notes, was deleted: closes their open note windows without saving, since
    /// the notes are gone (typing there could only fail to save).
    /// </summary>
    public static void CloseNotesOf(ShortcutViewModel deleted) {
      foreach (TextEditWindow window in openNoteWindows.ToList()) {
        if (IsSelfOrInside(window.shortcut, deleted)) {
          window.discarded = true;
          window.Close();
        }
      }
    }

    private static bool IsSelfOrInside(ShortcutViewModel shortcut, ShortcutViewModel container) {
      for (ShortcutViewModel s = shortcut; s != null; s = s.ParentShortcut) {
        if (ReferenceEquals(s, container) || (container.ID != 0 && s.ID == container.ID)) {
          return true;
        }
      }
      return false;
    }

    /// <summary>
    /// Saves unsaved typing in every open Comment window and snippet editor, and the typing and place of every open note
    /// window, marked as open (before an import too). Each is saved on its own, so one that fails doesn't stop the others.
    /// Returns the names of the buttons whose text could not be saved.
    /// </summary>
    public static IReadOnlyList<string> SaveOpenNotes() {
      List<string> failed = new List<string>();
      foreach (TextEditWindow window in openTextWindows.ToList()) {
        window.noteSaveTimer.Stop();
        if (!window.discarded && window.SaveText() != null) {
          failed.Add(string.IsNullOrWhiteSpace(window.shortcut.Name) ? "(no name)" : window.shortcut.Name);
        }
      }
      foreach (TextEditWindow window in openNoteWindows.ToList()) {
        if (window.discarded) {
          continue;
        }
        window.noteSaveTimer.Stop();
        if (window.SaveText() != null) {
          failed.Add(string.IsNullOrWhiteSpace(window.shortcut.Name) ? "(no name)" : window.shortcut.Name);
        }
        try {
          window.SaveNotePlace(isOpen: true);
        } catch (Exception ex) {
          AppLog.Warning(ex, "Could not save the place of a note window");
        }
      }
      return failed;
    }

    // True while ForgetOpenNotes closes the windows: nothing is saved then.
    private static bool forgetting;

    /// <summary>
    /// After an import: closes the note windows of the old data without saving their text or place, so they can't
    /// overwrite the imported data. Call <see cref="SaveOpenNotes"/> before the import.
    /// </summary>
    public static void ForgetOpenNotes() {
      forgetting = true;
      try {
        foreach (TextEditWindow window in openNoteWindows.ToList()) {
          window.Close();
        }
      } finally {
        forgetting = false;
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
    /// </summary>
    private void UseNoteTitleBar() {
      this.ButtonColors.Visibility = Visibility.Visible;
      this.ButtonColors.Click += (s, e) => ShowNoteColours();
      this.ButtonPin.Visibility = Visibility.Visible;
      this.ButtonClose.ToolTip = "Close (Esc)";
      SetPinned(false);
      UseNoteColours();
    }

    /// <summary>A comment has the window theme's colours (Light is its old look); a note keeps its own colours.</summary>
    private void UseWindowTheme() {
      // Without the window's black shades, the title bar buttons use the theme's.
      this.Resources.Remove("Theme.TitleButtonHover");
      this.Resources.Remove("Theme.TitleButtonPressed");
      SetResourceReference(BackgroundProperty, "Theme.WindowBackground");
      SetResourceReference(BorderBrushProperty, "Theme.WindowBorder");
      this.TitleBar.SetResourceReference(System.Windows.Controls.Border.BackgroundProperty, "Theme.BarBackground");
      this.TextTitle.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "Theme.TextStrong");
      this.TextTitleIcon.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "Theme.TextStrong");
      foreach (System.Windows.Controls.Button button in new[] { this.ButtonSize, this.ButtonFont, this.ButtonWrap, this.ButtonClose }) {
        button.SetResourceReference(ForegroundProperty, "Theme.TextStrong");
      }
      this.Body.SetResourceReference(System.Windows.Controls.Panel.BackgroundProperty, "Theme.WindowBackground");
      this.TextComment.SetResourceReference(System.Windows.Controls.Control.BackgroundProperty, "Theme.WindowBackground");
      this.TextComment.SetResourceReference(System.Windows.Controls.Control.ForegroundProperty, "Theme.Text");
      this.TextComment.SetResourceReference(System.Windows.Controls.Primitives.TextBoxBase.CaretBrushProperty, "Theme.Text");
      Helpers.WindowThemes.SetIsThemed(this, true);
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
      foreach (System.Windows.Controls.Button button in new[] { this.ButtonSize, this.ButtonFont, this.ButtonColors, this.ButtonWrap, this.ButtonPin, this.ButtonClose }) {
        button.SetBinding(ForegroundProperty, Bind(nameof(NoteColors.TitleTextColor)));
      }
      SetBinding(BackgroundProperty, Bind(nameof(NoteColors.BackgroundColor)));
      this.Body.SetBinding(System.Windows.Controls.Panel.BackgroundProperty, Bind(nameof(NoteColors.BackgroundColor)));
      this.TextComment.SetBinding(System.Windows.Controls.Control.BackgroundProperty, Bind(nameof(NoteColors.BackgroundColor)));
      this.TextComment.SetBinding(System.Windows.Controls.Control.ForegroundProperty, Bind(nameof(NoteColors.TextColor)));
      this.TextComment.SetBinding(System.Windows.Controls.Primitives.TextBoxBase.CaretBrushProperty, Bind(nameof(NoteColors.TextColor)));
    }

    /// <summary>A comment and a snippet have the window theme's colours and close in their title bar; they save by themselves.</summary>
    private void UseCommentTitleBar() {
      UseWindowTheme();
      this.ButtonClose.ToolTip = "Close (Esc)";
      System.Windows.Automation.AutomationProperties.SetName(this.ButtonClose, "Close");
    }

    /// <summary>
    /// Every kind saves itself shortly after each change (not on every key, to keep database writes few) and at once when
    /// the window closes (X, Esc or Ctrl+Enter), so nothing typed is lost. A comment or snippet window open at exit is
    /// saved with the open notes.
    /// </summary>
    private void UseAutoSave() {
      this.noteSaveTimer.Tick += (s, e) => SaveNoteNow();
      this.TextComment.TextChanged += (s, e) => {
        this.noteSaveTimer.Stop();
        this.noteSaveTimer.Start();
      };
      this.Closing += (s, e) => SaveBeforeClosing(e);
      if (this.kind != TextKind.Note) {
        openTextWindows.Add(this);
        this.Closed += (s, e) => openTextWindows.Remove(this);
      }
    }

    // The open Comment windows and snippet editors (they save at exit like notes).
    private static readonly List<TextEditWindow> openTextWindows = new List<TextEditWindow>();

    // "note", "snippet" or "comment", for messages.
    private string KindWord => this.kind == TextKind.Note ? "note" : this.kind == TextKind.Snippet ? "snippet" : "comment";

    // The saved text this window edits.
    private string StoredText() {
      switch (this.kind) {
        case TextKind.Note: return this.shortcut.NoteText;
        case TextKind.Snippet: return this.shortcut.SnippetText;
        default: return this.shortcut.Comment;
      }
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
        this.ButtonPin.SetResourceReference(BackgroundProperty, "Theme.TitleButtonPressed");
      } else {
        this.ButtonPin.ClearValue(BackgroundProperty);
      }
    }

    // Ctrl + wheel up makes the text bigger, down smaller. Handled, so the text doesn't also scroll.
    private void TextComment_PreviewMouseWheel(object sender, MouseWheelEventArgs e) {
      if ((KeyboardModifiers() & ModifierKeys.Control) != ModifierKeys.Control || e.Delta == 0) {
        return;
      }
      e.Handled = true;
      long size = NoteWindowPlace.NextFontSize(TextSize, e.Delta);
      if (size == TextSize) {
        return;
      }
      SetTextSize(size);
      SaveTextSize();
    }

    // A note saves its size with its window, a comment with its button; the snippet editor's size is only for editing.
    private void SaveTextSize() {
      if (this.kind == TextKind.Note) {
        SaveNotePlace();
      } else if (this.kind == TextKind.Comment && this.shortcut.ID != 0) {
        try {
          this.shortcut.SaveCommentFontSize(this.textPoints);
        } catch (Exception ex) {
          // A size that can't be saved must never break the window.
          AppLog.Warning(ex, "Could not save the text size of a comment");
        }
      }
    }

    private long textPoints;

    /// <summary>The text size now, in whole points.</summary>
    private long TextSize => this.textPoints;

    // Sizes the text (WPF wants DIPs) and shows the size on the size button.
    private void SetTextSize(long points) {
      this.textPoints = points;
      this.TextComment.FontSize = NoteWindowPlace.ToDips(points);
      this.ButtonSize.Content = NoteWindowPlace.SizeLabel(points);
    }

    // ---- The text size button and menu ----

    /// <summary>The text of the menu item that goes back to the default size.</summary>
    public static readonly string DefaultSizeText = "Default (" + NoteWindowPlace.SizeLabel(NoteWindowPlace.DefaultFontSize) + ")";

    /// <summary>The size menu while it is open (or last open); null before the size button was clicked.</summary>
    public System.Windows.Controls.ContextMenu SizeMenu { get; private set; }

    // A note and a snippet have a size button left of the font button; a pick applies at once (a note saves it).
    private void UseSizeButton() {
      this.ButtonSize.Visibility = Visibility.Visible;
      this.ButtonSize.Click += (s, e) => {
        this.SizeMenu = BuildSizeMenu();
        this.SizeMenu.PlacementTarget = this.ButtonSize;
        this.SizeMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        this.SizeMenu.IsOpen = true;
      };
    }

    /// <summary>
    /// The size menu: a small "Text size" label, the preset sizes in points with a tick on the size in use (none for a
    /// size from the wheel that isn't a preset), then "Default (12 pt)".
    /// </summary>
    public System.Windows.Controls.ContextMenu BuildSizeMenu() {
      System.Windows.Controls.ContextMenu menu = new System.Windows.Controls.ContextMenu();
      menu.Items.Add(new System.Windows.Controls.MenuItem { Header = "Text size", IsEnabled = false, FontSize = 11 });
      long current = TextSize;
      foreach (long size in NoteWindowPlace.SizePresets) {
        menu.Items.Add(SizeItem(NoteWindowPlace.SizeLabel(size), size, size == current));
      }
      menu.Items.Add(new System.Windows.Controls.Separator());
      menu.Items.Add(SizeItem(DefaultSizeText, NoteWindowPlace.DefaultFontSize, false));
      return menu;
    }

    private System.Windows.Controls.MenuItem SizeItem(string header, long size, bool isChecked) {
      System.Windows.Controls.MenuItem item = new System.Windows.Controls.MenuItem { Header = header, IsChecked = isChecked };
      item.Click += (s, e) => PickSize(size);
      return item;
    }

    /// <summary>Uses a text size at once; a note and a comment save it, like the wheel does.</summary>
    public void PickSize(long size) {
      SetTextSize(Math.Clamp(size, NoteWindowPlace.MinFontSize, NoteWindowPlace.MaxFontSize));
      SaveTextSize();
    }

    // ---- The font button and menu ----

    /// <summary>The font menu while it is open (or last open); null before the font button was clicked.</summary>
    public System.Windows.Controls.ContextMenu FontMenu { get; private set; }

    // Every kind has a font button left of the other title bar buttons; a pick applies and saves at once.
    private void UseFontButton() {
      this.ButtonFont.Visibility = Visibility.Visible;
      this.ButtonFont.Click += (s, e) => {
        this.FontMenu = BuildFontMenu();
        this.FontMenu.PlacementTarget = this.ButtonFont;
        this.FontMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        this.FontMenu.IsOpen = true;
      };
      ApplyFont();
    }

    // A comment has its own font; a note and a snippet use the text font.
    private string CurrentFont => this.kind == TextKind.Comment ? this.shortcut.CommentFont : this.shortcut.TextFont;

    private void ApplyFont() {
      this.TextComment.FontFamily = NoteFontFamilies.Resolve(CurrentFont);
    }

    /// <summary>
    /// The font menu: "From Windows" and "Comes with MDSHO", each a small label over its fonts, each name drawn in its own
    /// font, with a check mark on the current one.
    /// </summary>
    public System.Windows.Controls.ContextMenu BuildFontMenu() {
      System.Windows.Controls.ContextMenu menu = new System.Windows.Controls.ContextMenu();
      AddFontGroup(menu, NoteFonts.FromWindowsLabel, NoteFonts.FromWindows);
      menu.Items.Add(new System.Windows.Controls.Separator());
      AddFontGroup(menu, NoteFonts.ComesWithMdshoLabel, NoteFonts.ComesWithMdsho);
      return menu;
    }

    private void AddFontGroup(System.Windows.Controls.ContextMenu menu, string label, IEnumerable<string> fonts) {
      menu.Items.Add(new System.Windows.Controls.MenuItem { Header = label, IsEnabled = false, FontSize = 11 });
      string current = NoteFonts.OrDefault(CurrentFont);
      foreach (string font in fonts) {
        System.Windows.Controls.MenuItem item = new System.Windows.Controls.MenuItem {
          Header = font,
          FontFamily = NoteFontFamilies.Resolve(font),
          FontSize = 14,
          IsChecked = font == current
        };
        string picked = font;
        item.Click += (s, e) => PickFont(picked);
        menu.Items.Add(item);
      }
    }

    /// <summary>Uses a font for this note, snippet or comment at once and saves it.</summary>
    public void PickFont(string font) {
      try {
        if (this.kind == TextKind.Comment) {
          this.shortcut.SaveCommentFont(font);
        } else {
          this.shortcut.SaveTextFont(font);
        }
      } catch (Exception ex) {
        AppLog.Warning(ex, "The font could not be saved");
        ErrorDialog.ShowDialog("The font could not be saved.", ex);
        return;
      }
      ApplyFont();
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
        this.ButtonWrap.SetResourceReference(BackgroundProperty, "Theme.TitleButtonPressed");
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

    // True when the note was deleted: the window closes without saving.
    private bool discarded;
    // True after a failed save showed its error, until a save works again: the error shows once, not on every change.
    private bool saveErrorShown;

    /// <summary>
    /// Saves the note, comment or snippet now if its text changed, and keeps the window open (autosave, and Ctrl+Enter or
    /// Ctrl+S in a note). Returns false when the save failed: the text then still counts as unsaved, so the next save or
    /// closing tries again.
    /// </summary>
    public bool SaveNoteNow() {
      this.noteSaveTimer.Stop();
      if (forgetting || this.discarded) {
        return true;
      }
      Exception error = SaveText();
      if (error == null) {
        return true;
      }
      if (!this.saveErrorShown) {
        this.saveErrorShown = true;
        ErrorDialog.ShowDialog("This " + KindWord + " could not be saved. MDSHO tries again when you type more and when you close it.", error);
      }
      return false;
    }

    // Saves the text if it changed. Returns null when saved (or nothing to save), else the error (logged).
    private Exception SaveText() {
      string value = TextToSave();
      if (value == (StoredText() ?? "")) {
        return null;
      }
      try {
        switch (this.kind) {
          case TextKind.Note: this.shortcut.SaveNoteText(value); break;
          case TextKind.Snippet: this.shortcut.SaveSnippetText(value); break;
          default: this.shortcut.SaveComment(value); break;
        }
        this.Saved = true;
        this.saveErrorShown = false;
        return null;
      } catch (Exception ex) {
        if (this.kind == TextKind.Snippet) {
          // Never the value: it can be a password.
          AppLog.Warning(null, "A snippet could not be saved: {ErrorType}", ex.GetType().FullName);
        } else {
          AppLog.Warning(ex, "A " + KindWord + " could not be saved");
        }
        return ex;
      }
    }

    // Closing (X or Esc) saves first. If that fails, asks before the typing is lost; No keeps the window open. At exit the
    // failures were already reported (SaveOpenNotesForExit), so it just closes.
    private void SaveBeforeClosing(CancelEventArgs e) {
      this.noteSaveTimer.Stop();
      if (forgetting || this.discarded) {
        return;
      }
      Exception error = SaveText();
      if (error == null || IsExiting) {
        return;
      }
      string question = "This " + KindWord + " could not be saved: " + error.Message + "\n\nClose it anyway? What you typed since the last save will be lost.";
      bool close;
      using (Dialogs.UseIcons(this.icons.SolidIcons)) {
        close = Dialogs.Confirm(question, char.ToUpperInvariant(KindWord[0]) + KindWord.Substring(1) + " not saved");
      }
      if (!close) {
        e.Cancel = true;
      }
    }

    private void ShowIcons(IconGlyphs icons) {
      string glyph = this.kind == TextKind.Note ? icons.Note : this.kind == TextKind.Snippet ? icons.Snippet : icons.Comment;
      this.TextTitleIcon.Text = glyph;
      this.ButtonClose.Content = icons.Times;
      this.ButtonColors.Content = icons.Brush;
      this.ButtonFont.Content = icons.Font;
      this.ButtonWrap.Content = icons.WrapText;
      this.ButtonPin.Content = this.Topmost ? icons.PinOn : icons.PinOff;
      this.Icon = GlyphIcon.Create(glyph);
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
        // A comment or snippet closes (closing saves it).
        e.Handled = true;
        Close();
      }
    }
  }
}
