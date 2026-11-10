using System.Windows;
using System.Windows.Interop;
using MDSHO.ViewModels;

namespace MDSHO.Helpers {

  /// <summary>Finds the open window (view) of a view model and the screen a window is on.</summary>
  public static class WindowLookup {

    /// <summary>The open window whose DataContext is the given view model, or null when none is open.</summary>
    public static Window GetViewFromViewModel(DesktopWindowViewModel desktopWindow) {
      foreach (Window window in ((App)Application.Current).Windows) {
        if (window.DataContext is DesktopWindowViewModel) {
          if ((DesktopWindowViewModel)window.DataContext == desktopWindow) {
            return window;
          }
        }
      }
      return null;
    }

    /// <summary>The screen the window is mostly on.</summary>
    public static MDSHO.Common.ScreenInfo GetScreenFromWindow(Window window) {
      WindowInteropHelper windowInteropHelper = new WindowInteropHelper(window);
      return AppServices.Current.Screens.ScreenFromHandle(windowInteropHelper.Handle);
    }

    /// <summary>The screen the view model's open window is on, or null when its window is not open.</summary>
    public static MDSHO.Common.ScreenInfo GetScreenFromViewModel(DesktopWindowViewModel desktopWindow) {
      Window window = GetViewFromViewModel(desktopWindow);
      if (window != null) {
        return GetScreenFromWindow(window);
      }
      return null;
    }

  }

}
