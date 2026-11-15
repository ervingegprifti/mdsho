using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Interop;
using MDSHO.Common;
using MDSHO.Infrastructure;

namespace MDSHO.Helpers {

  /// <summary>
  /// The panels of the running app for <see cref="PanelsToFront"/>: every visible <see cref="DesktopWindowView"/> (main
  /// panels and clones). Raising uses WPF (restore, Topmost on and off, focus); the rest is <see cref="WindowZOrder"/>.
  /// </summary>
  public sealed class WpfPanelWindows : IPanelWindows {

    /// <inheritdoc/>
    public IntPtr Foreground() { return WindowZOrder.Foreground(); }
    /// <inheritdoc/>
    public bool IsAppWindow(IntPtr window) { return WindowZOrder.IsOwnWindow(window); }
    /// <inheritdoc/>
    public IList<IntPtr> Panels() { return WindowZOrder.TopFirst(Views().Select(Handle).Where(h => h != IntPtr.Zero)); }
    /// <inheritdoc/>
    public IntPtr WindowAbove(IntPtr window) { return WindowZOrder.VisibleWindowAbove(window); }
    /// <inheritdoc/>
    public bool IsAlive(IntPtr window) { return WindowZOrder.IsAlive(window); }
    /// <inheritdoc/>
    public bool IsTopmost(IntPtr window) { return WindowZOrder.IsTopmost(window); }
    /// <inheritdoc/>
    public void PlaceBehind(IntPtr panel, IntPtr insertAfter) { WindowZOrder.PlaceBehind(panel, insertAfter); }
    /// <inheritdoc/>
    public void Activate(IntPtr window) { WindowZOrder.Activate(window); }

    /// <inheritdoc/>
    public void Raise(IList<IntPtr> panels) {
      List<DesktopWindowView> views = panels.Select(ViewOf).Where(v => v != null).ToList();
      // Bottom panel first: each one goes to the top of the normal windows, so the order between the panels stays.
      for (int i = views.Count - 1; i >= 0; i--) {
        DesktopWindowView view = views[i];
        if (view.WindowState == WindowState.Minimized) {
          view.WindowState = WindowState.Normal;
        }
        view.Topmost = true;
        view.Topmost = false;
      }
      if (views.Count > 0) {
        // The press of the global hotkey lets MDSHO take the focus.
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
