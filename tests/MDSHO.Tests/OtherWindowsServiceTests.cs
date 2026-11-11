using System;
using System.Linq;
using System.Windows;
using System.Windows.Interop;
using MDSHO.Common;
using MDSHO.Infrastructure;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// The Windows layer of "Find Other Lost Windows", checked with real windows of the test process. The service normally
  /// skips MDSHO's own process; these tests pass process id 0 so it sees the test's windows.
  /// </summary>
  public class OtherWindowsServiceTests {

    private static void WithWindow(Action<Window, IntPtr> check, WindowState state = WindowState.Normal, WindowStyle style = WindowStyle.SingleBorderWindow, bool inTaskbar = true) {
      WpfAppHost.Run(() => {
        Window window = new Window {
          Title = "MDSHO lost window test " + Guid.NewGuid().ToString("N"),
          WindowStartupLocation = WindowStartupLocation.Manual,
          Left = -30000,
          Top = -30000,
          Width = 400,
          Height = 300,
          ShowActivated = false,
          WindowStyle = style,
          ShowInTaskbar = inTaskbar
        };
        window.Show();
        window.WindowState = state;
        WpfAppHost.Pump();
        try {
          check(window, new WindowInteropHelper(window).Handle);
        } finally {
          window.Close();
        }
      });
    }

    [Fact]
    public void Lists_a_normal_window_with_its_position() {
      WithWindow((window, handle) => {
        OtherWindow found = new OtherWindowsService(excludedProcessId: 0).List().SingleOrDefault(w => w.Handle == handle);
        Assert.NotNull(found);
        Assert.Equal(window.Title, found.Title);
        Assert.True(found.Bounds.X < -20000, "X is " + found.Bounds.X);
        Assert.False(found.IsMaximized);
      });
    }

    [Fact]
    public void Skips_the_own_process_by_default() {
      WithWindow((window, handle) => {
        Assert.DoesNotContain(new OtherWindowsService().List(), w => w.Handle == handle);
      });
    }

    [Fact]
    public void Skips_minimized_windows() {
      WithWindow((window, handle) => {
        Assert.DoesNotContain(new OtherWindowsService(excludedProcessId: 0).List(), w => w.Handle == handle);
      }, WindowState.Minimized);
    }

    [Fact]
    public void Skips_tool_windows() {
      // A real tool window (a floating palette) is not in the taskbar.
      WithWindow((window, handle) => {
        Assert.DoesNotContain(new OtherWindowsService(excludedProcessId: 0).List(), w => w.Handle == handle);
      }, style: WindowStyle.ToolWindow, inTaskbar: false);
    }

    [Fact]
    public void Lists_a_tool_style_window_that_is_in_the_taskbar_like_any_app_window() {
      WithWindow((window, handle) => {
        Assert.Contains(new OtherWindowsService(excludedProcessId: 0).List(), w => w.Handle == handle);
      }, style: WindowStyle.ToolWindow, inTaskbar: true);
    }

    [Fact]
    public void Moves_a_lost_window_onto_a_screen() {
      ScreenInfo primary = new ScreenService().PrimaryScreen();
      ScreenRect target = new ScreenRect(primary.WorkingArea.X + 50, primary.WorkingArea.Y + 50, 400, 300);
      WithWindow((window, handle) => {
        OtherWindowsService service = new OtherWindowsService(excludedProcessId: 0);
        OtherWindow lost = service.List().Single(w => w.Handle == handle);
        Assert.True(service.MoveTo(lost, target));
        OtherWindow moved = service.List().Single(w => w.Handle == handle);
        Assert.Equal(target, moved.Bounds);
      });
    }

    [Fact]
    public void Screen_at_cursor_is_one_of_the_screens() {
      ScreenService screens = new ScreenService();
      ScreenInfo atCursor = screens.ScreenAtCursor();
      Assert.Contains(screens.AllScreens(), s => s.DeviceName == atCursor.DeviceName);
    }
  }
}
