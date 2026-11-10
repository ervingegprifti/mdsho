using System;
using MDSHO.Data;
using MDSHO.ViewModels;

namespace MDSHO.Helpers {

  /// <summary>Keeps saved window positions reachable on the connected monitors.</summary>
  public static class ScreenInfoProvider {

    /// <summary>Restore: if the saved window (DIPs) is not reachable on any current screen, move it (named screen, else primary) and save it.</summary>
    public static void EnsureWindowOnScreen(DesktopWindowViewModel vm) {
      System.Collections.Generic.IList<MDSHO.Common.ScreenInfo> screens = AppServices.Current.Screens.AllScreens();
      MDSHO.Common.ScreenRect saved = new MDSHO.Common.ScreenRect(vm.Left, vm.Top, vm.Width, vm.Height);
      MDSHO.Common.ScreenRect fixedRect = MDSHO.Common.ScreenGeometry.EnsureOnScreen(saved, vm.ScreenDeviceName, screens);
      if (fixedRect == saved) {
        return;
      }
      vm.Left = (long)Math.Round(fixedRect.Left);
      vm.Top = (long)Math.Round(fixedRect.Top);
      vm.Width = (long)Math.Round(fixedRect.Width);
      vm.Height = (long)Math.Round(fixedRect.Height);
      string deviceName = vm.ScreenDeviceName;
      MDSHO.Common.ScreenPoint center = new MDSHO.Common.ScreenPoint(fixedRect.Left + fixedRect.Width / 2, fixedRect.Top + fixedRect.Height / 2);
      foreach (MDSHO.Common.ScreenInfo s in screens) {
        if (s.WorkingAreaDip.Contains(center)) { deviceName = s.DeviceName; break; }
      }
      vm.ScreenDeviceName = deviceName;
      if (vm.IsClone) {
        DataStore.Clones.UpdateLeftTopScreenDeviceName(vm.ID, vm.Left, vm.Top, deviceName);
        DataStore.Clones.UpdateWidthHeightScreenDeviceName(vm.ID, vm.Width, vm.Height, deviceName);
      } else {
        DataStore.Windows.UpdateLeftTopScreenDeviceName(vm.ID, vm.Left, vm.Top, deviceName);
        DataStore.Windows.UpdateWidthHeightScreenDeviceName(vm.ID, vm.Width, vm.Height, deviceName);
      }
    }

  }

}
