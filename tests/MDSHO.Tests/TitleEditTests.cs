using System;
using System.Linq;
using MDSHO.Core;
using MDSHO.Data;
using MDSHO.ViewModels;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>Window title edit API on DesktopWindowViewModel (same pattern as ShortcutViewModel.BeginEdit/CommitEdit/CancelEdit).</summary>
  public class TitleEditTests : IDisposable {
    private readonly TempDb db = new TempDb();
    private readonly AppViewModel app = new AppViewModel();
    private readonly DesktopWindowViewModel win;

    public TitleEditTests() { win = app.AddWindow("TEST", 0, 0); }
    public void Dispose() { db.Dispose(); }

    private string SavedTitle(long id) { return DataStore.Windows.LoadAll().Single(w => w.ID == id).Title; }
    private void LogUpdates() {
      db.Exec("CREATE TABLE UpdLog (RowID INTEGER NOT NULL);" +
              "CREATE TRIGGER trg_upd AFTER UPDATE ON MDSHOWindows BEGIN INSERT INTO UpdLog VALUES (NEW.ID); END;");
    }
    private long Updates(long id) { return db.Scalar("SELECT COUNT(*) FROM UpdLog WHERE RowID=" + id); }

    [Fact]
    public void BeginEditTitle_copies_the_title_and_sets_IsEditingTitle() {
      win.Appearance.Title = "Original";
      Assert.False(win.IsEditingTitle);
      win.BeginEditTitle();
      Assert.True(win.IsEditingTitle);
      Assert.Equal("Original", win.TitleEditText);
    }

    [Fact]
    public void CommitEditTitle_changes_Title_and_saves_once() {
      win.Appearance.Title = "Original";
      LogUpdates();
      win.BeginEditTitle();
      win.TitleEditText = "Renamed";
      win.CommitEditTitle();
      Assert.False(win.IsEditingTitle);
      Assert.Equal("Renamed", win.Appearance.Title);
      Assert.Equal("Renamed", SavedTitle(win.ID));
      Assert.Equal(1, Updates(win.ID));
    }

    [Fact]
    public void CommitEditTitle_without_change_does_not_write() {
      win.Appearance.Title = "Original";
      LogUpdates();
      win.BeginEditTitle();
      win.CommitEditTitle();
      Assert.False(win.IsEditingTitle);
      Assert.Equal(0, Updates(win.ID));
      Assert.Equal("Original", SavedTitle(win.ID));
    }

    [Fact]
    public void CancelEditTitle_restores_and_does_not_save() {
      win.Appearance.Title = "Original";
      LogUpdates();
      win.BeginEditTitle();
      win.TitleEditText = "Discard me";
      win.CancelEditTitle();
      Assert.False(win.IsEditingTitle);
      Assert.Equal("Original", win.Appearance.Title);
      Assert.Equal("Original", SavedTitle(win.ID));
      Assert.Equal(0, Updates(win.ID));
    }

    [Fact]
    public void Commit_or_cancel_without_begin_does_nothing() {
      win.Appearance.Title = "Original";
      LogUpdates();
      win.TitleEditText = "stray";
      win.CommitEditTitle(); win.CancelEditTitle();
      Assert.Equal("Original", win.Appearance.Title);
      Assert.Equal(0, Updates(win.ID));
    }

    [Fact]
    public void Edit_started_from_a_clone_renames_the_master_and_saves_once() {
      win.Appearance.Title = "Original";
      win.CloneWindow("TEST", 10, 10);
      DesktopWindowViewModel clone = win.Clones.Single();
      LogUpdates();
      clone.BeginEditTitle();
      Assert.Equal("Original", clone.TitleEditText);
      Assert.False(win.IsEditingTitle);
      clone.TitleEditText = "From clone";
      clone.CommitEditTitle();
      Assert.Equal("From clone", win.Appearance.Title);
      Assert.Equal("From clone", SavedTitle(win.ID));
      Assert.Equal(1, Updates(win.ID));
      Assert.Contains("From clone", win.TitleDisplay);
      Assert.Contains("From clone", clone.TitleDisplay);
    }

    [Fact]
    public void Commands_call_through() {
      win.Appearance.Title = "Original";
      win.BeginEditTitleCommand.Execute(null);
      Assert.True(win.IsEditingTitle);
      win.TitleEditText = "Via command";
      win.CommitEditTitleCommand.Execute(null);
      Assert.Equal("Via command", win.Appearance.Title);
      win.BeginEditTitleCommand.Execute(null);
      win.TitleEditText = "nope";
      win.CancelEditTitleCommand.Execute(null);
      Assert.False(win.IsEditingTitle);
      Assert.Equal("Via command", win.Appearance.Title);
    }

    [Fact]
    public void IsEditingTitle_and_TitleEditText_raise_PropertyChanged() {
      var changes = new System.Collections.Generic.List<string>();
      win.PropertyChanged += (o, e) => changes.Add(e.PropertyName);
      win.BeginEditTitle();
      Assert.Contains(nameof(DesktopWindowViewModel.IsEditingTitle), changes);
      Assert.Contains(nameof(DesktopWindowViewModel.TitleEditText), changes);
      changes.Clear();
      win.CancelEditTitle();
      Assert.Contains(nameof(DesktopWindowViewModel.IsEditingTitle), changes);
    }

    [Fact]
    public void Title_edit_does_not_affect_shortcut_editing() {
      ShortcutViewModel s = ViewModelFactory.NewProcessCommandShortcut(app, null, win);
      win.RootShortcuts.Add(s);
      s.BeginEdit(EditField.Name);
      s.EditText = "typing";
      win.BeginEditTitle();
      win.TitleEditText = "T";
      win.CancelEditTitle();
      Assert.True(s.IsEditing);
      Assert.Equal("typing", s.EditText);
      s.CommitEdit();
      Assert.Equal("typing", s.Name);
      Assert.False(win.IsEditingTitle);
    }
  }
}
