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
  /// The History button (the time icon) in the title bar of notes, comments and snippets (1.22.0): the window keeps a copy
  /// at the first change after it opens, when it is left after a change (the text from before that run of edits), and at
  /// once for a big removal; never one equal to Now or to the newest copy. The menu lists Now and up to N copies, newest
  /// first, and Clear history...; a pick puts a copy back.
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

    private static string[] TextRows(ShortcutViewModel s) => s.History(HistoryFields.Text).Select(h => h.Text).ToArray();
    private static string[] CommentRows(ShortcutViewModel s) => s.History(HistoryFields.Comment).Select(h => h.Text).ToArray();

    private static HistoryEntry AddRow(ShortcutViewModel s, string field, string text, string keptAt) {
      HistoryEntry row = new HistoryEntry { ShortcutID = s.ID, Field = field, Text = text, KeptAt = keptAt };
      DataStore.History.Add(row);
      return row;
    }

    // Focus moves to another window (Window.Deactivated).
    private static void Deactivate(Window window) {
      typeof(Window).GetMethod("OnDeactivated", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
        .Invoke(window, new object[] { EventArgs.Empty });
      WpfAppHost.Pump();
    }

    private static MenuItem Row(ContextMenu menu, string text) => Items(menu).Single(i => i.Tag is HistoryEntry h && h.Text == text);

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
        Assert.Equal("", history.Content);
        f.Win.Appearance.Icons.SolidIcons = false;
        WpfAppHost.Pump();
        Assert.Equal("", history.Content);
        foreach (TextEditWindow other in new[] { Show(TextEditWindow.ForComment(f.Command)), Show(TextEditWindow.ForSnippet(f.Snippet)) }) {
          Assert.Equal("", Named(other, "ButtonHistory").Content);
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
    public void The_first_change_after_opening_keeps_the_saved_text() {
      WithButtons(f => {
        TextEditWindow window = OpenNote(f.Note);
        TypeAtEnd(window, "\r\nbread");
        HistoryEntry kept = f.Note.History(HistoryFields.Text).Single();
        Assert.Equal(("milk\r\neggs", "2026-11-21T09:00:00"), (kept.Text, kept.KeptAt));
        f.Now = f.Now.AddMinutes(2);
        TypeAtEnd(window, "\r\nbutter");
        Assert.Equal(new[] { "milk\r\neggs" }, TextRows(f.Note));
      });
    }

    // A copy must never equal the text: typing a letter and deleting it again keeps no copy, also when the window closes.
    [Fact]
    public void Typing_a_letter_and_deleting_it_again_keeps_no_copy() {
      WithButtons(f => {
        TextEditWindow window = OpenNote(f.Note);
        TypeAtEnd(window, "x");
        DeleteLast(window);
        Assert.Equal("milk\r\neggs", Text(window).Text);
        Assert.Empty(TextRows(f.Note));
        window.Close();
        WpfAppHost.Pump();
        Assert.Empty(DataStore.History.GetAll());
      });
    }

    [Fact]
    public void After_an_undone_change_a_real_change_keeps_the_copy() {
      WithButtons(f => {
        TextEditWindow window = OpenNote(f.Note);
        TypeAtEnd(window, "x");
        DeleteLast(window);
        TypeAtEnd(window, "\r\nbread");
        Assert.Equal(new[] { "milk\r\neggs" }, TextRows(f.Note));
      });
    }

    [Fact]
    public void Undoing_a_big_removal_takes_its_copy_back() {
      WithButtons(f => {
        AddRow(f.Note, HistoryFields.Text, "old", "2026-11-20T18:30:00");
        TextEditWindow window = OpenNote(f.Note);
        TypeOverAll(window, "x");
        Assert.Equal(new[] { "milk\r\neggs", "old" }, TextRows(f.Note));
        Text(window).Text = "milk\r\neggs";
        WpfAppHost.Pump();
        Assert.Equal(new[] { "old" }, TextRows(f.Note));
      });
    }

    // Undoing a big removal goes back to the history just before that removal: the opening text stays.
    [Fact]
    public void Pasting_back_after_cut_all_keeps_the_opening_text_in_the_history() {
      WithButtons(f => {
        AddRow(f.Note, HistoryFields.Text, "h0", "2026-11-20T18:30:00");
        TextEditWindow window = OpenNote(f.Note);
        TypeAtEnd(window, "x");
        TypeAtEnd(window, "\r\nbread");
        string b = Text(window).Text;
        Assert.Equal(new[] { "milk\r\neggs", "h0" }, TextRows(f.Note));
        TypeOverAll(window, "");
        Assert.Equal(new[] { b, "milk\r\neggs", "h0" }, TextRows(f.Note));
        TypeOverAll(window, b);
        Assert.Equal(new[] { "milk\r\neggs", "h0" }, TextRows(f.Note));
      });
    }

    [Fact]
    public void Deleting_all_right_after_typing_and_pasting_it_back_keeps_the_opening_text() {
      WithButtons(f => {
        TextEditWindow window = OpenNote(f.Note);
        TypeAtEnd(window, "x");
        TypeOverAll(window, "");
        TypeOverAll(window, "milk\r\neggsx");
        Assert.Equal(new[] { "milk\r\neggs" }, TextRows(f.Note));
      });
    }

    [Fact]
    public void Undo_after_a_taken_back_copy_goes_back_one_step_only() {
      WithButtons(f => {
        AddRow(f.Note, HistoryFields.Text, "h0", "2026-11-20T18:30:00");
        TextEditWindow window = OpenNote(f.Note);
        TypeAtEnd(window, "x");
        DeleteLast(window);
        Assert.Equal(new[] { "h0" }, TextRows(f.Note));
        TypeAtEnd(window, "y");
        Assert.Equal(new[] { "milk\r\neggs", "h0" }, TextRows(f.Note));
        TypeOverAll(window, "");
        TypeOverAll(window, "milk\r\neggsy");
        Assert.Equal(new[] { "milk\r\neggs", "h0" }, TextRows(f.Note));
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
        Assert.Equal(new[] { "milk\r\neggs" }, TextRows(f.Note));
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
        Assert.Equal(new[] { "milk\r\neggs 1", "milk\r\neggs" }, TextRows(f.Note));
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
        Assert.Equal(new[] { "milk\r\neggs 1", "milk\r\neggs" }, TextRows(reopened.Shortcut));
      });
    }

    [Fact]
    public void Typing_over_all_the_text_keeps_a_copy_at_once_mid_run() {
      WithButtons(f => {
        TextEditWindow window = Show(TextEditWindow.ForComment(f.Command));
        TypeAtEnd(window, " Then Ben.");
        f.Now = f.Now.AddSeconds(30);
        TypeOverAll(window, "x");
        Assert.Equal(new[] { "Call Anna first. Then Ben.", "Call Anna first." }, CommentRows(f.Command));
        Assert.Empty(TextRows(f.Command));
      });
    }

    [Fact]
    public void The_snippet_editor_keeps_copies_in_the_text_history() {
      WithButtons(f => {
        TextEditWindow window = Show(TextEditWindow.ForSnippet(f.Snippet));
        TypeAtEnd(window, ", 1000 Sample Town");
        Assert.Equal(new[] { "Main Street 12" }, TextRows(f.Snippet));
      });
    }

    // ---- Keeping a copy when the window is left ----

    // Each later run of edits keeps the text from before it when the window is left, never a copy of Now.
    [Fact]
    public void Leaving_after_a_later_run_of_edits_keeps_the_text_from_before_that_run() {
      WithButtons(f => {
        TextEditWindow window = OpenNote(f.Note);
        TypeAtEnd(window, " 1");
        Deactivate(window);
        Assert.Equal(new[] { "milk\r\neggs" }, TextRows(f.Note));
        f.Now = f.Now.AddMinutes(5);
        TypeAtEnd(window, " 2");
        TypeAtEnd(window, " 3");
        Assert.Equal(new[] { "milk\r\neggs" }, TextRows(f.Note));
        Deactivate(window);
        Assert.Equal(new[] { "milk\r\neggs 1", "milk\r\neggs" }, TextRows(f.Note));
        Assert.Equal("2026-11-21T09:05:00", f.Note.History(HistoryFields.Text)[0].KeptAt);
        Deactivate(window);
        Assert.Equal(2, TextRows(f.Note).Length);
      });
    }

    [Fact]
    public void Leaving_without_a_change_keeps_nothing_and_the_first_change_still_keeps_at_once() {
      WithButtons(f => {
        TextEditWindow window = OpenNote(f.Note);
        Deactivate(window);
        Assert.Empty(TextRows(f.Note));
        TypeAtEnd(window, " 1");
        Assert.Equal(new[] { "milk\r\neggs" }, TextRows(f.Note));
      });
    }

    [Fact]
    public void Leaving_after_edits_that_come_back_to_the_same_text_keeps_nothing() {
      WithButtons(f => {
        TextEditWindow window = OpenNote(f.Note);
        TypeAtEnd(window, " 1");
        Deactivate(window);
        TypeAtEnd(window, "x");
        DeleteLast(window);
        Deactivate(window);
        Assert.Equal(new[] { "milk\r\neggs" }, TextRows(f.Note));
      });
    }

    [Fact]
    public void Minimising_closing_and_exit_also_count_as_leaving() {
      WithButtons(f => {
        TextEditWindow window = OpenNote(f.Note);
        TypeAtEnd(window, " 1");
        Deactivate(window);
        TypeAtEnd(window, " 2");
        window.WindowState = WindowState.Minimized;
        WpfAppHost.Pump();
        Assert.Equal("milk\r\neggs 1", TextRows(f.Note)[0]);
        window.WindowState = WindowState.Normal;
        WpfAppHost.Pump();
        TypeAtEnd(window, " 3");
        window.Close();
        WpfAppHost.Pump();
        Assert.Equal("milk\r\neggs 1 2", TextRows(f.Note)[0]);
        TextEditWindow again = OpenNote(f.Note);
        TypeAtEnd(again, " 4");
        Deactivate(again);
        TypeAtEnd(again, " 5");
        try {
          Assert.Empty(TextEditWindow.SaveOpenNotesForExit());
        } finally {
          TextEditWindow.IsExiting = false;
        }
        Assert.Equal(new[] { "milk\r\neggs 1 2 3 4", "milk\r\neggs 1 2 3", "milk\r\neggs 1 2", "milk\r\neggs 1", "milk\r\neggs" }, TextRows(f.Note));
      });
    }

    // A big removal in a later run keeps the text from before that run first, then the text before the removal.
    [Fact]
    public void A_big_removal_in_a_later_run_keeps_the_run_start_too_and_its_undo_takes_both_back() {
      WithButtons(f => {
        TextEditWindow window = OpenNote(f.Note);
        TypeAtEnd(window, " 1");
        Deactivate(window);
        TypeAtEnd(window, " 2");
        TypeOverAll(window, "");
        Assert.Equal(new[] { "milk\r\neggs 1 2", "milk\r\neggs 1", "milk\r\neggs" }, TextRows(f.Note));
        TypeOverAll(window, "milk\r\neggs 1 2");
        Assert.Equal(new[] { "milk\r\neggs" }, TextRows(f.Note));
        Deactivate(window);
        Assert.Equal(new[] { "milk\r\neggs 1", "milk\r\neggs" }, TextRows(f.Note));
      });
    }

    [Fact]
    public void With_keep_0_nothing_is_kept() {
      WithButtons(f => {
        TextHistorySetting.SetKeep(0);
        TextEditWindow window = OpenNote(f.Note);
        TypeAtEnd(window, " 1");
        Deactivate(window);
        TypeOverAll(window, "");
        Assert.Empty(DataStore.History.GetAll());
        List<MenuItem> items = Items(window.BuildHistoryMenu());
        Assert.Equal(new[] { "Header", "Now", "Clear" }, items.Select(i => (string)i.Tag).ToArray());
        Assert.False(items[2].IsEnabled);
      });
    }

    // ---- The menu ----

    [Fact]
    public void The_menu_shows_now_and_the_copies_newest_first_then_clear_history() {
      WithButtons(f => {
        AddRow(f.Note, HistoryFields.Text, "x\r\nmore", "2026-11-19T16:44:00");
        AddRow(f.Note, HistoryFields.Text, "  ", "2026-11-20T18:30:00");
        AddRow(f.Note, HistoryFields.Text, "Old list\r\nmore", "2026-11-21T08:05:00");
        AddRow(f.Note, HistoryFields.Comment, "a comment", "2026-11-21T08:06:00");
        f.Now = new DateTime(2026, 11, 21, 15, 0, 0);
        ContextMenu menu = OpenNote(f.Note).BuildHistoryMenu();
        object[] all = menu.Items.Cast<object>().ToArray();
        Assert.Equal(8, all.Length);
        Assert.IsType<Separator>(all[2]);
        Assert.IsType<Separator>(all[6]);
        List<MenuItem> items = Items(menu);
        Assert.Equal(new[] { "Text history", "Now  ·  milk…", "Today 08:05  ·  Old list…", "Yesterday 18:30  ·  (empty)", "19 Nov 16:44  ·  x…", "Clear history..." },
          items.Select(i => (string)i.Header).ToArray());
        Assert.False(items[0].IsEnabled);
        Assert.True(items[1].IsChecked);
        Assert.All(items.Skip(2), i => Assert.False(i.IsChecked));
        Assert.All(items.Skip(1), i => Assert.True(i.IsEnabled));
        Assert.Equal("Now", items[1].Tag);
        Assert.Equal("Clear", items[5].Tag);
      });
    }

    [Fact]
    public void An_empty_history_shows_now_and_a_disabled_clear_history() {
      WithButtons(f => {
        List<MenuItem> items = Items(OpenNote(f.Note).BuildHistoryMenu());
        Assert.Equal(new[] { "Header", "Now", "Clear" }, items.Select(i => (string)i.Tag).ToArray());
        Assert.False(items[2].IsEnabled);
      });
    }

    [Fact]
    public void Picking_a_copy_puts_it_back_saves_it_and_can_be_undone() {
      WithButtons(f => {
        AddRow(f.Note, HistoryFields.Text, "older list", "2026-11-19T10:00:00");
        AddRow(f.Note, HistoryFields.Text, "old list", "2026-11-20T18:30:00");
        TextEditWindow window = OpenNote(f.Note);
        Click(Row(window.BuildHistoryMenu(), "old list"));
        Assert.Equal("old list", Text(window).Text);
        Assert.Equal("old list", DataStore.Shortcuts.Get(f.Note.ID).NoteText);
        Assert.Equal(new[] { "milk\r\neggs", "older list" }, TextRows(f.Note));
        Assert.Equal("2026-11-21T09:00:00", f.Note.History(HistoryFields.Text)[0].KeptAt);
        Click(Row(window.BuildHistoryMenu(), "milk\r\neggs"));
        Assert.Equal("milk\r\neggs", Text(window).Text);
        Assert.Equal(new[] { "old list", "older list" }, TextRows(f.Note));
      });
    }

    [Fact]
    public void Picking_in_a_comment_keeps_the_text_you_had_first() {
      WithButtons(f => {
        AddRow(f.Command, HistoryFields.Comment, "e", "2026-11-19T10:00:00");
        AddRow(f.Command, HistoryFields.Comment, "b", "2026-11-20T18:30:00");
        TextEditWindow window = Show(TextEditWindow.ForComment(f.Command));
        Click(Row(window.BuildHistoryMenu(), "e"));
        Assert.Equal("e", Text(window).Text);
        Assert.Equal("e", DataStore.Shortcuts.Get(f.Command.ID).Comment);
        Assert.Equal(new[] { "Call Anna first.", "b" }, CommentRows(f.Command));
      });
    }

    [Fact]
    public void Clear_history_asks_first_and_clears_only_this_text() {
      WithButtons(f => {
        AddRow(f.Note, HistoryFields.Text, "old", "2026-11-20T18:30:00");
        AddRow(f.Note, HistoryFields.Comment, "a comment", "2026-11-20T18:31:00");
        TextEditWindow window = OpenNote(f.Note);
        Func<string, string, bool> oldConfirm = Dialogs.Confirm;
        List<string> asked = new List<string>();
        try {
          Dialogs.Confirm = (message, caption) => { asked.Add(caption + ": " + message); return false; };
          Click(Items(window.BuildHistoryMenu()).Single(i => i.Tag as string == "Clear"));
          Assert.Equal(new[] { "old" }, TextRows(f.Note));
          Dialogs.Confirm = (message, caption) => { asked.Add(caption + ": " + message); return true; };
          Click(Items(window.BuildHistoryMenu()).Single(i => i.Tag as string == "Clear"));
          Assert.Empty(TextRows(f.Note));
          Assert.Equal(new[] { "a comment" }, CommentRows(f.Note));
          Assert.Equal(2, asked.Count);
          Assert.Equal("Clear history: Clear the text history of this note? The earlier texts can't be brought back.", asked[0]);
          Assert.False(Items(window.BuildHistoryMenu()).Single(i => i.Tag as string == "Clear").IsEnabled);
        } finally {
          Dialogs.Confirm = oldConfirm;
        }
      });
    }

    // 50 copies: the menu scrolls like a normal menu instead of running off the screen.
    [Fact]
    public void A_menu_of_50_copies_scrolls() {
      WithButtons(f => {
        TextHistorySetting.SetKeep(50);
        for (int i = 0; i < 50; i++) {
          AddRow(f.Note, HistoryFields.Text, "copy " + i, TextHistory.Stamp(f.Now.AddMinutes(-60 + i)));
        }
        TextEditWindow window = OpenNote(f.Note);
        Button history = Named(window, "ButtonHistory");
        history.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent, history));
        WpfAppHost.Pump();
        ContextMenu menu = window.HistoryMenu;
        try {
          Assert.Equal(50, Items(menu).Count(i => i.Tag is HistoryEntry));
          ScrollViewer scroll = FindChild<ScrollViewer>(menu);
          Assert.NotNull(scroll);
          Assert.NotEqual(ScrollBarVisibility.Disabled, scroll.VerticalScrollBarVisibility);
          double screen = SystemParameters.VirtualScreenHeight;
          Assert.True(menu.ActualHeight <= screen, "The menu is " + menu.ActualHeight + " high on a " + screen + " high screen.");
        } finally {
          menu.IsOpen = false;
          WpfAppHost.Pump();
        }
      });
    }

    private static T FindChild<T>(DependencyObject parent) where T : DependencyObject {
      for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++) {
        DependencyObject child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
        if (child is T found) {
          return found;
        }
        T deeper = FindChild<T>(child);
        if (deeper != null) {
          return deeper;
        }
      }
      return null;
    }
  }
}
