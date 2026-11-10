using System.Windows;
using MDSHO.Helpers;

namespace MDSHO {

  /// <summary>Positions secondary windows relative to a parent window or the primary screen.</summary>
  public static class WindowPositioning {
    #region WINDOW POSITION
    /// <summary>Opens the window next to its parent (docked to the right of it), or centred on the primary screen when there is no parent.</summary>
    public static void PositionRelativeToParent(this Window window, Window parent) {
      if (parent == null) {
        // The call is coming from the taskbar context menu
        CenterToPrimaryScreen(window);
      } else {
        // The call is coming from one of the windows
        DockRightToParentWindow(window, parent);
      }
    }
    /// <summary>Centres the window on the primary screen.</summary>
    public static void CenterToPrimaryScreen(Window window) {
      MDSHO.Common.ScreenInfo screen = AppServices.Current.Screens.PrimaryScreen();
      MDSHO.Common.ScreenPoint p = MDSHO.Common.ScreenGeometry.CenterOnScreen(screen, window.Width, window.Height);
      window.Left = p.X;
      window.Top = p.Y;
    }
    /// <summary>Puts the window right beside the parent, top aligned.</summary>
    public static void DockRightToParentWindow(this Window window, Window parent) {
      // The call is coming from one of the windows
      window.Left = parent.Left + parent.Width;
      window.Top = parent.Top;
    }
    #endregion WINDOW POSITION
  }

}