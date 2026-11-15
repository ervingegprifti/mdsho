using System;
using System.Collections.Generic;

namespace MDSHO.Common {

  /// <summary>The windows that <see cref="PanelsToFront"/> works with, by handle. Tests use fakes.</summary>
  public interface IPanelWindows {
    /// <summary>The window with the keyboard focus, or zero.</summary>
    IntPtr Foreground();
    /// <summary>True for any MDSHO window.</summary>
    bool IsAppWindow(IntPtr window);
    /// <summary>The visible panels and clones, top one first.</summary>
    IList<IntPtr> Panels();
    /// <summary>The window right above this one, or zero.</summary>
    IntPtr WindowAbove(IntPtr window);
    /// <summary>The windows shown above this one, nearest first.</summary>
    IList<IntPtr> WindowsAbove(IntPtr window);
    /// <summary>The windows shown below this one, nearest first.</summary>
    IList<IntPtr> WindowsBelow(IntPtr window);
    /// <summary>True while the window exists.</summary>
    bool IsAlive(IntPtr window);
    /// <summary>True for an always-on-top window.</summary>
    bool IsTopmost(IntPtr window);
    /// <summary>Brings the panels above every window, in this order, and focuses the first.</summary>
    void Raise(IList<IntPtr> panels);
    /// <summary>Puts the panel behind <paramref name="insertAfter"/> without activating it. Zero means on top.</summary>
    void PlaceBehind(IntPtr panel, IntPtr insertAfter);
    /// <summary>Brings the window to the front with the focus.</summary>
    void Activate(IntPtr window);
  }

  /// <summary>
  /// "Bring panels to front". A press brings covered panels up, or sends them back if they are already in front.
  /// </summary>
  public sealed class PanelsToFront {
    private readonly IPanelWindows windows;
    // Each raised panel and the window that was right above it, top panel first.
    private List<(IntPtr Panel, IntPtr Above)> places = new List<(IntPtr, IntPtr)>();
    private IntPtr previousForeground;

    public PanelsToFront(IPanelWindows windows) {
      this.windows = windows ?? throw new ArgumentNullException(nameof(windows));
    }

    /// <summary>True while the panels are in front because of the shortcut or the tray item.</summary>
    public bool IsInFront { get; private set; }

    /// <summary>The shortcut or the tray item: brings the panels to front, or sends them back.</summary>
    public void Toggle() {
      if (IsInFront) {
        SendBack(giveFocusBack: true);
        return;
      }
      IList<IntPtr> panels = windows.Panels();
      if (panels.Count == 0) {
        return;
      }
      if (AlreadyInFront(panels)) {
        SendBehindAppWindows(panels);
      } else {
        BringToFront(panels);
      }
    }

    // Only other apps' normal windows count, not MDSHO or always-on-top windows.
    private bool IsOtherAppWindow(IntPtr window) {
      return !windows.IsAppWindow(window) && !windows.IsTopmost(window);
    }

    // A focused panel, or no app window above any panel. A focused note alone doesn't count.
    private bool AlreadyInFront(IList<IntPtr> panels) {
      IntPtr foreground = windows.Foreground();
      if (foreground != IntPtr.Zero && panels.Contains(foreground)) {
        return true;
      }
      foreach (IntPtr panel in panels) {
        foreach (IntPtr above in windows.WindowsAbove(panel)) {
          if (IsOtherAppWindow(above)) {
            return false;
          }
        }
      }
      return true;
    }

    // Each panel goes behind the nearest app window below it. Panels with the same target stack in order.
    private void SendBehindAppWindows(IList<IntPtr> panels) {
      IntPtr focus = IntPtr.Zero;
      IntPtr lastTarget = IntPtr.Zero;
      IntPtr lastPanel = IntPtr.Zero;
      foreach (IntPtr panel in panels) {
        IntPtr target = IntPtr.Zero;
        foreach (IntPtr below in windows.WindowsBelow(panel)) {
          if (IsOtherAppWindow(below)) {
            target = below;
            break;
          }
        }
        if (target == IntPtr.Zero) {
          continue;
        }
        windows.PlaceBehind(panel, target == lastTarget ? lastPanel : target);
        lastTarget = target;
        lastPanel = panel;
        if (focus == IntPtr.Zero) {
          focus = target;
        }
      }
      if (focus != IntPtr.Zero) {
        windows.Activate(focus);
      }
    }

    /// <summary>Esc in a panel: sends the panels back. True when handled.</summary>
    public bool HandleEscape(bool isEditing) {
      if (!IsInFront || isEditing) {
        return false;
      }
      SendBack(giveFocusBack: true);
      return true;
    }

    /// <summary>A button opened something: sends the panels back and leaves the focus there.</summary>
    public void ButtonOpened() {
      if (IsInFront) {
        SendBack(giveFocusBack: false);
      }
    }

    /// <summary>Another app was activated: forgets "in front" without moving anything.</summary>
    public void AppDeactivated() {
      IsInFront = false;
      places = new List<(IntPtr, IntPtr)>();
      previousForeground = IntPtr.Zero;
    }

    private void BringToFront(IList<IntPtr> panels) {
      IntPtr foreground = windows.Foreground();
      // Don't give the focus back to an MDSHO window later.
      previousForeground = foreground != IntPtr.Zero && !windows.IsAppWindow(foreground) ? foreground : IntPtr.Zero;
      places = new List<(IntPtr, IntPtr)>();
      foreach (IntPtr panel in panels) {
        places.Add((panel, windows.WindowAbove(panel)));
      }
      windows.Raise(panels);
      IsInFront = true;
    }

    private void SendBack(bool giveFocusBack) {
      List<(IntPtr Panel, IntPtr Above)> back = places;
      IntPtr focus = previousForeground;
      AppDeactivated();
      // Top panel first, so a panel can go back behind the one that was above it.
      foreach ((IntPtr panel, IntPtr above) in back) {
        if (windows.IsAlive(panel)) {
          windows.PlaceBehind(panel, InsertAfter(above));
        }
      }
      if (giveFocusBack && focus != IntPtr.Zero && windows.IsAlive(focus)) {
        windows.Activate(focus);
      }
    }

    // Never behind an always-on-top window: that would make the panel always on top too.
    private IntPtr InsertAfter(IntPtr above) {
      return above != IntPtr.Zero && windows.IsAlive(above) && !windows.IsTopmost(above) ? above : IntPtr.Zero;
    }
  }
}
