using System;
using System.Collections;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Windows;
using GongSolutions.Wpf.DragDrop;
using MDSHO.Common;
using MDSHO.Helpers;
using MDSHO.ViewModels;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// Drag and drop: the WPF part (gong IDropTarget) lives in MDSHO.App's ShortcutDropHandler; making shortcuts from
  /// dropped files and links is plain view model code.
  /// </summary>
  public class DropHandlerTests {

    /// <summary>A drop as gong describes it; only what the handler reads is set.</summary>
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
    public void The_view_model_is_not_a_gong_drop_target() {
      Assert.False(typeof(IDropTarget).IsAssignableFrom(typeof(DesktopWindowViewModel)));
      Assert.True(typeof(IDropTarget).IsAssignableFrom(typeof(ShortcutDropHandler)));
    }

    [Fact]
    public void Dropped_folder_and_url_file_become_shortcuts() {
      using (TempDb db = new TempDb()) {
        DesktopWindowViewModel win = new AppViewModel().AddWindow("TEST", 0, 0);
        string folder = Path.Combine(db.Folder, "My Folder");
        Directory.CreateDirectory(folder);
        string url = Path.Combine(db.Folder, "site.url");
        File.WriteAllText(url, "[InternetShortcut]\r\nURL=https://example.org/\r\n");
        System.Collections.Generic.List<ShortcutViewModel> created = new System.Collections.Generic.List<ShortcutViewModel>();
        win.AddDroppedFiles(new[] { folder, url }, null, win, created);
        Assert.Equal(2, created.Count);
        Assert.Equal("My Folder", created[0].Name);
        Assert.Equal(folder, created[0].Target);
        Assert.Equal(AppConstants.LnkExtension, created[0].Ext);
        Assert.Equal("site.url", created[1].Name);
        Assert.Equal(AppConstants.UrlExtension, created[1].Ext);
        Assert.True(File.Exists(Paths.GetShortcutFilePath(created[1].Guid, AppConstants.UrlExtension)));
      }
    }

    [Fact]
    public void Dropped_text_becomes_a_web_shortcut_only_when_it_is_an_absolute_address() {
      using (TempDb db = new TempDb()) {
        DesktopWindowViewModel win = new AppViewModel().AddWindow("TEST", 0, 0);
        System.Collections.Generic.List<ShortcutViewModel> created = new System.Collections.Generic.List<ShortcutViewModel>();
        win.AddDroppedText("not a link", null, win, created);
        Assert.Empty(created);
        win.AddDroppedText("https://example.org/a", null, win, created);
        ShortcutViewModel s = Assert.Single(created);
        Assert.Equal("https://example.org/a", s.Name);
        Assert.Equal(AppConstants.UrlExtension, s.Ext);
      }
    }

    [Fact]
    public void Dropping_files_from_Explorer_on_an_empty_window_adds_them_at_the_root() {
      using (TempDb db = new TempDb()) {
        WpfAppHost.Run(() => {
          DesktopWindowViewModel win = new AppViewModel().AddWindow("TEST", 0, 0);
          string folder = Path.Combine(db.Folder, "Dropped");
          Directory.CreateDirectory(folder);
          DataObject data = new DataObject();
          data.SetFileDropList(new StringCollection { folder });
          FakeDropInfo drop = new FakeDropInfo { Data = data, TargetCollection = win.RootShortcuts, InsertIndex = 0, UnfilteredInsertIndex = 0 };
          ShortcutDropHandler handler = new ShortcutDropHandler(win);

          handler.DragOver(drop);
          Assert.Equal(DragDropEffects.Link, drop.Effects);
          Assert.Equal(DropTargetAdorners.Insert, drop.DropTargetAdorner);

          handler.Drop(drop);
          ShortcutViewModel added = Assert.Single(win.RootShortcuts);
          Assert.Equal("Dropped", added.Name);
          Assert.Equal(1L, db.Scalar("SELECT COUNT(*) FROM Shortcuts"));
        });
      }
    }

    [Fact]
    public void The_window_view_uses_a_drop_handler_for_its_view_model() {
      using (TempDb db = new TempDb()) {
        WpfAppHost.Run(() => {
          DesktopWindowViewModel win = new AppViewModel().AddWindow("TEST", 0, 0);
          DesktopWindowView view = new DesktopWindowView(win) { ShowActivated = false, ShowInTaskbar = false, Left = -10000, Top = -10000 };
          view.Show();
          try {
            WpfAppHost.Pump();
            UIElement tree = (UIElement)view.FindName("TreeViewShortcuts");
            ShortcutDropHandler handler = Assert.IsType<ShortcutDropHandler>(GongSolutions.Wpf.DragDrop.DragDrop.GetDropHandler(tree));
            Assert.Same(win, handler.Window);
            Assert.True(GongSolutions.Wpf.DragDrop.DragDrop.GetIsDropTarget(tree));
            Assert.True(GongSolutions.Wpf.DragDrop.DragDrop.GetIsDragSource(tree));
          } finally {
            view.Close();
          }
        });
      }
    }
  }
}
