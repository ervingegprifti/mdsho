using System;
using System.Collections.Generic;
using System.Linq;
using MDSHO.Core;
using MDSHO.Data;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>Contract tests of the repositories (MDSHO.Core interfaces) against a real temp SQLite database.</summary>
  public class RepositoryTests : IDisposable {
    private readonly TempDb db = new TempDb();
    public void Dispose() { db.Dispose(); }

    private static IDesktopWindowRepository Windows { get { return DataStore.Windows; } }
    private static IDesktopWindowCloneRepository Clones { get { return DataStore.Clones; } }
    private static IShortcutRepository Shortcuts { get { return DataStore.Shortcuts; } }

    private static DesktopWindow NewWindow() { return Windows.Create("DEV", 5, 6); }

    #region WINDOWS
    [Fact]
    public void Window_Create_uses_defaults_and_returns_the_stored_row() {
      DesktopWindow w = Windows.Create("\\\\.\\DISPLAY2", 11, 22);
      Assert.True(w.ID > 0);
      Assert.Equal("MDSHO", w.Title);
      Assert.Equal("\\\\.\\DISPLAY2", w.ScreenDeviceName);
      Assert.Equal(11, w.Left);
      Assert.Equal(22, w.Top);
      Assert.Equal(MDSHO.Common.AppConstants.InitialWindowWidth, w.Width);
      Assert.Equal(MDSHO.Common.AppConstants.InitialWindowHeight, w.Height);
      Assert.Equal("#FF000000", w.BorderColor);
      Assert.Equal("#99000000", w.BackgroundColor);
      Assert.Equal("#FFFFFFFF", w.TextColor);
      Assert.Equal("#CC000000", w.TitleBackgroundColor);
      Assert.Equal("#FFFFFFFF", w.TitleTextColor);
      Assert.Equal("Notes for this window.", w.Notes);
      Assert.True(w.SolidIcons);
      Assert.False(w.SingleClickToOpen);
    }

    [Fact]
    public void Window_Add_then_Get_roundtrips_every_field() {
      DesktopWindow w = new DesktopWindow("T ü 'q' \"x\"", "DEV", -5, -6, 300, 400, false, "#1", "#2", "#3", "#4", "#5", "line1\nline2", true);
      long id = Windows.Add(w);
      Assert.True(id > 0);
      Assert.Equal(id, w.ID);
      DesktopWindow r = Windows.Get(id);
      Assert.Equal("T ü 'q' \"x\"", r.Title);
      Assert.Equal("DEV", r.ScreenDeviceName);
      Assert.Equal(-5, r.Left);
      Assert.Equal(-6, r.Top);
      Assert.Equal(300, r.Width);
      Assert.Equal(400, r.Height);
      Assert.False(r.SolidIcons);
      Assert.True(r.SingleClickToOpen);
      Assert.Equal("#1", r.BorderColor);
      Assert.Equal("#2", r.BackgroundColor);
      Assert.Equal("#3", r.TextColor);
      Assert.Equal("#4", r.TitleBackgroundColor);
      Assert.Equal("#5", r.TitleTextColor);
      Assert.Equal("line1\nline2", r.Notes);
      Assert.Equal(1, db.Scalar("SELECT SingleClickToOpen FROM MDSHOWindows WHERE ID=" + id)); // stored as INTEGER 0/1
    }

    [Fact]
    public void Window_Get_of_a_missing_id_returns_null_and_GetAll_lists_rows() {
      DesktopWindow a = NewWindow();
      DesktopWindow b = NewWindow();
      Assert.Null(Windows.Get(99999));
      List<long> ids = Windows.GetAll().Select(x => x.ID).ToList();
      Assert.Contains(a.ID, ids);
      Assert.Contains(b.ID, ids);
    }

    [Fact]
    public void Window_Update_writes_every_field() {
      DesktopWindow w = NewWindow();
      w.Title = "New"; w.ScreenDeviceName = "D9"; w.Left = 1; w.Top = 2; w.Width = 3; w.Height = 4;
      w.SolidIcons = false; w.SingleClickToOpen = true;
      w.BorderColor = "#a"; w.BackgroundColor = "#b"; w.TextColor = "#c"; w.TitleBackgroundColor = "#d"; w.TitleTextColor = "#e"; w.Notes = "nn";
      Windows.Update(w);
      DesktopWindow r = Windows.Get(w.ID);
      Assert.Equal("New", r.Title);
      Assert.Equal("D9", r.ScreenDeviceName);
      Assert.Equal((1L, 2L, 3L, 4L), (r.Left, r.Top, r.Width, r.Height));
      Assert.False(r.SolidIcons);
      Assert.True(r.SingleClickToOpen);
      Assert.Equal(("#a", "#b", "#c", "#d", "#e", "nn"), (r.BorderColor, r.BackgroundColor, r.TextColor, r.TitleBackgroundColor, r.TitleTextColor, r.Notes));
    }

    [Fact]
    public void Window_single_column_updates_change_only_their_column() {
      DesktopWindow other = NewWindow();
      DesktopWindow w = NewWindow();
      Windows.UpdateTitle(w.ID, "T2");
      Windows.UpdateBorderColor(w.ID, "#b1");
      Windows.UpdateBackgroundColor(w.ID, "#b2");
      Windows.UpdateTextColor(w.ID, "#b3");
      Windows.UpdateTitleBackgroundColor(w.ID, "#b4");
      Windows.UpdateTitleTextColor(w.ID, "#b5");
      Windows.UpdateSolidIcons(w.ID, false);
      Windows.UpdateSingleClickToOpen(w.ID, true);
      DesktopWindow r = Windows.Get(w.ID);
      Assert.Equal(("T2", "#b1", "#b2", "#b3", "#b4", "#b5"), (r.Title, r.BorderColor, r.BackgroundColor, r.TextColor, r.TitleBackgroundColor, r.TitleTextColor));
      Assert.False(r.SolidIcons);
      Assert.True(r.SingleClickToOpen);
      Assert.Equal((w.ScreenDeviceName, w.Left, w.Top, w.Width, w.Height, w.Notes), (r.ScreenDeviceName, r.Left, r.Top, r.Width, r.Height, r.Notes));
      Assert.Equal("MDSHO", Windows.Get(other.ID).Title); // other rows untouched
    }

    [Fact]
    public void Window_position_and_size_updates() {
      DesktopWindow w = NewWindow();
      long width = w.Width;
      Windows.UpdateLeftTopHeight(w.ID, 1, 2, 3);
      DesktopWindow r = Windows.Get(w.ID);
      Assert.Equal((1L, 2L, width, 3L), (r.Left, r.Top, r.Width, r.Height));
      Windows.UpdateLeftTopWidthHeight(w.ID, 10, 20, 30, 40);
      r = Windows.Get(w.ID);
      Assert.Equal((10L, 20L, 30L, 40L), (r.Left, r.Top, r.Width, r.Height));
      Windows.UpdateLeftTopScreenDeviceName(w.ID, 7, 8, "DX");
      r = Windows.Get(w.ID);
      Assert.Equal((7L, 8L, 30L, 40L, "DX"), (r.Left, r.Top, r.Width, r.Height, r.ScreenDeviceName));
      Windows.UpdateWidthHeightScreenDeviceName(w.ID, 50, 60, "DY");
      r = Windows.Get(w.ID);
      Assert.Equal((7L, 8L, 50L, 60L, "DY"), (r.Left, r.Top, r.Width, r.Height, r.ScreenDeviceName));
    }

    [Fact]
    public void Window_Delete_cascades_to_clones_and_shortcuts_but_not_to_other_windows() {
      DesktopWindow keep = NewWindow();
      DesktopWindow w = NewWindow();
      Clones.Create(w.ID, "D", 0, 0);
      Clones.Create(keep.ID, "D", 0, 0);
      Shortcuts.CreateGroup(null, w.ID);
      Shortcuts.CreateGroup(null, keep.ID);
      Shortcut g = Shortcuts.CreateGroup(null, w.ID);
      Shortcuts.Create(g.ID, null, "c", "t", ".lnk", "G");
      Windows.Delete(w.ID);
      Assert.Null(Windows.Get(w.ID));
      Assert.Equal(0, db.Scalar("SELECT count(*) FROM MDSHOWindowClones WHERE ParentMDSHOWindowID=" + w.ID));
      Assert.Equal(0, db.Scalar("SELECT count(*) FROM Shortcuts WHERE ParentMDSHOWindowID=" + w.ID));
      Assert.Equal(1, db.Scalar("SELECT count(*) FROM Shortcuts"));
      Assert.Equal(1, db.Scalar("SELECT count(*) FROM MDSHOWindowClones"));
    }

    [Fact]
    public void Window_LoadAll_seeds_the_first_window_on_an_empty_database() {
      db.Exec("DELETE FROM MDSHOWindows");
      List<DesktopWindow> all = Windows.LoadAll();
      Assert.Single(all);
      Assert.Equal("MDSHO", all[0].Title);
      Assert.Equal(1, db.Scalar("SELECT count(*) FROM MDSHOWindows"));
    }
    #endregion WINDOWS

    #region CLONES
    [Fact]
    public void Clone_Create_Add_Get_roundtrip() {
      DesktopWindow w = NewWindow();
      DesktopWindowClone c = Clones.Create(w.ID, "DC", 3, 4);
      Assert.True(c.ID > 0);
      Assert.Equal(w.ID, c.ParentWindowID);
      Assert.Equal(("DC", 3L, 4L), (c.ScreenDeviceName, c.Left, c.Top));
      Assert.Equal(MDSHO.Common.AppConstants.InitialWindowWidth, c.Width);
      Assert.Equal(MDSHO.Common.AppConstants.InitialWindowHeight, c.Height);

      DesktopWindowClone added = new DesktopWindowClone(w.ID, "DA", 1, 2, 30, 40);
      long id = Clones.Add(added);
      Assert.Equal(id, added.ID);
      DesktopWindowClone r = Clones.Get(id);
      Assert.Equal((w.ID, "DA", 1L, 2L, 30L, 40L), (r.ParentWindowID, r.ScreenDeviceName, r.Left, r.Top, r.Width, r.Height));
      Assert.Null(Clones.Get(99999));
      Assert.Equal(2, Clones.GetForWindow(w.ID).Count);
      Assert.Equal(2, Clones.GetAll().Count);
    }

    [Fact]
    public void Clone_Update_and_partial_updates() {
      DesktopWindow w = NewWindow();
      DesktopWindowClone c = Clones.Create(w.ID, "D", 0, 0);
      c.ScreenDeviceName = "Z"; c.Left = 9; c.Top = 8; c.Width = 7; c.Height = 6;
      Clones.Update(c);
      DesktopWindowClone r = Clones.Get(c.ID);
      Assert.Equal(("Z", 9L, 8L, 7L, 6L), (r.ScreenDeviceName, r.Left, r.Top, r.Width, r.Height));
      Clones.UpdateLeftTopHeight(c.ID, 1, 2, 3);
      r = Clones.Get(c.ID);
      Assert.Equal((1L, 2L, 7L, 3L), (r.Left, r.Top, r.Width, r.Height));
      Clones.UpdateLeftTopWidthHeight(c.ID, 11, 12, 13, 14);
      r = Clones.Get(c.ID);
      Assert.Equal((11L, 12L, 13L, 14L), (r.Left, r.Top, r.Width, r.Height));
      Clones.UpdateLeftTopScreenDeviceName(c.ID, 21, 22, "Q");
      r = Clones.Get(c.ID);
      Assert.Equal((21L, 22L, 13L, 14L, "Q"), (r.Left, r.Top, r.Width, r.Height, r.ScreenDeviceName));
      Clones.UpdateWidthHeightScreenDeviceName(c.ID, 31, 32, "R");
      r = Clones.Get(c.ID);
      Assert.Equal((21L, 22L, 31L, 32L, "R"), (r.Left, r.Top, r.Width, r.Height, r.ScreenDeviceName));
    }

    [Fact]
    public void Clone_Delete_removes_only_that_clone_and_a_bad_parent_is_rejected() {
      DesktopWindow w = NewWindow();
      DesktopWindowClone a = Clones.Create(w.ID, "D", 0, 0);
      DesktopWindowClone b = Clones.Create(w.ID, "D", 0, 0);
      Clones.Delete(a.ID);
      Assert.Null(Clones.Get(a.ID));
      Assert.NotNull(Clones.Get(b.ID));
      Assert.NotNull(Windows.Get(w.ID));
      Assert.ThrowsAny<Exception>(() => Clones.Create(99999, "D", 0, 0)); // foreign keys are enforced
    }
    #endregion CLONES

    #region SHORTCUTS
    [Fact]
    public void Shortcut_Create_variants_use_their_defaults_and_append_positions() {
      DesktopWindow w = NewWindow();
      Shortcut g = Shortcuts.CreateGroup(null, w.ID);
      Assert.Equal(("New Group", 1L, 1L, 0L, 0L), (g.Name, g.IsGroup, g.IsGroupExpanded, g.IsProcessCommand, g.Position));
      Assert.Equal("#FFFFFFFF", g.MarkColor);
      Assert.Null(g.ParentShortcutID);
      Assert.Equal(w.ID, g.ParentWindowID);
      Assert.False(string.IsNullOrEmpty(g.Guid));

      Shortcut s = Shortcuts.Create(null, w.ID, "calc", "C:\\calc.exe", ".lnk", "GUID-1");
      Assert.Equal(("calc", "C:\\calc.exe", ".lnk", "GUID-1", 0L, 0L, 1L), (s.Name, s.Target, s.Ext, s.Guid, s.IsGroup, s.IsProcessCommand, s.Position));

      Shortcut c = Shortcuts.CreateProcessCommand(null, w.ID);
      Assert.Equal(("New Command", 1L, 0L, 2L), (c.Name, c.IsProcessCommand, c.IsGroup, c.Position));
      Assert.Equal("", c.ProcessFileName);
      Assert.Equal("", c.ProcessArguments);

      // Positions are counted per parent: first child of the group starts at 0 again.
      Shortcut child = Shortcuts.Create(g.ID, null, "k", "t", ".lnk", "GUID-2");
      Assert.Equal(g.ID, child.ParentShortcutID);
      Assert.Null(child.ParentWindowID);
      Assert.Equal(0, child.Position);
      Assert.Equal(1, Shortcuts.Create(g.ID, null, "k2", "t", ".lnk", "GUID-3").Position);
    }

    [Fact]
    public void Shortcut_Append_copies_all_fields_and_takes_the_next_position() {
      DesktopWindow w = NewWindow();
      Shortcuts.CreateGroup(null, w.ID);
      Shortcut template = new Shortcut(null, w.ID, "n", "t", ".url", "G9", 0, 1, "notes", 1, "#FF112233", 77, 1, "cmd.exe", "/c dir");
      Shortcut r = Shortcuts.Append(template);
      Assert.True(r.ID > 0);
      Assert.Equal(1, r.Position); // template position is ignored
      Assert.Equal(("n", "t", ".url", "G9", 0L, 1L, "notes", 1L, "#FF112233", 1L, "cmd.exe", "/c dir"),
        (r.Name, r.Target, r.Ext, r.Guid, r.IsGroup, r.IsGroupExpanded, r.Notes, r.IsMarked, r.MarkColor, r.IsProcessCommand, r.ProcessFileName, r.ProcessArguments));
    }

    [Fact]
    public void Shortcut_Add_Update_Get_roundtrip_with_null_parents() {
      DesktopWindow w = NewWindow();
      Shortcut s = new Shortcut(null, w.ID, "n", "t", ".lnk", "G1", 0, 0, "x", 0, "#FFFFFFFF", 5, 0, "", "");
      long id = Shortcuts.Add(s);
      Assert.Equal(id, s.ID);
      Shortcut r = Shortcuts.Get(id);
      Assert.Null(r.ParentShortcutID);
      Assert.Equal(w.ID, r.ParentWindowID);
      Assert.Equal(5, r.Position);
      r.Name = "n2"; r.Target = "t2"; r.Ext = ".url"; r.Guid = "G2"; r.IsGroup = 1; r.IsGroupExpanded = 1; r.Notes = "y"; r.IsMarked = 1;
      r.MarkColor = "#FF000001"; r.Position = 9; r.IsProcessCommand = 1; r.ProcessFileName = "p"; r.ProcessArguments = "a";
      Shortcuts.Update(r);
      Shortcut u = Shortcuts.Get(id);
      Assert.Equal(("n2", "t2", ".url", "G2", 1L, 1L, "y", 1L, "#FF000001", 9L, 1L, "p", "a"),
        (u.Name, u.Target, u.Ext, u.Guid, u.IsGroup, u.IsGroupExpanded, u.Notes, u.IsMarked, u.MarkColor, u.Position, u.IsProcessCommand, u.ProcessFileName, u.ProcessArguments));
      Assert.Null(Shortcuts.Get(99999));
      Assert.Equal(1, Shortcuts.GetAll().Count);
    }

    [Fact]
    public void Shortcut_single_column_updates() {
      DesktopWindow w = NewWindow();
      Shortcut s = Shortcuts.Create(null, w.ID, "n", "t", ".lnk", "G");
      Shortcut other = Shortcuts.Create(null, w.ID, "o", "t", ".lnk", "G2");
      Shortcuts.UpdateName(s.ID, "renamed");
      Shortcuts.UpdateIsGroupExpanded(s.ID, true);
      Shortcuts.UpdateIsMarked(s.ID, true);
      Shortcuts.UpdateMarkColor(s.ID, "#FF00FF00");
      Shortcuts.UpdatePosition(s.ID, 42);
      Shortcuts.UpdateProcessFileName(s.ID, "pf");
      Shortcuts.UpdateProcessArguments(s.ID, "pa");
      Shortcut r = Shortcuts.Get(s.ID);
      Assert.Equal(("renamed", 1L, 1L, "#FF00FF00", 42L, "pf", "pa"), (r.Name, r.IsGroupExpanded, r.IsMarked, r.MarkColor, r.Position, r.ProcessFileName, r.ProcessArguments));
      Shortcuts.UpdateIsGroupExpanded(s.ID, false);
      Shortcuts.UpdateIsMarked(s.ID, false);
      r = Shortcuts.Get(s.ID);
      Assert.Equal((0L, 0L), (r.IsGroupExpanded, r.IsMarked));
      Assert.Equal("o", Shortcuts.Get(other.ID).Name);
    }

    [Fact]
    public void Shortcut_parent_moves() {
      DesktopWindow w1 = NewWindow();
      DesktopWindow w2 = NewWindow();
      Shortcut g = Shortcuts.CreateGroup(null, w1.ID);
      Shortcut s = Shortcuts.Create(null, w1.ID, "s", "t", ".lnk", "G");
      Shortcuts.UpdateParentShortcutID(s.ID, g.ID);
      Assert.Equal(g.ID, Shortcuts.Get(s.ID).ParentShortcutID);
      Shortcuts.UpdateParentShortcutID(s.ID, null);
      Assert.Null(Shortcuts.Get(s.ID).ParentShortcutID);
      Shortcuts.UpdateParentWindowID(s.ID, w2.ID);
      Assert.Equal(w2.ID, Shortcuts.Get(s.ID).ParentWindowID);
      Shortcuts.UpdateParentShortcutIDAndParentWindowID(s.ID, g.ID, null);
      Shortcut r = Shortcuts.Get(s.ID);
      Assert.Equal(g.ID, r.ParentShortcutID);
      Assert.Null(r.ParentWindowID);
      Shortcuts.UpdateParentShortcutIDAndParentWindowID(s.ID, null, w1.ID);
      r = Shortcuts.Get(s.ID);
      Assert.Null(r.ParentShortcutID);
      Assert.Equal(w1.ID, r.ParentWindowID);
      Assert.ThrowsAny<Exception>(() => Shortcuts.UpdateParentShortcutID(s.ID, 99999)); // foreign keys are enforced
    }

    [Fact]
    public void Shortcut_UpdatePositions_numbers_from_zero_in_the_given_order() {
      DesktopWindow w = NewWindow();
      long a = Shortcuts.CreateGroup(null, w.ID).ID;
      long b = Shortcuts.CreateGroup(null, w.ID).ID;
      long c = Shortcuts.CreateGroup(null, w.ID).ID;
      Shortcuts.UpdatePositions(new[] { c, a, b });
      Assert.Equal(new long[] { c, a, b }, Shortcuts.GetRoots(w.ID).Select(x => x.ID).ToArray());
      Assert.Equal(new long[] { 0, 1, 2 }, Shortcuts.GetRoots(w.ID).Select(x => x.Position).ToArray());
      Shortcuts.UpdatePositions(new long[0]); // empty is a no-op
    }

    [Fact]
    public void Shortcut_GetRoots_GetChildren_GetMaxPosition_are_ordered_and_scoped() {
      DesktopWindow w1 = NewWindow();
      DesktopWindow w2 = NewWindow();
      Assert.Equal(-1, Shortcuts.GetMaxPosition(null, w1.ID));
      Shortcut g = Shortcuts.CreateGroup(null, w1.ID);
      Shortcut a = Shortcuts.Create(g.ID, null, "a", "t", ".lnk", "A");
      Shortcut b = Shortcuts.Create(g.ID, null, "b", "t", ".lnk", "B");
      Shortcuts.Create(null, w2.ID, "z", "t", ".lnk", "Z");
      Shortcuts.UpdatePositions(new[] { b.ID, a.ID });
      Assert.Equal(new[] { "b", "a" }, Shortcuts.GetChildren(g.ID).Select(x => x.Name).ToArray());
      Assert.Equal(new[] { g.ID }, Shortcuts.GetRoots(w1.ID).Select(x => x.ID).ToArray());
      Assert.Equal(new[] { "z" }, Shortcuts.GetRoots(w2.ID).Select(x => x.Name).ToArray());
      Assert.Equal(1, Shortcuts.GetMaxPosition(g.ID, null));
      Assert.Equal(0, Shortcuts.GetMaxPosition(null, w1.ID));
      Assert.Empty(Shortcuts.GetChildren(a.ID));
    }

    [Fact]
    public void Shortcut_Delete_cascades_to_descendants_only() {
      DesktopWindow w = NewWindow();
      Shortcut g = Shortcuts.CreateGroup(null, w.ID);
      Shortcut inner = Shortcuts.CreateGroup(g.ID, null);
      Shortcuts.Create(inner.ID, null, "deep", "t", ".lnk", "D");
      Shortcut sibling = Shortcuts.Create(null, w.ID, "sib", "t", ".lnk", "S");
      Shortcuts.Delete(g.ID);
      Assert.Equal(1, db.Scalar("SELECT count(*) FROM Shortcuts"));
      Assert.NotNull(Shortcuts.Get(sibling.ID));
      Assert.Null(Shortcuts.Get(inner.ID));
    }
    #endregion SHORTCUTS
  }
}
