using System;
using System.Collections.Generic;

namespace MDSHO.Common {

  /// <summary>
  /// The windows <see cref="PanelsToFront"/> works with, by window handle. The app implements it with its panels and
  /// Win32 (z-order, foreground window); tests use fakes.
  /// </summary>
  public interface IPanelWindows {
    /// <summary>The window in front (the one with the keyboard focus), or zero.</summary>
    IntPtr Foreground();
    /// <summary>True for a panel or any other MDSHO window (a note, Settings, ...).</summary>
    bool IsAppWindow(IntPtr window);
    /// <summary>The visible panels (main panels and clones), the top one of the z-order first.</summary>
    IList<IntPtr> Panels();
    /// <summary>The window right above this one in the z-order, or zero when it is the top one.</summary>
    IntPtr WindowAbove(IntPtr window);
    /// <summary>The windows shown on screen above this one (visible, not minimized or cloaked, no tool windows or desktop), nearest first.</summary>
    IList<IntPtr> WindowsAbove(IntPtr window);
    /// <summary>The windows shown on screen below this one, by the same rules, nearest first.</summary>
    IList<IntPtr> WindowsBelow(IntPtr window);
    /// <summary>True while the window exists.</summary>
    bool IsAlive(IntPtr window);
    /// <summary>True for an always-on-top window.</summary>
    bool IsTopmost(IntPtr window);
    /// <summary>Restores these panels, brings them above every other window in this order and gives the first one the keyboard focus.</summary>
    void Raise(IList<IntPtr> panels);
    /// <summary>Puts the panel right behind <paramref name="insertAfter"/> (zero = on top of the normal windows) without activating it.</summary>
    void PlaceBehind(IntPtr panel, IntPtr insertAfter);
    /// <summary>Brings the window to the front with the keyboard focus.</summary>
    void Activate(IntPtr window);
  }

  /// <summary>
  /// "Bring panels to front": one press of the shortcut (or the tray menu item) does what the screen suggests. When an app
  /// window covers a panel, every visible panel comes above the other windows and the first one gets the focus; the next
  /// press, Esc in a panel, or opening a button puts each panel back where it was in the z-order (the press and Esc also
  /// give the focus back to the window that was in front). When the panels are already in front without the shortcut (a
  /// panel has the focus, or no app window covers any panel), the press sends them back behind the app windows below
  /// them. Switching to another app ends "brought to front" without moving anything.
  /// </summary>
  public sealed class PanelsToFront {
    private readonly IPanelWindows windows;
    // Each raised panel and the window that was right above it, the top panel first.
    private List<(IntPtr Panel, IntPtr Above)> places = new List<(IntPtr, IntPtr)>();
    private IntPtr previousForeground;

    /// <summary>Works with these windows.</summary>
    public PanelsToFront(IPanelWindows windows) {
      this.windows = windows ?? throw new ArgumentNullException(nameof(windows));
    }

    /// <summary>True while the panels are in front because of the shortcut or the tray menu item.</summary>
    public bool IsInFront { get; private set; }

    /// <summary>The shortcut or the tray menu item: brings the panels to front, or sends them back when they are.</summary>
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

    // A window that counts when deciding what is in front: another app's normal window (not always on top).
    private bool IsOtherAppWindow(IntPtr window) {
      return !windows.IsAppWindow(window) && !windows.IsTopmost(window);
    }

    // In front without the shortcut: a panel has the focus, or no other app's normal window is above any panel.
    // (A focused note or Settings window decides nothing by itself: the panels may still be covered.)
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

    // Each panel (top first) goes right behind the nearest other app's normal window below it; a panel whose window is
    // the same as the panel before it goes behind that panel, so the panels keep their order. The window under the top
    // panel that moves gets the focus. A panel with no such window below stays where it is.
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

    /// <summary>Esc in a panel: sends the panels back when they were brought to front and nothing is being typed. True = handled.</summary>
    public bool HandleEscape(bool isEditing) {
      if (!IsInFront || isEditing) {
        return false;
      }
      SendBack(giveFocusBack: true);
      return true;
    }

    /// <summary>A button opened something: the panels go back, and the focus stays with what opened.</summary>
    public void ButtonOpened() {
      if (IsInFront) {
        SendBack(giveFocusBack: false);
      }
    }

    /// <summary>The user switched to another app: the panels stay where they are now and the next press brings them up again.</summary>
    public void AppDeactivated() {
      IsInFront = false;
      places = new List<(IntPtr, IntPtr)>();
      previousForeground = IntPtr.Zero;
    }

    private void BringToFront(IList<IntPtr> panels) {
      IntPtr foreground = windows.Foreground();
      // An MDSHO window in front (a note, Settings) is not activated again on the way back.
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
      // Top panel first, so a panel that was right below another one can go back behind it.
      foreach ((IntPtr panel, IntPtr above) in back) {
        if (windows.IsAlive(panel)) {
          windows.PlaceBehind(panel, InsertAfter(above));
        }
      }
      if (giveFocusBack && focus != IntPtr.Zero && windows.IsAlive(focus)) {
        windows.Activate(focus);
      }
    }

    // Behind the window that was above the panel; on top of the normal windows when that window is gone or always on
    // top (placing a normal window behind an always-on-top one would make it always on top too).
    private IntPtr InsertAfter(IntPtr above) {
      return above != IntPtr.Zero && windows.IsAlive(above) && !windows.IsTopmost(above) ? above : IntPtr.Zero;
    }
  }
}
