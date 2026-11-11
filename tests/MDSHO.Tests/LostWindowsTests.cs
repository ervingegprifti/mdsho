using System;
using System.Collections.Generic;
using System.Linq;
using MDSHO.Common;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// "Find other lost windows": which windows of other apps count as lost (their title bar can't be grabbed on any
  /// screen), where each one lands on the target screen, and what the message says.
  /// </summary>
  public class LostWindowsTests {
    private static readonly ScreenRect LeftWork = new ScreenRect(0, 0, 1920, 1040);      // 1920x1080, taskbar at the bottom
    private static readonly ScreenRect RightWork = new ScreenRect(1920, 0, 2560, 1400);  // 2560x1440 to the right
    private static readonly ScreenRect[] Both = { LeftWork, RightWork };

    // ---- IsLost ----

    [Fact]
    public void A_window_fully_on_a_screen_is_not_lost() {
      Assert.False(LostWindows.IsLost(new ScreenRect(100, 100, 800, 600), Both));
    }

    [Fact]
    public void A_window_on_the_second_screen_is_not_lost() {
      Assert.False(LostWindows.IsLost(new ScreenRect(2500, 200, 800, 600), Both));
    }

    [Fact]
    public void A_window_far_outside_every_screen_is_lost() {
      Assert.True(LostWindows.IsLost(new ScreenRect(-32000, -32000, 800, 600), Both));
      Assert.True(LostWindows.IsLost(new ScreenRect(5000, 100, 800, 600), Both));
    }

    [Fact]
    public void A_window_whose_title_bar_is_above_every_screen_is_lost_even_if_its_body_shows() {
      // Only the bottom part shows; the title bar (the top 32 pixels) is above the screen, so it can't be dragged.
      Assert.True(LostWindows.IsLost(new ScreenRect(100, -300, 800, 600), Both));
    }

    [Fact]
    public void A_window_with_enough_of_its_title_bar_on_a_screen_is_not_lost() {
      // Mostly off the left edge, but the right 100 pixels of the title bar are on the screen: it can be dragged back.
      Assert.False(LostWindows.IsLost(new ScreenRect(-700, 100, 800, 600), Both));
    }

    [Fact]
    public void A_sliver_of_title_bar_is_too_little_to_grab_so_the_window_is_lost() {
      // Only 20 pixels of the title bar are on the screen.
      Assert.True(LostWindows.IsLost(new ScreenRect(-780, 100, 800, 600), Both));
    }

    [Fact]
    public void A_title_bar_hidden_under_the_taskbar_counts_as_lost() {
      // The work area ends at 1040; the window starts at 1050, behind the taskbar.
      Assert.True(LostWindows.IsLost(new ScreenRect(100, 1050, 800, 600), Both));
    }

    [Fact]
    public void A_small_window_is_judged_by_what_it_has() {
      // A 50x30 window fully on screen is fine, even though it is narrower than the usual minimum.
      Assert.False(LostWindows.IsLost(new ScreenRect(10, 10, 50, 30), Both));
    }

    [Fact]
    public void Without_screens_nothing_counts_as_lost() {
      Assert.False(LostWindows.IsLost(new ScreenRect(-32000, -32000, 800, 600), Array.Empty<ScreenRect>()));
    }

    // ---- Place ----

    [Fact]
    public void A_window_lands_in_the_middle_of_the_target_work_area_with_its_size() {
      ScreenRect placed = LostWindows.Place(new ScreenRect(-32000, -32000, 800, 600), RightWork, 0);
      Assert.Equal(new ScreenRect(1920 + (2560 - 800) / 2.0, (1400 - 600) / 2.0, 800, 600), placed);
    }

    [Fact]
    public void A_window_bigger_than_the_target_shrinks_to_fit_it() {
      ScreenRect placed = LostWindows.Place(new ScreenRect(5000, 0, 3000, 2000), LeftWork, 0);
      Assert.Equal(LeftWork, placed);
    }

    [Fact]
    public void Several_windows_cascade_so_none_hides_another_and_all_stay_inside() {
      List<ScreenRect> placed = Enumerable.Range(0, 40).Select(i => LostWindows.Place(new ScreenRect(-9000, 0, 800, 600), LeftWork, i)).ToList();
      Assert.NotEqual(placed[0].TopLeft, placed[1].TopLeft);
      Assert.Equal(placed[0].X + LostWindows.CascadeStep, placed[1].X);
      Assert.Equal(placed[0].Y + LostWindows.CascadeStep, placed[1].Y);
      Assert.All(placed, p => Assert.True(LeftWork.Contains(p), p + " is outside the work area"));
    }

    // ---- Rescue ----

    private sealed class FakeOtherWindows : IOtherWindowsService {
      public List<OtherWindow> Windows { get; } = new List<OtherWindow>();
      public HashSet<IntPtr> Refuse { get; } = new HashSet<IntPtr>();
      public List<(IntPtr Handle, ScreenRect Bounds)> Moves { get; } = new List<(IntPtr, ScreenRect)>();
      public IList<OtherWindow> List() { return Windows; }
      public bool MoveTo(OtherWindow window, ScreenRect bounds) {
        if (Refuse.Contains(window.Handle)) {
          return false;
        }
        Moves.Add((window.Handle, bounds));
        return true;
      }
    }

    private static ScreenInfo Screen(string name, ScreenRect work) {
      return new ScreenInfo(name, work, work, 1.0, name == "LEFT");
    }

    [Fact]
    public void Rescue_moves_only_lost_windows_to_the_target_screen_and_counts_them() {
      FakeOtherWindows windows = new FakeOtherWindows();
      windows.Windows.Add(new OtherWindow((IntPtr)1, "Fine", new ScreenRect(100, 100, 800, 600), false));
      windows.Windows.Add(new OtherWindow((IntPtr)2, "Lost left", new ScreenRect(-32000, -32000, 800, 600), false));
      windows.Windows.Add(new OtherWindow((IntPtr)3, "Lost right", new ScreenRect(9000, 100, 640, 480), true));
      ScreenInfo left = Screen("LEFT", LeftWork);
      ScreenInfo right = Screen("RIGHT", RightWork);

      LostWindowsResult result = LostWindows.Rescue(windows, new[] { left, right }, right);

      Assert.Equal(2, result.Moved);
      Assert.Equal(0, result.Skipped);
      Assert.Equal(new[] { (IntPtr)2, (IntPtr)3 }, windows.Moves.Select(m => m.Handle));
      Assert.Equal(LostWindows.Place(windows.Windows[1].Bounds, RightWork, 0), windows.Moves[0].Bounds);
      Assert.Equal(LostWindows.Place(windows.Windows[2].Bounds, RightWork, 1), windows.Moves[1].Bounds);
    }

    [Fact]
    public void Rescue_counts_windows_that_Windows_refused_to_move_as_skipped() {
      FakeOtherWindows windows = new FakeOtherWindows();
      windows.Windows.Add(new OtherWindow((IntPtr)7, "Admin app", new ScreenRect(-32000, 0, 800, 600), false));
      windows.Windows.Add(new OtherWindow((IntPtr)8, "Normal app", new ScreenRect(-32000, 0, 800, 600), false));
      windows.Refuse.Add((IntPtr)7);
      ScreenInfo left = Screen("LEFT", LeftWork);

      LostWindowsResult result = LostWindows.Rescue(windows, new[] { left }, left);

      Assert.Equal(1, result.Moved);
      Assert.Equal(1, result.Skipped);
    }

    [Fact]
    public void Rescue_with_nothing_lost_moves_nothing() {
      FakeOtherWindows windows = new FakeOtherWindows();
      windows.Windows.Add(new OtherWindow((IntPtr)1, "Fine", new ScreenRect(100, 100, 800, 600), false));
      ScreenInfo left = Screen("LEFT", LeftWork);
      LostWindowsResult result = LostWindows.Rescue(windows, new[] { left }, left);
      Assert.Equal(0, result.Moved);
      Assert.Equal(0, result.Skipped);
      Assert.Empty(windows.Moves);
    }

    // ---- Message ----

    [Theory]
    [InlineData(0, 0, "No lost windows found. Every window is on a screen.")]
    [InlineData(1, 0, "Moved 1 window to this screen.")]
    [InlineData(3, 0, "Moved 3 windows to this screen.")]
    [InlineData(3, 1, "Moved 3 windows to this screen.\nSkipped 1 window that Windows didn't let MDSHO move, for example an app running as administrator.")]
    [InlineData(0, 2, "Skipped 2 windows that Windows didn't let MDSHO move, for example apps running as administrator.")]
    public void The_message_says_what_happened(int moved, int skipped, string expected) {
      Assert.Equal(expected.Replace("\n", Environment.NewLine), new LostWindowsResult(moved, skipped).Message);
    }
  }
}
