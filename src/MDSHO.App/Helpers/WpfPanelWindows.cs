using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Interop;
using MDSHO.Common;
using MDSHO.Infrastructure;

namespace MDSHO.Helpers {

  /// <summary>The app's visible panels and clones, for <see cref="PanelsToFront"/>.</summary>
  public sealed class WpfPanelWindows : IPanelWindows {

    public IntPtr Foreground() { return WindowZOrder.Foreground(); }
    public bool IsAppWindow(IntPtr window) { return WindowZOrder.IsOwnWindow(window); }
    public IList<IntPtr> Panels() { return WindowZOrder.TopFirst(Views().Select(Handle).Where(h => h != IntPtr.Zero)); }
    public IntPtr WindowAbove(IntPtr window) { return WindowZOrder.VisibleWindowAbove(window); }
    public IList<IntPtr> WindowsAbove(IntPtr window) { return WindowZOrder.ShownWindowsAbove(window); }
    public IList<IntPtr> WindowsBelow(IntPtr window) { return WindowZOrder.ShownWindowsBelow(window); }
    public bool IsAlive(IntPtr window) { return WindowZOrder.IsAlive(window); }
    public bool IsTopmost(IntPtr window) { return WindowZOrder.IsTopmost(window); }
    public void PlaceBehind(IntPtr panel, IntPtr insertAfter) { WindowZOrder.PlaceBehind(panel, insertAfter); }
    public void Activate(IntPtr window) { WindowZOrder.Activate(window); }

    public void Raise(IList<IntPtr> panels) {
      List<DesktopWindowView> views = panels.Select(ViewOf).Where(v => v != null).ToList();
      // Bottom panel first: each goes to the top, so the panels keep their order.
      for (int i = views.Count - 1; i >= 0; i--) {
        DesktopWindowView view = views[i];
        if (view.WindowState == WindowState.Minimized) {
          view.WindowState = WindowState.Normal;
        }
        view.Topmost = true;
        view.Topmost = false;
      }
      if (views.Count > 0) {
        // Windows lets us take the focus because the hotkey was just pressed.
        views[0].Activate();
        views[0].FocusButtons();
      }
    }

    private static IEnumerable<DesktopWindowView> Views() {
      return Application.Current?.Windows.OfType<DesktopWindowView>().Where(v => v.IsVisible) ?? Enumerable.Empty<DesktopWindowView>();
    }

    private static IntPtr Handle(Window window) {
      return new WindowInteropHelper(window).Handle;
    }

    private static DesktopWindowView ViewOf(IntPtr handle) {
      return Views().FirstOrDefault(v => Handle(v) == handle);
    }
  }
}
