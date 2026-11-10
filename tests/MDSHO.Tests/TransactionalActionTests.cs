using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MDSHO.ViewModels;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>Multi-write user actions are all-or-nothing: on a mid-action DB failure the DB, the view model collections and the files are unchanged.</summary>
  public class TransactionalActionTests : IDisposable {
    private readonly TempDb db = new TempDb();
    private readonly AppViewModel app = new AppViewModel();
    private readonly DesktopWindowViewModel win;

    public TransactionalActionTests() {
      win = app.AddWindow("TEST", 0, 0);
    }
    public void Dispose() { db.Dispose(); }

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
    private long ParentShortcutId(ShortcutViewModel s) { return db.Scalar("SELECT ParentShortcutID FROM Shortcuts WHERE ID=" + s.ID); }
    private long Count() { return db.Scalar("SELECT COUNT(*) FROM Shortcuts"); }
    private long Pos(ShortcutViewModel s) { return db.Scalar("SELECT Position FROM Shortcuts WHERE ID=" + s.ID); }
    private void FailOnPositionUpdateOf(ShortcutViewModel s) {
      db.Exec("CREATE TRIGGER fail_pos BEFORE UPDATE OF Position ON Shortcuts WHEN NEW.ID=" + s.ID + " BEGIN SELECT RAISE(ABORT,'boom'); END;");
    }
    private void FailOnInsertNumber(long n) { // fails the n-th insert from now on
      long start = Count();
      db.Exec("CREATE TRIGGER fail_ins BEFORE INSERT ON Shortcuts WHEN (SELECT COUNT(*) FROM Shortcuts) >= " + (start + n - 1) + " BEGIN SELECT RAISE(ABORT,'boom'); END;");
    }
    private void DropTriggers() {
      db.Exec("DROP TRIGGER IF EXISTS fail_pos; DROP TRIGGER IF EXISTS fail_ins;");
    }

    // ---- Paste: cut ----
    [Fact]
    public void PasteCut_group_into_group_happy_path() {
      ShortcutViewModel a = Root(), x = Root(), c1 = Child(x);
      Child(a);
      app.CutShortcut(a, null, win);
      app.PasteShortcut(x, null);
      Assert.Equal(new[] { c1, a }, x.Children.ToArray());
      Assert.DoesNotContain(a, win.RootShortcuts);
      Assert.Same(x, a.ParentShortcut);
      Assert.Equal(x.ID, ParentShortcutId(a));
      Assert.Equal(0, Pos(c1));
      Assert.Equal(1, Pos(a));
    }

    [Fact]
    public void PasteCut_group_failing_midway_changes_neither_db_nor_vm() {
      ShortcutViewModel a = Root(), x = Root(), c1 = Child(x);
      ShortcutViewModel ac = Child(a);
      FailOnPositionUpdateOf(a);
      app.CutShortcut(a, null, win);
      Assert.ThrowsAny<Exception>(() => app.PasteShortcut(x, null));
      Assert.Equal(-1, ParentShortcutId(a));
      Assert.Equal(a.ID, ParentShortcutId(ac));
      Assert.Contains(a, win.RootShortcuts);
      Assert.Equal(new[] { c1 }, x.Children.ToArray());
      Assert.Null(a.ParentShortcut);
      DropTriggers();
      Assert.True(app.CanPasteTo(x)); // clipboard kept, retry works
      app.PasteShortcut(x, null);
      Assert.Equal(x.ID, ParentShortcutId(a));
    }

    // ---- Paste: copy ----
    [Fact]
    public void PasteCopy_group_subtree_happy_path() {
      ShortcutViewModel a = Root(), x = Root();
      ShortcutViewModel a1 = Child(a), a2 = Child(a);
      Child(a1);
      long before = Count();
      app.CopyShortcut(a);
      app.PasteShortcut(x, null);
      Assert.Equal(before + 4, Count());
      Assert.Single(x.Children);
      Assert.Equal(2, x.Children[0].Children.Count);
      Assert.Single(x.Children[0].Children[0].Children);
    }

    [Fact]
    public void PasteCopy_group_subtree_failing_midway_leaves_no_partial_copy() {
      ShortcutViewModel a = Root(), x = Root();
      Child(a); Child(a);
      long before = Count();
      FailOnInsertNumber(3); // root of the copy and one child succeed, the next insert fails
      app.CopyShortcut(a);
      Assert.ThrowsAny<Exception>(() => app.PasteShortcut(x, null));
      Assert.Equal(before, Count());
      Assert.Empty(x.Children);
      Assert.Equal(-1, db.Scalar("SELECT 1 FROM Shortcuts WHERE ParentShortcutID=" + x.ID));
    }

    // ---- Delete ----
    private (ShortcutViewModel g1, ShortcutViewModel g2, ShortcutViewModel g3, ShortcutViewModel kid, string file) ArrangeDelete() {
      ShortcutViewModel g1 = Root(), g2 = Root(), g3 = Root();
      ViewModelFactory.UpdateShortcutsPosition(win.RootShortcuts);
      ShortcutViewModel kid = Child(g2);
      Child(g2);
      kid.Guid = "kidguid"; kid.Ext = ".lnk";
      Directory.CreateDirectory(Common.Paths.GetShortcutsPath());
      string file = Path.Combine(Common.Paths.GetShortcutsPath(), "kidguid.lnk");
      File.WriteAllText(file, "x");
      return (g1, g2, g3, kid, file);
    }

    [Fact]
    public void Delete_group_with_children_renumbers_siblings_happy_path() {
      var (g1, g2, g3, kid, file) = ArrangeDelete();
      g2.DeleteShortcutCommand.Execute(null);
      Assert.Equal(new[] { g1, g3 }, win.RootShortcuts.ToArray());
      Assert.Equal(-1, db.Scalar("SELECT 1 FROM Shortcuts WHERE ID=" + g2.ID));
      Assert.Equal(-1, db.Scalar("SELECT 1 FROM Shortcuts WHERE ID=" + kid.ID));
      Assert.Equal(0, Pos(g1));
      Assert.Equal(1, Pos(g3));
      Assert.False(File.Exists(file));
    }

    [Fact]
    public void Delete_group_failing_on_renumbering_keeps_db_vm_and_files() {
      var (g1, g2, g3, kid, file) = ArrangeDelete();
      long before = Count();
      FailOnPositionUpdateOf(g3);
      Assert.ThrowsAny<Exception>(() => g2.DeleteShortcutCommand.Execute(null));
      Assert.Equal(before, Count());
      Assert.Equal(new[] { g1, g2, g3 }, win.RootShortcuts.ToArray());
      Assert.Equal(2, g2.Children.Count);
      Assert.Equal(2, Pos(g3));
      Assert.True(File.Exists(file));
    }

    // ---- Delete confirmation ----
    private List<(string message, string caption)> Prompts(bool answer) {
      var prompts = new List<(string message, string caption)>();
      Dialogs.Confirm = (m, c) => { prompts.Add((m, c)); return answer; };
      return prompts;
    }

    [Fact]
    public void Delete_declined_changes_nothing() {
      var (g1, g2, g3, kid, file) = ArrangeDelete();
      long before = Count();
      var prompts = Prompts(false);
      g2.DeleteShortcutCommand.Execute(null);
      Assert.Single(prompts);
      Assert.Equal(before, Count());
      Assert.Equal(new[] { g1, g2, g3 }, win.RootShortcuts.ToArray());
      Assert.Equal(2, g2.Children.Count);
      Assert.Equal(2, Pos(g3));
      Assert.True(File.Exists(file));
    }

    [Fact]
    public void Delete_confirmed_deletes_and_prompts_once_with_recursive_group_counts() {
      ShortcutViewModel g1 = Root(), g2 = Root();
      ViewModelFactory.UpdateShortcutsPosition(win.RootShortcuts);
      ShortcutViewModel sub = Child(g2);
      Child(sub);
      ShortcutViewModel s1 = ViewModelFactory.NewShortcut(app, g2, null, "s1", "http://a", ".url", "cg1");
      g2.Children.Add(s1);
      ShortcutViewModel s2 = ViewModelFactory.NewShortcut(app, sub, null, "s2", "http://b", ".url", "cg2");
      sub.Children.Add(s2);
      g2.Name = "Work";
      var prompts = Prompts(true);
      g2.DeleteShortcutCommand.Execute(null);
      Assert.Single(prompts);
      Assert.Equal("Delete group 'Work' and everything in it (2 shortcuts, 2 groups)?", prompts[0].message);
      Assert.Equal("Delete Group Confirmation", prompts[0].caption);
      Assert.Equal(new[] { g1 }, win.RootShortcuts.ToArray());
      Assert.Equal(-1, db.Scalar("SELECT 1 FROM Shortcuts WHERE ID=" + s2.ID));
    }

    [Fact]
    public void Delete_shortcut_and_empty_group_messages() {
      ShortcutViewModel g = Root();
      ShortcutViewModel s = ViewModelFactory.NewShortcut(app, g, null, "Docs", "http://a", ".url", "cg3");
      g.Children.Add(s);
      var prompts = Prompts(true);
      s.DeleteShortcutCommand.Execute(null);
      g.Name = "Empty";
      g.DeleteShortcutCommand.Execute(null);
      Assert.Equal(2, prompts.Count);
      Assert.Equal("Delete shortcut 'Docs'?", prompts[0].message);
      Assert.Equal("Delete Shortcut Confirmation", prompts[0].caption);
      Assert.Equal("Delete group 'Empty'? It is empty.", prompts[1].message);
      Assert.Empty(win.RootShortcuts);
    }

    // ---- Drop / reorder ----
    [Fact]
    public void Drop_reorder_within_root_list_happy_path() {
      ShortcutViewModel a = Root(), b = Root(), c = Root();
      ViewModelFactory.UpdateShortcutsPosition(win.RootShortcuts);
      ViewModelFactory.ApplyShortcutMove(win.RootShortcuts, win.RootShortcuts, 3, null, win, new[] { a }, null);
      Assert.Equal(new[] { b, c, a }, win.RootShortcuts.ToArray());
      Assert.Equal(0, Pos(b)); Assert.Equal(1, Pos(c)); Assert.Equal(2, Pos(a));
    }

    [Fact]
    public void Drop_reorder_failing_midway_changes_nothing() {
      ShortcutViewModel a = Root(), b = Root(), c = Root();
      ViewModelFactory.UpdateShortcutsPosition(win.RootShortcuts);
      FailOnPositionUpdateOf(a);
      Assert.ThrowsAny<Exception>(() => ViewModelFactory.ApplyShortcutMove(win.RootShortcuts, win.RootShortcuts, 3, null, win, new[] { a }, null));
      Assert.Equal(new[] { a, b, c }, win.RootShortcuts.ToArray());
      Assert.Equal(0, Pos(a)); Assert.Equal(1, Pos(b)); Assert.Equal(2, Pos(c));
    }

    [Fact]
    public void Drop_move_into_group_happy_path_and_failure() {
      ShortcutViewModel a = Root(), x = Root(), c1 = Child(x);
      ViewModelFactory.UpdateShortcutsPosition(win.RootShortcuts);
      FailOnPositionUpdateOf(a);
      Assert.ThrowsAny<Exception>(() => ViewModelFactory.ApplyShortcutMove(win.RootShortcuts, x.Children, 0, x, null, new[] { a }, null));
      Assert.Equal(-1, ParentShortcutId(a));
      Assert.Contains(a, win.RootShortcuts);
      Assert.Equal(new[] { c1 }, x.Children.ToArray());
      Assert.Null(a.ParentShortcut);
      DropTriggers();
      ViewModelFactory.ApplyShortcutMove(win.RootShortcuts, x.Children, 0, x, null, new[] { a }, null);
      Assert.Equal(new[] { a, c1 }, x.Children.ToArray());
      Assert.DoesNotContain(a, win.RootShortcuts);
      Assert.Same(x, a.ParentShortcut);
      Assert.Null(a.ParentWindowID);
      Assert.Equal(x.ID, ParentShortcutId(a));
      Assert.Equal(0, Pos(a)); Assert.Equal(1, Pos(c1));
    }

    [Fact]
    public void Drop_of_new_shortcuts_failing_midway_rolls_back_rows_and_files() {
      ShortcutViewModel a = Root();
      ViewModelFactory.UpdateShortcutsPosition(win.RootShortcuts);
      long before = Count();
      FailOnPositionUpdateOf(a); // new rows insert fine, the final renumbering fails
      Assert.ThrowsAny<Exception>(() => ViewModelFactory.ApplyShortcutMove(null, win.RootShortcuts, 0, null, win, new ShortcutViewModel[0], created => {
        created.Add(ViewModelFactory.NewShortcut(app, null, win, "n1", "http://a", ".url", "g1"));
        created.Add(ViewModelFactory.NewShortcut(app, null, win, "n2", "http://b", ".url", "g2"));
      }));
      Assert.Equal(before, Count());
      Assert.Single(win.RootShortcuts);
      Assert.False(File.Exists(Path.Combine(Common.Paths.GetShortcutsPath(), "g1.url")));
      DropTriggers();
      ViewModelFactory.ApplyShortcutMove(null, win.RootShortcuts, 0, null, win, new ShortcutViewModel[0], created => {
        created.Add(ViewModelFactory.NewShortcut(app, null, win, "n1", "http://a", ".url", "g1"));
        created.Add(ViewModelFactory.NewShortcut(app, null, win, "n2", "http://b", ".url", "g2"));
      });
      Assert.Equal(3, win.RootShortcuts.Count);
      Assert.Equal(0, Pos(win.RootShortcuts[0]));
      Assert.Equal(2, Pos(a));
    }
  }
}
