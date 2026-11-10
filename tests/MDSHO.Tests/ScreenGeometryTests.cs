using System.Collections.Generic;
using System.Windows;
using MDSHO.Common;
using Xunit;

namespace MDSHO.Tests {

  public class ScreenGeometryTests {
    // Primary 1920x1080 at 100%, taskbar 40px at the bottom.
    private static ScreenInfo Primary100() => new ScreenInfo("\\\\.\\DISPLAY1", new ScreenRect(0, 0, 1920, 1080), new ScreenRect(0, 0, 1920, 1040), 1.0, true);
    // Secondary 3000x1800 physical at 150% (= 2000x1200 DIPs), to the LEFT of primary (negative physical coordinates).
    private static ScreenInfo Left150() => new ScreenInfo("\\\\.\\DISPLAY2", new ScreenRect(-3000, 0, 3000, 1800), new ScreenRect(-3000, 0, 3000, 1740), 1.5, false);

    [Theory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    [InlineData(1.5)]
    [InlineData(2.0)]
    public void PhysicalToDipAndBack(double scale) {
      ScreenRect physical = new ScreenRect(300, 600, 1500, 900);
      ScreenRect dip = ScreenGeometry.PhysicalToDip(physical, scale);
      Assert.Equal(300 / scale, dip.Left, 6);
      Assert.Equal(600 / scale, dip.Top, 6);
      Assert.Equal(1500 / scale, dip.Width, 6);
      Assert.Equal(900 / scale, dip.Height, 6);
      ScreenRect back = ScreenGeometry.DipToPhysical(dip, scale);
      Assert.Equal(physical.Left, back.Left, 6);
      Assert.Equal(physical.Height, back.Height, 6);
    }

    [Fact]
    public void Center_At100_MatchesOldBehaviour() {
      ScreenPoint p = ScreenGeometry.CenterOnScreen(Primary100(), 600, 400);
      Assert.Equal((1920 - 600) / 2, p.X);
      Assert.Equal((1080 - 400) / 2, p.Y);
    }

    [Fact]
    public void Center_On150PercentSecondaryAtNegativeCoordinates() {
      // Screen in DIPs: Left=-2000, Width=2000, Height=1200.
      ScreenPoint p = ScreenGeometry.CenterOnScreen(Left150(), 600, 400);
      Assert.Equal(-2000 + (2000 - 600) / 2, p.X);
      Assert.Equal((1200 - 400) / 2, p.Y);
    }

    [Fact]
    public void Center_MixedDpi_UsesEachScreensOwnScale() {
      // Secondary to the right of a 100% primary, 150%, physical origin 1920.
      ScreenInfo right150 = new ScreenInfo("D2", new ScreenRect(1920, 0, 3000, 1800), new ScreenRect(1920, 0, 3000, 1740), 1.5, false);
      ScreenPoint p = ScreenGeometry.CenterOnScreen(right150, 600, 400);
      Assert.Equal(1920 / 1.5 + (2000 - 600) / 2, p.X, 6);
      Assert.Equal(400, p.Y);
    }

    [Fact]
    public void Dock_At125Percent() {
      // 2400x1350 physical at 125% = 1920x1080 DIPs, working area 2400x1300 physical = 1920x1040 DIPs.
      ScreenInfo s = new ScreenInfo("D", new ScreenRect(0, 0, 2400, 1350), new ScreenRect(0, 0, 2400, 1300), 1.25, true);
      ScreenRect left = ScreenGeometry.DockLeft(s, 300);
      Assert.Equal(new ScreenRect(0, 0, 300, 1040), left);
      ScreenRect right = ScreenGeometry.DockRight(s, 300);
      Assert.Equal(new ScreenRect(1620, 0, 300, 1040), right);
    }

    [Fact]
    public void Dock_OnOffsetScreenWithTaskbarOnTopAndLeft() {
      ScreenInfo s = new ScreenInfo("D", new ScreenRect(-1500, 0, 1500, 900), new ScreenRect(-1450, 50, 1450, 850), 1.5, false);
      ScreenRect left = ScreenGeometry.DockLeft(s, 200);
      Assert.Equal(-1450 / 1.5, left.Left, 6);
      Assert.Equal(50 / 1.5, left.Top, 6);
      Assert.Equal(850 / 1.5, left.Height, 6);
    }

    [Fact]
    public void Visibility_OnScreen_NotMoved() {
      ScreenRect r = new ScreenRect(100, 100, 600, 400);
      Assert.True(ScreenGeometry.IsTitleBarReachable(r, new[] { Primary100() }));
      Assert.Equal(r, ScreenGeometry.EnsureOnScreen(r, "D1", new[] { Primary100() }));
    }

    [Fact]
    public void Visibility_FullyOff_IsNotReachable() {
      ScreenRect r = new ScreenRect(2500, 100, 600, 400); // saved on a monitor that is gone
      Assert.False(ScreenGeometry.IsTitleBarReachable(r, new[] { Primary100() }));
    }

    [Fact]
    public void Visibility_TitleBarAboveWorkingArea_IsNotReachable() {
      ScreenRect r = new ScreenRect(100, -300, 600, 400); // only the bottom part is on screen
      Assert.False(ScreenGeometry.IsTitleBarReachable(r, new[] { Primary100() }));
    }

    [Fact]
    public void Visibility_PartlyVisibleTitleBar() {
      Assert.True(ScreenGeometry.IsTitleBarReachable(new ScreenRect(1920 - 150, 100, 600, 400), new[] { Primary100() }));
      Assert.False(ScreenGeometry.IsTitleBarReachable(new ScreenRect(1920 - 20, 100, 600, 400), new[] { Primary100() }));
    }

    [Fact]
    public void Visibility_OnSecondScreenOnly_IsReachableWhenScreenPresent() {
      ScreenRect r = new ScreenRect(-1800, 100, 600, 400);
      Assert.True(ScreenGeometry.IsTitleBarReachable(r, new[] { Primary100(), Left150() }));
      Assert.False(ScreenGeometry.IsTitleBarReachable(r, new[] { Primary100() }));
    }

    [Fact]
    public void Relocate_ToNamedScreenWhenStillExists() {
      // Window saved far right of the secondary screen's area, not reachable: goes to that screen, clamped.
      ScreenRect r = new ScreenRect(-5000, 100, 600, 400);
      ScreenRect result = ScreenGeometry.EnsureOnScreen(r, Left150().DeviceName, new[] { Primary100(), Left150() });
      Assert.Equal(-2000, result.Left);
      Assert.Equal(100, result.Top);
      Assert.Equal(600, result.Width);
    }

    [Fact]
    public void Relocate_FallsBackToPrimaryWhenNamedScreenGone() {
      ScreenRect r = new ScreenRect(-1800, 100, 600, 400);
      ScreenRect result = ScreenGeometry.EnsureOnScreen(r, "\\\\.\\DISPLAY9", new[] { Primary100() });
      Assert.Equal(0, result.Left);
      Assert.Equal(100, result.Top);
      Assert.True(ScreenGeometry.IsTitleBarReachable(result, new[] { Primary100() }));
    }

    [Fact]
    public void Relocate_ClampsAndShrinksOversizedWindow() {
      ScreenRect r = new ScreenRect(5000, 5000, 3000, 2000);
      ScreenRect result = ScreenGeometry.EnsureOnScreen(r, "x", new[] { Primary100() });
      Assert.Equal(new ScreenRect(0, 0, 1920, 1040), result);
    }

    [Fact]
    public void Relocate_ClampsBottomRightIntoWorkingArea() {
      ScreenRect r = new ScreenRect(5000, 5000, 600, 400);
      ScreenRect result = ScreenGeometry.EnsureOnScreen(r, "x", new[] { Primary100() });
      Assert.Equal(1920 - 600, result.Left);
      Assert.Equal(1040 - 400, result.Top);
    }

    [Fact]
    public void Relocate_NoScreens_ReturnsInput() {
      ScreenRect r = new ScreenRect(1, 2, 3, 4);
      Assert.Equal(r, ScreenGeometry.EnsureOnScreen(r, "x", new List<ScreenInfo>()));
    }

    // ---- Centring a dialog in a work area (Run Command window) ----

    [Fact]
    public void CenterInWorkArea_centres_the_window() {
      ScreenPoint p = ScreenGeometry.CenterInWorkArea(new ScreenRect(0, 0, 1920, 1040), 560, 540);
      Assert.Equal((1920 - 560) / 2.0, p.X);
      Assert.Equal((1040 - 540) / 2.0, p.Y);
    }

    [Fact]
    public void CenterInWorkArea_works_at_negative_coordinates_and_with_an_offset_top() {
      // A screen to the left of the primary, with the taskbar at its top (work area starts at y=40).
      ScreenPoint p = ScreenGeometry.CenterInWorkArea(new ScreenRect(-2000, 40, 2000, 1160), 600, 400);
      Assert.Equal(-2000 + (2000 - 600) / 2.0, p.X);
      Assert.Equal(40 + (1160 - 400) / 2.0, p.Y);
    }

    [Fact]
    public void CenterInWorkArea_clamps_a_window_bigger_than_the_work_area_to_its_top_left() {
      ScreenPoint p = ScreenGeometry.CenterInWorkArea(new ScreenRect(100, 50, 500, 300), 800, 600);
      Assert.Equal(100, p.X);
      Assert.Equal(50, p.Y);
    }

    [Fact]
    public void CenterInWorkArea_clamps_each_direction_on_its_own() {
      // Too wide but not too tall: left clamps, top still centres.
      ScreenPoint p = ScreenGeometry.CenterInWorkArea(new ScreenRect(0, 0, 500, 1000), 800, 400);
      Assert.Equal(0, p.X);
      Assert.Equal(300, p.Y);
    }

    [Fact]
    public void CenterInWorkArea_of_a_screen_uses_its_working_area_in_dips() {
      // 150% screen left of the primary: working area 3000x1740 px = 2000x1160 DIPs starting at x=-2000.
      ScreenPoint p = ScreenGeometry.CenterInWorkArea(Left150(), 600, 400);
      Assert.Equal(-2000 + (2000 - 600) / 2.0, p.X, 6);
      Assert.Equal((1160 - 400) / 2.0, p.Y, 6);
    }
    // ---- Centring in physical pixels (dialogs are placed through the window handle, so mixed scales don't matter) ----

    [Fact]
    public void CenterInWorkAreaPhysical_on_a_150_percent_screen_right_of_a_100_percent_primary() {
      // Work area 3000x1740 px starting at physical x=1920. A 600x400 DIP dialog is 900x600 px there.
      ScreenInfo right150 = new ScreenInfo("D2", new ScreenRect(1920, 0, 3000, 1800), new ScreenRect(1920, 0, 3000, 1740), 1.5, false);
      ScreenPoint p = ScreenGeometry.CenterInWorkAreaPhysical(right150, 900, 600);
      Assert.Equal(1920 + (3000 - 900) / 2, p.X);
      Assert.Equal((1740 - 600) / 2, p.Y);
      // The DIP answer (x = 1280 + ...) read as physical pixels would be on the primary screen; this one is not.
      Assert.True(right150.Bounds.Contains(new ScreenRect(p.X, p.Y, 900, 600)));
    }

    [Fact]
    public void CenterInWorkAreaPhysical_on_a_screen_left_of_the_primary_with_the_taskbar_on_top() {
      ScreenInfo s = new ScreenInfo("D2", new ScreenRect(-3000, 0, 3000, 1800), new ScreenRect(-3000, 60, 3000, 1740), 1.5, false);
      ScreenPoint p = ScreenGeometry.CenterInWorkAreaPhysical(s, 840, 810);
      Assert.Equal(-3000 + (3000 - 840) / 2, p.X);
      Assert.Equal(60 + (1740 - 810) / 2, p.Y);
    }

    [Fact]
    public void CenterInWorkAreaPhysical_gives_whole_pixels_and_clamps_to_the_top_left() {
      ScreenPoint odd = ScreenGeometry.CenterInWorkAreaPhysical(Primary100(), 901, 401);
      Assert.Equal(509, odd.X);
      Assert.Equal(319, odd.Y);
      ScreenPoint big = ScreenGeometry.CenterInWorkAreaPhysical(Primary100(), 2500, 1200);
      Assert.Equal(0, big.X);
      Assert.Equal(0, big.Y);
    }
  }
}
