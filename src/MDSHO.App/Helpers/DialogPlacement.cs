using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using MDSHO.Common;

namespace MDSHO.Helpers {

  /// <summary>Opens dialogs centred on the monitor of the window they belong to, not on the primary monitor.</summary>
  public static class DialogPlacement {

    /// <summary>The screen a window is on. Tests replace it so no real monitor is read.</summary>
    public static Func<Window, ScreenInfo> ScreenOf = window => WindowLookup.GetScreenFromWindow(window);

    /// <summary>The physical-pixel rect of a window's handle. Tests replace it so no real window is read.</summary>
    public static Func<Window, Rect> PhysicalBoundsOf = GetPhysicalBounds;

    /// <summary>Moves a window's handle to a physical-pixel top-left. Tests replace it so no real window is moved.</summary>
    public static Action<Window, Point> MoveToPhysical = MovePhysical;

    /// <summary>
    /// Makes <paramref name="owner"/> the dialog's owner and places the dialog in the middle of the work area (without
    /// the taskbar) of the owner's screen. The owner must have been shown.
    /// </summary>
    /// <remarks>
    /// Left/Top are only a first guess: WPF reads them with the DPI of the monitor the window is created on, which is
    /// wrong when the owner's monitor has another scale. So once the dialog has its handle (before it appears) it is moved
    /// in physical pixels, and centred again for the size Windows gives it on that monitor.
    /// </remarks>
    public static void CenterOnScreenOf(Window dialog, Window owner) {
      dialog.Owner = owner;
      dialog.WindowStartupLocation = WindowStartupLocation.Manual;
      ScreenInfo screen = ScreenOf(owner);
      double width = SizeOf(dialog.Width, dialog.MinWidth);
      double height = SizeOf(dialog.Height, dialog.MinHeight);
      Point topLeft = ScreenGeometry.CenterInWorkArea(screen, width, height).ToPoint();
      dialog.Left = topLeft.X;
      dialog.Top = topLeft.Y;

      bool opening = true;
      EventHandler placeOnHandle = null;
      placeOnHandle = (s, e) => {
        dialog.SourceInitialized -= placeOnHandle;
        MoveToPhysical(dialog, ScreenGeometry.CenterInWorkAreaPhysical(screen, width * screen.Scale, height * screen.Scale).ToPoint());
        CenterPhysical(dialog, screen);
      };
      dialog.SourceInitialized += placeOnHandle;
      // A DPI change while opening resizes the dialog: centre it again for the new size.
      dialog.DpiChanged += (s, e) => {
        if (opening) {
          dialog.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() => CenterPhysical(dialog, screen)));
        }
      };
      dialog.ContentRendered += (s, e) => opening = false;
      dialog.Closed += (s, e) => opening = false;
    }

    /// <summary>
    /// Makes <paramref name="owner"/> the dialog's owner and places the dialog centred on the owner window, at
    /// <paramref name="sizeRatio"/> of the owner's width and height (never below the dialog's minimum size). The owner must
    /// have been shown.
    /// </summary>
    /// <remarks>
    /// Like <see cref="CenterOnScreenOf"/>, Left/Top are only a first guess in DIPs; once the dialog has its handle it is
    /// centred on the owner's handle in physical pixels, which is right on any monitor scale and also when the owner is maximized.
    /// </remarks>
    public static void CenterOnWindow(Window dialog, Window owner, double sizeRatio) {
      dialog.Owner = owner;
      dialog.WindowStartupLocation = WindowStartupLocation.Manual;
      double width = Math.Max(dialog.MinWidth, owner.ActualWidth * sizeRatio);
      double height = Math.Max(dialog.MinHeight, owner.ActualHeight * sizeRatio);
      dialog.Width = width;
      dialog.Height = height;
      if (owner.WindowState == WindowState.Normal) {
        dialog.Left = owner.Left + (owner.ActualWidth - width) / 2;
        dialog.Top = owner.Top + (owner.ActualHeight - height) / 2;
      }
      bool opening = true;
      EventHandler placeOnHandle = null;
      placeOnHandle = (s, e) => {
        dialog.SourceInitialized -= placeOnHandle;
        CenterOnOwnerPhysical(dialog, owner);
        // Moving onto another monitor may resize it: centre again for the size Windows gave it there.
        CenterOnOwnerPhysical(dialog, owner);
      };
      dialog.SourceInitialized += placeOnHandle;
      dialog.DpiChanged += (s, e) => {
        if (opening) {
          dialog.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() => CenterOnOwnerPhysical(dialog, owner)));
        }
      };
      dialog.ContentRendered += (s, e) => opening = false;
      dialog.Closed += (s, e) => opening = false;
    }

    // Centres the dialog's handle on the owner's handle for its current physical size, if it is not centred yet.
    private static void CenterOnOwnerPhysical(Window dialog, Window owner) {
      Rect ownerBounds = PhysicalBoundsOf(owner);
      Rect bounds = PhysicalBoundsOf(dialog);
      if (ownerBounds.IsEmpty || bounds.IsEmpty) {
        return;
      }
      Point centred = new Point(Math.Round(ownerBounds.X + (ownerBounds.Width - bounds.Width) / 2), Math.Round(ownerBounds.Y + (ownerBounds.Height - bounds.Height) / 2));
      if (centred != bounds.TopLeft) {
        MoveToPhysical(dialog, centred);
      }
    }

    // Centres the dialog's handle in the screen's work area for its current physical size, if it is not centred yet.
    private static void CenterPhysical(Window dialog, ScreenInfo screen) {
      Rect bounds = PhysicalBoundsOf(dialog);
      if (bounds.IsEmpty) {
        return;
      }
      Point centred = ScreenGeometry.CenterInWorkAreaPhysical(screen, bounds.Width, bounds.Height).ToPoint();
      if (centred != bounds.TopLeft) {
        MoveToPhysical(dialog, centred);
      }
    }

    private static double SizeOf(double size, double minSize) {
      return double.IsNaN(size) ? minSize : Math.Max(size, minSize);
    }

    // ---- Real window handle ----

    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT {
      public int Left, Top, Right, Bottom;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

    private static Rect GetPhysicalBounds(Window window) {
      IntPtr handle = new WindowInteropHelper(window).Handle;
      if (handle == IntPtr.Zero || !GetWindowRect(handle, out RECT r)) {
        return Rect.Empty;
      }
      return new Rect(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);
    }

    private static void MovePhysical(Window window, Point topLeft) {
      IntPtr handle = new WindowInteropHelper(window).Handle;
      if (handle != IntPtr.Zero) {
        SetWindowPos(handle, IntPtr.Zero, (int)topLeft.X, (int)topLeft.Y, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
      }
    }
  }
}
