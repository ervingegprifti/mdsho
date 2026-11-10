using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using MDSHO.Common;
using MDSHO.ViewModels;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>Floating windows have rounded corners; a window docked to the left or right screen edge has square corners.</summary>
  public class DockedCornersTests : System.IDisposable {
    private readonly TempDb db = new TempDb();
    private readonly AppViewModel app = new AppViewModel();
    public void Dispose() { db.Dispose(); }

    // 1920x1080 at 100%, taskbar 40px at the bottom: work area 0,0,1920,1040.
    private static ScreenInfo Primary100() => new ScreenInfo("\\\\.\\DISPLAY1", new ScreenRect(0, 0, 1920, 1080), new ScreenRect(0, 0, 1920, 1040), 1.0, true);
    // 3000x1800 physical at 150% (= -2000,0,2000,1160 DIPs work area) to the left of the primary.
    private static ScreenInfo Left150() => new ScreenInfo("\\\\.\\DISPLAY2", new ScreenRect(-3000, 0, 3000, 1800), new ScreenRect(-3000, 0, 3000, 1740), 1.5, false);
    private static List<ScreenInfo> Screens() => new List<ScreenInfo> { Primary100(), Left150() };

    // The view turns IsDocked into the corner radii (DockedCornerRadiusConverter; see PlainViewModelTests for the view).
    private static void AssertRounded(DesktopWindowViewModel w) {
      Assert.False(w.IsDocked);
    }
    private static void AssertSquare(DesktopWindowViewModel w) {
      Assert.True(w.IsDocked);
    }

    [Fact]
    public void A_new_floating_window_has_rounded_corners() {
      DesktopWindowViewModel w = app.AddWindow("TEST", 100, 100);
      Assert.False(w.IsDocked);
      AssertRounded(w);
    }

    [Fact]
    public void Docking_makes_the_corners_square_at_once_and_raises_change_events() {
      DesktopWindowViewModel w = app.AddWindow("TEST", 100, 100);
      List<string> changed = new List<string>();
      w.PropertyChanged += (s, e) => changed.Add(e.PropertyName);
      w.ApplyDock(0, 0, 1040);
      Assert.True(w.IsDocked);
      AssertSquare(w);
      Assert.Contains(nameof(DesktopWindowViewModel.IsDocked), changed);
    }

    [Fact]
    public void Docking_a_clone_makes_only_the_clone_square() {
      DesktopWindowViewModel w = app.AddWindow("TEST", 100, 100);
      w.CloneWindow("TEST", 5, 5);
      DesktopWindowViewModel c = w.Clones.Single();
      c.ApplyDock(0, 0, 1040);
      AssertSquare(c);
      AssertRounded(w);
    }

    [Fact]
    public void Docking_still_saves_the_geometry() {
      DesktopWindowViewModel w = app.AddWindow("TEST", 100, 100);
      w.ApplyDock(11, 22, 333);
      Assert.Equal(11, db.Scalar("SELECT Left FROM MDSHOWindows WHERE ID=" + w.ID));
      Assert.Equal(22, db.Scalar("SELECT Top FROM MDSHOWindows WHERE ID=" + w.ID));
      Assert.Equal(333, db.Scalar("SELECT Height FROM MDSHOWindows WHERE ID=" + w.ID));
    }

    [Fact]
    public void Moving_a_docked_window_away_makes_it_rounded_again() {
      DesktopWindowViewModel w = app.AddWindow("TEST", 100, 100);
      w.ApplyDock(0, 0, 1040);
      w.Left = 300; w.Top = 200; w.Height = 500;
      w.RefreshDocked(Screens());
      Assert.False(w.IsDocked);
      AssertRounded(w);
    }

    [Theory]
    [InlineData(0, 0, 300, 1040)]       // left edge of the primary work area
    [InlineData(1620, 0, 300, 1040)]    // right edge of the primary work area
    [InlineData(-2000, 0, 250, 1160)]   // left edge of the 150% screen
    [InlineData(-250, 0, 250, 1160)]    // right edge of the 150% screen
    public void A_window_restored_at_a_docked_position_is_square(long left, long top, long width, long height) {
      DesktopWindowViewModel w = app.AddWindow("TEST", 100, 100);
      w.Left = left; w.Top = top; w.Width = width; w.Height = height;
      w.RefreshDocked(Screens());
      Assert.True(w.IsDocked);
      AssertSquare(w);
    }

    [Theory]
    [InlineData(10, 0, 300, 1040)]      // not at an edge
    [InlineData(0, 0, 300, 700)]        // at the left edge but not full height
    [InlineData(0, 50, 300, 990)]       // at the left edge but not at the top
    [InlineData(800, 300, 300, 400)]    // floating in the middle
    public void A_window_not_at_a_docked_position_is_rounded(long left, long top, long width, long height) {
      DesktopWindowViewModel w = app.AddWindow("TEST", 100, 100);
      w.ApplyDock(0, 0, 1040);
      w.Left = left; w.Top = top; w.Width = width; w.Height = height;
      w.RefreshDocked(Screens());
      Assert.False(w.IsDocked);
      AssertRounded(w);
    }

    [Fact]
    public void Docked_test_allows_one_pixel_of_rounding() {
      // Positions are saved as whole numbers, so a 125% screen can leave a docked window a fraction off the edge.
      ScreenRect wa = new ScreenRect(0, 0, 1536, 832.8);
      ScreenInfo s125 = new ScreenInfo("D", new ScreenRect(0, 0, 1920, 1080), ScreenGeometry.DipToPhysical(wa, 1.25), 1.25, true);
      Assert.True(ScreenGeometry.IsDockedToSide(new ScreenRect(0, 0, 300, 832), s125));
      Assert.True(ScreenGeometry.IsDockedToSide(new ScreenRect(1236, 0, 300, 832), s125));
      Assert.False(ScreenGeometry.IsDockedToSide(new ScreenRect(3, 0, 300, 832), s125));
    }

    [Fact]
    public void The_view_model_no_longer_has_a_RoundedCorners_setting() {
      Assert.Null(typeof(DesktopWindowViewModel).GetProperty("RoundedCorners"));
      Assert.Null(typeof(MDSHO.Core.DesktopWindow).GetProperty("RoundedCorners"));
      Assert.Null(typeof(MDSHO.Core.DesktopWindowClone).GetProperty("RoundedCorners"));
      Assert.Null(typeof(MDSHO.Core.IDesktopWindowRepository).GetMethod("UpdateRoundedCorners"));
      Assert.Null(typeof(MDSHO.Core.IDesktopWindowCloneRepository).GetMethod("UpdateRoundedCorners"));
    }
  }

  /// <summary>The real Settings window and panel view, never shown on screen.</summary>
  public class DockedCornersViewTests {
    [Fact]
    public void Settings_window_has_no_rounded_corners_checkbox() {
      WpfAppHost.Run(() => {
        using (TempDb db = new TempDb()) {
          AppViewModel app = new AppViewModel();
          DesktopWindowViewModel w = app.AddWindow("TEST", 0, 0);
          SettingsWindow settings = new SettingsWindow(w);
          settings.WindowStartupLocation = WindowStartupLocation.Manual;
          settings.Left = -10000; settings.Top = -10000;
          settings.ShowActivated = false; settings.ShowInTaskbar = false;
          settings.Show();
          try {
            WpfAppHost.Pump();
            List<string> boxes = WpfAppHost.VisualDescendants<System.Windows.Controls.CheckBox>(settings).Select(c => System.Convert.ToString(c.Content)).ToList();
            Assert.DoesNotContain(boxes, b => b.Contains("Rounded"));
          } finally {
            settings.Close();
            WpfAppHost.Pump();
          }
        }
      });
    }

    [Fact]
    public void A_panel_restored_docked_on_a_real_screen_is_square_when_its_view_is_built() {
      WpfAppHost.Run(() => {
        using (TempDb db = new TempDb()) {
          AppViewModel app = new AppViewModel();
          DesktopWindowViewModel w = app.AddWindow("TEST", 0, 0);
          ScreenInfo primary = new MDSHO.Infrastructure.ScreenService().AllScreens().First(s => s.IsPrimary);
          ScreenRect dock = ScreenGeometry.DockLeft(primary, 300);
          w.Left = (long)dock.Left; w.Top = (long)dock.Top; w.Width = 300; w.Height = (long)dock.Height;
          Assert.False(w.IsDocked);
          DesktopWindowView view = new DesktopWindowView(w);
          Assert.True(w.IsDocked);
          Assert.Equal(new CornerRadius(0), ((System.Windows.Controls.Border)view.FindName("BorderHeaderRoundedCorners")).CornerRadius);
        }
      });
    }
  }
}
