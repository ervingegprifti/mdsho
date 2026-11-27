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
      UseHighlights();
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
      UseHistory();
      UseTitleMenu();

      // The title icon, the title bar buttons and the window icon follow the window's solid or outline setting while open.
      IconGlyphs icons = this.icons;
      ShowIcons(icons);
      System.ComponentModel.PropertyChangedEventHandler iconsChanged = (s, e) => ShowIcons(icons);
      icons.PropertyChanged += iconsChanged;
      this.Closed += (s, e) => icons.PropertyChanged -= iconsChanged;

      this.ButtonClose.Click += (s, e) => Close();
      this.ButtonPin.Click += (s, e) => TogglePinned();
      // A comment and a snippet keep their wrap with their button; a note with its window place.
      SetWrapText(kind != TextKind.Note && ViewOptions.WrapText);
      this.ButtonWrap.Click += (s, e) => ToggleWrapText();
      // Ctrl + mouse wheel sizes the text; a note remembers the size with its window, a comment and a snippet with their button.
      SetTextSize(kind == TextKind.Comment ? NoteWindowPlace.FontSizeOrDefault(shortcut.CommentFontSize)
        : kind == TextKind.Snippet ? NoteWindowPlace.FontSizeOrDefault(shortcut.TextFontSize) : NoteWindowPlace.DefaultFontSize);
      this.TextComment.PreviewMouseWheel += TextComment_PreviewMouseWheel;
      this.PreviewKeyDown += Window_PreviewKeyDown;
      this.TextComment.PreviewMouseLeftButtonDown += TextComment_PreviewMouseLeftButtonDown;
      this.TextComment.PreviewMouseMove += TextComment_PreviewMouseMove;
      this.TextComment.PreviewMouseLeftButtonUp += TextComment_PreviewMouseLeftButtonUp;
      this.TextComment.LostMouseCapture += (s, e) => this.paddingAnchor = -1;
      this.TextComment.LostKeyboardFocus += (s, e) => this.paddingAnchor = -1;
      this.Loaded += (s, e) => {
        this.TextComment.Focus();
        this.TextComment.CaretIndex = this.TextComment.Text.Length;
      };
    }

    // ---- The title bar menu ----

    /// <summary>True for a character of the icon font (the private use area); other text shows in Segoe UI.</summary>
    public static bool IsIconFontGlyph(string glyph) {
      return !string.IsNullOrEmpty(glyph) && glyph[0] >= '' && glyph[0] <= '';
    }

    // The More options button shows IconGlyphs.More, the three dots in Segoe UI, smaller so they look as big as the other
    // icons. A glyph of the icon font would show as a 16 px icon like the other buttons.
    private void ShowMoreIcon(string glyph) {
      this.ButtonMore.Content = glyph;
      if (IsIconFontGlyph(glyph)) {
        this.ButtonMore.ClearValue(FontFamilyProperty);
        this.ButtonMore.ClearValue(FontSizeProperty);
        this.ButtonMore.ClearValue(ContentTemplateProperty);
      } else {
        this.ButtonMore.FontFamily = new System.Windows.Media.FontFamily("Segoe UI");
        this.ButtonMore.FontSize = 11;
        this.ButtonMore.ContentTemplate = null;
      }
    }

    /// <summary>Opens the title menu below More options, right edges lined up, with the button pressed while it is open (the button and Alt+F).</summary>
    public void OpenMoreMenu() {
      System.Windows.Controls.ContextMenu menu = BuildTitleMenu();
      menu.PlacementTarget = this.ButtonMore;
      menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Custom;
      menu.CustomPopupPlacementCallback = (popup, target, offset) => new[] {
        // Below the button, right edges lined up; above it when there is no room below.
        new System.Windows.Controls.Primitives.CustomPopupPlacement(new Point(target.Width - popup.Width, target.Height), System.Windows.Controls.Primitives.PopupPrimaryAxis.Horizontal),
        new System.Windows.Controls.Primitives.CustomPopupPlacement(new Point(target.Width - popup.Width, -popup.Height), System.Windows.Controls.Primitives.PopupPrimaryAxis.Horizontal)
      };
      this.ButtonMore.SetResourceReference(BackgroundProperty, "Theme.TitleButtonPressed");
      // Not pressed once the menu closes, however it closes (Closed only comes from a popup that really showed).
      DependencyPropertyDescriptor isOpen = DependencyPropertyDescriptor.FromProperty(System.Windows.Controls.ContextMenu.IsOpenProperty, typeof(System.Windows.Controls.ContextMenu));
      EventHandler closed = null;
      closed = (s, e) => {
        if (!menu.IsOpen) {
          isOpen.RemoveValueChanged(menu, closed);
          this.ButtonMore.ClearValue(BackgroundProperty);
        }
      };
      isOpen.AddValueChanged(menu, closed);
      this.TitleMenu = menu;
      WatchPreviews(menu);
      menu.IsOpen = true;
    }

    /// <summary>The title bar's menu while it is open (or last open); null before it was opened.</summary>
    public System.Windows.Controls.ContextMenu TitleMenu { get; private set; }

    private const int WM_NCRBUTTONUP = 0x00A5;
    private const int HTCAPTION = 2;

    // A right-click on the title bar opens the title menu. The title text takes the mouse itself (for renaming), so it gets
    // the menu the normal way; the rest of the title bar is the window's caption, where Windows would show its own menu.
    private void UseTitleMenu() {
      this.ButtonMore.Click += (s, e) => OpenMoreMenu();
      this.TitleBar.ContextMenu = new System.Windows.Controls.ContextMenu();
      this.TitleBar.ContextMenuOpening += (s, e) => {
        this.TitleMenu = BuildTitleMenu();
        WatchPreviews(this.TitleMenu);
        this.TitleBar.ContextMenu = this.TitleMenu;
      };
      this.SourceInitialized += (s, e) => {
        // Added after WindowChrome's own hook, so it is asked first.
        (PresentationSource.FromVisual(this) as System.Windows.Interop.HwndSource)?.AddHook(TitleWndProc);
      };
    }

    private IntPtr TitleWndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) {
      if (HandleTitleMessage(msg, wParam)) {
        handled = true;
      }
      return IntPtr.Zero;
    }

    /// <summary>A right-click released on the caption opens the title menu instead of the Windows menu; true when handled.</summary>
    public bool HandleTitleMessage(int msg, IntPtr wParam) {
      if (msg != WM_NCRBUTTONUP || wParam.ToInt64() != HTCAPTION) {
        return false;
      }
      OpenTitleMenu();
      return true;
    }

    /// <summary>Opens the title menu at the mouse.</summary>
    public void OpenTitleMenu() {
      this.TitleMenu = BuildTitleMenu();
      this.TitleMenu.PlacementTarget = this.TitleBar;
      this.TitleMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
      WatchPreviews(this.TitleMenu);
      this.TitleMenu.IsOpen = true;
    }

    /// <summary>
    /// The title menu (More options), only the items that apply: every command of the title bar buttons and the ones used less
    /// often. Text history (the history list), Wrap text, Highlight current line, Keep on top (a note), Text size (the sizes),
    /// Font (the font list), Note colours... (a note), Highlight whole words only, Highlight matching case only, Clear all
    /// highlights (a note; greyed out without any) and Close. The on/off items have a tick while on.
    /// </summary>
    public System.Windows.Controls.ContextMenu BuildTitleMenu() {
      bool note = this.kind == TextKind.Note;
      System.Windows.Controls.ContextMenu menu = new System.Windows.Controls.ContextMenu();
      System.Windows.Controls.MenuItem history = TitleItem("Text history", "History", this.icons.Time, null);
      MoveItems(BuildHistoryMenu(), history, skipTag: "Header");
      menu.Items.Add(history);
      menu.Items.Add(new System.Windows.Controls.Separator());
      menu.Items.Add(TitleToggle("Wrap text", "Wrap", this.icons.WrapText, this.IsWrapText, ToggleWrapText));
      menu.Items.Add(TitleToggle("Highlight current line", "CurrentLine", "", this.highlighter.CurrentLine, ToggleCurrentLine));
      if (note) {
        menu.Items.Add(TitleToggle("Keep on top", "Pin", this.Topmost ? this.icons.PinOn : this.icons.PinOff, this.Topmost, TogglePinned));
      }
      // Text size: the size button's sizes with a tick on the current one, then Default (12 pt); Ctrl + wheel also sizes.
      System.Windows.Controls.MenuItem size = new System.Windows.Controls.MenuItem { Header = "Text size", Tag = "Size" };
      MoveItems(BuildSizeMenu(), size, skipTag: "Header");
      menu.Items.Add(size);
      System.Windows.Controls.MenuItem font = TitleItem("Font", "Font", this.icons.Font, null);
      MoveItems(BuildFontMenu(), font, skipTag: null);
      menu.Items.Add(font);
      if (note) {
        menu.Items.Add(TitleItem("Note colours...", "Colours", this.icons.Brush, ShowNoteColours));
      }
      menu.Items.Add(new System.Windows.Controls.Separator());
      System.Windows.Controls.MenuItem wholeWords = TitleToggle("Highlight whole words only", "WholeWords", "", this.highlighter.Options.WholeWords, ToggleWholeWords);
      wholeWords.ToolTip = WholeWordsToolTip;
      menu.Items.Add(wholeWords);
      System.Windows.Controls.MenuItem matchCase = TitleToggle("Highlight matching case only", "MatchCase", "", this.highlighter.Options.MatchCase, ToggleMatchCase);
      matchCase.ToolTip = MatchCaseToolTip;
      menu.Items.Add(matchCase);
      if (note) {
        System.Windows.Controls.MenuItem clear = TitleItem("Clear all highlights", "ClearHighlights", this.icons.Delete, ClearHighlights);
        clear.IsEnabled = NoteHighlights.Parse(this.shortcut.NoteHighlights).Count > 0;
        menu.Items.Add(clear);
      }
      menu.Items.Add(new System.Windows.Controls.Separator());
      menu.Items.Add(TitleItem("Close", "Close", this.icons.Times, Close));
      return menu;
    }

    private System.Windows.Controls.MenuItem TitleItem(string header, string tag, string glyph, Action click) {
      System.Windows.Controls.MenuItem item = new System.Windows.Controls.MenuItem {
        Header = header, Tag = tag,
        Icon = new System.Windows.Controls.TextBlock { Text = glyph, Style = TryFindResource("StyleMenuItemFontIcon") as Style }
      };
      if (click != null) {
        item.Click += (s, e) => click();
      }
      return item;
    }

    // An item that is on or off: a tick while on (in place of its icon, like the Windows menus).
    private System.Windows.Controls.MenuItem TitleToggle(string header, string tag, string glyph, bool on, Action click) {
      System.Windows.Controls.MenuItem item = TitleItem(header, tag, glyph, click);
      item.IsChecked = on;
      if (on) {
        item.Icon = null;
      }
      return item;
    }

    // Moves the items of a menu built for a title bar button into a submenu of the title menu.
    private static void MoveItems(System.Windows.Controls.ContextMenu from, System.Windows.Controls.MenuItem into, string skipTag) {
      List<object> items = from.Items.Cast<object>().ToList();
      from.Items.Clear();
      foreach (object item in items) {
        if (skipTag != null && item is System.Windows.Controls.MenuItem m && m.Tag as string == skipTag) {
          continue;
        }
        into.Items.Add(item);
      }
    }

    /// <summary>Removes every saved colour highlight of this note.</summary>
    public void ClearHighlights() {
      if (this.kind == TextKind.Note && this.shortcut.NoteHighlights.Length > 0) {
        SaveHighlights("");
      }
    }

    /// <summary>The tooltip of Highlight whole words only.</summary>
    public const string WholeWordsToolTip = "When on, selecting “art” won't light up “start”.";
    /// <summary>The tooltip of Highlight matching case only.</summary>
    public const string MatchCaseToolTip = "When on, selecting “art” won't light up “Art”.";

    // The saved More options settings of this window: the comment's, or the note's or snippet's text.
    private TextViewOptions ViewOptions => this.kind == TextKind.Comment ? this.shortcut.CommentViewOptions : this.shortcut.TextViewOptions;

    // What this window shows now (a note's wrap is kept with its window place, so the saved one stays).
    private TextViewOptions ShownViewOptions => new TextViewOptions(this.highlighter.CurrentLine, this.highlighter.Options.WholeWords,
      this.highlighter.Options.MatchCase, this.kind == TextKind.Note ? ViewOptions.WrapText : this.IsWrapText);

    private void ToggleCurrentLine() => SaveViewOptions(ShownViewOptions with { CurrentLine = !this.highlighter.CurrentLine });

    private void ToggleWholeWords() => SaveViewOptions(ShownViewOptions with { WholeWords = !this.highlighter.Options.WholeWords });

    private void ToggleMatchCase() => SaveViewOptions(ShownViewOptions with { MatchCase = !this.highlighter.Options.MatchCase });

    // Shows the settings, then saves them with the button (one that can't be saved still applies to this window).
    private void SaveViewOptions(TextViewOptions options) {
      ShowViewOptions(options);
      try {
        this.shortcut.SaveViewOptions(this.kind == TextKind.Comment, options);
      } catch (Exception ex) {
        AppLog.Warning(ex, "The More options settings could not be saved");
      }
    }

    // Current line and the match options redraw the highlights at once; a comment or snippet also wraps.
    private void ShowViewOptions(TextViewOptions options) {
      this.highlighter.CurrentLine = options.CurrentLine;
      this.highlighter.Options = new MatchOptions(options.WholeWords, options.MatchCase);
      if (this.kind != TextKind.Note && this.IsWrapText != options.WrapText) {
        SetWrapText(options.WrapText);
      }
    }

    // Keep on top (the pin button and the title menu); a note remembers it with its window.
    private void TogglePinned() {
      SetPinned(!this.Topmost);
      SaveNotePlace();
    }

    // Wrap text (the button and the title menu): a note remembers it with its window, a comment or snippet with its button.
    private void ToggleWrapText() {
      if (this.kind == TextKind.Note) {
        SetWrapText(!this.IsWrapText);
        SaveNotePlace();
      } else {
        SaveViewOptions(ShownViewOptions with { WrapText = !this.IsWrapText });
      }
    }

    private void ShowTitle() {
      this.Title = this.shortcut.Name;
      this.TextTitle.Text = this.Title;
    }

    // ---- Highlights ----

    private TextHighlighter highlighter;

    /// <summary>The other places with the selected text, softly highlighted (not the selection itself).</summary>
    public IReadOnlyList<(int Start, int Length)> LiveMatches => this.highlighter.LiveMatches;

    /// <summary>Every match of the note's saved terms with its colour, term by term (none for a comment or snippet).</summary>
    public IReadOnlyList<(int Start, int Length, string Color)> SavedMatches => this.highlighter.SavedMatches;

    // A note's saved colour highlights: they follow its saved terms, and its text menu can add and remove them.
    // Comments and snippets keep the plain text menu of Windows.
    private void UseSavedHighlights() {
      if (this.kind != TextKind.Note) {
        return;
      }
      this.highlighter.SavedTerms = this.shortcut.NoteHighlights;
      PropertyChangedEventHandler changed = (s, e) => {
        if (e.PropertyName == nameof(ShortcutViewModel.NoteHighlights)) {
          this.highlighter.SavedTerms = this.shortcut.NoteHighlights;
        }
      };
      this.shortcut.PropertyChanged += changed;
      this.Closed += (s, e) => this.shortcut.PropertyChanged -= changed;
      this.TextComment.ContextMenu = BuildTextMenu();
      // Built again each time, so it shows the selection's colour and what can be removed.
      this.TextComment.ContextMenuOpening += (s, e) => this.TextComment.ContextMenu = BuildTextMenu();
    }

    /// <summary>
    /// A note's text menu: Cut, Copy, Paste, then Highlight (the five colours, a tick on the selection's colour; needs a
    /// selection of at least 2 letters) and Remove highlight (the saved term selected or around the caret). Null for a
    /// comment or snippet.
    /// </summary>
    public System.Windows.Controls.ContextMenu BuildTextMenu() {
      if (this.kind != TextKind.Note) {
        return null;
      }
      System.Windows.Controls.ContextMenu menu = new System.Windows.Controls.ContextMenu();
      menu.Items.Add(CommandItem("Cut", ApplicationCommands.Cut, this.icons.Cut));
      menu.Items.Add(CommandItem("Copy", ApplicationCommands.Copy, this.icons.Copy));
      menu.Items.Add(CommandItem("Paste", ApplicationCommands.Paste, this.icons.Paste));
      menu.Items.Add(new System.Windows.Controls.Separator());
      string selected = this.TextComment.SelectedText;
      SavedHighlight current = TextMatches.IsSearchable(selected) ? NoteHighlights.Find(this.shortcut.NoteHighlights, selected) : null;
      System.Windows.Controls.MenuItem highlight = new System.Windows.Controls.MenuItem {
        Header = "Highlight", Tag = "Highlight", IsEnabled = TextMatches.IsSearchable(selected), Icon = Swatch(current?.Color ?? HighlightColors.All[0])
      };
      foreach (string color in HighlightColors.All) {
        System.Windows.Controls.MenuItem item = new System.Windows.Controls.MenuItem {
          Header = color, Tag = color, IsChecked = current?.Color == color, Icon = Swatch(color)
        };
        string picked = color;
        item.Click += (s, e) => PickHighlight(picked);
        highlight.Items.Add(item);
      }
      menu.Items.Add(highlight);
      System.Windows.Controls.MenuItem remove = new System.Windows.Controls.MenuItem {
        Header = "Remove highlight", Tag = "RemoveHighlight", Icon = Swatch(null),
        IsEnabled = NoteHighlights.TermAt(this.TextComment.Text, this.shortcut.NoteHighlights, this.TextComment.SelectionStart, this.TextComment.SelectionLength, this.highlighter.Options) != null
      };
      remove.Click += (s, e) => RemoveHighlight();
      menu.Items.Add(remove);
      return menu;
    }

    private System.Windows.Controls.MenuItem CommandItem(string header, RoutedUICommand command, string glyph) {
      return new System.Windows.Controls.MenuItem {
        Header = header, Tag = header, Command = command, CommandTarget = this.TextComment,
        Icon = new System.Windows.Controls.TextBlock { Text = glyph, Style = TryFindResource("StyleMenuItemFontIcon") as Style }
      };
    }

    // A small square of a highlight colour for the menu; an empty one (just the edge) for null.
    private static System.Windows.Controls.Border Swatch(string color) {
      return new System.Windows.Controls.Border {
        Width = 14, Height = 14, CornerRadius = new CornerRadius(2), BorderThickness = new Thickness(1),
        BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x59, 0, 0, 0)),
        Background = color == null ? System.Windows.Media.Brushes.Transparent : new System.Windows.Media.SolidColorBrush(TextHighlighter.SwatchColor(color))
      };
    }

    /// <summary>Saves the selected text as a highlight in this colour (a saved term changes colour); it colours every match.</summary>
    public void PickHighlight(string color) {
      string selected = this.TextComment.SelectedText;
      if (this.kind != TextKind.Note || !TextMatches.IsSearchable(selected)) {
        return;
      }
      SaveHighlights(NoteHighlights.With(this.shortcut.NoteHighlights, selected, color));
    }

    /// <summary>Removes the saved highlight whose term is selected, or whose match is around the caret.</summary>
    public void RemoveHighlight() {
      SavedHighlight term = NoteHighlights.TermAt(this.TextComment.Text, this.shortcut.NoteHighlights, this.TextComment.SelectionStart, this.TextComment.SelectionLength, this.highlighter.Options);
      if (this.kind == TextKind.Note && term != null) {
        SaveHighlights(NoteHighlights.Without(this.shortcut.NoteHighlights, term.Term));
      }
    }

    // Saves the highlights at once, apart from the text. Never the terms in the log: they are the note's own words.
    private void SaveHighlights(string stored) {
      try {
        this.shortcut.SaveNoteHighlights(stored);
      } catch (Exception ex) {
        AppLog.Warning(null, "The highlights of a note could not be saved: {ErrorType}", ex.GetType().FullName);
        ErrorDialog.ShowDialog("The highlights could not be saved.", ex);
      }
    }

    // While text is selected, every other place with the same text is highlighted and the title bar shows the count.
    private void UseHighlights() {
      this.highlighter = new TextHighlighter(this.TextComment, this.HighlightLayer, this.LineLayer);
      // Highlight current line and the match options are saved with the button (the comment apart from the text).
      ShowViewOptions(ViewOptions);
      string optionsName = this.kind == TextKind.Comment ? nameof(ShortcutViewModel.CommentViewOptions) : nameof(ShortcutViewModel.TextViewOptions);
      PropertyChangedEventHandler optionsChanged = (s, e) => {
        if (e.PropertyName == optionsName) {
          ShowViewOptions(ViewOptions);
        }
      };
      this.shortcut.PropertyChanged += optionsChanged;
      this.Closed += (s, e) => this.shortcut.PropertyChanged -= optionsChanged;
      this.highlighter.Changed += () => {
        string label = TextMatches.CountLabel(this.highlighter.MatchCount);
        this.TextMatchCount.Text = label;
        this.TextMatchCount.Visibility = label.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
      };
      UseSavedHighlights();
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

    /// <summary>
    /// The buttons whose note, comment or snippet window has unsaved typing now: sync leaves them for a later round, so
    /// nothing is changed under the user. An open window with nothing typed since its last save takes synced text live.
    /// </summary>
    public static HashSet<long> UnsavedTextShortcutIds() {
      HashSet<long> ids = new HashSet<long>();
      foreach (TextEditWindow window in AllOpen()) {
        if (window.shortcut != null && window.HasUnsavedTyping) {
          ids.Add(window.shortcut.ID);
        }
      }
      return ids;
    }

    /// <summary>
    /// After a sync round: every open window with nothing typed since its last save shows its button's saved text (see
    /// <see cref="TakeSyncedText"/>). A window whose button is gone is left to close on its own. Never throws.
    /// </summary>
    public static void ShowSyncedTexts() {
      foreach (TextEditWindow window in AllOpen()) {
        try {
          if (window.shortcut == null || window.shortcut.ID == 0) {
            continue;
          }
          Shortcut row = DataStore.Shortcuts.Get(window.shortcut.ID);
          if (row == null) {
            continue;
          }
          window.TakeSyncedText(window.kind == TextKind.Note ? row.NoteText : window.kind == TextKind.Snippet ? row.SnippetText : row.Comment);
        } catch (Exception ex) {
          AppLog.Warning(ex, "A synced text could not be shown in its open window");
        }
      }
    }

    /// <summary>Every open note, comment and snippet window.</summary>
    public static IReadOnlyList<TextEditWindow> AllOpen() {
      List<TextEditWindow> all = new List<TextEditWindow>(openNoteWindows);
      if (Application.Current != null) {
        all.AddRange(Application.Current.Windows.OfType<TextEditWindow>().Where(w => !all.Contains(w)));
      }
      return all;
    }

    /// <summary>True when something was typed since the last save (its autosave is still waiting, or the text differs).</summary>
    public bool HasUnsavedTyping => this.noteSaveTimer.IsEnabled || TextToSave() != (StoredText() ?? "");

    /// <summary>
    /// Shows a text another PC sent, already saved by sync, when nothing was typed since the last save: the old text goes
    /// into Text history like any change, and the caret and scroll stay where they were as far as the text allows. Returns
    /// false (and changes nothing) when there is unsaved typing.
    /// </summary>
    public bool TakeSyncedText(string text) {
      text ??= "";
      if (forgetting || this.discarded || HasUnsavedTyping) {
        return false;
      }
      string shown = this.TextComment.Text ?? "";
      if (text == shown) {
        return true;
      }
      KeepHistoryOnLeave();
      TryHistory(() => KeepHistory(shown));
      this.shortcut.ShowSyncedText(this.kind == TextKind.Note ? ShortcutTextKind.Note : this.kind == TextKind.Snippet ? ShortcutTextKind.Snippet : ShortcutTextKind.Comment, text);
      int caret = this.TextComment.CaretIndex;
      double top = this.TextComment.VerticalOffset;
      double left = this.TextComment.HorizontalOffset;
      this.pickingHistory = true;
      try {
        this.TextComment.Text = text;
        this.TextComment.CaretIndex = Math.Min(caret, text.Length);
        this.TextComment.ScrollToVerticalOffset(top);
        this.TextComment.ScrollToHorizontalOffset(left);
      } finally {
        this.pickingHistory = false;
      }
      // The text came saved: nothing for the autosave to do.
      this.noteSaveTimer.Stop();
      this.historyUndo = null;
      this.lastPick = null;
      return true;
    }

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
        if (place != null && place.Width > 0) {
          window.UseSavedPlace(place);
        } else {
          PlaceNoteWindow(window);
          // A row that sync made has the note's text size and wrap from another PC, but no place on this PC yet.
          if (place != null) {
            window.SetWrapText(place.IsWrapText);
            window.SetTextSize(NoteWindowPlace.FontSizeOrDefault(place.FontSize));
          }
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
      // Exiting leaves every window: a run of edits keeps the text from before it.
      foreach (TextEditWindow window in openTextWindows.Concat(openNoteWindows).ToList()) {
        if (!window.discarded) {
          window.KeepHistoryOnLeave();
        }
      }
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
    /// A note has pin (keep on top) and close in its title bar and no bottom bar, so the text gets the whole window. Its
    /// colours are in the More options menu, since they aren't changed every day.
    /// </summary>
    private void UseNoteTitleBar() {
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
      foreach (System.Windows.Controls.Button button in new[] { this.ButtonSize, this.ButtonFont, this.ButtonWrap, this.ButtonMore, this.ButtonClose }) {
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
      foreach (System.Windows.Controls.Button button in new[] { this.ButtonSize, this.ButtonFont, this.ButtonWrap, this.ButtonPin, this.ButtonMore, this.ButtonClose }) {
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

    // A note saves its size with its window, a comment and a snippet with their button.
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
      } else if (this.kind == TextKind.Snippet && this.shortcut.ID != 0) {
        try {
          this.shortcut.SaveTextFontSize(this.textPoints);
        } catch (Exception ex) {
          AppLog.Warning(ex, "Could not save the text size of a snippet");
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
      this.highlighter?.QueueDraw();
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
        WatchPreviews(this.SizeMenu);
        this.SizeMenu.IsOpen = true;
      };
    }

    /// <summary>
    /// The size menu: a small "Text size" label, the preset sizes in points with a tick on the size in use (none for a
    /// size from the wheel that isn't a preset), then "Default (12 pt)".
    /// </summary>
    public System.Windows.Controls.ContextMenu BuildSizeMenu() {
      System.Windows.Controls.ContextMenu menu = new System.Windows.Controls.ContextMenu();
      menu.Items.Add(new System.Windows.Controls.MenuItem { Header = "Text size", Tag = "Header", IsEnabled = false, FontSize = 11 });
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
      this.previews.AddOrUpdate(item, () => PreviewSize(size));
      return item;
    }

    // ---- Preview in the Text size and Font menus ----

    // What a size or font item shows while it is highlighted.
    private readonly System.Runtime.CompilerServices.ConditionalWeakTable<System.Windows.Controls.MenuItem, Action> previews = new();

    // The item whose size or font the text shows now; null when it shows its own.
    private System.Windows.Controls.MenuItem previewItem;

    private static readonly DependencyPropertyDescriptor IsHighlightedDescriptor =
      DependencyPropertyDescriptor.FromProperty(System.Windows.Controls.MenuItem.IsHighlightedProperty, typeof(System.Windows.Controls.MenuItem));

    // While the menu is open, a highlighted size or font item (mouse or arrow keys) shows on the text. Leaving it, or
    // closing the menu, puts the text's own size and font back; a click has saved the new one by then.
    private void WatchPreviews(System.Windows.Controls.ContextMenu menu) {
      List<(System.Windows.Controls.MenuItem Item, EventHandler Handler)> watched = new();
      foreach (System.Windows.Controls.MenuItem item in MenuItems(menu)) {
        if (this.previews.TryGetValue(item, out Action preview)) {
          EventHandler handler = (s, e) => {
            if (item.IsHighlighted) {
              this.previewItem = item;
              preview();
            } else if (this.previewItem == item) {
              EndPreview();
            }
          };
          IsHighlightedDescriptor.AddValueChanged(item, handler);
          watched.Add((item, handler));
        }
      }
      DependencyPropertyDescriptor isOpen = DependencyPropertyDescriptor.FromProperty(System.Windows.Controls.ContextMenu.IsOpenProperty, typeof(System.Windows.Controls.ContextMenu));
      EventHandler closed = null;
      closed = (s, e) => {
        if (!menu.IsOpen) {
          isOpen.RemoveValueChanged(menu, closed);
          foreach ((System.Windows.Controls.MenuItem item, EventHandler handler) in watched) {
            IsHighlightedDescriptor.RemoveValueChanged(item, handler);
          }
          EndPreview();
        }
      };
      isOpen.AddValueChanged(menu, closed);
    }

    private static IEnumerable<System.Windows.Controls.MenuItem> MenuItems(System.Windows.Controls.ItemsControl parent) {
      foreach (System.Windows.Controls.MenuItem item in parent.Items.OfType<System.Windows.Controls.MenuItem>()) {
        yield return item;
        foreach (System.Windows.Controls.MenuItem child in MenuItems(item)) {
          yield return child;
        }
      }
    }

    // Shows a size on the text only: the size button and the saved size stay.
    private void PreviewSize(long points) {
      this.TextComment.FontSize = NoteWindowPlace.ToDips(Math.Clamp(points, NoteWindowPlace.MinFontSize, NoteWindowPlace.MaxFontSize));
      this.highlighter?.QueueDraw();
    }

    private void PreviewFont(string font) {
      this.TextComment.FontFamily = NoteFontFamilies.Resolve(font);
      this.highlighter?.QueueDraw();
    }

    // The text shows its own size and font again.
    private void EndPreview() {
      this.previewItem = null;
      this.TextComment.FontSize = NoteWindowPlace.ToDips(this.textPoints);
      ApplyFont();
    }

    /// <summary>Uses a text size at once; a note and a comment save it, like the wheel does.</summary>
    public void PickSize(long size) {
      SetTextSize(Math.Clamp(size, NoteWindowPlace.MinFontSize, NoteWindowPlace.MaxFontSize));
      SaveTextSize();
    }

    // ---- The text history ----

    /// <summary>The clock for the times the text history shows (labels only). Tests replace it.</summary>
    public static Func<DateTime> Now = () => DateTime.Now;

    // The text before the latest change.
    private string historyLastText = "";
    // True while a pick from the history puts its text in the window: that change keeps no copy.
    private bool pickingHistory;
    // The current run of edits: the text before its first change (null when no run is going on), whether a copy for it
    // was already kept, and whether it keeps its copy at once (only the first run after the window opens does).
    private string runStart;
    private bool runCovered;
    private bool runAtOnce;
    // True once any run started in this window.
    private bool anyRun;
    // The copies the latest keep added and the text they were kept from: going back to exactly that text (a letter typed
    // and deleted, an undone big removal) takes them back and puts the run as it was.
    private HistoryUndo historyUndo;
    // The copy the last pick put in the window (its text and its own time), until the text changes, the history is
    // cleared or the window closes: picking again then puts it back in its place instead of keeping it as a new copy.
    private HistoryEntry lastPick;

    private sealed class HistoryUndo {
      public List<HistoryEntry> Rows;
      public string Text;
      public string RunStart;
      public bool RunCovered;
      public bool RunAtOnce;
      public bool AnyRun;
    }

    // A comment keeps its own history; a note and a snippet keep theirs as the text history.
    private string HistoryField => this.kind == TextKind.Comment ? HistoryFields.Comment : HistoryFields.Text;

    // Every kind keeps earlier copies of its text, with no timers: at the first change after the window opens, when the
    // window is left after a change (the text from before that run of edits), and at once for an edit that removes a lot.
    private void UseHistory() {
      this.historyLastText = this.TextComment.Text ?? "";
      this.TextComment.TextChanged += (s, e) => {
        string before = this.historyLastText;
        string text = this.TextComment.Text ?? "";
        this.historyLastText = text;
        if (this.pickingHistory || this.shortcut.ID == 0) {
          return;
        }
        this.lastPick = null;
        OnHistoryChange(before, text, e.Changes.Sum(c => c.RemovedLength));
      };
      // Leaving the window ends the run of edits: focus moves away, it is minimised or closed, or MDSHO exits.
      this.Deactivated += (s, e) => KeepHistoryOnLeave();
      this.StateChanged += (s, e) => {
        if (this.WindowState == WindowState.Minimized) {
          KeepHistoryOnLeave();
        }
      };
      this.Closing += (s, e) => {
        KeepHistoryOnLeave();
        this.lastPick = null;
      };
    }

    private void OnHistoryChange(string before, string text, int removed) {
      if (this.historyUndo != null && text == this.historyUndo.Text) {
        // Back to the very text that was kept: take its copies back, so no copy equals the text, and put the run back.
        HistoryUndo undo = this.historyUndo;
        this.historyUndo = null;
        foreach (HistoryEntry row in undo.Rows) {
          TryHistory(() => this.shortcut.DeleteHistory(row.ID));
        }
        this.runStart = undo.RunStart;
        this.runCovered = undo.RunCovered;
        this.runAtOnce = undo.RunAtOnce;
        this.anyRun = undo.AnyRun;
        return;
      }
      if (TextHistory.IsBigRemoval(removed, before.Length)) {
        // A big removal keeps the text from before it at once, after the start of this run when no copy has it yet.
        HistoryUndo undo = Snapshot(before);
        if (this.runStart != null && !this.runCovered && this.runStart != before) {
          AddKept(undo, this.runStart);
        }
        AddKept(undo, before);
        this.historyUndo = undo.Rows.Count > 0 ? undo : null;
        if (this.runStart == null) {
          this.runStart = before;
          this.runAtOnce = false;
        }
        this.runCovered = true;
        this.anyRun = true;
        return;
      }
      if (this.runStart == null) {
        this.runStart = before;
        this.runCovered = false;
        this.runAtOnce = !this.anyRun;
        this.anyRun = true;
      }
      if (this.runAtOnce && !this.runCovered && text != this.runStart) {
        // The first run after the window opens keeps its start as soon as the text really differs from it.
        HistoryUndo undo = Snapshot(this.runStart);
        AddKept(undo, this.runStart);
        this.historyUndo = undo.Rows.Count > 0 ? undo : null;
        this.runCovered = true;
      }
    }

    private HistoryUndo Snapshot(string text) {
      return new HistoryUndo {
        Rows = new List<HistoryEntry>(), Text = text, RunStart = this.runStart, RunCovered = this.runCovered,
        RunAtOnce = this.runAtOnce, AnyRun = this.anyRun
      };
    }

    private void AddKept(HistoryUndo undo, string text) {
      HistoryEntry kept = KeepHistory(text);
      if (kept != null) {
        undo.Rows.Add(kept);
      }
    }

    /// <summary>
    /// The window was left: when a run of edits has no copy yet, keeps the text from before that run (never a copy equal
    /// to the text now or to the newest copy), and ends the run.
    /// </summary>
    public void KeepHistoryOnLeave() {
      if (this.shortcut.ID != 0 && this.runStart != null && !this.runCovered && this.runStart != TextToSave()) {
        KeepHistory(this.runStart);
      }
      this.runStart = null;
      this.runCovered = false;
      this.runAtOnce = false;
      this.historyUndo = null;
    }

    private HistoryEntry KeepHistory(string text) {
      HistoryEntry kept = null;
      TryHistory(() => kept = this.shortcut.KeepHistory(HistoryField, text, Now()));
      return kept;
    }

    // A copy that can't be kept or taken back must never break typing; the text itself still saves. Never the text in the log.
    private static void TryHistory(Action action) {
      try {
        action();
      } catch (Exception ex) {
        AppLog.Warning(null, "The text history could not be saved: {ErrorType}", ex.GetType().FullName);
      }
    }

    /// <summary>
    /// The history menu: a small "Text history" label, Now (ticked) with its first line, a separator, the copies newest
    /// first ("Today 14:05:09  ·  first line"), then a separator and Clear history... (greyed out when there are none).
    /// </summary>
    public System.Windows.Controls.ContextMenu BuildHistoryMenu() {
      System.Windows.Controls.ContextMenu menu = new System.Windows.Controls.ContextMenu();
      menu.Items.Add(new System.Windows.Controls.MenuItem { Header = "Text history", Tag = "Header", IsEnabled = false, FontSize = 11 });
      DateTime now = Now();
      string first = TextHistory.FirstLine(TextToSave());
      menu.Items.Add(new System.Windows.Controls.MenuItem { Header = first.Length == 0 ? "Now" : "Now  ·  " + first, Tag = "Now", IsChecked = true });
      menu.Items.Add(new System.Windows.Controls.Separator());
      IReadOnlyList<HistoryEntry> rows = new List<HistoryEntry>();
      TryHistory(() => rows = this.shortcut.History(HistoryField));
      foreach (HistoryEntry row in rows) {
        System.Windows.Controls.MenuItem item = new System.Windows.Controls.MenuItem { Header = TextHistory.MenuLine(row.KeptAt, row.Text, now, row.FromPc), Tag = row };
        item.Click += (s, e) => PickHistory(row);
        menu.Items.Add(item);
      }
      if (rows.Count > 0) {
        menu.Items.Add(new System.Windows.Controls.Separator());
      }
      System.Windows.Controls.MenuItem clear = new System.Windows.Controls.MenuItem { Header = "Clear history...", Tag = "Clear", IsEnabled = rows.Count > 0 };
      clear.Click += (s, e) => ClearHistory();
      menu.Items.Add(clear);
      return menu;
    }

    /// <summary>
    /// Puts a copy back in the window and saves it. The text you had is kept first, so the pick can be undone; when the
    /// text is still the last pick (nothing typed since), that copy goes back with its own time instead, so stepping
    /// through the list never loses or moves a copy.
    /// </summary>
    public void PickHistory(HistoryEntry entry) {
      HistoryEntry restore = this.lastPick != null && TextToSave() == this.lastPick.Text ? this.lastPick : null;
      try {
        this.shortcut.SwapHistory(HistoryField, TextToSave(), restore, entry.ID, Now());
      } catch (Exception ex) {
        AppLog.Warning(null, "The text history could not be saved: {ErrorType}", ex.GetType().FullName);
        ErrorDialog.ShowDialog("The text could not be put back.", ex);
        return;
      }
      this.pickingHistory = true;
      try {
        this.TextComment.Text = entry.Text;
        this.TextComment.CaretIndex = this.TextComment.Text.Length;
      } finally {
        this.pickingHistory = false;
      }
      // The pick ends the run: the next change starts a new one, kept when the window is left.
      this.runStart = null;
      this.runCovered = false;
      this.runAtOnce = false;
      this.anyRun = true;
      this.historyUndo = null;
      this.lastPick = entry;
      SaveNoteNow();
    }

    // Asks first in the MDSHO dialog, then deletes every copy of this text.
    private void ClearHistory() {
      bool clear;
      using (Dialogs.UseIcons(this.icons.SolidIcons)) {
        clear = Dialogs.Confirm("Clear the text history of this " + KindWord + "? The earlier texts can't be brought back.", "Clear history");
      }
      if (!clear) {
        return;
      }
      try {
        this.shortcut.ClearHistory(HistoryField);
      } catch (Exception ex) {
        AppLog.Warning(null, "The text history could not be cleared: {ErrorType}", ex.GetType().FullName);
        ErrorDialog.ShowDialog("The text history could not be cleared.", ex);
        return;
      }
      this.historyUndo = null;
      this.lastPick = null;
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
        WatchPreviews(this.FontMenu);
        this.FontMenu.IsOpen = true;
      };
      ApplyFont();
    }

    // A comment has its own font; a note and a snippet use the text font.
    private string CurrentFont => this.kind == TextKind.Comment ? this.shortcut.CommentFont : this.shortcut.TextFont;

    private void ApplyFont() {
      this.TextComment.FontFamily = NoteFontFamilies.Resolve(CurrentFont);
      this.highlighter?.QueueDraw();
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
        this.previews.AddOrUpdate(item, () => PreviewFont(picked));
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
      this.highlighter?.QueueDraw();
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

    /// <summary>The Windows double-click time in milliseconds.</summary>
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern uint GetDoubleClickTime();

    // The run of quick clicks in the editor, counted here (see TextComment_PreviewMouseLeftButtonDown).
    private readonly ClickRun clickRun = new ClickRun();

    // A double click selects the word without the space after it, a triple click the whole paragraph, a fourth click all text.
    // WPF measures its double-click time from the first click of a run, so a 4th click at a normal pace comes too late and
    // WPF counts it as a new single click. The editor counts the run itself: each click within the double-click time and
    // spot of the click before.
    // A click in the padding, where the text box ignores clicks, goes to the nearest spot of the text
    // (see TextBoxPoints.CaretAt). Scroll bars keep their clicks.
    private void TextComment_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
      if (TextBoxPoints.OnScrollBar(this.TextComment, e.OriginalSource as DependencyObject)) {
        return;
      }
      Point at = e.GetPosition(this.TextComment);
      int clicks = this.clickRun.Next(e.ClickCount, e.Timestamp, at.X, at.Y, (int)GetDoubleClickTime(),
        SystemParameters.MinimumHorizontalDragDistance, SystemParameters.MinimumVerticalDragDistance);
      bool inPadding = TextBoxPoints.InPadding(this.TextComment, at);
      if (clicks < 2) {
        if (inPadding) {
          StartPaddingClick(at);
          e.Handled = true;
        }
        return;
      }
      int index = inPadding ? TextBoxPoints.CaretAt(this.TextComment, at) : CharacterIndexAt(this.TextComment, e);
      if (index < 0 && clicks < 4) {
        return;
      }
      string text = this.TextComment.Text ?? "";
      (int start, int length) = TextClickSelection.ForClicks(text, index, clicks).Value;
      this.TextComment.Focus();
      this.TextComment.Select(start, length);
      e.Handled = true;
    }

    // A press in the padding: the anchor of the selection while the button is held (see StartPaddingClick).
    private int paddingAnchor = -1;

    // Puts the caret at the nearest spot (Shift extends the selection to it) and selects while the mouse is dragged.
    private void StartPaddingClick(Point at) {
      System.Windows.Controls.TextBox box = this.TextComment;
      box.Focus();
      int caret = TextBoxPoints.CaretAt(box, at);
      int anchor = caret;
      if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0) {
        // From the end of the selection farther from the click (a text box doesn't tell which end the caret is at).
        int start = box.SelectionStart, end = start + box.SelectionLength;
        anchor = Math.Abs(caret - start) >= Math.Abs(caret - end) ? start : end;
      }
      SelectBetween(anchor, caret);
      this.paddingAnchor = anchor;
      this.paddingPress = at;
    }

    // Where the press in the padding was: the mouse is captured once it moves from there, not before, since a capture
    // would end WPF's count of a double click.
    private Point paddingPress;

    private void SelectBetween(int anchor, int caret) {
      this.TextComment.Select(Math.Min(anchor, caret), Math.Abs(caret - anchor));
    }

    private void TextComment_PreviewMouseMove(object sender, MouseEventArgs e) {
      if (this.paddingAnchor < 0) {
        return;
      }
      System.Windows.Controls.TextBox box = this.TextComment;
      Point at = e.GetPosition(box);
      if (!box.IsMouseCaptured) {
        if (Math.Abs(at.X - this.paddingPress.X) <= SystemParameters.MinimumHorizontalDragDistance
          && Math.Abs(at.Y - this.paddingPress.Y) <= SystemParameters.MinimumVerticalDragDistance) {
          return;
        }
        box.CaptureMouse();
      }
      // Out of the text area: scroll toward the mouse, as a drag from the text does.
      Rect area = TextBoxPoints.TextArea(box);
      if (at.Y < area.Top) {
        box.LineUp();
      } else if (at.Y > area.Bottom) {
        box.LineDown();
      }
      if (at.X < area.Left) {
        box.LineLeft();
      } else if (at.X > area.Right) {
        box.LineRight();
      }
      box.UpdateLayout();
      SelectBetween(this.paddingAnchor, TextBoxPoints.CaretAt(box, at));
      e.Handled = true;
    }

    private void TextComment_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e) {
      if (this.paddingAnchor < 0) {
        return;
      }
      this.paddingAnchor = -1;
      if (this.TextComment.IsMouseCaptured) {
        this.TextComment.ReleaseMouseCapture();
      }
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
          // A snippet's text never goes into the log.
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
      this.ButtonFont.Content = icons.Font;
      this.ButtonWrap.Content = icons.WrapText;
      ShowMoreIcon(icons.More);
      // The text buttons look like the icons: bold with solid icons, normal with outline ones.
      FontWeight weight = icons.SolidIcons ? FontWeights.Bold : FontWeights.Normal;
      this.ButtonSize.FontWeight = weight;
      this.ButtonMore.FontWeight = IsIconFontGlyph(icons.More) ? FontWeights.Normal : weight;
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
      } else if (e.Key == Key.System && e.SystemKey == Key.F && KeyboardModifiers() == ModifierKeys.Alt) {
        // Alt+F opens the title menu, like the ⋯ menu of Edge. Shift+F10 and the Menu key keep the text menu.
        e.Handled = true;
        OpenMoreMenu();
      }
    }
  }
}
