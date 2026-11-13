using System;

namespace MDSHO.Common {

  /// <summary>The screen side a window is docked to.</summary>
  public enum DockSide {
    /// <summary>Floating: not docked.</summary>
    None,
    /// <summary>Docked to the left side of a screen.</summary>
    Left,
    /// <summary>Docked to the right side of a screen.</summary>
    Right
  }

  /// <summary>
  /// The strip of the screen one docked window holds (with "Reserve screen space when docked"), the way the taskbar holds
  /// its own: Windows keeps desktop icons and maximised programs out of it. Each update reserves the strip again, so it
  /// follows the window's width; a floating window, or the setting off, gives it back.
  /// </summary>
  public sealed class DockedSpace {
    // A window counts as still on its strip when its left and right edges are this close to the strip's (pixels).
    private const double Tolerance = 2;
    private readonly IAppBarService bars;
    private readonly IntPtr window;

    /// <summary>Creates the strip holder for a window (by its handle). Nothing is reserved yet.</summary>
    public DockedSpace(IAppBarService bars, IntPtr window) {
      this.bars = bars;
      this.window = window;
    }

    /// <summary>The side whose strip the window holds, or None.</summary>
    public DockSide HeldSide { get; private set; }

    /// <summary>The strip Windows gave the window, in physical pixels, or null when it holds none.</summary>
    public ScreenRect? Strip { get; private set; }

    /// <summary>
    /// Reserves the strip of <paramref name="geometrySide"/> when <paramref name="enabled"/>, or gives it back. The side
    /// comes from where the window is; a window that hasn't moved off the strip it holds keeps that side, since another
    /// docked window beside it moves the work area's edge away from it. Returns the side now held.
    /// </summary>
    public DockSide Update(bool enabled, DockSide geometrySide) {
      DockSide side = geometrySide;
      if (side == DockSide.None && HeldSide != DockSide.None && Strip.HasValue && OnStrip(bars.WindowRect(window), Strip.Value)) {
        side = HeldSide;
      }
      if (!enabled || side == DockSide.None) {
        Release();
        return DockSide.None;
      }
      if (HeldSide != DockSide.None && HeldSide != side) {
        Release();
      }
      ScreenRect? granted = bars.Reserve(window, side);
      if (granted == null) {
        HeldSide = DockSide.None;
        Strip = null;
        return DockSide.None;
      }
      HeldSide = side;
      Strip = granted;
      return side;
    }

    /// <summary>Gives the strip back, if the window holds one.</summary>
    public void Release() {
      if (HeldSide == DockSide.None) {
        return;
      }
      bars.Release(window);
      HeldSide = DockSide.None;
      Strip = null;
    }

    private static bool OnStrip(ScreenRect windowRect, ScreenRect strip) {
      return Math.Abs(windowRect.Left - strip.Left) <= Tolerance && Math.Abs(windowRect.Right - strip.Right) <= Tolerance;
    }
  }
}
