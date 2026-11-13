using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using MDSHO.Common;

namespace MDSHO.Infrastructure {

  /// <summary>
  /// Reserves screen strips for docked windows as Windows app bars (SHAppBarMessage), like the taskbar: Windows then keeps
  /// desktop icons and maximised programs out of them. A window must be on screen before it reserves (registering a window
  /// not yet shown made Windows ignore the strip in a test on Windows 10).
  /// </summary>
  public sealed class AppBarService : IAppBarService {
    private const uint ABM_NEW = 0x00;
    private const uint ABM_REMOVE = 0x01;
    private const uint ABM_QUERYPOS = 0x02;
    private const uint ABM_SETPOS = 0x03;
    private const uint ABE_LEFT = 0;
    private const uint ABE_RIGHT = 2;
    private const uint MONITOR_DEFAULTTONEAREST = 2;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT {
      public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct APPBARDATA {
      public int cbSize;
      public IntPtr hWnd;
      public uint uCallbackMessage;
      public uint uEdge;
      public RECT rc;
      public IntPtr lParam;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO {
      public int cbSize;
      public RECT rcMonitor;
      public RECT rcWork;
      public uint dwFlags;
    }

    [DllImport("shell32.dll")]
    private static extern UIntPtr SHAppBarMessage(uint message, ref APPBARDATA data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern uint RegisterWindowMessage(string name);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr window, out RECT rect);
    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO info);

    // The windows that hold a strip, with the strip last set for each (null between registering and the first set).
    private readonly Dictionary<IntPtr, RECT?> held = new Dictionary<IntPtr, RECT?>();
    private readonly uint callbackMessage = RegisterWindowMessage("MDSHO.AppBar");

    /// <inheritdoc/>
    public int CallbackMessage => (int)callbackMessage;

    /// <inheritdoc/>
    public ScreenRect? Reserve(IntPtr window, DockSide side) {
      if (window == IntPtr.Zero || side == DockSide.None) {
        return null;
      }
      if (!GetWindowRect(window, out RECT windowRect)) {
        return null;
      }
      MONITORINFO monitor = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
      if (!GetMonitorInfo(MonitorFromWindow(window, MONITOR_DEFAULTTONEAREST), ref monitor)) {
        return null;
      }
      APPBARDATA data = NewData(window);
      if (!held.ContainsKey(window)) {
        if (SHAppBarMessage(ABM_NEW, ref data) == UIntPtr.Zero) {
          return null;
        }
        held[window] = null;
      }
      int width = windowRect.Right - windowRect.Left;
      data.uEdge = side == DockSide.Left ? ABE_LEFT : ABE_RIGHT;
      // Propose the whole screen edge; Windows moves the strip past the taskbar and any other strip on that edge.
      data.rc = monitor.rcMonitor;
      if (side == DockSide.Left) {
        data.rc.Right = data.rc.Left + width;
      } else {
        data.rc.Left = data.rc.Right - width;
      }
      SHAppBarMessage(ABM_QUERYPOS, ref data);
      if (side == DockSide.Left) {
        data.rc.Right = data.rc.Left + width;
      } else {
        data.rc.Left = data.rc.Right - width;
      }
      // Setting the same strip again would only make Windows tell every strip holder again, over and over.
      RECT? last = held[window];
      if (!(last.HasValue && Same(last.Value, data.rc))) {
        SHAppBarMessage(ABM_SETPOS, ref data);
        held[window] = data.rc;
      }
      return new ScreenRect(data.rc.Left, data.rc.Top, data.rc.Right - data.rc.Left, data.rc.Bottom - data.rc.Top);
    }

    /// <inheritdoc/>
    public void Release(IntPtr window) {
      if (!held.Remove(window)) {
        return;
      }
      APPBARDATA data = NewData(window);
      SHAppBarMessage(ABM_REMOVE, ref data);
    }

    /// <inheritdoc/>
    public void ReleaseAll() {
      foreach (IntPtr window in new List<IntPtr>(held.Keys)) {
        Release(window);
      }
    }

    /// <inheritdoc/>
    public ScreenRect WindowRect(IntPtr window) {
      if (!GetWindowRect(window, out RECT r)) {
        return new ScreenRect(0, 0, 0, 0);
      }
      return new ScreenRect(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);
    }

    private APPBARDATA NewData(IntPtr window) {
      return new APPBARDATA { cbSize = Marshal.SizeOf<APPBARDATA>(), hWnd = window, uCallbackMessage = callbackMessage };
    }

    private static bool Same(RECT a, RECT b) {
      return a.Left == b.Left && a.Top == b.Top && a.Right == b.Right && a.Bottom == b.Bottom;
    }
  }
}
