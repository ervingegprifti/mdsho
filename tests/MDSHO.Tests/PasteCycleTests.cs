using System;
using MDSHO.ViewModels;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>Bug 4: a cut group must not be pasted into itself or one of its descendants.</summary>
  public class PasteCycleTests : IDisposable {
    private readonly TempDb db = new TempDb();
    private readonly AppViewModel app = new AppViewModel();
    private readonly DesktopWindowViewModel win;

    public PasteCycleTests() {
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
    private long ParentShortcutId(ShortcutViewModel s) {
      return db.Scalar("SELECT ParentShortcutID FROM Shortcuts WHERE ID=" + s.ID); // -1 = NULL
    }
    private void Cut(ShortcutViewModel s) { app.CutShortcut(s, null, win); }

    [Fact]
    public void Cut_into_itself_is_refused() {
      ShortcutViewModel a = Root();
      Cut(a);
      Assert.False(app.CanPasteTo(a));
    }

    [Fact]
    public void Cut_into_child_grandchild_greatgrandchild_is_refused_and_paste_changes_nothing() {
      ShortcutViewModel a = Root(), b = Child(a), c = Child(b), d = Child(c);
      foreach (ShortcutViewModel target in new[] { a, b, c, d }) {
        Cut(a);
        Assert.False(app.CanPasteTo(target));
        app.PasteShortcut(target, null);
        Assert.Contains(a, win.RootShortcuts);
        Assert.Single(win.RootShortcuts);
        Assert.Single(a.Children);
        Assert.Equal(-1, ParentShortcutId(a));
        Assert.Equal(a.ID, ParentShortcutId(b));
        Assert.Equal(b.ID, ParentShortcutId(c));
        Assert.Equal(c.ID, ParentShortcutId(d));
      }
    }

    [Fact]
    public void Cut_into_unrelated_group_is_allowed_and_moves_it() {
      ShortcutViewModel a = Root(), x = Root();
      Child(a);
      Cut(a);
      Assert.True(app.CanPasteTo(x));
      app.PasteShortcut(x, null);
      Assert.Contains(a, x.Children);
      Assert.DoesNotContain(a, win.RootShortcuts);
      Assert.Equal(x.ID, ParentShortcutId(a));
    }

    [Fact]
    public void Cut_of_a_child_into_its_parent_or_sibling_is_allowed() {
      ShortcutViewModel a = Root(), b = Child(a), s = Child(a);
      app.CutShortcut(b, a, null);
      Assert.True(app.CanPasteTo(a));
      Assert.True(app.CanPasteTo(s));
    }

    [Fact]
    public void Cut_into_window_root_is_allowed() {
      ShortcutViewModel a = Root(), b = Child(a);
      app.CutShortcut(b, a, null);
      Assert.True(app.CanPasteTo(null));
    }

    [Fact]
    public void Copy_into_itself_and_descendants_is_allowed_by_design() {
      // A copy deep-clones the group, so no cycle can form; the code intentionally only guards cut.
      ShortcutViewModel a = Root(), b = Child(a);
      app.CopyShortcut(a);
      Assert.True(app.CanPasteTo(a));
      Assert.True(app.CanPasteTo(b));
    }

    [Fact]
    public void Copy_paste_into_descendant_creates_independent_clone_without_cycle() {
      ShortcutViewModel a = Root(), b = Child(a);
      app.CopyShortcut(a);
      app.PasteShortcut(b, null);
      Assert.Single(b.Children);
      ShortcutViewModel clone = b.Children[0];
      Assert.NotEqual(a.ID, clone.ID);
      Assert.Equal(b.ID, ParentShortcutId(clone));
      Assert.Equal(-1, ParentShortcutId(a));
    }

    [Fact]
    public void Nothing_to_paste_cannot_paste() {
      ShortcutViewModel a = Root();
      Assert.False(app.CanPasteTo(a));
    }

    [Fact]
    public void Refused_paste_keeps_clipboard_so_a_valid_paste_still_works() {
      ShortcutViewModel a = Root(), b = Child(a), x = Root();
      Cut(a);
      app.PasteShortcut(b, null); // refused
      Assert.True(app.CanPasteTo(x));
      app.PasteShortcut(x, null);
      Assert.Contains(a, x.Children);
    }
  }
}
