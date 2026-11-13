using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using MDSHO.Common;
using MDSHO.Helpers;
using MDSHO.ViewModels;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// "Reserve screen space when docked" (a panel's Settings, shared with its clones, off by default): a docked panel holds
  /// its strip of the screen the way the taskbar does, so desktop icons and maximised programs stay out of it. The strip
  /// follows the panel's width and is given back when the panel floats again, the setting is turned off, or it closes.
  /// </summary>
  public class ReserveDockedSpaceTests {

    // ---- Which side a window is docked to ----

    // 1920x1080 at 100%, taskbar 78 px on the right: work area 0,0,1842,1080.
    private static ScreenInfo Screen(double workLeft = 0, double workRight = 1842) =>
      new ScreenInfo("\\\\.\\DISPLAY1", new ScreenRect(0, 0, 1920, 1080), new ScreenRect(workLeft, 0, workRight - workLeft, 1080), 1.0, true);

    [Theory]
    [InlineData(0, 300, DockSide.Left)]       // at the left edge of the work area
    [InlineData(1542, 300, DockSide.Right)]   // at the right edge, beside the taskbar
    [InlineData(400, 300, DockSide.None)]     // full height but floating in the middle
    public void A_full_height_window_at_a_work_area_edge_is_docked_to_that_side(double left, double width, DockSide side) {
      Assert.Equal(side, ScreenGeometry.SideDockedTo(new ScreenRect(left, 0, width, 1080), Screen()));
      Assert.Equal(side != DockSide.None, ScreenGeometry.IsDockedToSide(new ScreenRect(left, 0, width, 1080), Screen()));
    }

    [Fact]
    public void A_window_that_reserved_its_strip_sits_just_outside_the_work_area_and_is_still_docked() {
      // Its own strip moved the work area's edge to the window's inner edge.
      Assert.Equal(DockSide.Left, ScreenGeometry.SideDockedTo(new ScreenRect(0, 0, 300, 1080), Screen(workLeft: 300)));
      Assert.Equal(DockSide.Right, ScreenGeometry.SideDockedTo(new ScreenRect(1242, 0, 300, 1080), Screen(workRight: 1242)));
    }

    [Fact]
    public void A_window_beside_the_work_area_but_off_the_screen_or_not_full_height_is_not_docked() {
      Assert.Equal(DockSide.None, ScreenGeometry.SideDockedTo(new ScreenRect(-300, 0, 300, 1080), Screen()));
      Assert.Equal(DockSide.None, ScreenGeometry.SideDockedTo(new ScreenRect(1842, 0, 300, 1080), Screen()));
      Assert.Equal(DockSide.None, ScreenGeometry.SideDockedTo(new ScreenRect(0, 0, 300, 700), Screen(workLeft: 300)));
    }

    [Fact]
    public void Docking_again_counts_the_strip_the_window_holds_as_free() {
      // A window holding 0..300 on the left: docking it left again must put it back at 0, not beside its own strip.
      ScreenInfo screen = ScreenGeometry.IncludeStrip(Screen(workLeft: 300), new ScreenRect(0, 0, 300, 1080));
      Assert.Equal(new ScreenRect(0, 0, 1842, 1080), screen.WorkingArea);
      Assert.Equal(new ScreenRect(0, 0, 300, 1080), ScreenGeometry.DockLeft(screen, 300));
      ScreenInfo right = ScreenGeometry.IncludeStrip(Screen(workRight: 1542), new ScreenRect(1542, 0, 300, 1080));
      Assert.Equal(new ScreenRect(0, 0, 1842, 1080), right.WorkingArea);
      // A strip on another screen, or not touching the work area, changes nothing.
      Assert.Equal(new ScreenRect(300, 0, 1542, 1080), ScreenGeometry.IncludeStrip(Screen(workLeft: 300), new ScreenRect(5000, 0, 300, 1080)).WorkingArea);
      Assert.Equal(new ScreenRect(0, 0, 1842, 1080), ScreenGeometry.IncludeStrip(Screen(), new ScreenRect(800, 0, 300, 1080)).WorkingArea);
    }

    // ---- The strip a window holds (DockedSpace) ----

    private static readonly IntPtr Handle = new IntPtr(1234);

    [Fact]
    public void Turned_on_and_docked_reserves_the_side_and_turned_off_gives_it_back() {
      FakeAppBars bars = new FakeAppBars();
      DockedSpace space = new DockedSpace(bars, Handle);
      Assert.Equal(DockSide.None, space.HeldSide);
      Assert.Equal(DockSide.Left, space.Update(true, DockSide.Left));
      Assert.Equal(DockSide.Left, space.HeldSide);
      Assert.Equal(new ScreenRect(0, 0, 300, 1040), space.Strip);
      Assert.Equal(DockSide.None, space.Update(false, DockSide.Left));
      Assert.Equal(DockSide.None, space.HeldSide);
      Assert.Null(space.Strip);
      Assert.Equal(new[] { "Reserve Left", "Release" }, bars.Calls);
      Assert.All(bars.Windows, w => Assert.Equal(Handle, w));
    }

    [Fact]
    public void Nothing_is_reserved_while_off_or_floating() {
      FakeAppBars bars = new FakeAppBars();
      DockedSpace space = new DockedSpace(bars, Handle);
      space.Update(false, DockSide.Right);
      space.Update(true, DockSide.None);
      space.Release();
      Assert.Empty(bars.Calls);
    }

    [Fact]
    public void Floating_again_gives_the_strip_back() {
      FakeAppBars bars = new FakeAppBars();
      DockedSpace space = new DockedSpace(bars, Handle);
      space.Update(true, DockSide.Right);
      bars.Rect = new ScreenRect(700, 200, 300, 500); // dragged away
      Assert.Equal(DockSide.None, space.Update(true, DockSide.None));
      Assert.Equal(new[] { "Reserve Right", "Release" }, bars.Calls);
    }

    [Fact]
    public void Each_update_while_docked_reserves_again_so_the_strip_follows_the_width() {
      FakeAppBars bars = new FakeAppBars();
      DockedSpace space = new DockedSpace(bars, Handle);
      space.Update(true, DockSide.Left);
      bars.Rect = new ScreenRect(0, 0, 420, 1040);
      space.Update(true, DockSide.Left);
      Assert.Equal(new[] { "Reserve Left", "Reserve Left" }, bars.Calls);
      Assert.Equal(new ScreenRect(0, 0, 420, 1040), space.Strip);
    }

    [Fact]
    public void Docking_to_the_other_side_moves_the_strip_there() {
      FakeAppBars bars = new FakeAppBars();
      DockedSpace space = new DockedSpace(bars, Handle);
      space.Update(true, DockSide.Left);
      space.Update(true, DockSide.Right);
      Assert.Equal(new[] { "Reserve Left", "Release", "Reserve Right" }, bars.Calls);
      Assert.Equal(DockSide.Right, space.HeldSide);
    }

    [Fact]
    public void A_window_still_on_its_strip_keeps_it_when_the_work_area_edge_moved_away() {
      // Another panel docked beside it on the same side moves the work area's edge; this window hasn't moved.
      FakeAppBars bars = new FakeAppBars();
      DockedSpace space = new DockedSpace(bars, Handle);
      space.Update(true, DockSide.Left);
      Assert.Equal(DockSide.Left, space.Update(true, DockSide.None));
      Assert.Equal(new[] { "Reserve Left", "Reserve Left" }, bars.Calls);
    }

    [Fact]
    public void A_strip_Windows_refuses_is_not_held() {
      FakeAppBars bars = new FakeAppBars { Grant = false };
      DockedSpace space = new DockedSpace(bars, Handle);
      Assert.Equal(DockSide.None, space.Update(true, DockSide.Left));
      Assert.Equal(DockSide.None, space.HeldSide);
      Assert.Null(space.Strip);
    }

    [Fact]
    public void Release_gives_the_strip_back_once() {
      FakeAppBars bars = new FakeAppBars();
      DockedSpace space = new DockedSpace(bars, Handle);
      space.Update(true, DockSide.Left);
      space.Release();
      space.Release();
      Assert.Equal(new[] { "Reserve Left", "Release" }, bars.Calls);
    }

    // ---- The setting ----

    [Fact]
    public void The_setting_is_off_for_a_new_panel_and_saved_once_by_the_real_window_for_it_and_its_clones() {
      using (TempDb db = new TempDb()) {
        AppViewModel app = new AppViewModel();
        DesktopWindowViewModel w = app.AddWindow("TEST", 100, 100);
        w.CloneWindow("TEST", 5, 5);
        DesktopWindowViewModel clone = w.Clones.Single();
        Assert.False(w.Appearance.ReserveDockedSpace);
        Assert.Same(w.Appearance, clone.Appearance);
        clone.Appearance.ReserveDockedSpace = true;
        Assert.Equal(1, db.Scalar("SELECT ReserveDockedSpace FROM MDSHOWindows WHERE ID=" + w.ID));
        // Read back as it starts next time.
        DesktopWindowViewModel reloaded = ViewModelFactory.WindowToViewModel(app, MDSHO.Data.DataStore.Windows.Get(w.ID));
        Assert.True(reloaded.Appearance.ReserveDockedSpace);
      }
    }

    [Fact]
    public void A_window_holding_its_strip_has_square_corners_even_away_from_the_work_area_edge() {
      using (TempDb db = new TempDb()) {
        DesktopWindowViewModel w = new AppViewModel().AddWindow("TEST", 100, 100);
        w.Left = 0; w.Top = 0; w.Width = 300; w.Height = 1080;
        w.RefreshDocked(new[] { Screen(workLeft: 600) });
        Assert.False(w.IsDocked);
        w.RefreshDocked(new[] { Screen(workLeft: 600) }, reservedHere: true);
        Assert.True(w.IsDocked);
      }
    }

    // ---- The Settings window ----

    [Fact]
    public void Settings_has_the_checkbox_under_Other_and_closing_without_Apply_puts_it_back() {
      using (TempDb db = new TempDb()) {
        WpfAppHost.Run(() => {
          DesktopWindowViewModel w = new AppViewModel().AddWindow("TEST", 100, 100);
          SettingsWindow settings = new SettingsWindow(w) { WindowStartupLocation = WindowStartupLocation.Manual, Left = -10000, Top = -10000, ShowActivated = false, ShowInTaskbar = false };
          settings.Show();
          try {
            WpfAppHost.Pump();
            CheckBox single = (CheckBox)settings.FindName("CheckSingleClickToOpen");
            CheckBox reserve = Assert.IsType<CheckBox>(settings.FindName("CheckReserveDockedSpace"));
            Assert.Equal("Reserve screen space when docked", reserve.Content);
            Assert.False(string.IsNullOrEmpty(reserve.ToolTip as string));
            // Right below Single click to open, under Other.
            StackPanel other = (StackPanel)single.Parent;
            Assert.Same(other, reserve.Parent);
            Assert.Equal(other.Children.IndexOf(single) + 1, other.Children.IndexOf(reserve));
            Assert.False(reserve.IsChecked);
            reserve.IsChecked = true;
            WpfAppHost.Pump();
            Assert.True(w.Appearance.ReserveDockedSpace);
          } finally {
            settings.Close();
            WpfAppHost.Pump();
          }
          Assert.False(w.Appearance.ReserveDockedSpace);
        });
      }
    }

    // ---- The panel window ----

    // One screen far off the real desktop, so the panel can dock without ever being seen.
    private sealed class FakeScreens : IScreenService {
      public readonly ScreenInfo Screen;
      public FakeScreens(double workLeftInset = 0) {
        Screen = new ScreenInfo("\\\\.\\FAKE", new ScreenRect(-10000, -10000, 1920, 1080), new ScreenRect(-10000 + workLeftInset, -10000, 1920 - workLeftInset, 1080), 1.0, true);
      }
      public IList<ScreenInfo> AllScreens() { return new List<ScreenInfo> { Screen }; }
      public ScreenInfo PrimaryScreen() { return Screen; }
      public ScreenInfo ScreenFromHandle(IntPtr windowHandle) { return Screen; }
      public ScreenInfo ScreenAtCursor() { return Screen; }
    }

    private static void WithPanel(Action<DesktopWindowViewModel, DesktopWindowView, FakeAppServices> body) {
      using (TempDb db = new TempDb()) {
        WpfAppHost.Run(() => {
          FakeAppServices services = new FakeAppServices();
          services.Services.Screens = new FakeScreens();
          try {
            DesktopWindowViewModel w = new AppViewModel().AddWindow("TEST", 100, 100);
            DesktopWindowView view = new DesktopWindowView(w) { ShowActivated = false, ShowInTaskbar = false };
            view.Show();
            try {
              WpfAppHost.Pump();
              body(w, view, services);
            } finally {
              view.Close();
              WpfAppHost.Pump();
            }
          } finally {
            services.Dispose();
          }
        });
      }
    }

    private static void Click(DesktopWindowView view, string button) {
      Button b = (Button)view.FindName(button);
      b.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, b));
      WpfAppHost.Pump();
    }

    [Fact]
    public void A_docked_panel_reserves_only_while_the_setting_is_on() {
      WithPanel((w, view, services) => {
        FakeAppBars bars = services.AppBars;
        Click(view, "ButtonHeaderDockWindowLeft");
        Assert.True(w.IsDocked);
        Assert.Empty(bars.Calls);
        w.Appearance.ReserveDockedSpace = true;
        WpfAppHost.Pump();
        Assert.Equal(new[] { "Reserve Left" }, bars.Calls);
        Assert.Equal(new WindowInteropHelper(view).Handle, bars.Windows.Last());
        w.Appearance.ReserveDockedSpace = false;
        WpfAppHost.Pump();
        Assert.Equal(new[] { "Reserve Left", "Release" }, bars.Calls);
      });
    }

    [Fact]
    public void Docking_with_the_setting_on_reserves_that_side_and_floating_gives_it_back() {
      WithPanel((w, view, services) => {
        FakeAppBars bars = services.AppBars;
        w.Appearance.ReserveDockedSpace = true;
        WpfAppHost.Pump();
        Assert.Empty(bars.Calls); // floating
        Click(view, "ButtonHeaderDockWindowRight");
        Assert.Equal("Reserve Right", bars.Calls.Last());
        Click(view, "ButtonHeaderDockWindowLeft");
        Assert.Equal(new[] { "Reserve Right", "Release", "Reserve Left" }, bars.Calls);
        // Dragged away from the edge: the panel floats again.
        bars.Rect = new ScreenRect(-9000, -9500, 300, 400);
        w.Left = -9000; w.Top = -9500; w.Height = 400;
        view.UpdateDockedSpace();
        Assert.Equal("Release", bars.Calls.Last());
        Assert.False(w.IsDocked);
      });
    }

    [Fact]
    public void Docking_left_again_while_holding_the_left_strip_keeps_the_panel_at_the_screen_edge() {
      WithPanel((w, view, services) => {
        FakeAppBars bars = services.AppBars;
        w.Appearance.ReserveDockedSpace = true;
        Click(view, "ButtonHeaderDockWindowLeft");
        Assert.Equal(-10000, w.Left);
        bars.Rect = new ScreenRect(-10000, -10000, w.Width, 1080);
        view.UpdateDockedSpace();
        // Windows now leaves the strip out of the work area.
        services.Services.Screens = new FakeScreens(workLeftInset: w.Width);
        Click(view, "ButtonHeaderDockWindowLeft");
        Assert.Equal(-10000, w.Left);
        Assert.True(w.IsDocked);
      });
    }

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [Fact]
    public void A_work_area_change_and_the_app_bar_message_make_the_panel_check_its_strip_again() {
      WithPanel((w, view, services) => {
        FakeAppBars bars = services.AppBars;
        w.Appearance.ReserveDockedSpace = true;
        Click(view, "ButtonHeaderDockWindowLeft");
        int before = bars.Calls.Count;
        IntPtr hwnd = new WindowInteropHelper(view).Handle;
        SendMessage(hwnd, 0x001A /* WM_SETTINGCHANGE */, new IntPtr(0x002F /* SPI_SETWORKAREA */), IntPtr.Zero);
        Assert.Equal(before + 1, bars.Calls.Count);
        SendMessage(hwnd, bars.CallbackMessage, new IntPtr(1 /* ABN_POSCHANGED */), IntPtr.Zero);
        Assert.Equal(before + 2, bars.Calls.Count);
        Assert.Equal("Reserve Left", bars.Calls.Last());
      });
    }

    [Fact]
    public void Closing_the_panel_gives_the_strip_back() {
      FakeAppBars seen = null;
      WithPanel((w, view, services) => {
        seen = services.AppBars;
        w.Appearance.ReserveDockedSpace = true;
        Click(view, "ButtonHeaderDockWindowLeft");
        Assert.Equal("Reserve Left", seen.Calls.Last());
      });
      Assert.Equal("Release", seen.Calls.Last());
    }
  }
}
