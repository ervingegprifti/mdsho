using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using GongSolutions.Wpf.DragDrop;
using MDSHO.Core;
using MDSHO.Helpers;
using MDSHO.ViewModels;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>Bugs found by the 1.1.1 bug hunt: each test reproduces one of them.</summary>
  public class BugHuntTests : IDisposable {
    private readonly TempDb db = new TempDb();
    private readonly AppViewModel app = new AppViewModel();
    private readonly DesktopWindowViewModel win;

    public BugHuntTests() {
      win = app.AddWindow("TEST", 0, 0);
    }
    public void Dispose() { db.Dispose(); }

    private ShortcutViewModel RootGroup() {
      ShortcutViewModel g = ViewModelFactory.NewGroupShortcut(app, null, win);
      win.RootShortcuts.Add(g);
      return g;
    }
    private ShortcutViewModel ChildGroup(ShortcutViewModel parent) {
      ShortcutViewModel g = ViewModelFactory.NewGroupShortcut(app, parent, null);
      parent.Children.Add(g);
      return g;
    }
    private long Count() { return db.Scalar("SELECT COUNT(*) FROM Shortcuts"); }

    // ---- 1. Keep window open with an empty first argument line ----

    [Theory]
    [InlineData("cmd.exe", "/K \r\ndir\r\nver", "/K dir & ver")]
    [InlineData("cmd.exe", "/C   \n\n  dir", "/C dir")]
    [InlineData("powershell.exe", "-NoExit \r\nGet-Date\r\nGet-Location", "-NoExit Get-Date; Get-Location")]
    [InlineData("pwsh.exe", "-NoExit\n\nGet-Date", "-NoExit Get-Date")]
    public void The_keep_open_switch_is_not_joined_to_the_commands_with_a_separator(string program, string stored, string expected) {
      Assert.Equal(expected, RunCommandLine.JoinLines(program, stored));
    }

    [Fact]
    public void A_command_typed_after_an_empty_first_line_still_keeps_the_window_open() {
      string stored = RunCommandLine.ApplyKeepOpen("cmd.exe", "\r\nping localhost", true);
      Assert.Equal("/K ping localhost", RunCommandLine.JoinLines("cmd.exe", stored));
      System.Diagnostics.ProcessStartInfo psi = ShortcutViewModel.CreateRunCommandStartInfo("cmd.exe", stored, "", false, null);
      Assert.Equal("/K ping localhost", psi.Arguments);
    }

    // ---- 2. Paste after the cut or copied item was deleted ----

    [Fact]
    public void Pasting_a_cut_item_that_was_deleted_since_adds_nothing() {
      ShortcutViewModel cut = RootGroup(), target = RootGroup();
      app.CutShortcut(cut, null, win);
      cut.DeleteShortcutCommand.Execute(null);
      Assert.False(app.CanPaste);
      app.PasteShortcut(target, null);
      Assert.Empty(target.Children);
      Assert.Equal(1, Count());
    }

    [Fact]
    public void Pasting_a_copied_item_that_was_deleted_since_adds_nothing() {
      ShortcutViewModel copied = RootGroup(), target = RootGroup();
      ChildGroup(copied);
      app.CopyShortcut(copied);
      copied.DeleteShortcutCommand.Execute(null);
      Assert.False(app.CanPaste);
      app.PasteShortcut(target, null);
      Assert.Empty(target.Children);
      Assert.Equal(1, Count());
    }

    [Fact]
    public void Pasting_an_item_inside_a_deleted_group_adds_nothing() {
      ShortcutViewModel group = RootGroup(), target = RootGroup();
      ShortcutViewModel inner = ChildGroup(group);
      app.CopyShortcut(inner);
      group.DeleteShortcutCommand.Execute(null);
      Assert.False(app.CanPaste);
    }

    [Fact]
    public void Deleting_another_item_keeps_the_cut_item_ready_to_paste() {
      ShortcutViewModel cut = RootGroup(), other = RootGroup(), target = RootGroup();
      app.CutShortcut(cut, null, win);
      other.DeleteShortcutCommand.Execute(null);
      Assert.True(app.CanPaste);
      app.PasteShortcut(target, null);
      Assert.Same(cut, Assert.Single(target.Children));
    }

    [Fact]
    public void Pasting_after_the_window_of_the_cut_item_was_deleted_adds_nothing() {
      DesktopWindowViewModel other = app.AddWindow("TEST", 0, 0);
      ShortcutViewModel cut = ViewModelFactory.NewGroupShortcut(app, null, other);
      other.RootShortcuts.Add(cut);
      ShortcutViewModel target = RootGroup();
      app.CutShortcut(cut, null, other);
      other.DeleteWindowAndShortcuts();
      Assert.False(app.CanPaste);
      app.PasteShortcut(target, null);
      Assert.Empty(target.Children);
    }

    // ---- 3. Paste of a cut item that was dragged somewhere else since ----

    [Fact]
    public void Pasting_a_cut_item_that_was_moved_since_moves_it_from_where_it_is_now() {
      ShortcutViewModel x = RootGroup(), g = RootGroup(), h = RootGroup();
      app.CutShortcut(x, null, win);
      // Dragged into g before pasting.
      ViewModelFactory.ApplyShortcutMove(win.RootShortcuts, g.Children, 0, g, null, new[] { x }, null);
      app.PasteShortcut(h, null);
      Assert.Empty(g.Children);
      Assert.Same(x, Assert.Single(h.Children));
      Assert.Equal(h.ID, db.Scalar("SELECT ParentShortcutID FROM Shortcuts WHERE ID=" + x.ID));
    }

    // ---- 4. Ctrl + drag inside the tree ----

    private sealed class FakeDragInfo : IDragInfo {
      public DataFormat DataFormat { get; set; }
      public object Data { get; set; }
      public Point DragStartPosition { get; set; }
      public Point PositionInDraggedItem { get; set; }
      public DragDropEffects Effects { get; set; }
      public System.Windows.Input.MouseButton MouseButton { get; set; }
      public IEnumerable SourceCollection { get; set; }
      public int SourceIndex { get; set; }
      public object SourceItem { get; set; }
      public IEnumerable SourceItems { get; set; }
      public System.Windows.Data.CollectionViewGroup SourceGroup { get; set; }
      public UIElement VisualSource { get; set; }
      public UIElement VisualSourceItem { get; set; }
      public FlowDirection VisualSourceFlowDirection { get; set; }
      public object DataObject { get; set; }
      public Func<DependencyObject, object, DragDropEffects, DragDropEffects> DragDropHandler { get; set; }
      public DragDropKeyStates DragDropCopyKeyState { get; set; }
      public void RefreshSourceItems(object sender) { }
    }

    private sealed class FakeDropInfo : IDropInfo {
      public object Data { get; set; }
      public IDragInfo DragInfo { get; set; }
      public Point DropPosition { get; set; }
      public Type DropTargetAdorner { get; set; }
      public DragDropEffects Effects { get; set; }
      public int InsertIndex { get; set; }
      public int UnfilteredInsertIndex { get; set; }
      public IEnumerable TargetCollection { get; set; }
      public object TargetItem { get; set; }
      public System.Windows.Data.CollectionViewGroup TargetGroup { get; set; }
      public System.Windows.Controls.ScrollViewer TargetScrollViewer { get; set; }
      public ScrollingMode TargetScrollingMode { get; set; }
      public UIElement VisualTarget { get; set; }
      public UIElement VisualTargetItem { get; set; }
      public System.Windows.Controls.Orientation VisualTargetOrientation { get; set; }
      public FlowDirection VisualTargetFlowDirection { get; set; }
      public string DestinationText { get; set; }
      public string EffectText { get; set; }
      public RelativeInsertPosition InsertPosition { get; set; }
      public DragDropKeyStates KeyStates { get; set; }
      public bool NotHandled { get; set; }
      public bool IsSameDragDropContextAsSource { get; set; }
      public EventType EventType { get; set; }
      public bool AcceptChildItem { get; set; }
      public Type DropTargetHintAdorner { get; set; }
      public DropHintState DropTargetHintState { get; set; }
      public string DropHintText { get; set; }
    }

    [Fact]
    public void Ctrl_drag_of_a_group_onto_the_window_root_copies_it_and_leaves_the_original_in_place() {
      WpfAppHost.Run(() => {
        ShortcutViewModel g = RootGroup(), item = ChildGroup(g);
        FakeDragInfo drag = new FakeDragInfo {
          Data = item, SourceItem = item, SourceCollection = g.Children, DragDropCopyKeyState = DragDropKeyStates.ControlKey
        };
        FakeDropInfo drop = new FakeDropInfo {
          Data = item, DragInfo = drag, KeyStates = DragDropKeyStates.ControlKey, IsSameDragDropContextAsSource = true,
          TargetCollection = win.RootShortcuts, InsertIndex = 1, UnfilteredInsertIndex = 1
        };
        Assert.Equal(DragDropEffects.Copy, ShortcutDropHandler.GetDragDropEffects(drop));
        new ShortcutDropHandler(win).Drop(drop);
        // The original stays where it was, in the database too.
        Assert.Same(item, Assert.Single(g.Children));
        Assert.Same(g, item.ParentShortcut);
        Assert.Equal(g.ID, db.Scalar("SELECT ParentShortcutID FROM Shortcuts WHERE ID=" + item.ID));
        // A new copy is at the root.
        Assert.Equal(2, win.RootShortcuts.Count);
        ShortcutViewModel copy = win.RootShortcuts[1];
        Assert.NotSame(item, copy);
        Assert.NotEqual(item.ID, copy.ID);
        Assert.Null(copy.ParentShortcut);
        Assert.Equal(win.ID, db.Scalar("SELECT ParentMDSHOWindowID FROM Shortcuts WHERE ID=" + copy.ID));
        Assert.Equal(3, Count());
      });
    }

    // ---- 5. Dropping a network share root ----

    [Fact]
    public void Dropping_a_network_share_root_makes_a_shortcut_named_after_the_share() {
      Action<Action> oldRunner = ShortcutViewModel.BackgroundRunner;
      MDSHO.Common.IShellLinkWriter oldWriter = ShortcutFileService.LinkWriter;
      ShortcutViewModel.BackgroundRunner = a => { }; // never touch the network in a test
      ShortcutFileService.LinkWriter = new NoLinkWriter();
      try {
        List<ShortcutViewModel> created = new List<ShortcutViewModel>();
        win.AddDroppedFiles(new[] { @"\\server\share" }, null, win, created);
        ShortcutViewModel s = Assert.Single(created);
        Assert.Equal(@"\\server\share", s.Name);
        Assert.Equal(@"\\server\share", s.Target);
      } finally {
        ShortcutViewModel.BackgroundRunner = oldRunner;
        ShortcutFileService.LinkWriter = oldWriter;
      }
    }

    private sealed class NoLinkWriter : MDSHO.Common.IShellLinkWriter {
      public void CreateLnk(string lnkFilePath, string targetPath, string workingFolder) { }
      public void CreateUrl(string urlFilePath, string url) { }
    }

    // ---- 6. Copying a run command ----

    [Fact]
    public void Copying_a_run_command_keeps_its_stored_target() {
      ShortcutViewModel group = RootGroup();
      ShortcutViewModel command = ViewModelFactory.NewProcessCommandShortcut(app, group, null);
      group.Children.Add(command);
      command.ProcessFileName = "cmd.exe";
      command.ProcessArguments = "/K dir";
      app.CopyShortcut(command);
      app.PasteShortcut(group, null);
      ShortcutViewModel copy = group.Children[1];
      Assert.Equal("", db.ScalarText("SELECT Target FROM Shortcuts WHERE ID=" + copy.ID));
      Assert.Equal("cmd.exe", copy.ProcessFileName);
      Assert.Equal("/K dir", copy.ProcessArguments);
    }
  }
}
