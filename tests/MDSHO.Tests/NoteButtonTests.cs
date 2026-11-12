using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using MDSHO.Core;
using MDSHO.Data;
using MDSHO.Helpers;
using MDSHO.ViewModels;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// Note buttons: a third kind of button next to shortcuts and run commands. "New note" creates one and opens the Note
  /// window; opening a note (double-click, or single click) opens the Note window; the tooltip shows the first lines of
  /// the note; "Copy note text" puts the note on the clipboard. Notes have no shortcut file.
  /// </summary>
  public class NoteButtonTests {

    private sealed class Fixture {
      public AppViewModel App;
      public DesktopWindowViewModel Win;
      public ShortcutViewModel Group;
      public List<ShortcutViewModel> NotesOpened = new List<ShortcutViewModel>();
      public List<ProcessStartInfo> Started = new List<ProcessStartInfo>();
      public List<string> Copied = new List<string>();
    }

    private static void WithGroup(Action<Fixture> body) {
      WpfAppHost.Run(() => {
        Action<ShortcutViewModel, bool> oldRunEditor = ShortcutViewModel.OpenRunCommandEditor;
        Action<ShortcutViewModel> oldNoteEditor = ShortcutViewModel.OpenNoteEditor;
        Action<ProcessStartInfo> oldStarter = ShortcutViewModel.ProcessStarter;
        Action<string> oldCopy = ShortcutViewModel.CopyText;
        Fixture f = new Fixture();
        ShortcutViewModel.OpenRunCommandEditor = (s, isNew) => { };
        ShortcutViewModel.OpenNoteEditor = s => f.NotesOpened.Add(s);
        ShortcutViewModel.ProcessStarter = psi => f.Started.Add(psi);
        ShortcutViewModel.CopyText = text => f.Copied.Add(text);
        try {
          using (TempDb db = new TempDb()) {
            f.App = new AppViewModel();
            f.Win = f.App.AddWindow("TEST", 0, 0);
            f.Group = ViewModelFactory.NewGroupShortcut(f.App, null, f.Win);
            f.Win.RootShortcuts.Add(f.Group);
            body(f);
          }
        } finally {
          ShortcutViewModel.OpenRunCommandEditor = oldRunEditor;
          ShortcutViewModel.OpenNoteEditor = oldNoteEditor;
          ShortcutViewModel.ProcessStarter = oldStarter;
          ShortcutViewModel.CopyText = oldCopy;
        }
      });
    }

    private static ShortcutViewModel NewNoteInGroup(Fixture f) {
      f.Group.NewNoteShortcutCommand.Execute(null);
      return f.Group.Children.Last();
    }

    private static Shortcut Stored(long id) {
      return DataStore.Shortcuts.GetAll().Single(s => s.ID == id);
    }

    // ---- Creating ----

    [Fact]
    public void New_note_in_a_group_is_a_note_named_New_note_and_opens_the_note_window() {
      WithGroup(f => {
        ShortcutViewModel note = NewNoteInGroup(f);
        Assert.Equal("New note", note.Name);
        Assert.True(note.IsNote);
        Assert.True(note.ShowNote);
        Assert.False(note.ShowShortcut);
        Assert.False(note.ShowCommand);
        Assert.False(note.ShowGroup);
        Assert.Equal(ShortcutTargetKind.Note, note.TargetKind);
        Assert.Equal(new[] { note }, f.NotesOpened);
        Shortcut row = Stored(note.ID);
        Assert.Equal((1L, "", 0L, 0L), (row.IsNote, row.NoteText, row.IsGroup, row.IsProcessCommand));
      });
    }

    [Fact]
    public void New_note_at_the_window_root_is_added_there_and_opens_the_note_window() {
      WithGroup(f => {
        f.Win.NewNoteShortcutCommand.Execute(null);
        ShortcutViewModel note = f.Win.RootShortcuts.Last();
        Assert.True(note.IsNote);
        Assert.Equal(f.Win.ID, Stored(note.ID).ParentWindowID);
        Assert.Equal(new[] { note }, f.NotesOpened);
      });
    }

    [Fact]
    public void Only_groups_offer_New_note() {
      WithGroup(f => {
        ShortcutViewModel note = NewNoteInGroup(f);
        Assert.True(f.Group.NewNoteShortcutCommand.CanExecute(null));
        Assert.False(note.NewNoteShortcutCommand.CanExecute(null));
      });
    }

    // ---- Text, opening, copying ----

    [Fact]
    public void Setting_the_note_text_saves_it() {
      WithGroup(f => {
        ShortcutViewModel note = NewNoteInGroup(f);
        note.NoteText = "1. Tell the help desk.\r\n2. Restart SRV01.";
        Assert.Equal("1. Tell the help desk.\r\n2. Restart SRV01.", Stored(note.ID).NoteText);
      });
    }

    [Fact]
    public void Opening_a_note_opens_the_note_window_and_starts_nothing() {
      WithGroup(f => {
        ShortcutViewModel note = NewNoteInGroup(f);
        f.NotesOpened.Clear();
        note.RunShortcut(false);
        Assert.Equal(new[] { note }, f.NotesOpened);
        Assert.Empty(f.Started);
      });
    }

    [Fact]
    public void Edit_note_opens_the_note_window() {
      WithGroup(f => {
        ShortcutViewModel note = NewNoteInGroup(f);
        f.NotesOpened.Clear();
        note.EditNoteCommand.Execute(null);
        Assert.Equal(new[] { note }, f.NotesOpened);
      });
    }

    [Fact]
    public void Copy_note_text_puts_the_whole_note_on_the_clipboard() {
      WithGroup(f => {
        ShortcutViewModel note = NewNoteInGroup(f);
        note.NoteText = "line 1\r\nline 2";
        note.CopyNoteTextCommand.Execute(null);
        Assert.Equal(new[] { "line 1\r\nline 2" }, f.Copied);
      });
    }

    [Fact]
    public void A_note_has_no_target_items_in_its_menu() {
      WithGroup(f => {
        ShortcutViewModel note = NewNoteInGroup(f);
        Assert.False(note.ShowSetNameTheSameAsTarget);
        Assert.False(note.ShowCopyTargetToMemory);
      });
    }

    // ---- Tooltip ----

    [Fact]
    public void The_tooltip_shows_the_first_ten_lines_of_the_note_and_the_comment_below() {
      WithGroup(f => {
        ShortcutViewModel note = NewNoteInGroup(f);
        Assert.Null(note.ToolTipText);
        Assert.False(note.HasToolTip);
        note.NoteText = string.Join("\r\n", Enumerable.Range(1, 12).Select(i => "step " + i));
        string[] lines = note.ToolTipText.Split(Environment.NewLine);
        Assert.Equal(11, lines.Length);
        Assert.Equal("step 10", lines[9]);
        Assert.Equal("…", lines[10]);
        Assert.True(note.HasToolTip);
        // Notes have no comments: even a stored comment never shows.
        note.Comment = "Reviewed in May";
        Assert.False(note.ShowCommentDivider);
        Assert.False(note.HasComment);
      });
    }

    [Fact]
    public void Note_text_changes_refresh_the_tooltip() {
      WithGroup(f => {
        ShortcutViewModel note = NewNoteInGroup(f);
        List<string> raised = new List<string>();
        note.PropertyChanged += (s, e) => raised.Add(e.PropertyName);
        note.NoteText = "x";
        Assert.Contains(nameof(ShortcutViewModel.ToolTipText), raised);
        Assert.Contains(nameof(ShortcutViewModel.HasToolTip), raised);
      });
    }

    // ---- No comments on notes ----

    [Fact]
    public void Notes_do_not_offer_comments_but_other_buttons_do() {
      WithGroup(f => {
        ShortcutViewModel note = NewNoteInGroup(f);
        ShortcutViewModel command = ViewModelFactory.NewProcessCommandShortcut(f.App, f.Group, null);
        f.Group.Children.Add(command);
        Assert.False(note.ShowComment);
        Assert.True(command.ShowComment);
        Assert.True(f.Group.ShowComment);
        Assert.False(note.EditCommentCommand.CanExecute(null));
        Assert.True(command.EditCommentCommand.CanExecute(null));
      });
    }

    [Fact]
    public void Edit_comment_on_a_note_opens_nothing() {
      WithGroup(f => {
        ShortcutViewModel note = NewNoteInGroup(f);
        Action<ShortcutViewModel> old = ShortcutViewModel.OpenCommentEditor;
        List<ShortcutViewModel> opened = new List<ShortcutViewModel>();
        ShortcutViewModel.OpenCommentEditor = s => opened.Add(s);
        try {
          note.EditCommentCommand.Execute(null);
        } finally {
          ShortcutViewModel.OpenCommentEditor = old;
        }
        Assert.Empty(opened);
      });
    }

    [Fact]
    public void A_note_never_shows_the_comment_marker_or_a_comment_in_its_tooltip() {
      WithGroup(f => {
        ShortcutViewModel note = NewNoteInGroup(f);
        note.NoteText = "text";
        note.Comment = "stored before";
        Assert.False(note.HasComment);
        Assert.Null(note.CommentPreview);
        Assert.False(note.ShowCommentDivider);
        Assert.Equal("text", note.ToolTipText);
      });
    }

    // ---- Loading, copy and paste ----

    [Fact]
    public void A_saved_note_loads_back_as_a_note_with_its_text() {
      WithGroup(f => {
        ShortcutViewModel note = NewNoteInGroup(f);
        note.NoteText = "kept";
        AppViewModel reloaded = ViewModelFactory.CreateAppViewModel();
        ShortcutViewModel loaded = reloaded.Windows.SelectMany(w => w.RootShortcuts).SelectMany(s => s.Children).Single(s => s.ID == note.ID);
        Assert.True(loaded.IsNote);
        Assert.Equal("kept", loaded.NoteText);
      });
    }

    [Fact]
    public void Copy_and_paste_makes_a_second_note_with_the_same_text() {
      WithGroup(f => {
        ShortcutViewModel note = NewNoteInGroup(f);
        note.NoteText = "copy me";
        note.CopyShortcutCommand.Execute(null);
        f.Group.PasteShortcutCommand.Execute(null);
        List<ShortcutViewModel> notes = f.Group.Children.Where(s => s.IsNote).ToList();
        Assert.Equal(2, notes.Count);
        Assert.All(notes, n => Assert.Equal("copy me", n.NoteText));
        Assert.All(notes, n => Assert.Equal((1L, "copy me"), (Stored(n.ID).IsNote, Stored(n.ID).NoteText)));
      });
    }

    // ---- Note window ----

    private static void Shown(Window window, Action check) {
      window.WindowStartupLocation = WindowStartupLocation.Manual;
      window.Left = -10000;
      window.Top = -10000;
      window.ShowActivated = false;
      window.Show();
      try {
        WpfAppHost.Pump();
        check();
      } finally {
        if (window.IsVisible) {
          window.Close();
        }
        WpfAppHost.Pump();
      }
    }

    private static T Named<T>(Window window, string name) where T : class {
      return Assert.IsAssignableFrom<T>(window.FindName(name));
    }

    [Fact]
    public void The_note_window_edits_the_note_text_with_the_note_icon() {
      WithGroup(f => {
        ShortcutViewModel note = NewNoteInGroup(f);
        note.Name = "Restart checklist";
        note.NoteText = "old";
        TextEditWindow window = TextEditWindow.ForNote(note);
        Shown(window, () => {
          Assert.Equal("Restart checklist", Named<TextBlock>(window, "TextTitle").Text);
          Assert.Equal(note.OwningWindow.Appearance.Icons.Rename, Named<TextBlock>(window, "TextTitleIcon").Text);
          Assert.Equal(note.OwningWindow.Appearance.Icons.Rename, GlyphIcon.GlyphOf(window.Icon));
          TextBox text = Named<TextBox>(window, "TextComment");
          Assert.Equal("old", text.Text);
          Assert.True(window.Width > 520, "The note window is bigger than the comment window.");
          text.Text = "new text";
          PumpUntil(() => note.NoteText == "new text");
          Assert.True(window.IsVisible, "Saving a note keeps its window open.");
        });
        Assert.Equal("new text", note.NoteText);
        Assert.Equal("", note.Comment ?? "");
        Assert.Equal("new text", Stored(note.ID).NoteText);
      });
    }

    // ---- Note window layout ----

    private static Rect BoundsIn(FrameworkElement element, Visual relativeTo) {
      return element.TransformToAncestor(relativeTo).TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));
    }

    [Fact]
    public void The_note_editor_fills_the_body_with_the_window_colour_padding_and_scroll_bars_when_needed() {
      WithGroup(f => {
        ShortcutViewModel note = NewNoteInGroup(f);
        TextEditWindow window = TextEditWindow.ForNote(note);
        Shown(window, () => {
          window.UpdateLayout();
          TextBox editor = Named<TextBox>(window, "TextComment");
          FrameworkElement body = Named<FrameworkElement>(window, "Body");
          Rect e = BoundsIn(editor, body);
          Assert.Equal(0, e.X, 1);
          Assert.Equal(0, e.Y, 1);
          Assert.Equal(body.ActualWidth, e.Width, 1);
          Assert.Equal(body.ActualHeight, e.Height, 1);
          Assert.Equal(((SolidColorBrush)window.Background).Color, Assert.IsType<SolidColorBrush>(editor.Background).Color);
          Assert.Equal(new Thickness(0), editor.BorderThickness);
          Assert.Equal(new Thickness(10), editor.Padding);
          Assert.Equal(TextWrapping.NoWrap, editor.TextWrapping);
          Assert.Equal(ScrollBarVisibility.Auto, editor.HorizontalScrollBarVisibility);
          Assert.Equal(ScrollBarVisibility.Auto, editor.VerticalScrollBarVisibility);
        });
      });
    }

    [Fact]
    public void The_note_window_has_pin_and_close_in_its_title_bar_no_save_button_and_no_bottom_bar() {
      WithGroup(f => {
        ShortcutViewModel note = NewNoteInGroup(f);
        TextEditWindow window = TextEditWindow.ForNote(note);
        Shown(window, () => {
          window.UpdateLayout();
          Assert.Equal(Visibility.Collapsed, Named<FrameworkElement>(window, "Footer").Visibility);
          Button pin = Named<Button>(window, "ButtonPin");
          Button close = Named<Button>(window, "ButtonClose");
          // The note saves itself, so there is no Save button.
          Assert.Null(window.FindName("ButtonTitleSave"));
          Assert.Equal(Visibility.Visible, pin.Visibility);
          Assert.Equal("Keep on top", pin.ToolTip);
          Assert.Equal("Close (Esc)", close.ToolTip);
          Assert.Equal("Close", System.Windows.Automation.AutomationProperties.GetName(close));
          // Left to right: pin, close.
          FrameworkElement bar = Named<FrameworkElement>(window, "TitleBar");
          double xPin = BoundsIn(pin, bar).X, xClose = BoundsIn(close, bar).X;
          Assert.True(xPin < xClose, "Order " + xPin + ", " + xClose);
          // The text reaches the bottom of the window.
          FrameworkElement client = (FrameworkElement)window.Content;
          TextBox editor = Named<TextBox>(window, "TextComment");
          Assert.Equal(client.ActualHeight, BoundsIn(editor, client).Bottom, 1);
        });
      });
    }

    [Fact]
    public void The_comment_window_keeps_its_bottom_bar_and_has_no_pin() {
      WithGroup(f => {
        TextEditWindow window = TextEditWindow.ForComment(f.Group);
        Shown(window, () => {
          Assert.Equal(Visibility.Visible, Named<FrameworkElement>(window, "Footer").Visibility);
          Assert.Equal(Visibility.Collapsed, Named<Button>(window, "ButtonPin").Visibility);
          Assert.Equal("Close", Named<Button>(window, "ButtonClose").ToolTip);
        });
      });
    }

    [Fact]
    public void Typing_in_a_note_saves_it_by_itself_and_keeps_the_window_open() {
      WithGroup(f => {
        ShortcutViewModel note = NewNoteInGroup(f);
        note.NoteText = "old";
        TextEditWindow window = TextEditWindow.ForNote(note);
        Shown(window, () => {
          TextBox text = Named<TextBox>(window, "TextComment");
          text.Text = "old and new";
          PumpUntil(() => Stored(note.ID).NoteText == "old and new");
          Assert.Equal("old and new", note.NoteText);
          // A note of only spaces and new lines is saved as empty.
          text.Text = "  \n ";
          PumpUntil(() => Stored(note.ID).NoteText == "");
          Assert.True(window.IsVisible);
        });
      });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Closing_a_note_right_after_typing_keeps_what_was_typed(bool escape) {
      WithGroup(f => {
        ShortcutViewModel note = NewNoteInGroup(f);
        note.NoteText = "old";
        TextEditWindow window = TextEditWindow.ForNote(note);
        window.ShowActivated = false;
        window.Left = -10000;
        window.Top = -10000;
        window.Show();
        WpfAppHost.Pump();
        Named<TextBox>(window, "TextComment").Text = "typed then closed";
        if (escape) {
          System.Windows.Input.KeyEventArgs args = new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice, PresentationSource.FromVisual(window), 0, System.Windows.Input.Key.Escape) { RoutedEvent = System.Windows.Input.Keyboard.PreviewKeyDownEvent };
          window.RaiseEvent(args);
        } else {
          Button close = Named<Button>(window, "ButtonClose");
          close.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, close));
        }
        WpfAppHost.Pump();
        Assert.False(window.IsVisible);
        Assert.Equal("typed then closed", note.NoteText);
        Assert.Equal("typed then closed", Stored(note.ID).NoteText);
      });
    }

    // Pumps the dispatcher (so timers run) until the condition holds, for at most 5 seconds.
    private static void PumpUntil(Func<bool> condition) {
      DateTime end = DateTime.UtcNow.AddSeconds(5);
      while (!condition()) {
        Assert.True(DateTime.UtcNow < end, "Timed out waiting for the note to save itself.");
        System.Threading.Thread.Sleep(20);
        WpfAppHost.Pump();
      }
    }

    [Fact]
    public void Ctrl_Enter_and_Ctrl_S_save_a_note_and_keep_it_open() {
      foreach (System.Windows.Input.Key key in new[] { System.Windows.Input.Key.Enter, System.Windows.Input.Key.S }) {
        WithGroup(f => {
          ShortcutViewModel note = NewNoteInGroup(f);
          TextEditWindow window = TextEditWindow.ForNote(note);
          Shown(window, () => {
            Named<TextBox>(window, "TextComment").Text = "typed " + key;
            TextEditWindow.KeyboardModifiers = () => System.Windows.Input.ModifierKeys.Control;
            try {
              System.Windows.Input.KeyEventArgs args = new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice, PresentationSource.FromVisual(window), 0, key) { RoutedEvent = System.Windows.Input.Keyboard.PreviewKeyDownEvent };
              window.RaiseEvent(args);
              WpfAppHost.Pump();
            } finally {
              TextEditWindow.KeyboardModifiers = () => System.Windows.Input.Keyboard.Modifiers;
            }
            Assert.Equal("typed " + key, note.NoteText);
            Assert.True(window.IsVisible, key + " closed the window");
          });
        });
      }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Each_scroll_bar_shows_only_when_its_direction_needs_it(bool note) {
      WithGroup(f => {
        ShortcutViewModel button = note ? NewNoteInGroup(f) : f.Group;
        TextEditWindow window = note ? TextEditWindow.ForNote(button) : TextEditWindow.ForComment(button);
        Shown(window, () => {
          TextBox editor = Named<TextBox>(window, "TextComment");
          ScrollViewer scroll = WpfAppHost.VisualDescendants<ScrollViewer>(editor).First();
          (Visibility H, Visibility V) Bars(string text) {
            editor.Text = text;
            window.UpdateLayout();
            WpfAppHost.Pump();
            return (scroll.ComputedHorizontalScrollBarVisibility, scroll.ComputedVerticalScrollBarVisibility);
          }
          Assert.Equal((Visibility.Collapsed, Visibility.Collapsed), Bars("short"));
          Assert.Equal((Visibility.Visible, Visibility.Collapsed), Bars(new string('x', 400)));
          string many = string.Join(Environment.NewLine, Enumerable.Range(1, 80));
          Assert.Equal((Visibility.Collapsed, Visibility.Visible), Bars(many));
          Assert.Equal((Visibility.Visible, Visibility.Visible), Bars(new string('x', 400) + Environment.NewLine + many));
        });
      });
    }

    [Fact]
    public void The_comment_window_has_the_same_look_as_the_note_window_at_its_own_size() {
      WithGroup(f => {
        TextEditWindow window = TextEditWindow.ForComment(f.Group);
        Shown(window, () => {
          window.UpdateLayout();
          TextBox editor = Named<TextBox>(window, "TextComment");
          FrameworkElement body = Named<FrameworkElement>(window, "Body");
          Rect e = BoundsIn(editor, body);
          Assert.Equal(0, e.X, 1);
          Assert.Equal(0, e.Y, 1);
          Assert.Equal(body.ActualWidth, e.Width, 1);
          Assert.Equal(body.ActualHeight, e.Height, 1);
          Assert.Equal(((SolidColorBrush)window.Background).Color, Assert.IsType<SolidColorBrush>(editor.Background).Color);
          Assert.Equal(new Thickness(0), editor.BorderThickness);
          Assert.Equal(new Thickness(10), editor.Padding);
          Assert.Equal(TextWrapping.NoWrap, editor.TextWrapping);
          Assert.Equal(ScrollBarVisibility.Auto, editor.HorizontalScrollBarVisibility);
          Assert.Equal(ScrollBarVisibility.Auto, editor.VerticalScrollBarVisibility);
          Assert.Equal(Visibility.Collapsed, Named<TextBlock>(window, "TextHint").Visibility);
          Assert.Equal(Visibility.Visible, Named<TextBlock>(window, "TextFooterHint").Visibility);
          FrameworkElement footer = Named<FrameworkElement>(window, "Footer");
          Button save = Named<Button>(window, "ButtonSave");
          double h = save.ActualHeight;
          Assert.Equal(h, BoundsIn(save, footer).Top, 1);
          Assert.Equal(h, BoundsIn(Named<TextBlock>(window, "TextFooterHint"), footer).Left, 1);
          // A comment is short: its window stays smaller than a note's.
          Assert.Equal(520, window.Width);
        });
      });
    }

    // ---- Several note windows at once ----

    /// <summary>Opens note windows off screen through the real ShowNoteFor, then closes every one of them.</summary>
    private static void WithNoteWindows(Action check) {
      Action<TextEditWindow> oldPlace = TextEditWindow.PlaceNoteWindow;
      Func<IList<MDSHO.Common.ScreenRect>> oldAreas = TextEditWindow.WorkAreas;
      // The far-away test area counts as a screen, so a reopened test window is never moved onto the real screen.
      TextEditWindow.WorkAreas = () => new List<MDSHO.Common.ScreenRect> { new MDSHO.Common.ScreenRect(-20000, -20000, 15000, 15000) };
      TextEditWindow.PlaceNoteWindow = w => {
        w.WindowStartupLocation = WindowStartupLocation.Manual;
        w.Left = -10000;
        w.Top = -10000;
        w.ShowActivated = false;
      };
      try {
        check();
      } finally {
        foreach (TextEditWindow w in TextEditWindow.OpenNoteWindows.ToList()) {
          w.Close();
        }
        WpfAppHost.Pump();
        TextEditWindow.PlaceNoteWindow = oldPlace;
        TextEditWindow.WorkAreas = oldAreas;
      }
    }

    [Fact]
    public void Two_notes_open_in_two_windows_at_the_same_time() {
      WithGroup(f => {
        ShortcutViewModel first = NewNoteInGroup(f);
        ShortcutViewModel second = NewNoteInGroup(f);
        WithNoteWindows(() => {
          // ShowNoteFor returns at once (the window is not a dialog), so both can be open.
          TextEditWindow.ShowNoteFor(first);
          TextEditWindow.ShowNoteFor(second);
          WpfAppHost.Pump();
          Assert.Equal(2, TextEditWindow.OpenNoteWindows.Count);
          Assert.All(TextEditWindow.OpenNoteWindows, w => Assert.True(w.IsVisible));
        });
      });
    }

    [Fact]
    public void Opening_a_note_that_is_already_open_reuses_its_window() {
      WithGroup(f => {
        ShortcutViewModel note = NewNoteInGroup(f);
        WithNoteWindows(() => {
          TextEditWindow.ShowNoteFor(note);
          TextEditWindow window = TextEditWindow.OpenNoteWindows.Single();
          TextEditWindow.ShowNoteFor(note);
          WpfAppHost.Pump();
          Assert.Same(window, TextEditWindow.OpenNoteWindows.Single());
        });
      });
    }

    [Fact]
    public void A_closed_note_window_can_be_opened_again() {
      WithGroup(f => {
        ShortcutViewModel note = NewNoteInGroup(f);
        WithNoteWindows(() => {
          TextEditWindow.ShowNoteFor(note);
          TextEditWindow.OpenNoteWindows.Single().Close();
          WpfAppHost.Pump();
          Assert.Empty(TextEditWindow.OpenNoteWindows);
          TextEditWindow.ShowNoteFor(note);
          Assert.Single(TextEditWindow.OpenNoteWindows);
        });
      });
    }

    [Fact]
    public void Closing_one_note_window_saves_it_and_leaves_the_other_open() {
      WithGroup(f => {
        ShortcutViewModel first = NewNoteInGroup(f);
        ShortcutViewModel second = NewNoteInGroup(f);
        WithNoteWindows(() => {
          TextEditWindow.ShowNoteFor(first);
          TextEditWindow.ShowNoteFor(second);
          TextEditWindow firstWindow = TextEditWindow.OpenNoteWindows.Single(w => w.Shortcut == first);
          ((TextBox)firstWindow.FindName("TextComment")).Text = "saved";
          firstWindow.Close();
          WpfAppHost.Pump();
          Assert.Equal("saved", first.NoteText);
          Assert.Equal("", second.NoteText ?? "");
          Assert.Single(TextEditWindow.OpenNoteWindows);
        });
      });
    }

    // ---- Panel view ----

    private static void WithView(Fixture f, ShortcutViewModel note, Action<DesktopWindowView, Button> check) {
      DesktopWindowView view = new DesktopWindowView(f.Win) { ShowActivated = false, Left = -10000, Top = -10000 };
      try {
        view.Show();
        WpfAppHost.Pump();
        f.Group.IsGroupExpanded = true;
        WpfAppHost.Pump();
        TreeView tree = (TreeView)view.FindName("TreeViewShortcuts");
        TreeViewItem item = WpfAppHost.VisualDescendants<TreeViewItem>(tree).Single(i => i.DataContext == note);
        Button button = WpfAppHost.VisualDescendants<Button>(item).First(b => b.DataContext == note);
        check(view, button);
      } finally {
        view.Close();
        WpfAppHost.Pump();
      }
    }

    [Fact]
    public void A_note_button_shows_the_note_icon_only() {
      WithGroup(f => {
        ShortcutViewModel note = NewNoteInGroup(f);
        WithView(f, note, (view, button) => {
          TextBlock icon = WpfAppHost.VisualDescendants<TextBlock>(button).Single(t => t.Name == "TextNoteIcon");
          Assert.Equal(Visibility.Visible, icon.Visibility);
          Assert.Equal(f.Win.Appearance.Icons.Rename, icon.Text);
        });
      });
    }

    [Fact]
    public void Menus_have_New_note_and_a_note_has_Edit_note_and_Copy_note_text() {
      WithGroup(f => {
        ShortcutViewModel note = NewNoteInGroup(f);
        WithView(f, note, (view, button) => {
          TreeView tree = (TreeView)view.FindName("TreeViewShortcuts");
          MenuItem treeNew = tree.ContextMenu.Items.OfType<MenuItem>().Single(m => (m.Header as string) == "New note");
          Assert.Equal(nameof(DesktopWindowViewModel.NewNoteShortcutCommand), BindingOperations.GetBinding(treeNew, MenuItem.CommandProperty).Path.Path);
          List<MenuItem> items = button.ContextMenu.Items.OfType<MenuItem>().ToList();
          MenuItem groupNew = items.Single(m => (m.Header as string) == "New note");
          Assert.Equal(nameof(ShortcutViewModel.ShowNewShortcut), BindingOperations.GetBinding(groupNew, UIElement.VisibilityProperty).Path.Path);
          MenuItem edit = items.Single(m => (m.Header as string) == "Edit note...");
          Assert.Equal(nameof(ShortcutViewModel.EditNoteCommand), BindingOperations.GetBinding(edit, MenuItem.CommandProperty).Path.Path);
          Assert.Equal(nameof(ShortcutViewModel.ShowNote), BindingOperations.GetBinding(edit, UIElement.VisibilityProperty).Path.Path);
          Assert.Equal("OwningWindow.Appearance.Icons.Rename", BindingOperations.GetBinding((TextBlock)edit.Icon, TextBlock.TextProperty).Path.Path);
          MenuItem comment = items.Single(m => (m.Header as string) == "Comment...");
          Assert.Equal(nameof(ShortcutViewModel.ShowComment), BindingOperations.GetBinding(comment, UIElement.VisibilityProperty).Path.Path);
          MenuItem copy = items.Single(m => (m.Header as string) == "Copy note text");
          Assert.Equal(nameof(ShortcutViewModel.CopyNoteTextCommand), BindingOperations.GetBinding(copy, MenuItem.CommandProperty).Path.Path);
          Assert.Equal(nameof(ShortcutViewModel.ShowNote), BindingOperations.GetBinding(copy, UIElement.VisibilityProperty).Path.Path);
        });
      });
    }
  }
}
