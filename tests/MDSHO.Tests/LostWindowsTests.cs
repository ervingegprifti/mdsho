using System;
using MDSHO.Common;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>Which windows count as lost: their title bar can't be grabbed on any screen (used for notes and comments).</summary>
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
  }
}
