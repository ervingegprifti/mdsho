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
  /// 2.0.1: a panel that arrives from another PC opens in the centre of the primary screen, like New panel; when several
  /// arrive in one round, each next one is 30 px right and down from the one before. The app gives the place
  /// (<see cref="SyncOptions.NewPanelPlace"/>); without one (tests) they start at 100, 100.
  /// </summary>
  public class SyncPanelPlaceTests : IDisposable {
    private readonly SyncPcs pcs = new SyncPcs();
    private readonly SyncPc a;
    private readonly SyncPc b;

    public SyncPanelPlaceTests() {
      a = pcs.Add("Home PC");
      b = pcs.Add("Work PC");
    }

    public void Dispose() { pcs.Dispose(); }

    private List<string> ThreeMorePanelsAtHome() {
      a.Use();
      List<string> ids = new List<string> { a.FirstPanelSyncId };
      for (int i = 0; i < 2; i++) {
        ids.Add(DataStore.Windows.Create("HOME-SCREEN", 7, 8).SyncId);
      }
      return ids;
    }

    [Fact]
    public void Panels_from_another_PC_open_at_the_given_place_each_next_one_shifted_30() {
      List<string> ids = ThreeMorePanelsAtHome();
      a.Sync();
      b.NewPanelPlace = () => new SyncPanelPlace { ScreenDeviceName = @"\.\DISPLAY1", Left = 640, Top = 270 };
      b.Sync();
      List<DesktopWindow> arrived = ids.Select(b.Panel).OrderBy(p => p.Left).ToList();
      Assert.Equal(new[] { (640L, 270L), (670L, 300L), (700L, 330L) }, arrived.Select(p => (p.Left, p.Top)).ToArray());
      Assert.All(arrived, p => {
        Assert.Equal(@"\.\DISPLAY1", p.ScreenDeviceName);
        Assert.Equal((long)AppConstants.InitialWindowWidth, p.Width);
        Assert.Equal((long)AppConstants.InitialWindowHeight, p.Height);
      });
      // The panel this PC already had stays where it was.
      Assert.NotEqual(640L, b.Panels().Single(p => p.ID == b.FirstPanelId).Left);
    }

    [Fact]
    public void Without_a_place_new_panels_start_at_100_and_shift_30() {
      List<string> ids = ThreeMorePanelsAtHome();
      a.Sync();
      b.Sync();
      List<DesktopWindow> arrived = ids.Select(b.Panel).OrderBy(p => p.Left).ToList();
      Assert.Equal(new[] { (100L, 100L), (130L, 130L), (160L, 160L) }, arrived.Select(p => (p.Left, p.Top)).ToArray());
      Assert.All(arrived, p => Assert.Equal("", p.ScreenDeviceName));
    }

    [Fact]
    public void A_moved_panel_keeps_its_place_in_later_rounds() {
      List<string> ids = ThreeMorePanelsAtHome();
      a.Sync();
      b.NewPanelPlace = () => new SyncPanelPlace { ScreenDeviceName = @"\.\DISPLAY1", Left = 640, Top = 270 };
      b.Sync();
      b.Use();
      DesktopWindow moved = b.Panel(ids[1]);
      DataStore.Windows.UpdateLeftTopWidthHeight(moved.ID, 5, 6, 333, 444);
      a.Use();
      DataStore.Windows.UpdateTitle(a.Panel(ids[1]).ID, "Renamed at home");
      a.Sync();
      b.Sync();
      DesktopWindow after = b.Panel(ids[1]);
      Assert.Equal("Renamed at home", after.Title);
      Assert.Equal((5L, 6L, 333L, 444L), (after.Left, after.Top, after.Width, after.Height));
    }

    [Fact]
    public void The_place_is_asked_only_when_a_panel_arrives() {
      int asked = 0;
      b.NewPanelPlace = () => { asked++; return new SyncPanelPlace { ScreenDeviceName = "X", Left = 1, Top = 2 }; };
      a.Sync();
      b.Sync();
      Assert.Equal(1, asked);
      b.Sync();
      Assert.Equal(1, asked);
    }

    [Fact]
    public void The_centre_of_a_screen_is_where_New_panel_puts_a_panel() {
      ScreenRect work = new ScreenRect(0, 0, 3840, 2120);
      ScreenInfo screen = new ScreenInfo(@"\.\DISPLAY1", new ScreenRect(0, 0, 3840, 2160), work, 1.5, true);
      ScreenPoint centre = ScreenGeometry.CenterNewWindow(screen);
      SyncPanelPlace place = SyncService.CentreOf(screen);
      Assert.Equal(@"\.\DISPLAY1", place.ScreenDeviceName);
      Assert.Equal(((long)centre.X, (long)centre.Y), (place.Left, place.Top));
    }

    [Fact]
    public void The_service_passes_the_place_to_each_round() {
      using (TempDb db = new TempDb()) {
        SyncPanelPlace given = new SyncPanelPlace { ScreenDeviceName = "P", Left = 3, Top = 4 };
        SyncOptions seen = null;
        SyncService sync = new SyncService(TimeProvider.System, "2.0.1", new ScreenSetupTests.FakeTimer(), action => action(), null,
          null, null, null, options => { seen = options; return new SyncResult { FolderReachable = true }; },
          () => given);
        DataStore.AppSettings.Set(SyncSettings.FolderKey, db.Folder);
        DataStore.AppSettings.Set(SyncSettings.OnKey, "On");
        sync.SyncNow();
        Assert.NotNull(seen);
        Assert.Same(given, seen.NewPanelPlace());
      }
    }
  }
}
