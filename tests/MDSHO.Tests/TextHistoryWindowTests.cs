using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using MDSHO.Common;
using MDSHO.Core;
using MDSHO.Data;
using MDSHO.ViewModels;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// The History button (the time icon) in the title bar of notes, comments and snippets: the window keeps a copy of the
  /// saved text when an editing burst starts, the menu lists Now, Before and Earlier, and a pick puts a copy back.
  /// </summary>
  public class TextHistoryWindowTests {
    private static readonly ScreenRect TestArea = new ScreenRect(-20000, -20000, 15000, 15000);

    private sealed class Fixture {
      public AppViewModel App;
      public DesktopWindowViewModel Win;
      public ShortcutViewModel Note;
      public ShortcutViewModel Snippet;
      public ShortcutViewModel Command;
      public DateTime Now = new DateTime(2026, 11, 21, 9, 0, 0);
    }

    private static void WithButtons(Action<Fixture> body) {
      WpfAppHost.Run(() => {
        Action<ShortcutViewModel> oldNoteEditor = ShortcutViewModel.OpenNoteEditor;
        Action<TextEditWindow> oldPlace = TextEditWindow.PlaceNoteWindow;
        Func<IList<ScreenRect>> oldAreas = TextEditWindow.WorkAreas;
        Func<DateTime> oldNow = TextEditWindow.Now;
        ShortcutViewModel.OpenNoteEditor = s => { };
        TextEditWindow.PlaceNoteWindow = w => {
          w.WindowStartupLocation = WindowStartupLocation.Manual;
          w.Left = -10000;
          w.Top = -10000;
        };
        TextEditWindow.WorkAreas = () => new List<ScreenRect> { TestArea };
        Fixture f = new Fixture();
        TextEditWindow.Now = () => f.Now;
        try {
          using (TempDb tempDb = new TempDb()) {
            f.App = new AppViewModel();
            f.Win = f.App.AddWindow("TEST", 0, 0);
            f.Win.NewNoteShortcutCommand.Execute(null);
            f.Note = f.Win.RootShortcuts.Last();
            f.Note.CancelEdit();
            f.Note.Name = "Checklist";
            f.Note.SaveNoteText("milk\r\neggs");
            f.Win.NewSnippetShortcutCommand.Execute(null);
            f.Snippet = f.Win.RootShortcuts.Last();
            f.Snippet.CancelEdit();
            f.Snippet.Name = "Address";
            f.Snippet.SaveSnippetText("Main Street 12");
            f.Command = ViewModelFactory.NewProcessCommandShortcut(f.App, null, f.Win);
            f.Win.RootShortcuts.Add(f.Command);
            f.Command.Name = "Ping";
            f.Command.SaveComment("Call Anna first.");
            body(f);
          }
        } finally {
          foreach (Window w in Application.Current.Windows.OfType<TextEditWindow>().ToList()) {
            w.Close();
          }
          WpfAppHost.Pump();
          ShortcutViewModel.OpenNoteEditor = oldNoteEditor;
          TextEditWindow.PlaceNoteWindow = oldPlace;
          TextEditWindow.WorkAreas = oldAreas;
          TextEditWindow.Now = oldNow;
        }
      });
    }

    private static TextEditWindow OpenNote(ShortcutViewModel note) {
      TextEditWindow.ShowNoteFor(note);
      WpfAppHost.Pump();
      return TextEditWindow.OpenNoteWindows.Single(w => w.Shortcut == note);
    }

    private static TextEditWindow Show(TextEditWindow window) {
      window.WindowStartupLocation = WindowStartupLocation.Manual;
      window.Left = -10000;
      window.Top = -10000;
      window.ShowActivated = false;
      window.ShowInTaskbar = false;
      window.Show();
      WpfAppHost.Pump();
      return window;
    }

    private static TextBox Text(Window window) => (TextBox)window.FindName("TextComment");
    private static Button Named(Window window, string name) => (Button)window.FindName(name);
    private static int ColumnOf(UIElement e) => Grid.GetColumn(e);

    // Types at the end of the text, like a key press.
    private static void TypeAtEnd(TextEditWindow window, string more) {
      TextBox box = Text(window);
      box.CaretIndex = box.Text.Length;
      box.SelectedText = more;
      box.CaretIndex = box.Text.Length;
      WpfAppHost.Pump();
    }

    // Deletes the last character, like Backspace.
    private static void DeleteLast(TextEditWindow window) {
      TextBox box = Text(window);
      box.Select(box.Text.Length - 1, 1);
      box.SelectedText = "";
      WpfAppHost.Pump();
    }

    // Selects all and types over it (a big removal).
    private static void TypeOverAll(TextEditWindow window, string text) {
      TextBox box = Text(window);
      box.SelectAll();
      box.SelectedText = text;
      WpfAppHost.Pump();
    }

    private static List<MenuItem> Items(ContextMenu menu) => menu.Items.OfType<MenuItem>().ToList();

    private static void Click(MenuItem item) {
      item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent, item));
      WpfAppHost.Pump();
    }

    // ---- The button ----

    [Fact]
    public void Notes_comments_and_snippets_have_the_history_button_left_of_pin_and_close() {
      WithButtons(f => {
        TextEditWindow note = OpenNote(f.Note);
        Button history = Named(note, "ButtonHistory");
        Assert.Equal(Visibility.Visible, history.Visibility);
        Assert.Equal(f.Win.Appearance.Icons.Time, history.Content);
        Assert.Contains("MDSHOFont", history.FontFamily.Source);
        Assert.Equal("Text history", history.ToolTip);
        Assert.Equal("Text history", System.Windows.Automation.AutomationProperties.GetName(history));
        Assert.True(System.Windows.Shell.WindowChrome.GetIsHitTestVisibleInChrome(history));
        Assert.Equal(ColumnOf(Named(note, "ButtonPin")) - 1, ColumnOf(history));
        Assert.True(ColumnOf(Named(note, "ButtonWrap")) < ColumnOf(history));
        foreach (TextEditWindow other in new[] { Show(TextEditWindow.ForComment(f.Command)), Show(TextEditWindow.ForSnippet(f.Snippet)) }) {
          Button h = Named(other, "ButtonHistory");
          Assert.Equal(Visibility.Visible, h.Visibility);
          Assert.True(ColumnOf(h) < ColumnOf(Named(other, "ButtonClose")));
          Assert.True(ColumnOf(Named(other, "ButtonWrap")) < ColumnOf(h));
        }
      });
    }

    [Fact]
    public void The_history_button_shows_the_time_icon_and_follows_the_icon_set() {
      WithButtons(f => {
        TextEditWindow window = OpenNote(f.Note);
        Button history = Named(window, "ButtonHistory");
        f.Win.Appearance.Icons.SolidIcons = true;
        WpfAppHost.Pump();
        Assert.Equal("\uE95E", history.Content);
        f.Win.Appearance.Icons.SolidIcons = false;
        WpfAppHost.Pump();
        Assert.Equal("\uE95F", history.Content);
        foreach (TextEditWindow other in new[] { Show(TextEditWindow.ForComment(f.Command)), Show(TextEditWindow.ForSnippet(f.Snippet)) }) {
          Assert.Equal("\uE95F", Named(other, "ButtonHistory").Content);
        }
      });
    }

    [Fact]
    public void The_history_button_opens_the_menu_below_it() {
      WithButtons(f => {
        TextEditWindow window = OpenNote(f.Note);
        Button history = Named(window, "ButtonHistory");
        history.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent, history));
        WpfAppHost.Pump();
        try {
          Assert.True(window.HistoryMenu.IsOpen);
          Assert.Same(history, window.HistoryMenu.PlacementTarget);
          Assert.Equal(System.Windows.Controls.Primitives.PlacementMode.Bottom, window.HistoryMenu.Placement);
        } finally {
          window.HistoryMenu.IsOpen = false;
          WpfAppHost.Pump();
        }
      });
    }

    // ---- Keeping copies while typing ----

    [Fact]
    public void The_first_change_after_opening_keeps_the_saved_text_as_before() {
      WithButtons(f => {
        TextEditWindow window = OpenNote(f.Note);
        TypeAtEnd(window, "\r\nbread");
        Assert.Equal(new TextHistorySlots("milk\r\neggs", "2026-11-21T09:00:00", "", ""), f.Note.TextHistory);
        Assert.Equal("milk\r\neggs", DataStore.Shortcuts.Get(f.Note.ID).TextPrevious1);
        f.Now = f.Now.AddMinutes(2);
        TypeAtEnd(window, "\r\nbutter");
        Assert.Equal("milk\r\neggs", f.Note.TextHistory.Previous1);
        Assert.Equal("", f.Note.TextHistory.Previous2);
      });
    }

    // Before must never equal the text: typing a letter and deleting it again keeps no copy.
    [Fact]
    public void Typing_a_letter_and_deleting_it_again_keeps_no_copy() {
      WithButtons(f => {
        TextEditWindow window = OpenNote(f.Note);
        TypeAtEnd(window, "x");
        DeleteLast(window);
        Assert.Equal("milk\r\neggs", Text(window).Text);
        Assert.Equal(TextHistorySlots.Empty, f.Note.TextHistory);
        Assert.Equal("", DataStore.Shortcuts.Get(f.Note.ID).TextPrevious1);
        window.Close();
        WpfAppHost.Pump();
        Assert.Equal(TextHistorySlots.Empty, f.Note.TextHistory);
      });
    }

    [Fact]
    public void After_an_undone_change_a_real_change_keeps_the_copy() {
      WithButtons(f => {
        TextEditWindow window = OpenNote(f.Note);
        TypeAtEnd(window, "x");
        DeleteLast(window);
        TypeAtEnd(window, "\r\nbread");
        Assert.Equal("milk\r\neggs", f.Note.TextHistory.Previous1);
        Assert.Equal("milk\r\neggs", DataStore.Shortcuts.Get(f.Note.ID).TextPrevious1);
        Assert.NotEqual(Text(window).Text, f.Note.TextHistory.Previous1);
      });
    }

    [Fact]
    public void Undoing_a_big_removal_takes_its_copy_back() {
      WithButtons(f => {
        f.Note.SaveTextHistory(new TextHistorySlots("old", "2026-11-20T18:30:00", "", ""));
        TextEditWindow window = OpenNote(f.Note);
        TypeOverAll(window, "x");
        Assert.Equal("milk\r\neggs", f.Note.TextHistory.Previous1);
        Text(window).Text = "milk\r\neggs";
        WpfAppHost.Pump();
        Assert.Equal(new TextHistorySlots("old", "2026-11-20T18:30:00", "", ""), f.Note.TextHistory);
        Assert.Equal("old", DataStore.Shortcuts.Get(f.Note.ID).TextPrevious1);
      });
    }

    // Undoing a big removal goes back to the history just before that removal, never to an older one: the text the
    // window opened with stays in the history.
    [Fact]
    public void Pasting_back_after_cut_all_keeps_the_opening_text_in_the_history() {
      WithButtons(f => {
        f.Note.SaveTextHistory(new TextHistorySlots("h0", "2026-11-20T18:30:00", "", ""));
        TextEditWindow window = OpenNote(f.Note);
        TypeAtEnd(window, "x");
        TypeAtEnd(window, "\r\nbread");
        string b = Text(window).Text;
        TextHistorySlots afterFirst = f.Note.TextHistory;
        Assert.Equal("milk\r\neggs", afterFirst.Previous1);
        TypeOverAll(window, "");
        Assert.Equal((b, "milk\r\neggs"), (f.Note.TextHistory.Previous1, f.Note.TextHistory.Previous2));
        TypeOverAll(window, b);
        Assert.Equal(afterFirst, f.Note.TextHistory);
        Assert.Equal("milk\r\neggs", DataStore.Shortcuts.Get(f.Note.ID).TextPrevious1);
        Assert.Equal("h0", DataStore.Shortcuts.Get(f.Note.ID).TextPrevious2);
      });
    }

    [Fact]
    public void Deleting_all_right_after_typing_and_pasting_it_back_keeps_the_opening_text_as_before() {
      WithButtons(f => {
        TextEditWindow window = OpenNote(f.Note);
        TypeAtEnd(window, "x");
        TypeOverAll(window, "");
        TypeOverAll(window, "milk\r\neggsx");
        Assert.Equal("milk\r\neggs", f.Note.TextHistory.Previous1);
        Assert.Equal("", f.Note.TextHistory.Previous2);
        Assert.Equal("milk\r\neggs", DataStore.Shortcuts.Get(f.Note.ID).TextPrevious1);
      });
    }

    // After a copy was taken back, the next real change keeps it again and an undo of that goes back only one step.
    [Fact]
    public void Undo_after_a_taken_back_copy_goes_back_one_step_only() {
      WithButtons(f => {
        f.Note.SaveTextHistory(new TextHistorySlots("h0", "2026-11-20T18:30:00", "", ""));
        TextEditWindow window = OpenNote(f.Note);
        TypeAtEnd(window, "x");
        DeleteLast(window);
        Assert.Equal("h0", f.Note.TextHistory.Previous1);
        TypeAtEnd(window, "y");
        Assert.Equal(("milk\r\neggs", "h0"), (f.Note.TextHistory.Previous1, f.Note.TextHistory.Previous2));
        TypeOverAll(window, "");
        TypeOverAll(window, "milk\r\neggsy");
        Assert.Equal(("milk\r\neggs", "h0"), (f.Note.TextHistory.Previous1, f.Note.TextHistory.Previous2));
      });
    }

    [Fact]
    public void A_window_kept_open_keeps_no_more_copies_however_long_it_waits() {
      WithButtons(f => {
        TextEditWindow window = OpenNote(f.Note);
        TypeAtEnd(window, " 1");
        Assert.True(window.SaveNoteNow());
        f.Now = f.Now.AddHours(8);
        TypeAtEnd(window, " 2");
        Assert.Equal("milk\r\neggs", f.Note.TextHistory.Previous1);
        Assert.Equal("", f.Note.TextHistory.Previous2);
      });
    }

    [Fact]
    public void Closing_and_opening_the_note_again_keeps_a_copy_at_the_first_change() {
      WithButtons(f => {
        TextEditWindow window = OpenNote(f.Note);
        TypeAtEnd(window, " 1");
        window.Close();
        WpfAppHost.Pump();
        TypeAtEnd(OpenNote(f.Note), " 2");
        Assert.Equal("milk\r\neggs 1", f.Note.TextHistory.Previous1);
        Assert.Equal("milk\r\neggs", f.Note.TextHistory.Previous2);
      });
    }

    // A note window MDSHO opens by itself at start (after MDSHO or Windows restarted) counts as opened.
    [Fact]
    public void A_note_reopened_at_start_keeps_a_copy_at_its_first_change() {
      WithButtons(f => {
        TextEditWindow window = OpenNote(f.Note);
        TypeAtEnd(window, " 1");
        Assert.Empty(TextEditWindow.SaveOpenNotesForExit());
        TextEditWindow.IsExiting = true;
        try {
          window.Close();
          WpfAppHost.Pump();
        } finally {
          TextEditWindow.IsExiting = false;
        }
        Assert.True(DataStore.NoteWindows.Get(f.Note.ID).IsOpen);
        // The next start: everything is loaded from the database, as the app does, and the note window opens again.
        AppViewModel next = ViewModelFactory.CreateAppViewModel(System.IO.Path.GetTempPath());
        TextEditWindow.ReopenOpenNotes(next);
        WpfAppHost.Pump();
        TextEditWindow reopened = TextEditWindow.OpenNoteWindows.Single(w => w.Shortcut.ID == f.Note.ID);
        Assert.NotSame(window, reopened);
        TypeAtEnd(reopened, " 2");
        Assert.Equal("milk\r\neggs 1", reopened.Shortcut.TextHistory.Previous1);
        Assert.Equal("milk\r\neggs 1", DataStore.Shortcuts.Get(f.Note.ID).TextPrevious1);
        Assert.Equal("milk\r\neggs", DataStore.Shortcuts.Get(f.Note.ID).TextPrevious2);
      });
    }

    [Fact]
    public void Typing_over_all_the_text_keeps_a_copy_at_once_mid_burst() {
      WithButtons(f => {
        TextEditWindow window = Show(TextEditWindow.ForComment(f.Command));
        TypeAtEnd(window, " Then Ben.");
        f.Now = f.Now.AddSeconds(30);
        TypeOverAll(window, "x");
        Assert.Equal("Call Anna first. Then Ben.", f.Command.CommentHistory.Previous1);
        Assert.Equal("Call Anna first.", f.Command.CommentHistory.Previous2);
        Assert.Equal(TextHistorySlots.Empty, f.Command.TextHistory);
      });
    }

    [Fact]
    public void The_snippet_editor_keeps_copies_in_the_text_history() {
      WithButtons(f => {
        TextEditWindow window = Show(TextEditWindow.ForSnippet(f.Snippet));
        TypeAtEnd(window, ", 1000 Sample Town");
        Assert.Equal("Main Street 12", f.Snippet.TextHistory.Previous1);
      });
    }

    // ---- The menu ----

    [Fact]
    public void The_menu_shows_a_header_now_before_and_earlier_with_times_and_first_lines() {
      WithButtons(f => {
        f.Note.SaveTextHistory(new TextHistorySlots("Old list\r\nmore", "2026-11-21T08:05:00", "", ""));
        f.Now = new DateTime(2026, 11, 21, 15, 0, 0);
        ContextMenu menu = OpenNote(f.Note).BuildHistoryMenu();
        List<MenuItem> items = Items(menu);
        Assert.Equal(new[] { "Text history", "Now", "Before", "Earlier" }, items.Select(i => i.Tag as string ?? (string)i.Header).ToArray());
        Assert.False(items[0].IsEnabled);
        Assert.Equal("Now  ·  milk", items[1].Header);
        Assert.Equal("Before  ·  today 08:05  ·  Old list", items[2].Header);
        Assert.Equal("Earlier", items[3].Header);
        Assert.Equal(new[] { true, true, false }, items.Skip(1).Select(i => i.IsEnabled).ToArray());
        Assert.Equal(new[] { true, false, false }, items.Skip(1).Select(i => i.IsChecked).ToArray());
      });
    }

    [Fact]
    public void Picking_before_puts_it_back_saves_it_and_can_be_undone() {
      WithButtons(f => {
        f.Note.SaveTextHistory(new TextHistorySlots("old list", "2026-11-20T18:30:00", "older list", "2026-11-19T10:00:00"));
        TextEditWindow window = OpenNote(f.Note);
        Click(Items(window.BuildHistoryMenu()).Single(i => (string)i.Tag == "Before"));
        Assert.Equal("old list", Text(window).Text);
        Assert.Equal("old list", DataStore.Shortcuts.Get(f.Note.ID).NoteText);
        Assert.Equal(new TextHistorySlots("milk\r\neggs", "2026-11-21T09:00:00", "older list", "2026-11-19T10:00:00"), f.Note.TextHistory);
        Assert.Equal(f.Note.TextHistory.Previous1, DataStore.Shortcuts.Get(f.Note.ID).TextPrevious1);
        // The pick itself is not a new editing burst.
        TypeAtEnd(window, "!");
        Assert.Equal("milk\r\neggs", f.Note.TextHistory.Previous1);
        Click(Items(window.BuildHistoryMenu()).Single(i => (string)i.Tag == "Before"));
        Assert.Equal("milk\r\neggs", Text(window).Text);
      });
    }

    [Fact]
    public void Picking_earlier_in_a_comment_keeps_the_text_you_had_as_before() {
      WithButtons(f => {
        f.Command.SaveCommentHistory(new TextHistorySlots("b", "2026-11-20T18:30:00", "e", "2026-11-19T10:00:00"));
        TextEditWindow window = Show(TextEditWindow.ForComment(f.Command));
        Click(Items(window.BuildHistoryMenu()).Single(i => (string)i.Tag == "Earlier"));
        Assert.Equal("e", Text(window).Text);
        Assert.Equal("e", DataStore.Shortcuts.Get(f.Command.ID).Comment);
        Assert.Equal(("Call Anna first.", "b"), (f.Command.CommentHistory.Previous1, f.Command.CommentHistory.Previous2));
      });
    }
  }
}
