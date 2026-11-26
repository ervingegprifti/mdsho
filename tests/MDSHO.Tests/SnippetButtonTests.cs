using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Microsoft.Data.Sqlite;
using MDSHO.Common;
using MDSHO.Core;
using MDSHO.Data;
using MDSHO.ViewModels;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// Snippets (1.19.2): a name and a value. Double click (single click in Single click to open panels) pastes the
  /// value into the window the user came from; Copy snippet to memory copies it; Edit snippet... edits it. Stored in
  /// Shortcuts.IsSnippet and Shortcuts.SnippetText (database version 19). The real paste never runs here: it goes
  /// through <see cref="ShortcutViewModel.PasteValue"/>, which the tests replace.
  /// </summary>
  public class SnippetButtonTests : IDisposable {
    private TempDb db = new TempDb();
    private readonly AppViewModel app = new AppViewModel();
    private readonly DesktopWindowViewModel win;
    private readonly Action<string> oldCopy = ShortcutViewModel.CopyText;
    private readonly Func<string, Task<PasteOutcome>> oldPaste = ShortcutViewModel.PasteValue;
    private readonly Action<ShortcutViewModel> oldValueEditor = ShortcutViewModel.OpenSnippetEditor;
    private readonly Action<string, string, string> oldInfo = Dialogs.ShowInfo;
    private readonly Action<string, string, string> oldError = Dialogs.ShowError;
    private readonly List<string> copied = new List<string>();
    private readonly List<string> pasted = new List<string>();
    private readonly List<ShortcutViewModel> edited = new List<ShortcutViewModel>();
    private readonly List<(string caption, string heading, string text)> infos = new List<(string, string, string)>();
    private readonly List<(string caption, string error, string detail)> errors = new List<(string, string, string)>();
    private PasteOutcome outcome = PasteOutcome.Pasted;
    private readonly string work = Path.Combine(Path.GetTempPath(), "MDSHO.Tests", "clip-" + Guid.NewGuid().ToString("N"));

    public SnippetButtonTests() {
      Directory.CreateDirectory(work);
      ShortcutViewModel.CopyText = text => copied.Add(text);
      ShortcutViewModel.PasteValue = value => { pasted.Add(value); return Task.FromResult(outcome); };
      ShortcutViewModel.OpenSnippetEditor = s => edited.Add(s);
      Dialogs.ShowInfo = (caption, heading, text) => infos.Add((caption, heading, text));
      Dialogs.ShowError = (caption, error, detail) => errors.Add((caption, error, detail));
      win = app.AddWindow("TEST", 0, 0);
    }

    public void Dispose() {
      ShortcutViewModel.CopyText = oldCopy;
      ShortcutViewModel.PasteValue = oldPaste;
      ShortcutViewModel.OpenSnippetEditor = oldValueEditor;
      Dialogs.ShowInfo = oldInfo;
      Dialogs.ShowError = oldError;
      db.Dispose();
      try { Directory.Delete(work, true); } catch (IOException) { }
    }

    private ShortcutViewModel NewSnippet(string name = "Home address", string value = "Main Street 12\r\n1000 Sample Town") {
      win.NewSnippetShortcutCommand.Execute(null);
      ShortcutViewModel clip = win.RootShortcuts.Last();
      clip.CancelEdit();
      clip.Name = name;
      clip.SnippetText = value;
      return clip;
    }

    private ShortcutViewModel Group(string name) {
      ShortcutViewModel g = ViewModelFactory.NewGroupShortcut(app, null, win);
      win.RootShortcuts.Add(g);
      g.Name = name;
      return g;
    }

    private static Shortcut Stored(long id) {
      return DataStore.Shortcuts.Get(id);
    }

    // ---- Database version 19 ----

    private long Columns(string table, string column) {
      return db.Scalar("SELECT count(*) FROM pragma_table_info('" + table + "') WHERE name = '" + column + "' COLLATE NOCASE");
    }

    // A version 18 database with one note and one run command.
    private void BuildV18Db() {
      db.Dispose();
      db = new TempDb(false);
      db.Exec("PRAGMA user_version = 9;" + DatabaseV10MigrationTests.V9Sql
        + "INSERT INTO MDSHOWindows (ID,Title,ScreenDeviceName,Left,Top,Width,Height,BorderColor,BackgroundColor,TextColor,TitleBackgroundColor,TitleTextColor,Notes,SolidIcons,SingleClickToOpen) VALUES "
        + "(1,'W1','D1',0,0,300,400,'#FF000001','#FF000002','#FF000003','#FF000004','#FF000005','',1,1);"
        + "INSERT INTO Shortcuts VALUES (1,NULL,1,'N','','','g1',0,0,'',0,'#F',0,0,'','',0,'',1,'text');"
        + "INSERT INTO Shortcuts VALUES (2,NULL,1,'C','','','g2',0,0,'c',0,'#F',1,1,'cmd.exe','/K dir',0,'',0,'');");
      using (SqliteConnection c = db.Open()) {
        foreach (int version in new[] { 10, 11, 12, 13, 14, 15, 16, 17, 18 }) {
          SchemaManager.Migrations.Single(m => m.Version == version).Apply(c);
        }
      }
      db.Exec("PRAGMA user_version = 18;");
    }

    [Fact]
    public void Version_19_is_a_known_step() {
      Assert.True(AppConstants.DatabaseVersion >= 19);
      Assert.Single(SchemaManager.Migrations, m => m.Version == 19);
    }

    [Fact]
    public void V18_database_migrates_to_19_with_no_snippets_and_keeps_the_rest() {
      BuildV18Db();
      DataStore.Upgrade();
      Assert.Equal(AppConstants.DatabaseVersion, db.Scalar("PRAGMA user_version"));
      Assert.Equal(1, Columns("Shortcuts", "IsSnippet"));
      Assert.Equal(1, Columns("Shortcuts", "SnippetText"));
      Shortcut note = Stored(1), command = Stored(2);
      Assert.Equal((1L, "text", 0L, ""), (note.IsNote, note.NoteText, note.IsSnippet, note.SnippetText));
      Assert.Equal((1L, "/K dir", 0L, ""), (command.IsProcessCommand, command.ProcessArguments, command.IsSnippet, command.SnippetText));
      Assert.Single(Directory.GetFiles(db.Folder, "test.sqlite3.bak-v18-*"));
    }

    [Fact]
    public void Running_the_v19_step_twice_does_nothing_the_second_time() {
      BuildV18Db();
      DataStore.Upgrade();
      using (SqliteConnection c = db.Open()) {
        SchemaManager.Migrations.Single(m => m.Version == 19).Apply(c);
      }
      Assert.Equal(1, Columns("Shortcuts", "SnippetText"));
    }

    [Fact]
    public void A_new_database_has_the_columns_and_stores_a_snippet_and_its_text() {
      Assert.Equal(1, Columns("Shortcuts", "IsSnippet"));
      Assert.Equal(1, Columns("Shortcuts", "SnippetText"));
      Shortcut created = DataStore.Shortcuts.CreateSnippet(null, win.ID);
      Assert.Equal(("New snippet", 1L, ""), (created.Name, created.IsSnippet, created.SnippetText));
      DataStore.Shortcuts.UpdateSnippetText(created.ID, "line 1\nline 2");
      Assert.Equal("line 1\nline 2", Stored(created.ID).SnippetText);
      DataStore.Shortcuts.UpdateSnippetText(created.ID, null);
      Assert.Equal("", Stored(created.ID).SnippetText);
    }

    // ---- New snippet ----

    [Fact]
    public void New_snippet_in_a_panel_adds_an_empty_snippet_starts_its_rename_and_needs_no_file() {
      win.NewSnippetShortcutCommand.Execute(null);
      ShortcutViewModel clip = Assert.Single(win.RootShortcuts);
      Assert.True(clip.IsSnippet);
      Assert.Equal("New snippet", clip.Name);
      Assert.Equal("", clip.SnippetText);
      Assert.Equal(EditField.Name, clip.EditField);
      Assert.True(clip.IsSelected);
      Assert.Empty(edited);
      Assert.False(Directory.Exists(Paths.GetShortcutsPath()) && Directory.GetFiles(Paths.GetShortcutsPath()).Length > 0);
      Shortcut stored = Stored(clip.ID);
      Assert.Equal((1L, 0L, 0L, 0L), (stored.IsSnippet, stored.IsNote, stored.IsProcessCommand, stored.IsGroup));
    }

    [Fact]
    public void New_snippet_in_a_group_adds_it_to_the_group_and_only_groups_offer_it() {
      ShortcutViewModel group = Group("G");
      Assert.True(group.NewSnippetShortcutCommand.CanExecute(null));
      group.NewSnippetShortcutCommand.Execute(null);
      ShortcutViewModel clip = Assert.Single(group.Children);
      Assert.True(clip.IsSnippet);
      Assert.Equal(EditField.Name, clip.EditField);
      Assert.Equal(group.ID, Stored(clip.ID).ParentShortcutID);
      Assert.False(clip.NewSnippetShortcutCommand.CanExecute(null));
    }

    [Fact]
    public void New_snippet_in_a_clone_goes_to_its_panel() {
      ViewModelFactory.CloneWindow(app, win, DataStore.Clones.Create(win.ID, "D9", 1, 2));
      DesktopWindowViewModel clone = win.Clones.Single();
      clone.NewSnippetShortcutCommand.Execute(null);
      Assert.True(Assert.Single(win.RootShortcuts).IsSnippet);
    }

    // ---- What a snippet shows ----

    [Fact]
    public void Its_kind_and_icon_are_the_snippet_glyph_in_both_icon_sets() {
      ShortcutViewModel clip = NewSnippet();
      Assert.Equal(ShortcutTargetKind.Snippet, clip.TargetKind);
      Assert.Equal(new IconGlyphs(true).Snippet, new IconGlyphs(true).ForKind(ShortcutTargetKind.Snippet));
      Assert.Equal(new IconGlyphs(false).Snippet, new IconGlyphs(false).ForKind(ShortcutTargetKind.Snippet));
      Assert.NotEqual(new IconGlyphs(true).Paste, new IconGlyphs(true).ForKind(ShortcutTargetKind.Snippet));
    }

    [Fact]
    public void It_shows_the_snippet_items_and_not_the_ones_of_other_kinds() {
      ShortcutViewModel clip = NewSnippet();
      Assert.True(clip.ShowSnippet);
      Assert.True(clip.ShowCopySnippetToMemory);
      Assert.True(clip.ShowComment);
      Assert.False(clip.ShowShortcut);
      Assert.False(clip.ShowCopyTargetToMemory);
      Assert.False(clip.ShowSetNameTheSameAsTarget);
      Assert.False(clip.ShowNote);
      Assert.False(clip.ShowCommand);
      Assert.False(clip.ShowCopyCommandToMemory);
      Assert.False(clip.ShowGroup);
      ShortcutViewModel group = Group("G");
      Assert.False(group.ShowSnippet);
      Assert.False(group.ShowCopySnippetToMemory);
    }

    [Fact]
    public void The_tooltip_shows_double_click_to_paste_then_the_text_and_never_the_name() {
      ShortcutViewModel clip = NewSnippet();
      string nl = Environment.NewLine;
      Assert.Equal("Double-click to paste:" + nl + "Main Street 12" + nl + "1000 Sample Town", clip.ToolTipText);
      Assert.True(clip.HasToolTip);
    }

    [Fact]
    public void In_a_single_click_panel_the_tooltip_says_click_to_paste_and_follows_the_setting() {
      ShortcutViewModel clip = NewSnippet(value: "v");
      List<string> changed = new List<string>();
      clip.PropertyChanged += (s, e) => changed.Add(e.PropertyName);
      win.Appearance.SingleClickToOpen = true;
      Assert.StartsWith("Click to paste:" + Environment.NewLine, clip.ToolTipText);
      Assert.Contains(nameof(ShortcutViewModel.ToolTipText), changed);
      win.Appearance.SingleClickToOpen = false;
      Assert.StartsWith("Double-click to paste:" + Environment.NewLine, clip.ToolTipText);
    }

    [Fact]
    public void A_button_inside_a_group_follows_its_panels_click_setting_too() {
      ShortcutViewModel group = Group("G");
      group.NewSnippetShortcutCommand.Execute(null);
      ShortcutViewModel clip = group.Children.Single();
      clip.CancelEdit();
      List<string> changed = new List<string>();
      clip.PropertyChanged += (s, e) => changed.Add(e.PropertyName);
      win.Appearance.SingleClickToOpen = true;
      Assert.StartsWith("Click to paste:", clip.ToolTipText);
      Assert.Contains(nameof(ShortcutViewModel.ToolTipText), changed);
    }

    [Fact]
    public void The_tooltip_shows_the_comment_below_like_other_buttons() {
      ShortcutViewModel clip = NewSnippet(value: "v");
      clip.Comment = "For the bank form";
      Assert.True(clip.HasComment);
      Assert.True(clip.ShowCommentDivider);
      Assert.Equal("For the bank form", clip.CommentPreview);
    }

    [Fact]
    public void An_empty_snippet_shows_the_hint_and_a_grey_empty() {
      ShortcutViewModel clip = NewSnippet(value: "");
      string nl = Environment.NewLine;
      Assert.Equal("Double-click to paste:" + nl + "(empty)", clip.ToolTipText);
      Assert.True(Assert.Single(clip.ToolTipLines).IsGrey);
    }

    [Fact]
    public void A_changed_text_is_saved_and_updates_the_tooltip() {
      ShortcutViewModel clip = NewSnippet(value: "old");
      List<string> changed = new List<string>();
      clip.PropertyChanged += (s, e) => changed.Add(e.PropertyName);
      clip.SaveSnippetText("new value");
      Assert.Equal("new value", clip.SnippetText);
      Assert.Equal("new value", Stored(clip.ID).SnippetText);
      Assert.Contains(nameof(ShortcutViewModel.ToolTipText), changed);
    }

    // ---- Copy snippet to memory and Edit snippet ----

    [Fact]
    public void Copy_snippet_to_memory_copies_the_text_exactly_without_group_values() {
      ShortcutViewModel group = Group("Prod {SRV01}");
      group.NewSnippetShortcutCommand.Execute(null);
      ShortcutViewModel clip = group.Children.Single();
      clip.CancelEdit();
      clip.SnippetText = "ping {=1}\r\nsecond line";
      clip.CopySnippetToMemoryCommand.Execute(null);
      Assert.Equal(new[] { "ping {=1}\r\nsecond line" }, copied);
      Assert.Empty(pasted);
    }

    [Fact]
    public void Copy_snippet_to_memory_of_an_empty_text_copies_nothing() {
      ShortcutViewModel clip = NewSnippet(value: "");
      clip.CopySnippetToMemoryCommand.Execute(null);
      Assert.Empty(copied);
    }

    [Fact]
    public void Set_name_the_same_as_value_uses_the_first_non_blank_line_trimmed_and_saves_it() {
      ShortcutViewModel clip = NewSnippet(name: "Old", value: "\r\n   \r\n  Main Street 12  \r\n1000 Sample Town");
      Assert.True(clip.SetNameTheSameAsValueCommand.CanExecute(null));
      clip.SetNameTheSameAsValueCommand.Execute(null);
      Assert.Equal("Main Street 12", clip.Name);
      Assert.Equal("Main Street 12", Stored(clip.ID).Name);
    }

    [Fact]
    public void Set_name_the_same_as_value_does_nothing_and_is_disabled_for_a_blank_value() {
      ShortcutViewModel clip = NewSnippet(name: "Keep", value: " \r\n \t ");
      Assert.False(clip.SetNameTheSameAsValueCommand.CanExecute(null));
      clip.SetNameTheSameAsValueCommand.Execute(null);
      Assert.Equal("Keep", clip.Name);
    }

    [Fact]
    public void Set_name_the_same_as_value_is_enabled_once_the_snippet_gets_text() {
      ShortcutViewModel clip = NewSnippet(value: "");
      bool changed = false;
      clip.SetNameTheSameAsValueCommand.CanExecuteChanged += (s, e) => changed = true;
      Assert.False(clip.SetNameTheSameAsValueCommand.CanExecute(null));
      clip.SaveSnippetText("Customer 42");
      Assert.True(changed);
      Assert.True(clip.SetNameTheSameAsValueCommand.CanExecute(null));
    }

    [Fact]
    public void Only_snippets_show_set_name_the_same_as_value() {
      ShortcutViewModel clip = NewSnippet();
      Assert.True(clip.ShowSetNameTheSameAsValue);
      Assert.False(Group("G").ShowSetNameTheSameAsValue);
      ShortcutViewModel file = new ShortcutViewModel(null, null, 2, null, "f", @"C:\x.txt", ".lnk", "g2", false, false, null, false, null, 0, false, "", "");
      Assert.False(file.ShowSetNameTheSameAsValue);
      Assert.True(file.ShowSetNameTheSameAsTarget);
    }

    [Fact]
    public void Edit_snippet_opens_the_snippet_editor() {
      ShortcutViewModel clip = NewSnippet();
      clip.EditSnippetCommand.Execute(null);
      Assert.Same(clip, Assert.Single(edited));
    }

    // ---- Double click pastes for the user ----

    [Fact]
    public void Double_click_pastes_the_text_exactly_without_group_values_and_shows_nothing_when_it_worked() {
      ShortcutViewModel group = Group("Prod {SRV01}");
      group.NewSnippetShortcutCommand.Execute(null);
      ShortcutViewModel clip = group.Children.Single();
      clip.CancelEdit();
      clip.SnippetText = "user {=1}";
      clip.DoubleClick(false);
      Assert.Equal(new[] { "user {=1}" }, pasted);
      Assert.Empty(infos);
      Assert.Empty(copied);
    }

    [Fact]
    public void A_single_click_pastes_only_in_single_click_panels() {
      ShortcutViewModel clip = NewSnippet(value: "v");
      clip.Click(false);
      Assert.Empty(pasted);
      clip.DoubleClick(true);
      Assert.Empty(pasted);
      clip.Click(true);
      Assert.Equal(new[] { "v" }, pasted);
    }

    [Fact]
    public void Run_pastes_too() {
      ShortcutViewModel clip = NewSnippet(value: "v");
      clip.Run();
      Assert.Equal(new[] { "v" }, pasted);
    }

    [Fact]
    public void Panels_brought_to_front_go_back_before_the_paste_but_not_for_an_empty_text() {
      Action oldOpened = ShortcutViewModel.Opened;
      List<string> order = new List<string>();
      try {
        ShortcutViewModel.Opened = () => order.Add("sent back");
        ShortcutViewModel.PasteValue = value => { order.Add("paste"); return Task.FromResult(PasteOutcome.Pasted); };
        NewSnippet(value: "v").DoubleClick(false);
        Assert.Equal(new[] { "sent back", "paste" }, order);
        order.Clear();
        NewSnippet(value: "").DoubleClick(false);
        Assert.Empty(order);
      } finally {
        ShortcutViewModel.Opened = oldOpened;
      }
    }

    [Fact]
    public void A_button_being_renamed_does_not_paste() {
      ShortcutViewModel clip = NewSnippet(value: "v");
      clip.BeginEdit(EditField.Name);
      clip.DoubleClick(false);
      Assert.Empty(pasted);
    }

    [Fact]
    public void An_empty_snippet_opens_edit_snippet_instead_of_pasting() {
      ShortcutViewModel clip = NewSnippet(value: "");
      clip.DoubleClick(false);
      Assert.Empty(pasted);
      Assert.Same(clip, Assert.Single(edited));
    }

    [Theory]
    [InlineData(PasteOutcome.NoTarget)]
    [InlineData(PasteOutcome.NotEditable)]
    public void With_no_place_to_paste_into_it_says_so_with_the_exact_text(PasteOutcome result) {
      outcome = result;
      ShortcutViewModel clip = NewSnippet();
      clip.DoubleClick(false);
      var info = Assert.Single(infos);
      Assert.Equal("MDSHO", info.caption);
      Assert.Equal("No place to paste into.", info.heading);
      Assert.Equal("Click in a text field first, then double-click \"Home address\"." + Environment.NewLine + Environment.NewLine
        + "To paste it yourself, right-click the button, choose Copy snippet to memory, then press Ctrl+V. Your clipboard is unchanged.", info.text);
    }

    [Fact]
    public void In_a_single_click_panel_the_no_place_message_says_click() {
      outcome = PasteOutcome.NoTarget;
      win.Appearance.SingleClickToOpen = true;
      ShortcutViewModel clip = NewSnippet();
      clip.Click(true);
      Assert.StartsWith("Click in a text field first, then click \"Home address\".", Assert.Single(infos).text);
    }

    [Fact]
    public void An_administrator_window_gets_its_own_message_with_the_exact_text() {
      outcome = PasteOutcome.Elevated;
      ShortcutViewModel clip = NewSnippet();
      clip.DoubleClick(false);
      var info = Assert.Single(infos);
      Assert.Equal("MDSHO", info.caption);
      Assert.Equal("Can't paste into this window.", info.heading);
      Assert.Equal("It runs as administrator, and Windows doesn't let MDSHO paste into it." + Environment.NewLine + Environment.NewLine
        + "Right-click \"Home address\", choose Copy snippet to memory, then press Ctrl+V. Your clipboard is unchanged.", info.text);
    }

    [Fact]
    public void A_busy_clipboard_says_try_again() {
      outcome = PasteOutcome.ClipboardBusy;
      NewSnippet().DoubleClick(false);
      var info = Assert.Single(infos);
      Assert.Equal("Can't paste right now.", info.heading);
      Assert.Equal("Another program is using the clipboard. Try again in a moment.", info.text);
    }

    [Fact]
    public void A_paste_that_is_already_running_shows_nothing() {
      outcome = PasteOutcome.AlreadyPasting;
      NewSnippet().DoubleClick(false);
      Assert.Empty(infos);
      Assert.Empty(errors);
    }

    [Fact]
    public void A_failed_paste_shows_a_short_error_and_logs_no_text() {
      ShortcutViewModel.PasteValue = value => Task.FromException<PasteOutcome>(new InvalidOperationException("secret Main Street 12"));
      using (LogCapture log = new LogCapture()) {
        ShortcutViewModel clip = NewSnippet();
        clip.PasteForUserAsync().GetAwaiter().GetResult();
        var error = Assert.Single(errors);
        Assert.Equal("Home address", error.caption);
        Assert.Equal(ShortcutViewModel.CouldNotPasteText, error.error);
        Assert.Equal(ShortcutViewModel.CopySnippetInsteadText, error.detail);
        Assert.DoesNotContain(log.Events, e => LogCapture.FullText(e).Contains("Main Street"));
      }
    }

    [Fact]
    public void Pasting_and_editing_never_log_the_text() {
      using (LogCapture log = new LogCapture()) {
        ShortcutViewModel clip = NewSnippet(value: "P@ssw0rd-123");
        clip.SaveSnippetText("P@ssw0rd-456");
        clip.DoubleClick(false);
        clip.CopySnippetToMemoryCommand.Execute(null);
        Assert.DoesNotContain(log.Events, e => LogCapture.FullText(e).Contains("P@ssw0rd"));
      }
    }

    // ---- Copy, cut, paste, clones, export, import and backups carry the snippet text ----

    [Fact]
    public void Copy_and_paste_of_a_button_carries_the_kind_and_the_text() {
      ShortcutViewModel clip = NewSnippet();
      clip.Comment = "c";
      ShortcutViewModel group = Group("G");
      ShortcutViewModel copy = ViewModelFactory.CopyShortcut(clip, group, null);
      Assert.True(copy.IsSnippet);
      Assert.Equal("Main Street 12\r\n1000 Sample Town", copy.SnippetText);
      Shortcut stored = Stored(copy.ID);
      Assert.Equal((1L, "Main Street 12\r\n1000 Sample Town", "c"), (stored.IsSnippet, stored.SnippetText, stored.Comment));
      Assert.NotEqual(clip.ID, copy.ID);
    }

    [Fact]
    public void Copying_a_group_carries_the_snippets_inside() {
      ShortcutViewModel group = Group("G");
      group.NewSnippetShortcutCommand.Execute(null);
      ShortcutViewModel clip = group.Children.Single();
      clip.CancelEdit();
      clip.SnippetText = "inside";
      ShortcutViewModel copy = ViewModelFactory.CopyShortcut(group, null, win);
      ShortcutViewModel inner = copy.Children.Single();
      Assert.True(inner.IsSnippet);
      Assert.Equal("inside", Stored(inner.ID).SnippetText);
    }

    [Fact]
    public void The_panel_reloaded_from_the_database_keeps_the_snippet() {
      ShortcutViewModel clip = NewSnippet();
      AppViewModel reloaded = ViewModelFactory.CreateAppViewModel(work);
      ShortcutViewModel again = reloaded.Windows.SelectMany(w => w.RootShortcuts).Single(s => s.ID == clip.ID);
      Assert.True(again.IsSnippet);
      Assert.Equal("Main Street 12\r\n1000 Sample Town", again.SnippetText);
    }

    [Fact]
    public void Export_and_replace_import_keep_the_snippet_and_its_text() {
      ShortcutViewModel clip = NewSnippet();
      string zip = Path.Combine(work, "export.zip");
      DataExporter.Export(zip);
      JsonObject row = ReadData(zip)["shortcuts"].AsArray().Single(s => (long)s["id"] == clip.ID).AsObject();
      Assert.Equal((1L, "Main Street 12\r\n1000 Sample Town"), ((long)row["isSnippet"], (string)row["snippetText"]));
      DataStore.Shortcuts.Delete(clip.ID);
      DataImporter.Import(zip, ImportMode.Replace);
      Shortcut back = Stored(clip.ID);
      Assert.Equal((1L, "Main Street 12\r\n1000 Sample Town"), (back.IsSnippet, back.SnippetText));
    }

    [Fact]
    public void Add_as_new_import_keeps_the_snippet_and_its_text() {
      ShortcutViewModel clip = NewSnippet();
      string zip = Path.Combine(work, "export.zip");
      DataExporter.Export(zip);
      DataImporter.Import(zip, ImportMode.AddAsNew);
      List<Shortcut> clips = DataStore.Shortcuts.GetAll().Where(s => s.IsSnippet == 1).ToList();
      Assert.Equal(2, clips.Count);
      Assert.All(clips, s => Assert.Equal("Main Street 12\r\n1000 Sample Town", s.SnippetText));
    }

    [Fact]
    public void An_export_from_before_snippets_still_imports() {
      NewSnippet();
      string zip = Path.Combine(work, "export.zip");
      DataExporter.Export(zip);
      string old = Path.Combine(work, "old.zip");
      File.Copy(zip, old);
      using (ZipArchive archive = ZipFile.Open(old, ZipArchiveMode.Update)) {
        JsonNode data = JsonNode.Parse(ReadEntryText(archive, "data.json"));
        foreach (JsonNode s in data["shortcuts"].AsArray()) {
          s.AsObject().Remove("isSnippet");
          s.AsObject().Remove("snippetText");
        }
        JsonNode manifest = JsonNode.Parse(ReadEntryText(archive, "manifest.json"));
        manifest["schemaVersion"] = 18;
        manifest["appVersion"] = "1.18.0";
        WriteEntry(archive, "data.json", data.ToJsonString());
        WriteEntry(archive, "manifest.json", manifest.ToJsonString());
      }
      DataImporter.Import(old, ImportMode.Replace);
      Shortcut only = Assert.Single(DataStore.Shortcuts.GetAll());
      Assert.Equal((0L, ""), (only.IsSnippet, only.SnippetText));
    }

    [Fact]
    public void A_backup_before_the_upgrade_from_18_reads_the_buttons_with_an_empty_text() {
      BuildV18Db();
      string zip = Path.Combine(work, "before.zip");
      ExportManifest m = DataExporter.Export(zip, new ExportOptions { BeforeUpgrade = true });
      Assert.Equal(18, m.SchemaVersion);
      JsonArray rows = ReadData(zip)["shortcuts"].AsArray();
      Assert.Equal(2, rows.Count);
      Assert.All(rows, r => Assert.Equal("", (string)r["snippetText"]));
    }

    private static JsonNode ReadData(string zip) {
      using (ZipArchive archive = ZipFile.OpenRead(zip)) {
        return JsonNode.Parse(ReadEntryText(archive, "data.json"));
      }
    }

    private static string ReadEntryText(ZipArchive archive, string name) {
      using (StreamReader reader = new StreamReader(archive.GetEntry(name).Open())) {
        return reader.ReadToEnd();
      }
    }

    private static void WriteEntry(ZipArchive archive, string name, string text) {
      archive.GetEntry(name).Delete();
      using (StreamWriter writer = new StreamWriter(archive.CreateEntry(name).Open())) {
        writer.Write(text);
      }
    }
  }

  /// <summary>The panel menus, the button's icon and menu, the Edit snippet window and the paste messages (shown off screen).</summary>
  public class SnippetButtonViewTests {

    private sealed class Fixture {
      public AppViewModel App;
      public DesktopWindowViewModel Win;
      public ShortcutViewModel Group;
      public ShortcutViewModel Clip;
    }

    private static void WithSnippet(Action<Fixture> body) {
      WpfAppHost.Run(() => {
        Action<ShortcutViewModel> oldValueEditor = ShortcutViewModel.OpenSnippetEditor;
        ShortcutViewModel.OpenSnippetEditor = s => { };
        try {
          using (TempDb db = new TempDb()) {
            Fixture f = new Fixture();
            f.App = new AppViewModel();
            f.Win = f.App.AddWindow("TEST", 0, 0);
            f.Group = ViewModelFactory.NewGroupShortcut(f.App, null, f.Win);
            f.Win.RootShortcuts.Add(f.Group);
            f.Group.NewSnippetShortcutCommand.Execute(null);
            f.Clip = f.Group.Children.Single();
            f.Clip.CancelEdit();
            f.Clip.Name = "Home address";
            f.Clip.SnippetText = "Main Street 12\r\n1000 Sample Town";
            body(f);
          }
        } finally {
          ShortcutViewModel.OpenSnippetEditor = oldValueEditor;
        }
      });
    }

    private static void WithView(Fixture f, Action<DesktopWindowView> body) {
      DesktopWindowView view = new DesktopWindowView(f.Win) { ShowActivated = false, Left = -10000, Top = -10000 };
      try {
        view.Show();
        WpfAppHost.Pump();
        f.Group.IsGroupExpanded = true;
        WpfAppHost.Pump();
        body(view);
      } finally {
        view.Close();
        WpfAppHost.Pump();
      }
    }

    private static string Describe(object item) {
      return item is Separator ? "-" : item is MenuItem m ? m.Header as string ?? "(item)" : item.ToString();
    }

    private static List<object> Visible(ContextMenu menu) {
      return menu.Items.Cast<object>().Where(i => !(i is FrameworkElement e) || e.Visibility != Visibility.Collapsed).ToList();
    }

    private static ContextMenu MenuOf(DesktopWindowView view, ShortcutViewModel shortcut) {
      TreeView tree = (TreeView)view.FindName("TreeViewShortcuts");
      TreeViewItem item = WpfAppHost.VisualDescendants<TreeViewItem>(tree).Single(i => i.DataContext == shortcut);
      ContextMenu menu = WpfAppHost.VisualDescendants<Button>(item).First(b => b.DataContext == shortcut).ContextMenu;
      menu.DataContext = shortcut;
      WpfAppHost.Pump();
      return menu;
    }

    private static string IconPath(MenuItem item) {
      return System.Windows.Data.BindingOperations.GetBinding((TextBlock)item.Icon, TextBlock.TextProperty).Path.Path;
    }

    [Fact]
    public void The_panel_menu_has_new_snippet_after_new_note_with_the_snippet_icon() {
      WithSnippet(f => WithView(f, view => {
        TreeView tree = (TreeView)view.FindName("TreeViewShortcuts");
        ContextMenu menu = tree.ContextMenu;
        menu.DataContext = f.Win;
        WpfAppHost.Pump();
        List<object> visible = Visible(menu);
        List<string> names = visible.Select(Describe).ToList();
        int note = names.IndexOf("New note");
        MenuItem clip = Assert.IsType<MenuItem>(visible[note + 1]);
        Assert.Equal("New snippet", clip.Header);
        Assert.Same(f.Win.NewSnippetShortcutCommand, clip.Command);
        Assert.Equal("Appearance.Icons.Snippet", IconPath(clip));
      }));
    }

    [Fact]
    public void A_groups_menu_has_new_snippet_after_new_note_and_a_buttons_menu_does_not() {
      WithSnippet(f => WithView(f, view => {
        List<object> visible = Visible(MenuOf(view, f.Group));
        List<string> names = visible.Select(Describe).ToList();
        Assert.Equal("New snippet", names[names.IndexOf("New note") + 1]);
        Assert.Same(f.Group.NewSnippetShortcutCommand, ((MenuItem)visible[names.IndexOf("New snippet")]).Command);
        Assert.Equal("OwningWindow.Appearance.Icons.Snippet", IconPath((MenuItem)visible[names.IndexOf("New snippet")]));
        Assert.DoesNotContain("New snippet", Visible(MenuOf(view, f.Clip)).Select(Describe));
      }));
    }

    [Fact]
    public void A_snippets_menu_matches_the_mockup() {
      WithSnippet(f => WithView(f, view => {
        List<object> visible = Visible(MenuOf(view, f.Clip));
        List<string> names = visible.Select(Describe).ToList();
        string all = string.Join(" | ", names);
        int rename = names.IndexOf("Rename");
        Assert.True(rename >= 0, all);
        Assert.Equal(new[] { "Rename", "Set name the same as value", "Copy snippet to memory", "Comment...", "-", "Edit snippet...", "-", "Cut", "Copy", "Paste", "-", "Delete" },
          names.Skip(rename).ToArray());
        MenuItem setName = (MenuItem)visible[rename + 1];
        Assert.Same(f.Clip.SetNameTheSameAsValueCommand, setName.Command);
        Assert.Null(setName.Icon);
        MenuItem copy = (MenuItem)visible[rename + 2];
        Assert.Same(f.Clip.CopySnippetToMemoryCommand, copy.Command);
        Assert.Null(copy.Icon);
        MenuItem edit = (MenuItem)visible[names.IndexOf("Edit snippet...")];
        Assert.Same(f.Clip.EditSnippetCommand, edit.Command);
        Assert.Equal("OwningWindow.Appearance.Icons.Snippet", IconPath(edit));
        foreach (string other in new[] { "Edit note...", "Edit run command...", "Copy target to memory", "Copy command to memory", "Set name the same as target" }) {
          Assert.DoesNotContain(other, names);
        }
        // Other kinds don't get the snippet items.
        List<string> group = Visible(MenuOf(view, f.Group)).Select(Describe).ToList();
        Assert.DoesNotContain("Edit snippet...", group);
        Assert.DoesNotContain("Copy snippet to memory", group);
        for (int i = 1; i < group.Count; i++) {
          Assert.False(group[i] == "-" && group[i - 1] == "-", "Two separators in a row: " + string.Join(" | ", group));
        }
      }));
    }

    [Fact]
    public void The_button_shows_the_snippet_glyph_as_its_icon() {
      WithSnippet(f => WithView(f, view => {
        TreeView tree = (TreeView)view.FindName("TreeViewShortcuts");
        TreeViewItem item = WpfAppHost.VisualDescendants<TreeViewItem>(tree).Single(i => i.DataContext == f.Clip);
        Button button = WpfAppHost.VisualDescendants<Button>(item).First(b => b.DataContext == f.Clip);
        List<TextBlock> icons = WpfAppHost.VisualDescendants<TextBlock>(button)
          .Where(t => t.IsVisible && t.Text == f.Win.Appearance.Icons.Snippet).ToList();
        Assert.Single(icons);
        Assert.DoesNotContain(WpfAppHost.VisualDescendants<TextBlock>(button), t => t.IsVisible && t.Text == f.Win.Appearance.Icons.Shortcut);
      }));
    }

    // ---- The Edit snippet window ----

    private static void Shown(Window window, Action check) {
      window.WindowStartupLocation = WindowStartupLocation.Manual;
      window.Left = -10000;
      window.Top = -10000;
      window.ShowActivated = false;
      window.ShowInTaskbar = false;
      window.Show();
      try {
        WpfAppHost.Pump();
        check();
      } finally {
        window.Close();
        WpfAppHost.Pump();
      }
    }

    private static T Named<T>(Window window, string name) where T : class {
      return Assert.IsAssignableFrom<T>(window.FindName(name));
    }

    private static void Click(Button button) {
      button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, button));
      WpfAppHost.Pump();
    }

    private static void Press(Window window, Key key) {
      KeyEventArgs args = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(window), 0, key) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
      window.RaiseEvent(args);
      WpfAppHost.Pump();
    }

    [Fact]
    public void The_edit_snippet_window_shows_the_name_the_snippet_icon_the_text_ok_wrap_and_close() {
      WithSnippet(f => {
        TextEditWindow window = TextEditWindow.ForSnippet(f.Clip);
        Shown(window, () => {
          Assert.True(window.IsSnippetWindow);
          Assert.Equal("Home address", window.Title);
          Assert.Equal("Home address", Named<TextBlock>(window, "TextTitle").Text);
          Assert.Equal(f.Win.Appearance.Icons.Snippet, Named<TextBlock>(window, "TextTitleIcon").Text);
          TextBox editor = Named<TextBox>(window, "TextComment");
          Assert.Equal("Main Street 12\r\n1000 Sample Town", editor.Text);
          Assert.True(editor.AcceptsReturn);
          Assert.Equal("Snippet", System.Windows.Automation.AutomationProperties.GetName(editor));
          Assert.Null(window.FindName("ButtonOk"));
          Assert.Equal(Visibility.Visible, Named<Button>(window, "ButtonWrap").Visibility);
          Assert.Equal(Visibility.Visible, Named<Button>(window, "ButtonClose").Visibility);
          Assert.NotEqual(Visibility.Visible, Named<Button>(window, "ButtonPin").Visibility);
          Assert.Null(window.FindName("ButtonColors"));
          Assert.Equal(TextEditWindow.RenameSnippetTitleToolTip, Named<TextBlock>(window, "TextTitle").ToolTip);
        });
      });
    }

    [Fact]
    public void Closing_saves_the_text() {
      WithSnippet(f => {
        TextEditWindow window = TextEditWindow.ForSnippet(f.Clip);
        Shown(window, () => {
          Named<TextBox>(window, "TextComment").Text = "New Street 1";
          window.Close();
          WpfAppHost.Pump();
          Assert.True(window.Saved);
          Assert.Equal("New Street 1", f.Clip.SnippetText);
          Assert.Equal("New Street 1", DataStore.Shortcuts.Get(f.Clip.ID).SnippetText);
          Assert.Equal("", f.Clip.Comment ?? "");
        });
      });
    }

    [Fact]
    public void Esc_and_ctrl_enter_close_and_save() {
      WithSnippet(f => {
        Func<ModifierKeys> oldModifiers = TextEditWindow.KeyboardModifiers;
        try {
          TextEditWindow window = TextEditWindow.ForSnippet(f.Clip);
          Shown(window, () => {
            Named<TextBox>(window, "TextComment").Text = "typed";
            Press(window, Key.Escape);
            Assert.True(window.Saved);
            Assert.Equal("typed", f.Clip.SnippetText);
          });
          TextEditWindow second = TextEditWindow.ForSnippet(f.Clip);
          Shown(second, () => {
            Named<TextBox>(second, "TextComment").Text = "by keys";
            TextEditWindow.KeyboardModifiers = () => ModifierKeys.Control;
            Press(second, Key.Enter);
            Assert.True(second.Saved);
            Assert.Equal("by keys", f.Clip.SnippetText);
          });
        } finally {
          TextEditWindow.KeyboardModifiers = oldModifiers;
        }
      });
    }

    [Fact]
    public void The_title_can_be_renamed_and_follows_renames() {
      WithSnippet(f => {
        TextEditWindow window = TextEditWindow.ForSnippet(f.Clip);
        Shown(window, () => {
          window.BeginRenameTitle();
          WpfAppHost.Pump();
          Assert.True(window.IsRenamingTitle);
          Named<TextBox>(window, "TextTitleEdit").Text = "Work address";
          window.EndRenameTitle(true);
          Assert.Equal("Work address", f.Clip.Name);
          f.Clip.Name = "Other";
          Assert.Equal("Other", window.Title);
        });
      });
    }

    [Fact]
    public void Wrap_text_is_shared_with_the_comment_windows_of_the_panel() {
      WithSnippet(f => {
        TextEditWindow window = TextEditWindow.ForSnippet(f.Clip);
        Shown(window, () => {
          Assert.False(window.IsWrapText);
          Click(Named<Button>(window, "ButtonWrap"));
          Assert.True(window.IsWrapText);
          Assert.True(f.Win.Appearance.CommentWrapText);
        });
      });
    }

    [Fact]
    public void The_app_opens_edit_snippet_in_this_window() {
      WpfAppHost.Run(() => {
        // ConnectEditors sets every editor seam: all are put back, so later tests keep their fakes.
        Action<ShortcutViewModel> old = ShortcutViewModel.OpenSnippetEditor;
        Action<ShortcutViewModel, bool> oldRun = ShortcutViewModel.OpenRunCommandEditor;
        Action<ShortcutViewModel> oldComment = ShortcutViewModel.OpenCommentEditor;
        Action<ShortcutViewModel> oldNote = ShortcutViewModel.OpenNoteEditor;
        Action<ShortcutViewModel> oldToggle = ShortcutViewModel.ToggleNoteEditor;
        Action<ShortcutViewModel> oldDeleted = ShortcutViewModel.NotesDeleted;
        try {
          ShortcutViewModel.OpenSnippetEditor = null;
          App.ConnectEditors();
          Assert.NotNull(ShortcutViewModel.OpenSnippetEditor);
        } finally {
          ShortcutViewModel.OpenSnippetEditor = old;
          ShortcutViewModel.OpenRunCommandEditor = oldRun;
          ShortcutViewModel.OpenCommentEditor = oldComment;
          ShortcutViewModel.OpenNoteEditor = oldNote;
          ShortcutViewModel.ToggleNoteEditor = oldToggle;
          ShortcutViewModel.NotesDeleted = oldDeleted;
        }
      });
    }

    // ---- The paste messages ----

    [Fact]
    public void The_paste_message_window_shows_mdsho_the_info_icon_the_heading_the_text_and_OK() {
      WpfAppHost.Run(() => {
        PasteMessageWindow window = new PasteMessageWindow("MDSHO", "No place to paste into.", "Click in a text field first.\r\n\r\nYour clipboard is unchanged.");
        bool closed = false;
        window.Closed += (s, e) => closed = true;
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Left = -10000;
        window.Top = -10000;
        window.ShowActivated = false;
        window.ShowInTaskbar = false;
        window.Show();
        try {
          WpfAppHost.Pump();
          Assert.StartsWith("MDSHO", window.Title);
          Assert.Equal(new IconGlyphs(true).Info, ((TextBlock)window.FindName("TextIcon")).Text);
          TextBlock heading = (TextBlock)window.FindName("TextHeading");
          Assert.Equal("No place to paste into.", heading.Text);
          Assert.True(heading.FontWeight.ToOpenTypeWeight() >= FontWeights.SemiBold.ToOpenTypeWeight());
          // The one MDSHO dialog: the logo in the title bar, the heading at the normal size and colour.
          Assert.IsAssignableFrom<MessageWindow>(window);
          Assert.Equal(new IconGlyphs(true).MDSHO, ((Controls.DialogTitleBar)window.FindName("TitleBar")).Glyph);
          Assert.Equal(window.FontSize, heading.FontSize);
          Assert.Equal(((System.Windows.Media.SolidColorBrush)window.FindResource("Theme.Text")).Color, ((System.Windows.Media.SolidColorBrush)heading.Foreground).Color);
          Assert.Equal("Click in a text field first.\r\n\r\nYour clipboard is unchanged.", ((TextBlock)window.FindName("TextMessage")).Text);
          Button ok = (Button)window.FindName("ButtonOK");
          Assert.Equal("OK", ok.Content);
          Assert.True(ok.IsDefault);
          Assert.True(ok.IsCancel);
          Click(ok);
          Assert.True(closed);
        } finally {
          if (!closed) { window.Close(); }
          WpfAppHost.Pump();
        }
      });
    }

    [Fact]
    public void The_app_shows_paste_messages_in_this_window() {
      WpfAppHost.Run(() => {
        Action<PasteMessageWindow> oldShow = PasteMessageWindow.ShowWindow;
        Action<string, string, string> oldInfo = Dialogs.ShowInfo;
        Func<string, string, bool> oldConfirm = Dialogs.Confirm;
        Action<string, string> oldMessage = Dialogs.ShowMessage;
        Action<string, string, string> oldError = Dialogs.ShowError;
        List<PasteMessageWindow> shown = new List<PasteMessageWindow>();
        try {
          PasteMessageWindow.ShowWindow = w => shown.Add(w);
          MDSHO.Helpers.WpfBridge.Connect();
          Dialogs.ShowInfo("MDSHO", "Heading.", "Text.");
          PasteMessageWindow window = Assert.Single(shown);
          Assert.Equal("Heading.", ((TextBlock)window.FindName("TextHeading")).Text);
          window.Close();
        } finally {
          PasteMessageWindow.ShowWindow = oldShow;
          Dialogs.ShowInfo = oldInfo;
          Dialogs.Confirm = oldConfirm;
          Dialogs.ShowMessage = oldMessage;
          Dialogs.ShowError = oldError;
        }
      });
    }
  }
}
