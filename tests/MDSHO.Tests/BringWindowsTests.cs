using System;
using System.Collections.Generic;
using System.Linq;
using MDSHO.Common;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// "Bring other windows to this screen": every normal window of another app, lost or not, moves to the target screen,
  /// centred and cascaded; what the message says afterwards.
  /// </summary>
  public class BringWindowsTests {
    private static readonly ScreenRect LeftWork = new ScreenRect(0, 0, 1920, 1040);      // 1920x1080, taskbar at the bottom
    private static readonly ScreenRect RightWork = new ScreenRect(1920, 0, 2560, 1400);  // 2560x1440 to the right

    // ---- Place ----

    [Fact]
    public void A_window_lands_in_the_middle_of_the_target_work_area_with_its_size() {
      ScreenRect placed = BringWindows.Place(new ScreenRect(-32000, -32000, 800, 600), RightWork, 0);
      Assert.Equal(new ScreenRect(1920 + (2560 - 800) / 2.0, (1400 - 600) / 2.0, 800, 600), placed);
    }

    [Fact]
    public void A_window_bigger_than_the_target_shrinks_to_fit_it() {
      ScreenRect placed = BringWindows.Place(new ScreenRect(5000, 0, 3000, 2000), LeftWork, 0);
      Assert.Equal(LeftWork, placed);
    }

    [Fact]
    public void Several_windows_cascade_so_none_hides_another_and_all_stay_inside() {
      List<ScreenRect> placed = Enumerable.Range(0, 40).Select(i => BringWindows.Place(new ScreenRect(-9000, 0, 800, 600), LeftWork, i)).ToList();
      Assert.NotEqual(placed[0].TopLeft, placed[1].TopLeft);
      Assert.Equal(placed[0].X + BringWindows.CascadeStep, placed[1].X);
      Assert.Equal(placed[0].Y + BringWindows.CascadeStep, placed[1].Y);
      Assert.All(placed, p => Assert.True(LeftWork.Contains(p), p + " is outside the work area"));
    }

    // ---- Bring ----

    internal sealed class FakeOtherWindows : IOtherWindowsService {
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
    public void Bring_moves_every_window_lost_or_not_to_the_target_screen_and_counts_them() {
      FakeOtherWindows windows = new FakeOtherWindows();
      windows.Windows.Add(new OtherWindow((IntPtr)1, "On the left screen", new ScreenRect(100, 100, 800, 600), false));
      windows.Windows.Add(new OtherWindow((IntPtr)2, "Lost", new ScreenRect(-32000, -32000, 800, 600), false));
      windows.Windows.Add(new OtherWindow((IntPtr)3, "Maximized on the right", new ScreenRect(1920, 0, 2560, 1400), true));
      ScreenInfo right = Screen("RIGHT", RightWork);

      BringWindowsResult result = BringWindows.Bring(windows, right);

      Assert.Equal(3, result.Moved);
      Assert.Equal(0, result.Skipped);
      Assert.Equal(new[] { (IntPtr)1, (IntPtr)2, (IntPtr)3 }, windows.Moves.Select(m => m.Handle));
      for (int i = 0; i < 3; i++) {
        Assert.Equal(BringWindows.Place(windows.Windows[i].Bounds, RightWork, i), windows.Moves[i].Bounds);
      }
    }

    [Fact]
    public void Bring_counts_windows_that_Windows_refused_to_move_as_skipped() {
      FakeOtherWindows windows = new FakeOtherWindows();
      windows.Windows.Add(new OtherWindow((IntPtr)7, "Admin app", new ScreenRect(100, 100, 800, 600), false));
      windows.Windows.Add(new OtherWindow((IntPtr)8, "Normal app", new ScreenRect(200, 200, 800, 600), false));
      windows.Refuse.Add((IntPtr)7);

      BringWindowsResult result = BringWindows.Bring(windows, Screen("LEFT", LeftWork));

      Assert.Equal(1, result.Moved);
      Assert.Equal(1, result.Skipped);
    }

    [Fact]
    public void Bring_with_no_other_windows_moves_nothing() {
      FakeOtherWindows windows = new FakeOtherWindows();
      BringWindowsResult result = BringWindows.Bring(windows, Screen("LEFT", LeftWork));
      Assert.Equal(new BringWindowsResult(0, 0), result);
      Assert.Empty(windows.Moves);
    }

    // ---- Message ----

    [Theory]
    [InlineData(0, 0, "No other windows to bring.")]
    [InlineData(1, 0, "Moved 1 window to this screen.")]
    [InlineData(3, 0, "Moved 3 windows to this screen.")]
    [InlineData(3, 1, "Moved 3 windows to this screen.\nSkipped 1 window that Windows didn't let MDSHO move, for example an app running as administrator.")]
    [InlineData(0, 2, "Skipped 2 windows that Windows didn't let MDSHO move, for example apps running as administrator.")]
    public void The_message_says_what_happened(int moved, int skipped, string expected) {
      Assert.Equal(expected.Replace("\n", Environment.NewLine), new BringWindowsResult(moved, skipped).Message);
    }
  }
}
