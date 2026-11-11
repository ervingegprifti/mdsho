using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
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
        note.Comment = "Reviewed in May";
        Assert.True(note.ShowCommentDivider);
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
          Assert.Equal("Note: Restart checklist", Named<TextBlock>(window, "TextTitle").Text);
          Assert.Equal(note.OwningWindow.Appearance.Icons.Rename, Named<TextBlock>(window, "TextTitleIcon").Text);
          Assert.Equal(note.OwningWindow.Appearance.Icons.Rename, GlyphIcon.GlyphOf(window.Icon));
          TextBox text = Named<TextBox>(window, "TextComment");
          Assert.Equal("old", text.Text);
          Assert.True(window.Width > 520, "The note window is bigger than the comment window.");
          text.Text = "new text";
          Button save = Named<Button>(window, "ButtonSave");
          save.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, save));
          WpfAppHost.Pump();
        });
        Assert.Equal("new text", note.NoteText);
        Assert.Equal("", note.Comment ?? "");
        Assert.Equal("new text", Stored(note.ID).NoteText);
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
          MenuItem copy = items.Single(m => (m.Header as string) == "Copy note text");
          Assert.Equal(nameof(ShortcutViewModel.CopyNoteTextCommand), BindingOperations.GetBinding(copy, MenuItem.CommandProperty).Path.Path);
          Assert.Equal(nameof(ShortcutViewModel.ShowNote), BindingOperations.GetBinding(copy, UIElement.VisibilityProperty).Path.Path);
        });
      });
    }
  }
}
