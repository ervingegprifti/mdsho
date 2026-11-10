using System;
using System.Linq;
using MDSHO.Core;
using MDSHO.Data;
using MDSHO.ViewModels;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>Bug 1: clones share the parent's DesktopWindowAppearance; only the real window row may be saved, once.</summary>
  public class CloneSharedInfoTests : IDisposable {
    private readonly TempDb db = new TempDb();
    private readonly AppViewModel app = new AppViewModel();

    public void Dispose() { db.Dispose(); }

    // Arrange: windows A and B; clones of A get IDs 1..n, so one clone's ID collides with unrelated window B's ID.
    private (DesktopWindowViewModel a, DesktopWindowViewModel b, DesktopWindowViewModel clone) Arrange() {
      DesktopWindowViewModel a = app.AddWindow("TEST", 0, 0);
      DesktopWindowViewModel b = app.AddWindow("TEST", 0, 0);
      // Clone IDs start at 1 and increment, so cloning A enough times makes a clone ID equal B's window ID.
      for (long i = 1; i <= b.ID; i++) {
        a.CloneWindow("TEST", 10 * (int)i, 10 * (int)i);
      }
      DesktopWindowViewModel colliding = a.Clones.SingleOrDefault(c => c.ID == b.ID);
      Assert.True(colliding != null, "a=" + a.ID + " b=" + b.ID + " clones=" + string.Join(",", a.Clones.Select(c => c.ID)));
      Assert.True(colliding.IsClone);
      Assert.NotEqual(a.ID, b.ID);
      // Count every UPDATE on MDSHOWindows per row.
      db.Exec("CREATE TABLE UpdLog (RowID INTEGER NOT NULL);" +
              "CREATE TRIGGER trg_upd AFTER UPDATE ON MDSHOWindows BEGIN INSERT INTO UpdLog VALUES (NEW.ID); END;");
      return (a, b, colliding);
    }
    private long Updates(long id) { return db.Scalar("SELECT COUNT(*) FROM UpdLog WHERE RowID=" + id); }
    private DesktopWindow Row(long id) { return DataStore.Windows.LoadAll().Single(w => w.ID == id); }

    [Fact]
    public void Title_change_saves_only_original_window_once() {
      var (a, b, _) = Arrange();
      string bTitle = Row(b.ID).Title;
      a.Appearance.Title = "Renamed";
      Assert.Equal("Renamed", Row(a.ID).Title);
      Assert.Equal(bTitle, Row(b.ID).Title);
      Assert.Equal(1, Updates(a.ID));
      Assert.Equal(0, Updates(b.ID));
    }

    [Fact]
    public void SolidIcons_change_saves_only_original_window_once() {
      var (a, b, _) = Arrange();
      bool aOld = Row(a.ID).SolidIcons, bOld = Row(b.ID).SolidIcons;
      a.Appearance.SolidIcons = !aOld;
      Assert.Equal(!aOld, Row(a.ID).SolidIcons);
      Assert.Equal(bOld, Row(b.ID).SolidIcons);
      Assert.Equal(1, Updates(a.ID));
      Assert.Equal(0, Updates(b.ID));
    }

    [Fact]
    public void SingleClickToOpen_change_saves_only_original_window_once() {
      var (a, b, _) = Arrange();
      bool aOld = Row(a.ID).SingleClickToOpen, bOld = Row(b.ID).SingleClickToOpen;
      a.Appearance.SingleClickToOpen = !aOld;
      Assert.Equal(!aOld, Row(a.ID).SingleClickToOpen);
      Assert.Equal(bOld, Row(b.ID).SingleClickToOpen);
      Assert.Equal(1, Updates(a.ID));
      Assert.Equal(0, Updates(b.ID));
    }

    [Fact]
    public void Change_through_the_clones_view_of_the_shared_info_saves_to_original_only() {
      var (a, b, clone) = Arrange();
      Assert.Same(a.Appearance, clone.Appearance);
      string bTitle = Row(b.ID).Title;
      clone.Appearance.Title = "ViaClone";
      Assert.Equal("ViaClone", Row(a.ID).Title);
      Assert.Equal(bTitle, Row(b.ID).Title);
      Assert.Equal(1, Updates(a.ID));
      Assert.Equal(0, Updates(b.ID));
    }
  }
}
