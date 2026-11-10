using System;
using System.Linq;
using System.Collections.Generic;
using System.Windows.Input;
using MDSHO.ViewModels;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>Keyboard shortcuts on the selected shortcut/group, decided by <see cref="DesktopWindowViewModel.HandleKey"/>.</summary>
  public class KeyboardShortcutTests : IDisposable {
    private readonly TempDb db = new TempDb();
    private readonly AppViewModel app = new AppViewModel();
    private readonly DesktopWindowViewModel win;
    private readonly List<ShortcutViewModel> ran = new List<ShortcutViewModel>();

    public KeyboardShortcutTests() {
      win = app.AddWindow("TEST", 0, 0);
      win.RunShortcutAction = s => ran.Add(s);
    }
    public void Dispose() { db.Dispose(); }
    private bool AnyEditing { get { return win.RootShortcuts.Any(s => s.IsEditing); } }

    private ShortcutViewModel Root() {
      ShortcutViewModel g = ViewModelFactory.NewGroupShortcut(app, null, win);
      win.RootShortcuts.Add(g);
      return g;
    }
    private ShortcutViewModel Child(ShortcutViewModel parent) {
      ShortcutViewModel g = ViewModelFactory.NewGroupShortcut(app, parent, null);
      parent.Children.Add(g);
      return g;
    }
    private ShortcutViewModel Leaf(ShortcutViewModel parent) {
      ShortcutViewModel s = ViewModelFactory.NewProcessCommandShortcut(app, parent, parent == null ? win : null);
      if (parent == null) win.RootShortcuts.Add(s); else parent.Children.Add(s);
      return s;
    }
    private bool Key(Key k, ModifierKeys m, ShortcutViewModel sel, bool editing = false) { return win.HandleKey(Helpers.KeyInput.ToShortcutKey(k), Helpers.KeyInput.ToShortcutModifiers(m), sel, editing); }
    private long ParentShortcutId(ShortcutViewModel s) { return db.Scalar("SELECT ParentShortcutID FROM Shortcuts WHERE ID=" + s.ID); }
    private long Count() { return db.Scalar("SELECT COUNT(*) FROM Shortcuts"); }

    [Fact]
    public void F2_requests_rename_of_selected() {
      ShortcutViewModel a = Root();
      Assert.True(Key(System.Windows.Input.Key.F2, ModifierKeys.None, a));
      Assert.Equal(EditField.Name, a.EditField);
    }

    [Fact]
    public void Delete_deletes_selected_after_confirm() {
      ShortcutViewModel a = Root(), b = Root();
      Assert.True(Key(System.Windows.Input.Key.Delete, ModifierKeys.None, a));
      Assert.DoesNotContain(a, win.RootShortcuts);
      Assert.Contains(b, win.RootShortcuts);
    }

    [Fact]
    public void Delete_with_confirm_No_changes_nothing() {
      ShortcutViewModel a = Root();
      long before = Count();
      Func<string, string, bool> old = Dialogs.Confirm;
      Dialogs.Confirm = (m, c) => false;
      try {
        Assert.True(Key(System.Windows.Input.Key.Delete, ModifierKeys.None, a));
      } finally { Dialogs.Confirm = old; }
      Assert.Contains(a, win.RootShortcuts);
      Assert.Equal(before, Count());
    }

    [Fact]
    public void CtrlC_then_CtrlV_into_selected_group_copies() {
      ShortcutViewModel a = Root(), x = Root();
      Assert.True(Key(System.Windows.Input.Key.C, ModifierKeys.Control, a));
      Assert.True(Key(System.Windows.Input.Key.V, ModifierKeys.Control, x));
      Assert.Single(x.Children);
      Assert.Contains(a, win.RootShortcuts);
    }

    [Fact]
    public void CtrlX_then_CtrlV_into_selected_group_moves() {
      ShortcutViewModel a = Root(), x = Root();
      Key(System.Windows.Input.Key.X, ModifierKeys.Control, a);
      Key(System.Windows.Input.Key.V, ModifierKeys.Control, x);
      Assert.Contains(a, x.Children);
      Assert.DoesNotContain(a, win.RootShortcuts);
      Assert.Equal(x.ID, ParentShortcutId(a));
    }

    [Fact]
    public void CtrlV_with_nothing_selected_pastes_into_window_root() {
      ShortcutViewModel a = Root(), b = Child(a);
      Key(System.Windows.Input.Key.X, ModifierKeys.Control, b);
      Assert.True(Key(System.Windows.Input.Key.V, ModifierKeys.Control, null));
      Assert.Contains(b, win.RootShortcuts);
      Assert.Empty(a.Children);
    }

    [Fact]
    public void CtrlV_with_root_leaf_selected_pastes_into_window_root() {
      ShortcutViewModel a = Root(), b = Child(a), leaf = Leaf(null);
      Key(System.Windows.Input.Key.X, ModifierKeys.Control, b);
      Key(System.Windows.Input.Key.V, ModifierKeys.Control, leaf);
      Assert.Contains(b, win.RootShortcuts);
    }

    [Fact]
    public void CtrlV_with_nested_leaf_selected_pastes_into_its_parent_group() {
      ShortcutViewModel a = Root(), x = Root(), leaf = Leaf(x);
      Key(System.Windows.Input.Key.X, ModifierKeys.Control, a);
      Key(System.Windows.Input.Key.V, ModifierKeys.Control, leaf);
      Assert.Contains(a, x.Children);
    }

    [Fact]
    public void CtrlV_of_cut_group_into_own_descendant_is_refused() {
      ShortcutViewModel a = Root(), b = Child(a), c = Child(b);
      Key(System.Windows.Input.Key.X, ModifierKeys.Control, a);
      Key(System.Windows.Input.Key.V, ModifierKeys.Control, c);
      Key(System.Windows.Input.Key.V, ModifierKeys.Control, a);
      Assert.Contains(a, win.RootShortcuts);
      Assert.Single(win.RootShortcuts);
      Assert.Equal(-1, ParentShortcutId(a));
      Assert.Equal(a.ID, ParentShortcutId(b));
      Assert.Equal(b.ID, ParentShortcutId(c));
    }

    [Fact]
    public void Enter_runs_a_shortcut() {
      ShortcutViewModel leaf = Leaf(null);
      Assert.True(Key(System.Windows.Input.Key.Enter, ModifierKeys.None, leaf));
      Assert.Equal(new[] { leaf }, ran);
    }

    [Fact]
    public void Enter_toggles_group_expansion_and_does_not_run() {
      ShortcutViewModel g = Root();
      bool initial = g.IsGroupExpanded;
      Key(System.Windows.Input.Key.Enter, ModifierKeys.None, g);
      Assert.Equal(!initial, g.IsGroupExpanded);
      Key(System.Windows.Input.Key.Enter, ModifierKeys.None, g);
      Assert.Equal(initial, g.IsGroupExpanded);
      Assert.Empty(ran);
    }

    [Fact]
    public void No_selection_does_nothing_and_does_not_throw() {
      Root();
      long before = Count();
      foreach (Key k in new[] { System.Windows.Input.Key.F2, System.Windows.Input.Key.Delete, System.Windows.Input.Key.Enter }) {
        Assert.False(Key(k, ModifierKeys.None, null));
      }
      Assert.False(Key(System.Windows.Input.Key.X, ModifierKeys.Control, null));
      Assert.False(Key(System.Windows.Input.Key.C, ModifierKeys.Control, null));
      Assert.Equal(before, Count());
      Assert.False(AnyEditing);
      Assert.Empty(ran);
    }

    [Fact]
    public void Keys_are_ignored_while_editing() {
      ShortcutViewModel a = Root(), x = Root();
      long before = Count();
      foreach (var (k, m) in new[] {
        (System.Windows.Input.Key.F2, ModifierKeys.None), (System.Windows.Input.Key.Delete, ModifierKeys.None),
        (System.Windows.Input.Key.Enter, ModifierKeys.None), (System.Windows.Input.Key.Escape, ModifierKeys.None),
        (System.Windows.Input.Key.X, ModifierKeys.Control), (System.Windows.Input.Key.C, ModifierKeys.Control),
        (System.Windows.Input.Key.V, ModifierKeys.Control) }) {
        Assert.False(Key(k, m, a, editing: true));
      }
      Assert.Equal(before, Count());
      Assert.False(AnyEditing);
      Assert.Empty(ran);
      Assert.False(app.CanPasteTo(x)); // nothing was cut/copied
    }

    [Fact]
    public void Unmapped_keys_and_wrong_modifiers_are_not_handled() {
      ShortcutViewModel a = Root();
      Assert.False(Key(System.Windows.Input.Key.A, ModifierKeys.None, a));
      Assert.False(Key(System.Windows.Input.Key.C, ModifierKeys.None, a));
      Assert.False(Key(System.Windows.Input.Key.Delete, ModifierKeys.Control, a));
      Assert.False(Key(System.Windows.Input.Key.C, ModifierKeys.Control | ModifierKeys.Shift, a));
      Assert.False(app.CanPasteTo(null));
    }
  }
}
