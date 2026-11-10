using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace MDSHO.Helpers {

  /// <summary>
  /// Rounded corners for windows with a custom title bar (WindowStyle None), which lose the automatic Windows 11
  /// rounding. On Windows 11 the window asks DWM for round corners; on Windows 10 nothing happens (square, like any window there).
  /// </summary>
  public static class WindowCorners {
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWCP_ROUND = 2;

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    /// <summary>Called with the window handle once it exists. Tests replace it to record the request.</summary>
    public static Action<IntPtr> RequestRounded = RequestRoundedFromDwm;
    /// <summary>True on Windows 11. Tests replace it to try both systems.</summary>
    public static Func<bool> IsWindows11 = () => IsWindows11Version(Environment.OSVersion.Version);
    /// <summary>Sets a DWM window attribute and returns its HRESULT. Tests replace it so no real call is made.</summary>
    public static Func<IntPtr, int, int, int> SetDwmAttribute = (hwnd, attribute, value) => DwmSetWindowAttribute(hwnd, attribute, ref value, sizeof(int));

    /// <summary>True for Windows 11 (Windows 10.0 build 22000 or later).</summary>
    public static bool IsWindows11Version(Version version) {
      return version.Major > 10 || (version.Major == 10 && version.Build >= 22000);
    }

    /// <summary>Asks for rounded corners as soon as the window's handle exists (SourceInitialized).</summary>
    public static void UseRoundedCorners(Window window) {
      window.SourceInitialized += (sender, e) => RequestRounded(new WindowInteropHelper(window).Handle);
    }

    /// <summary>On Windows 11 asks DWM to round the window's corners (DWM keeps a maximised window square itself). Never throws.</summary>
    public static void RequestRoundedFromDwm(IntPtr hwnd) {
      try {
        if (IsWindows11()) {
          // The returned HRESULT is ignored: without rounding the window just stays square.
          SetDwmAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, DWMWCP_ROUND);
        }
      } catch (DllNotFoundException) {
      } catch (EntryPointNotFoundException) {
      }
    }
  }

}
