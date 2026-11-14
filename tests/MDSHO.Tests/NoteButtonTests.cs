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
  /// the note. Notes have no shortcut file.
  /// </summary>
  public class NoteButtonTests {

    private sealed class Fixture {
      public AppViewModel App;
      public DesktopWindowViewModel Win;
      public ShortcutViewModel Group;
      public List<ShortcutViewModel> NotesOpened = new List<ShortcutViewModel>();
      public List<ProcessStartInfo> Started = new List<ProcessStartInfo>();
    }

    private static void WithGroup(Action<Fixture> body) {
      WpfAppHost.Run(() => {
        Action<ShortcutViewModel, bool> oldRunEditor = ShortcutViewModel.OpenRunCommandEditor;
        Action<ShortcutViewModel> oldNoteEditor = ShortcutViewModel.OpenNoteEditor;
        Action<ProcessStartInfo> oldStarter = ShortcutViewModel.ProcessStarter;
        Fixture f = new Fixture();
        ShortcutViewModel.OpenRunCommandEditor = (s, isNew) => { };
        ShortcutViewModel.OpenNoteEditor = s => f.NotesOpened.Add(s);
        ShortcutViewModel.ProcessStarter = psi => f.Started.Add(psi);
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


    // ---- Wrap text ----

    private static void Click(Button button) {
      button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, button));
      WpfAppHost.Pump();
    }

    private static void AssertWraps(TextEditWindow window, bool wraps) {
      TextBox editor = Named<TextBox>(window, "TextComment");
      Button wrap = Named<Button>(window, "ButtonWrap");
      Assert.Equal(wraps ? TextWrapping.Wrap : TextWrapping.NoWrap, editor.TextWrapping);
      Assert.Equal(wraps ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto, editor.HorizontalScrollBarVisibility);
      string tip = wraps ? "Stop wrapping text" : "Wrap text";
      Assert.Equal(tip, wrap.ToolTip);
      Assert.Equal(tip, System.Windows.Automation.AutomationProperties.GetName(wrap));
      // Shaded while on, like the pin.
      Assert.Equal(wraps, wrap.ReadLocalValue(Control.BackgroundProperty) != DependencyProperty.UnsetValue);
    }

    [Fact]
    public void The_note_window_has_wrap_text_just_right_of_the_brush() {
      WithGroup(f => {
        ShortcutViewModel note = NewNoteInGroup(f);
        TextEditWindow window = TextEditWindow.ForNote(note);
        Shown(window, () => {
          window.UpdateLayout();
          Button wrap = Named<Button>(window, "ButtonWrap");
          Assert.Equal(Visibility.Visible, wrap.Visibility);
          Assert.Equal(note.OwningWindow.Appearance.Icons.WrapText, wrap.Content);
          FrameworkElement bar = Named<FrameworkElement>(window, "TitleBar");
          Rect rColors = BoundsIn(Named<Button>(window, "ButtonColors"), bar), rWrap = BoundsIn(wrap, bar), rPin = BoundsIn(Named<Button>(window, "ButtonPin"), bar);
          Assert.Equal(rColors.Right, rWrap.Left, 1);
          Assert.Equal(rWrap.Right, rPin.Left, 1);
          AssertWraps(window, false);
        });
      });
    }

    [Fact]
    public void Renaming_a_button_updates_the_title_of_its_open_note_and_comment_windows() {
      WithGroup(f => {
        ShortcutViewModel note = NewNoteInGroup(f);
        TextEditWindow noteWindow = TextEditWindow.ForNote(note);
        Shown(noteWindow, () => {
          note.Name = "Renamed note";
          Assert.Equal("Renamed note", noteWindow.Title);
          Assert.Equal("Renamed note", Named<TextBlock>(noteWindow, "TextTitle").Text);
        });
        TextEditWindow commentWindow = TextEditWindow.ForComment(f.Group);
        Shown(commentWindow, () => {
          f.Group.Name = "Renamed group";
          Assert.Equal("Renamed group", commentWindow.Title);
          Assert.Equal("Renamed group", Named<TextBlock>(commentWindow, "TextTitle").Text);
        });
        // Closed windows stop following the name.
        note.Name = "After close";
        Assert.Equal("Renamed note", noteWindow.Title);
      });
    }

    // ---- Renaming a note in its Note window ----

    private static void PressTitleKey(Window window, System.Windows.Input.Key key) {
      window.RaiseEvent(new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice, PresentationSource.FromVisual(window), 0, key) {
        RoutedEvent = System.Windows.Input.Keyboard.PreviewKeyDownEvent
      });
      WpfAppHost.Pump();
    }

    /// <summary>The visible items of an open menu, in order: a Separator shows as "-".</summary>
    private static List<string> VisibleMenuItems(FrameworkElement owner) {
      ContextMenu menu = owner.ContextMenu;
      menu.PlacementTarget = owner;
      menu.IsOpen = true;
      menu.UpdateLayout();
      WpfAppHost.Pump();
      try {
        return menu.Items.OfType<Control>().Where(c => c.Visibility == Visibility.Visible)
          .Select(c => c is Separator ? "-" : (((MenuItem)c).Header as string ?? "?")).ToList();
      } finally {
        menu.IsOpen = false;
        WpfAppHost.Pump();
      }
    }

    [Fact]
    public void Note_group_and_command_menus_have_no_separator_next_to_another_or_at_an_end() {
      WithGroup(f => {
        ShortcutViewModel note = NewNoteInGroup(f);
        ShortcutViewModel command = NewCommandInGroup(f);
        foreach (ShortcutViewModel shown in new[] { note, f.Group, command }) {
          WithView(f, shown, (view, button) => {
            List<string> items = VisibleMenuItems(button);
            string list = string.Join(" | ", items);
            Assert.NotEqual("-", items.First());
            Assert.NotEqual("-", items.Last());
            for (int i = 1; i < items.Count; i++) {
              Assert.False(items[i] == "-" && items[i - 1] == "-", "Two separators in a row: " + list);
            }
          });
        }
      });
    }

    [Fact]
    public void The_note_icon_stays_where_it_is_while_the_title_is_renamed() {
      WithGroup(f => {
        ShortcutViewModel note = NewNoteInGroup(f);
        TextEditWindow window = TextEditWindow.ForNote(note);
        Shown(window, () => {
          FrameworkElement bar = Named<FrameworkElement>(window, "TitleBar");
          TextBlock icon = Named<TextBlock>(window, "TextTitleIcon");
          window.UpdateLayout();
          Rect before = BoundsIn(icon, bar);
          double barHeight = bar.ActualHeight;
          window.BeginRenameTitle();
          WpfAppHost.Pump();
          window.UpdateLayout();
          Assert.Equal(before, BoundsIn(icon, bar));
          Assert.Equal(barHeight, bar.ActualHeight);
          window.EndRenameTitle(save: false);
        });
      });
    }

    private static ShortcutViewModel NewCommandInGroup(Fixture f) {
      ShortcutViewModel command = ViewModelFactory.NewProcessCommandShortcut(f.App, f.Group, null);
      f.Group.Children.Add(command);
      return command;
    }

    [Fact]
    public void A_run_command_has_Rename_but_no_target_items_in_its_menu_a_file_shortcut_has_them() {
      WithGroup(f => {
        ShortcutViewModel command = NewCommandInGroup(f);
        Assert.False(command.ShowSetNameTheSameAsTarget);
        Assert.False(command.ShowCopyTargetToMemory);
        ShortcutViewModel file = ViewModelFactory.NewShortcut(f.App, f.Group, null, "notepad", @"C:\Windows\notepad.exe", ".exe", "");
        Assert.True(file.ShowSetNameTheSameAsTarget);
        Assert.True(file.ShowCopyTargetToMemory);
        WithView(f, command, (view, button) => {
          List<string> items = VisibleMenuItems(button);
          Assert.Contains("Rename", items);
          Assert.DoesNotContain("Set name the same as target", items);
          Assert.DoesNotContain("Copy target to memory", items);
        });
      });
    }

    [Fact]
    public void Rename_and_F2_rename_a_note_and_a_run_command_in_the_panel_like_other_buttons() {
      WithGroup(f => {
        ShortcutViewModel note = NewNoteInGroup(f);
        ShortcutViewModel command = NewCommandInGroup(f);
        f.NotesOpened.Clear(); // a new note opens its window
        foreach (ShortcutViewModel button in new[] { note, command, f.Group }) {
          Assert.True(f.Win.HandleKey(ShortcutKey.F2, ShortcutModifiers.None, button, false));
          Assert.Equal(EditField.Name, button.EditField);
          button.CancelEditCommand.Execute(null);
          Assert.Equal(EditField.None, button.EditField);
          button.RenameCommand.Execute(null);
          Assert.Equal(EditField.Name, button.EditField);
          button.CancelEditCommand.Execute(null);
        }
        // Nothing opens a window to rename any more.
        Assert.Empty(f.NotesOpened);
      });
    }

    [Fact]
    public void The_note_title_says_double_click_to_rename_the_comment_title_does_not() {
      WithGroup(f => {
        ShortcutViewModel note = NewNoteInGroup(f);
        Assert.Equal(TextEditWindow.RenameTitleToolTip, Named<TextBlock>(TextEditWindow.ForNote(note), "TextTitle").ToolTip);
        Assert.Null(Named<TextBlock>(TextEditWindow.ForComment(f.Group), "TextTitle").ToolTip);
      });
    }

    [Fact]
    public void Renaming_from_the_note_title_saves_on_Enter_and_Esc_or_a_blank_name_keeps_it() {
      WithGroup(f => {
        ShortcutViewModel note = NewNoteInGroup(f);
        note.Name = "Old name";
        TextEditWindow window = TextEditWindow.ForNote(note);
        Shown(window, () => {
          TextBlock title = Named<TextBlock>(window, "TextTitle");
          TextBox edit = Named<TextBox>(window, "TextTitleEdit");
          Assert.False(window.IsRenamingTitle);
          window.BeginRenameTitle();
          WpfAppHost.Pump();
          Assert.True(window.IsRenamingTitle);
          Assert.Equal(Visibility.Collapsed, title.Visibility);
          Assert.Equal("Old name", edit.Text);
          edit.Text = "New name";
          PressTitleKey(window, System.Windows.Input.Key.Enter);
          Assert.False(window.IsRenamingTitle);
          Assert.True(window.IsVisible);
          Assert.Equal("New name", note.Name);
          Assert.Equal("New name", Stored(note.ID).Name);
          Assert.Equal("New name", title.Text);
          // Esc keeps the name and leaves the window open.
          window.BeginRenameTitle();
          edit.Text = "Discarded";
          PressTitleKey(window, System.Windows.Input.Key.Escape);
          Assert.True(window.IsVisible);
          Assert.Equal("New name", note.Name);
          // A blank name is ignored.
          window.BeginRenameTitle();
          edit.Text = "   ";
          window.EndRenameTitle(save: true);
          Assert.Equal("New name", note.Name);
        });
      });
    }

    [Fact]
    public void Closing_the_note_while_renaming_keeps_the_new_name() {
      WithGroup(f => {
        ShortcutViewModel note = NewNoteInGroup(f);
        note.Name = "Before";
        TextEditWindow window = TextEditWindow.ForNote(note);
        Shown(window, () => {
          window.BeginRenameTitle();
          Named<TextBox>(window, "TextTitleEdit").Text = "After";
          window.Close();
          WpfAppHost.Pump();
        });
        Assert.Equal("After", note.Name);
      });
    }

    [Fact]
    public void The_comment_window_has_wrap_text_just_left_of_ok() {
      WithGroup(f => {
        TextEditWindow window = TextEditWindow.ForComment(f.Group);
        Shown(window, () => {
          window.UpdateLayout();
          Button wrap = Named<Button>(window, "ButtonWrap");
          Assert.Equal(Visibility.Visible, wrap.Visibility);
          Assert.Equal(f.Win.Appearance.Icons.WrapText, wrap.Content);
          FrameworkElement bar = Named<FrameworkElement>(window, "TitleBar");
          Assert.Equal(BoundsIn(Named<Button>(window, "ButtonOk"), bar).Left, BoundsIn(wrap, bar).Right, 1);
          AssertWraps(window, false);
        });
      });
    }

    [Fact]
    public void The_wrap_text_icon_follows_the_solid_icons_setting_while_open() {
      WithGroup(f => {
        IconGlyphs icons = f.Win.Appearance.Icons;
        bool solid = icons.SolidIcons;
        TextEditWindow window = TextEditWindow.ForComment(f.Group);
        try {
          Shown(window, () => {
            icons.SolidIcons = !solid;
            WpfAppHost.Pump();
            Assert.Equal(icons.WrapText, Named<Button>(window, "ButtonWrap").Content);
          });
        } finally {
          icons.SolidIcons = solid;
        }
      });
    }

    [Fact]
    public void Wrap_text_in_a_comment_is_saved_with_its_panel_and_is_not_a_change_to_the_comment() {
      WithGroup(f => {
        f.Group.Comment = "old";
        TextEditWindow window = TextEditWindow.ForComment(f.Group);
        Shown(window, () => {
          Button wrap = Named<Button>(window, "ButtonWrap");
          Click(wrap);
          AssertWraps(window, true);
          Assert.True(f.Win.Appearance.CommentWrapText);
          Assert.True(DataStore.Windows.Get(f.Win.ID).CommentWrapText);
          Assert.False(Named<Button>(window, "ButtonOk").IsEnabled);
          Click(wrap);
          AssertWraps(window, false);
          Assert.False(f.Win.Appearance.CommentWrapText);
          Assert.False(DataStore.Windows.Get(f.Win.ID).CommentWrapText);
        });
        Assert.Equal("old", f.Group.Comment);
      });
    }

    [Fact]
    public void A_comment_opens_wrapped_when_its_panel_wraps_comments() {
      WithGroup(f => {
        f.Win.Appearance.CommentWrapText = true;
        TextEditWindow window = TextEditWindow.ForComment(f.Group);
        Shown(window, () => AssertWraps(window, true));
      });
    }

    [Fact]
    public void Wrapping_a_note_does_not_change_how_the_panel_wraps_comments() {
      WithGroup(f => {
        ShortcutViewModel note = NewNoteInGroup(f);
        TextEditWindow window = TextEditWindow.ForNote(note);
        Shown(window, () => {
          Click(Named<Button>(window, "ButtonWrap"));
          AssertWraps(window, true);
          Assert.False(f.Win.Appearance.CommentWrapText);
        });
      });
    }

    [Fact]
    public void A_panel_loads_its_comment_wrap_text_from_the_database() {
      WithGroup(f => {
        DataStore.Windows.UpdateCommentWrapText(f.Win.ID, true);
        DesktopWindowViewModel loaded = ViewModelFactory.WindowToViewModel(f.App, DataStore.Windows.Get(f.Win.ID));
        Assert.True(loaded.Appearance.CommentWrapText);
      });
    }


    // ---- Toggling from the button ----

    [Fact]
    public void Triggering_a_note_button_toggles_its_note_window() {
      WithGroup(f => {
        ShortcutViewModel note = NewNoteInGroup(f);
        f.NotesOpened.Clear();
        List<ShortcutViewModel> toggled = new List<ShortcutViewModel>();
        Action<ShortcutViewModel> old = ShortcutViewModel.ToggleNoteEditor;
        ShortcutViewModel.ToggleNoteEditor = s => toggled.Add(s);
        try {
          note.Click(true);
          note.DoubleClick(false);
          note.RunShortcut(false);
          Assert.Equal(new[] { note, note, note }, toggled);
          Assert.Empty(f.NotesOpened);
          // Edit note... from the menu always opens it (or brings it to the front).
          note.EditNoteCommand.Execute(null);
          Assert.Equal(new[] { note }, f.NotesOpened);
          Assert.Equal(3, toggled.Count);
        } finally {
          ShortcutViewModel.ToggleNoteEditor = old;
        }
      });
    }

    // ---- Double and triple click in the editor ----

    private static void ClickEditor(TextEditWindow window, int clicks, out bool handled) {
      TextBox editor = Named<TextBox>(window, "TextComment");
      System.Windows.Input.MouseButtonEventArgs args = new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0, System.Windows.Input.MouseButton.Left) {
        RoutedEvent = UIElement.PreviewMouseLeftButtonDownEvent
      };
      typeof(System.Windows.Input.MouseButtonEventArgs).GetProperty("ClickCount").SetValue(args, clicks);
      editor.RaiseEvent(args);
      handled = args.Handled;
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Double_click_selects_the_word_and_triple_click_the_paragraph(bool isNote) {
      WithGroup(f => {
        ShortcutViewModel button = isNote ? NewNoteInGroup(f) : f.Group;
        TextEditWindow window = isNote ? TextEditWindow.ForNote(button) : TextEditWindow.ForComment(button);
        Func<TextBox, System.Windows.Input.MouseButtonEventArgs, int> old = TextEditWindow.CharacterIndexAt;
        TextEditWindow.CharacterIndexAt = (box, e) => 9;
        try {
          Shown(window, () => {
            TextBox editor = Named<TextBox>(window, "TextComment");
            editor.Text = "Restart the server now.\r\nThen check the dashboard.";
            editor.Select(0, 0);
            ClickEditor(window, 2, out bool handled);
            Assert.True(handled);
            Assert.Equal("the", editor.SelectedText);
            ClickEditor(window, 3, out handled);
            Assert.True(handled);
            Assert.Equal("Restart the server now.", editor.SelectedText);
            // A single click is left to the text box.
            editor.Select(0, 0);
            ClickEditor(window, 1, out handled);
            Assert.False(handled);
            Assert.Equal("", editor.SelectedText);
          });
        } finally {
          TextEditWindow.CharacterIndexAt = old;
        }
      });
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
          Assert.Equal(note.OwningWindow.Appearance.Icons.Note, Named<TextBlock>(window, "TextTitleIcon").Text);
          Assert.Equal(note.OwningWindow.Appearance.Icons.Note, GlyphIcon.GlyphOf(window.Icon));
          TextBox text = Named<TextBox>(window, "TextComment");
          Assert.Equal("old", text.Text);
          Assert.Equal(640, window.Width);
          Assert.Equal(520, window.Height);
          // A note keeps its own smallest size.
          Assert.Equal(380, window.MinWidth);
          Assert.Equal(300, window.MinHeight);
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
          Assert.Null(window.FindName("Footer"));
          Assert.Equal(Visibility.Collapsed, Named<Button>(window, "ButtonOk").Visibility);
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
    public void The_pin_shows_the_thumbtack_left_when_not_on_top_and_down_when_on_top() {
      WithGroup(f => {
        ShortcutViewModel note = NewNoteInGroup(f);
        IconGlyphs icons = note.OwningWindow.Appearance.Icons;
        bool solid = icons.SolidIcons;
        TextEditWindow window = TextEditWindow.ForNote(note);
        try {
          Shown(window, () => {
            Button pin = Named<Button>(window, "ButtonPin");
            Assert.NotEqual("Segoe MDL2 Assets", pin.FontFamily.Source);
            Assert.False(window.Topmost);
            Assert.Equal(icons.PinOff, pin.Content);
            Assert.Equal("Keep on top", pin.ToolTip);
            pin.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, pin));
            WpfAppHost.Pump();
            Assert.True(window.Topmost);
            Assert.Equal(icons.PinOn, pin.Content);
            Assert.Equal("Stop keeping on top", pin.ToolTip);
            Assert.Equal("Stop keeping on top", System.Windows.Automation.AutomationProperties.GetName(pin));
            // The solid or outline setting changes the thumbtack while the window is open.
            icons.SolidIcons = !solid;
            WpfAppHost.Pump();
            Assert.Equal(icons.PinOn, pin.Content);
            pin.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, pin));
            WpfAppHost.Pump();
            Assert.False(window.Topmost);
            Assert.Equal(icons.PinOff, pin.Content);
          });
        } finally {
          icons.SolidIcons = solid;
        }
      });
    }

    [Fact]
    public void The_comment_window_has_ok_and_close_in_its_title_bar_and_no_bottom_bar() {
      WithGroup(f => {
        TextEditWindow window = TextEditWindow.ForComment(f.Group);
        Shown(window, () => {
          window.UpdateLayout();
          foreach (string gone in new[] { "Footer", "ButtonSave", "ButtonCancel", "TextHint", "TextFooterHint" }) {
            Assert.Null(window.FindName(gone));
          }
          Assert.Equal(Visibility.Collapsed, Named<Button>(window, "ButtonPin").Visibility);
          Assert.Equal(Visibility.Collapsed, Named<Button>(window, "ButtonColors").Visibility);
          Button ok = Named<Button>(window, "ButtonOk");
          Button close = Named<Button>(window, "ButtonClose");
          Assert.Equal(Visibility.Visible, ok.Visibility);
          Assert.Equal("Save (Ctrl+Enter)", ok.ToolTip);
          Assert.Equal("Save", System.Windows.Automation.AutomationProperties.GetName(ok));
          Assert.Equal("Close without saving (Esc)", close.ToolTip);
          Assert.Equal("Close without saving", System.Windows.Automation.AutomationProperties.GetName(close));
          // OK sits just left of X.
          FrameworkElement bar = Named<FrameworkElement>(window, "TitleBar");
          Rect rOk = BoundsIn(ok, bar), rClose = BoundsIn(close, bar);
          Assert.Equal(rClose.Left, rOk.Right, 1);
          // The text reaches the bottom of the window.
          FrameworkElement client = (FrameworkElement)window.Content;
          Assert.Equal(client.ActualHeight, BoundsIn(Named<TextBox>(window, "TextComment"), client).Bottom, 1);
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
          // A comment is short: its window is small (300 x 150), and may be made that small again by hand.
          Assert.Equal(300, window.Width);
          Assert.Equal(150, window.Height);
          Assert.Equal(300, window.MinWidth);
          Assert.Equal(150, window.MinHeight);
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
          Assert.Equal(f.Win.Appearance.Icons.Note, icon.Text);
        });
      });
    }

    [Fact]
    public void New_group_command_and_note_show_the_icon_of_the_button_they_make_in_both_menus() {
      WithGroup(f => {
        ShortcutViewModel note = NewNoteInGroup(f);
        WithView(f, note, (view, button) => {
          TreeView tree = (TreeView)view.FindName("TreeViewShortcuts");
          TreeViewItem groupItem = WpfAppHost.VisualDescendants<TreeViewItem>(tree).Single(i => i.DataContext == f.Group);
          Button groupButton = WpfAppHost.VisualDescendants<Button>(groupItem).First(b => b.DataContext == f.Group);
          (string header, string icon)[] expected = { ("New group", "Shortcuts"), ("New command", "Run"), ("New note", "Note") };
          // Empty space of the panel (bound to the window) and a group's menu (bound to the group's window).
          foreach ((ContextMenu menu, string prefix) in new[] { (tree.ContextMenu, "Appearance.Icons."), (groupButton.ContextMenu, "OwningWindow.Appearance.Icons.") }) {
            foreach ((string header, string icon) in expected) {
              MenuItem item = menu.Items.OfType<MenuItem>().Single(m => (m.Header as string) == header);
              Assert.Equal(prefix + icon, BindingOperations.GetBinding((TextBlock)item.Icon, TextBlock.TextProperty).Path.Path);
            }
          }
          // And they show the glyphs of the panel's icon set (here the group's menu, opened).
          groupButton.ContextMenu.PlacementTarget = groupButton;
          groupButton.ContextMenu.IsOpen = true;
          WpfAppHost.Pump();
          try {
            IconGlyphs icons = f.Win.Appearance.Icons;
            string[] glyphs = expected.Select(e => ((TextBlock)groupButton.ContextMenu.Items.OfType<MenuItem>().Single(m => (m.Header as string) == e.header).Icon).Text).ToArray();
            Assert.Equal(new[] { icons.Shortcuts, icons.Run, icons.Note }, glyphs);
            Assert.DoesNotContain(icons.Plus, glyphs);
          } finally {
            groupButton.ContextMenu.IsOpen = false;
            WpfAppHost.Pump();
          }
        });
      });
    }

    [Fact]
    public void Menus_have_New_note_and_a_note_has_Edit_note_but_no_Copy_note_text() {
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
          Assert.Equal("OwningWindow.Appearance.Icons.Note", BindingOperations.GetBinding((TextBlock)edit.Icon, TextBlock.TextProperty).Path.Path);
          MenuItem comment = items.Single(m => (m.Header as string) == "Comment...");
          Assert.Equal(nameof(ShortcutViewModel.ShowComment), BindingOperations.GetBinding(comment, UIElement.VisibilityProperty).Path.Path);
          // The note's text is copied from its Note window.
          Assert.DoesNotContain(items, m => (m.Header as string) == "Copy note text");
          Assert.Equal(new[] { "Mark", "-", "Rename", "-", "Edit note...", "-", "Cut", "Copy", "Paste", "-", "Delete" }, VisibleMenuItems(button));
        });
      });
    }
  }
}
