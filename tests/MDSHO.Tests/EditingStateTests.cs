using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using MDSHO.ViewModels;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>While a name/process edit box is open (IsEditing) clicks and Enter must never run or toggle the item.</summary>
  public class EditingStateTests : IDisposable {
    private readonly TempDb db = new TempDb();
    private readonly AppViewModel app = new AppViewModel();
    private readonly DesktopWindowViewModel win;
    private readonly List<ShortcutViewModel> started = new List<ShortcutViewModel>();

    public EditingStateTests() { win = app.AddWindow("TEST", 0, 0); }
    public void Dispose() { db.Dispose(); }

    private ShortcutViewModel Group() {
      ShortcutViewModel g = ViewModelFactory.NewGroupShortcut(app, null, win);
      win.RootShortcuts.Add(g);
      return g;
    }
    private ShortcutViewModel Leaf() {
      ShortcutViewModel s = ViewModelFactory.NewProcessCommandShortcut(app, null, win);
      win.RootShortcuts.Add(s);
      s.StartProcessAction = (v, admin) => started.Add(v);
      return s;
    }

    [Fact]
    public void Run_does_nothing_while_editing() {
      ShortcutViewModel s = Leaf();
      s.BeginEdit(EditField.Name);
      s.Run();
      s.RunShortcut(true);
      Assert.Empty(started);
    }

    [Fact]
    public void Run_works_when_not_editing() {
      ShortcutViewModel s = Leaf();
      s.Run();
      Assert.Equal(new[] { s }, started);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Click_and_DoubleClick_do_nothing_while_editing(bool singleClickToOpen) {
      ShortcutViewModel s = Leaf(), g = Group();
      s.BeginEdit(EditField.Name); g.BeginEdit(EditField.Name);
      bool expanded = g.IsGroupExpanded;
      s.Click(singleClickToOpen); s.DoubleClick(singleClickToOpen);
      g.Click(singleClickToOpen); g.DoubleClick(singleClickToOpen);
      Assert.Empty(started);
      Assert.Equal(expanded, g.IsGroupExpanded);
    }

    [Fact]
    public void Click_modes_behave_as_before_when_not_editing() {
      ShortcutViewModel s = Leaf(), g = Group();
      bool expanded = g.IsGroupExpanded;
      s.Click(false); s.DoubleClick(true);
      g.Click(false); g.DoubleClick(true);
      Assert.Empty(started);
      Assert.Equal(expanded, g.IsGroupExpanded);
      s.Click(true);
      Assert.Single(started);
      s.DoubleClick(false);
      Assert.Equal(2, started.Count);
      g.Click(true);
      Assert.Equal(!expanded, g.IsGroupExpanded);
      g.DoubleClick(false);
      Assert.Equal(expanded, g.IsGroupExpanded);
    }

    [Fact]
    public void Enter_does_nothing_while_item_is_editing() {
      ShortcutViewModel s = Leaf(), g = Group();
      var ran = new List<ShortcutViewModel>();
      win.RunShortcutAction = v => ran.Add(v);
      s.BeginEdit(EditField.Name); g.BeginEdit(EditField.Name);
      bool expanded = g.IsGroupExpanded;
      Assert.False(win.HandleKey(ShortcutKey.Enter, ShortcutModifiers.None, s, false));
      Assert.False(win.HandleKey(ShortcutKey.Enter, ShortcutModifiers.None, g, false));
      Assert.Empty(ran);
      Assert.Equal(expanded, g.IsGroupExpanded);
    }

    [Fact]
    public void IsEditing_is_set_by_BeginEdit_and_cleared_by_commit_and_cancel() {
      ShortcutViewModel s = Leaf();
      Assert.False(s.IsEditing);
      s.BeginEdit(EditField.Name);
      Assert.True(s.IsEditing);
      s.CommitEdit();
      Assert.False(s.IsEditing);
      s.BeginEdit(EditField.Name);
      s.CancelEdit();
      Assert.False(s.IsEditing);
    }

    private MDSHO.Core.Shortcut Row(ShortcutViewModel s) {
      return MDSHO.Data.DataStore.Windows.LoadAll().Single(w => w.ID == win.ID).RootShortcuts.Single(r => r.ID == s.ID);
    }
    private static List<string> Changes(ShortcutViewModel s) {
      var l = new List<string>();
      s.PropertyChanged += (o, e) => l.Add(e.PropertyName);
      return l;
    }

    [Theory]
    [InlineData(EditField.Name)]
    [InlineData(EditField.ProcessFileName)]
    [InlineData(EditField.ProcessArguments)]
    public void BeginEdit_sets_EditField_and_IsEditing(EditField field) {
      ShortcutViewModel s = Leaf();
      Assert.Equal(EditField.None, s.EditField);
      s.BeginEdit(field);
      Assert.Equal(field, s.EditField);
      Assert.True(s.IsEditing);
      s.CancelEdit();
      Assert.Equal(EditField.None, s.EditField);
      Assert.False(s.IsEditing);
    }

    [Fact]
    public void Rename_command_begins_the_name_edit_without_any_control() {
      ShortcutViewModel s = Leaf();
      s.RenameCommand.Execute(null);
      Assert.Equal(EditField.Name, s.EditField);
    }

    [Fact]
    public void Only_one_field_is_edited_at_a_time_and_the_previous_edit_is_committed() {
      ShortcutViewModel s = Leaf();
      s.Name = "n"; s.ProcessFileName = "f";
      s.BeginEdit(EditField.Name);
      s.EditText = "renamed";
      s.BeginEdit(EditField.ProcessFileName);
      Assert.Equal(EditField.ProcessFileName, s.EditField);
      Assert.Equal("renamed", s.Name);
      Assert.Equal("f", s.EditText);
    }

    [Theory]
    [InlineData(EditField.Name)]
    [InlineData(EditField.ProcessFileName)]
    [InlineData(EditField.ProcessArguments)]
    public void CommitEdit_keeps_the_new_value_and_saves_it(EditField field) {
      ShortcutViewModel s = Leaf();
      s.BeginEdit(field);
      s.EditText = "new value";
      s.CommitEdit();
      Assert.False(s.IsEditing);
      MDSHO.Core.Shortcut row = Row(s);
      string saved = field == EditField.Name ? row.Name : field == EditField.ProcessFileName ? row.ProcessFileName : row.ProcessArguments;
      Assert.Equal("new value", saved);
      Assert.Equal("new value", field == EditField.Name ? s.Name : field == EditField.ProcessFileName ? s.ProcessFileName : s.ProcessArguments);
    }

    [Theory]
    [InlineData(EditField.Name)]
    [InlineData(EditField.ProcessFileName)]
    [InlineData(EditField.ProcessArguments)]
    public void CancelEdit_restores_the_original_and_does_not_save(EditField field) {
      ShortcutViewModel s = Leaf();
      s.BeginEdit(field);
      string original = s.EditText;
      s.EditText = "discard me";
      s.CancelEdit();
      Assert.False(s.IsEditing);
      Assert.Equal(original, field == EditField.Name ? s.Name : field == EditField.ProcessFileName ? s.ProcessFileName : s.ProcessArguments);
      MDSHO.Core.Shortcut row = Row(s);
      string saved = field == EditField.Name ? row.Name : field == EditField.ProcessFileName ? row.ProcessFileName : row.ProcessArguments;
      Assert.NotEqual("discard me", saved);
    }

    [Fact]
    public void Commit_or_cancel_without_an_edit_does_nothing() {
      ShortcutViewModel s = Leaf();
      string name = s.Name;
      s.CommitEdit(); s.CancelEdit();
      Assert.Equal(name, s.Name);
      Assert.False(s.IsEditing);
    }

    [Fact]
    public void EditField_and_IsEditing_raise_PropertyChanged() {
      ShortcutViewModel s = Leaf();
      List<string> changes = Changes(s);
      s.BeginEdit(EditField.Name);
      Assert.Contains(nameof(ShortcutViewModel.EditField), changes);
      Assert.Contains(nameof(ShortcutViewModel.IsEditing), changes);
      changes.Clear();
      s.CancelEdit();
      Assert.Contains(nameof(ShortcutViewModel.EditField), changes);
      Assert.Contains(nameof(ShortcutViewModel.IsEditing), changes);
    }

    [Fact]
    public void F2_begins_a_name_edit_on_the_selected_group() {
      ShortcutViewModel s = Group(); // notes and run commands are renamed in their own windows
      Assert.True(win.HandleKey(ShortcutKey.F2, ShortcutModifiers.None, s, false));
      Assert.Equal(EditField.Name, s.EditField);
    }

    [Fact]
    public void Run_and_toggle_are_refused_during_every_kind_of_edit() {
      foreach (EditField field in new[] { EditField.Name, EditField.ProcessFileName, EditField.ProcessArguments }) {
        ShortcutViewModel s = Leaf(), g = Group();
        s.BeginEdit(field); g.BeginEdit(field);
        s.Run(); s.Click(true); s.DoubleClick(false);
        bool expanded = g.IsGroupExpanded;
        g.Click(true); g.DoubleClick(false);
        Assert.Empty(started);
        Assert.Equal(expanded, g.IsGroupExpanded);
      }
    }

    [Fact]
    public void CommitEditCommand_and_CancelEditCommand_call_through() {
      ShortcutViewModel s = Leaf();
      s.BeginEdit(EditField.Name);
      s.EditText = "via command";
      s.CommitEditCommand.Execute(null);
      Assert.False(s.IsEditing);
      Assert.Equal("via command", s.Name);
      s.BeginEdit(EditField.Name);
      s.EditText = "discarded";
      s.CancelEditCommand.Execute(null);
      Assert.False(s.IsEditing);
      Assert.Equal("via command", s.Name);
    }

    [Fact]
    public void Run_and_toggle_stay_refused_after_title_edit_starts() {
      ShortcutViewModel s = Leaf();
      s.BeginEdit(EditField.Name);
      win.BeginEditTitle();
      s.Run();
      Assert.Empty(started);
    }
  }
}
