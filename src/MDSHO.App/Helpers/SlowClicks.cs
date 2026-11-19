using System;
using System.Runtime.InteropServices;
using System.Windows;
using MDSHO.Common;

namespace MDSHO.Helpers {

  /// <summary>Makes a <see cref="SlowClickRename"/> with the system double-click time and drag distance.</summary>
  internal static class SlowClicks {

    [DllImport("user32.dll")]
    private static extern uint GetDoubleClickTime();

    /// <summary>A slow-click detector on the UI thread that calls <paramref name="rename"/> with the clicked item.</summary>
    public static SlowClickRename Create(Action<object> rename) {
      int doubleClickTime = (int)GetDoubleClickTime();
      if (doubleClickTime <= 0) { doubleClickTime = 500; }
      return new SlowClickRename(() => Environment.TickCount64, new DispatcherOneShotTimer(), doubleClickTime,
        SystemParameters.MinimumHorizontalDragDistance, SystemParameters.MinimumVerticalDragDistance, rename);
    }
  }
}
