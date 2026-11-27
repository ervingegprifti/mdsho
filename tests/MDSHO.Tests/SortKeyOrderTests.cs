using System;
using System.Collections.Generic;
using System.Linq;
using MDSHO.Common;
using MDSHO.Core;
using MDSHO.Data;
using MDSHO.ViewModels;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// 1.30.0: panels and buttons are ordered by a text sort key (then SyncId), so an insert or a move changes only the
  /// keys it must, and two PCs that insert in the same list while apart never collide.
  /// </summary>
  public class SortKeyOrderTests : IDisposable {
    private readonly TempDb db = new TempDb();
    private readonly AppViewModel app = new AppViewModel();
    private readonly DesktopWindowViewModel win;

    public SortKeyOrderTests() {
      win = app.AddWindow("TEST", 0, 0);
    }

    public void Dispose() { db.Dispose(); }

    private static bool Less(string a, string b) { return string.CompareOrdinal(a, b) < 0; }
    private static string Key(long id) { return DataStore.Shortcuts.Get(id).SortKey; }
    private Dictionary<long, (string Key, long At)> Rows() {
      return DataStore.Shortcuts.GetAll().ToDictionary(s => s.ID, s => (s.SortKey, s.ChangedAt));
    }
    private void OldRecords() { db.Exec("UPDATE Shortcuts SET ChangedAt = 5, ChangedBy = 'x'"); }

    private ShortcutViewModel Note(ShortcutViewModel parent = null) {
      ShortcutViewModel note = ViewModelFactory.NewNoteShortcut(app, parent, parent == null ? win : null);
      (parent == null ? win.RootShortcuts : parent.Children).Add(note);
      return note;
    }

    // The saved order as loaded from the database, the same as the panel shows after a restart.
    private long[] SavedRoots() {
      return DataStore.Windows.LoadAll().Single(w => w.ID == win.ID).RootShortcuts.Select(s => s.ID).ToArray();
    }

    [Fact]
    public void New_buttons_go_last_with_increasing_keys() {
      Shortcut a = DataStore.Shortcuts.CreateNote(null, win.ID);
      Shortcut b = DataStore.Shortcuts.CreateGroup(null, win.ID);
      Shortcut c = DataStore.Shortcuts.CreateSnippet(null, win.ID);
      Shortcut child = DataStore.Shortcuts.CreateNote(b.ID, null);
      Assert.True(Less(a.SortKey, b.SortKey) && Less(b.SortKey, c.SortKey));
      Assert.True(SortKeys.IsValid(child.SortKey));
      Assert.Equal(new[] { a.ID, b.ID, c.ID }, DataStore.Shortcuts.GetRoots(win.ID).Select(s => s.ID));
    }

    [Fact]
    public void Saving_an_order_changes_only_the_moved_key() {
      Shortcut a = DataStore.Shortcuts.CreateNote(null, win.ID);
      Shortcut b = DataStore.Shortcuts.CreateNote(null, win.ID);
      Shortcut c = DataStore.Shortcuts.CreateNote(null, win.ID);
      Shortcut d = DataStore.Shortcuts.CreateNote(null, win.ID);
      OldRecords();
      DataStore.Shortcuts.UpdatePositions(new[] { d.ID, a.ID, b.ID, c.ID });
      Dictionary<long, (string Key, long At)> rows = Rows();
      Assert.Equal((a.SortKey, 5L), rows[a.ID]);
      Assert.Equal((b.SortKey, 5L), rows[b.ID]);
      Assert.Equal((c.SortKey, 5L), rows[c.ID]);
      Assert.True(Less(rows[d.ID].Key, a.SortKey));
      Assert.NotEqual(5L, rows[d.ID].At);
      Assert.Equal(new[] { d.ID, a.ID, b.ID, c.ID }, DataStore.Shortcuts.GetRoots(win.ID).Select(s => s.ID));
    }

    [Fact]
    public void Equal_keys_are_ordered_by_sync_id() {
      Shortcut a = DataStore.Shortcuts.CreateNote(null, win.ID);
      Shortcut b = DataStore.Shortcuts.CreateNote(null, win.ID);
      db.Exec("UPDATE Shortcuts SET SortKey = 'a5' WHERE ID IN (" + a.ID + "," + b.ID + ")");
      db.Exec("UPDATE Shortcuts SET SyncId = 'ffffffff-0000-4000-8000-000000000000' WHERE ID = " + a.ID);
      db.Exec("UPDATE Shortcuts SET SyncId = '00000000-0000-4000-8000-000000000000' WHERE ID = " + b.ID);
      Assert.Equal(new[] { b.ID, a.ID }, DataStore.Shortcuts.GetRoots(win.ID).Select(s => s.ID));
      Assert.Equal(new[] { b.ID, a.ID }, SavedRoots());
      // Saving that order gives them different keys, still in that order.
      DataStore.Shortcuts.UpdatePositions(new[] { b.ID, a.ID });
      Assert.True(Less(Key(b.ID), Key(a.ID)));
    }

    [Fact]
    public void Panels_get_increasing_keys_and_load_in_key_order() {
      DesktopWindow first = DataStore.Windows.Get(win.ID);
      DesktopWindow second = DataStore.Windows.Create("S", 0, 0);
      Assert.True(Less(first.SortKey, second.SortKey));
      db.Exec("UPDATE MDSHOWindows SET SortKey = 'Zz' WHERE ID = " + second.ID);
      Assert.Equal(second.ID, DataStore.Windows.GetAll().First().ID);
      Assert.Equal(second.ID, DataStore.Windows.LoadAll().First().ID);
    }

    [Fact]
    public void A_drop_between_two_buttons_gets_a_key_between_theirs_and_nothing_else_changes() {
      ShortcutViewModel a = Note(), b = Note(), c = Note();
      ShortcutViewModel group = ViewModelFactory.NewGroupShortcut(app, null, win);
      win.RootShortcuts.Add(group);
      ShortcutViewModel x = Note(group), y = Note(group);
      OldRecords();
      Dictionary<long, (string Key, long At)> before = Rows();
      // Drag y from the group to between a and b.
      ViewModelFactory.ApplyShortcutMove(group.Children, win.RootShortcuts, 1, null, win, new List<ShortcutViewModel> { y }, null);
      Dictionary<long, (string Key, long At)> after = Rows();
      Assert.True(Less(after[a.ID].Key, after[y.ID].Key) && Less(after[y.ID].Key, after[b.ID].Key));
      foreach (ShortcutViewModel same in new[] { a, b, c, group, x }) {
        Assert.Equal(before[same.ID], after[same.ID]);
      }
      Assert.Equal(win.RootShortcuts.Select(s => s.ID), SavedRoots());
    }

    [Fact]
    public void A_drop_inside_the_same_list_keeps_the_others() {
      ShortcutViewModel a = Note(), b = Note(), c = Note(), d = Note();
      OldRecords();
      Dictionary<long, (string Key, long At)> before = Rows();
      ViewModelFactory.ApplyShortcutMove(win.RootShortcuts, win.RootShortcuts, 4, null, win, new List<ShortcutViewModel> { a }, null);
      Assert.Equal(new[] { b.ID, c.ID, d.ID, a.ID }, win.RootShortcuts.Select(s => s.ID));
      Assert.Equal(new[] { b.ID, c.ID, d.ID, a.ID }, SavedRoots());
      Dictionary<long, (string Key, long At)> after = Rows();
      Assert.All(new[] { b, c, d }, s => Assert.Equal(before[s.ID], after[s.ID]));
    }

    [Fact]
    public void Cut_and_paste_goes_last_and_copy_and_paste_adds_last() {
      ShortcutViewModel a = Note(), b = Note();
      ShortcutViewModel group = ViewModelFactory.NewGroupShortcut(app, null, win);
      win.RootShortcuts.Add(group);
      ShortcutViewModel x = Note(group);
      app.CutShortcut(a, null, win);
      app.PasteShortcut(group, null);
      Assert.Equal(new[] { x.ID, a.ID }, DataStore.Shortcuts.GetChildren(group.ID).Select(s => s.ID));
      app.CopyShortcut(b);
      app.PasteShortcut(group, null);
      long[] children = DataStore.Shortcuts.GetChildren(group.ID).Select(s => s.ID).ToArray();
      Assert.Equal(3, children.Length);
      Assert.Equal(group.Children.Select(s => s.ID), children);
      Assert.Equal(win.RootShortcuts.Select(s => s.ID), SavedRoots());
    }

    [Fact]
    public void Deleting_a_button_does_not_touch_the_others() {
      ShortcutViewModel a = Note(), b = Note(), c = Note();
      OldRecords();
      Dictionary<long, (string Key, long At)> before = Rows();
      b.DeleteShortcutCommand.Execute(null);
      Dictionary<long, (string Key, long At)> after = Rows();
      Assert.Equal(before[a.ID], after[a.ID]);
      Assert.Equal(before[c.ID], after[c.ID]);
      Assert.Equal(new[] { a.ID, c.ID }, SavedRoots());
    }

    [Fact]
    public void Saving_the_list_order_after_a_new_button_in_the_middle_keeps_the_rest() {
      ShortcutViewModel a = Note(), b = Note();
      OldRecords();
      Dictionary<long, (string Key, long At)> before = Rows();
      ShortcutViewModel n = ViewModelFactory.NewNoteShortcut(app, null, win);
      win.RootShortcuts.Insert(1, n);
      ViewModelFactory.UpdateShortcutsPosition(win.RootShortcuts);
      Assert.Equal(new[] { a.ID, n.ID, b.ID }, SavedRoots());
      Assert.Equal(before[a.ID], Rows()[a.ID]);
      Assert.Equal(before[b.ID], Rows()[b.ID]);
    }
  }
}
