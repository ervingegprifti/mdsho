using System;
using System.Runtime.InteropServices;
using System.Windows;

namespace MDSHO.Helpers {

  /// <summary>
  /// Shows a modal dialog and, when it has no owner window (a message from the tray), gives the focus back afterwards to
  /// the window that was active before it opened. With an owner, Windows already gives the focus back to the owner.
  /// </summary>
  public static class DialogFocus {

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    /// <summary>The active window now. Tests replace it.</summary>
    public static Func<IntPtr> Foreground = GetForegroundWindow;

    /// <summary>True when the window is still there and shown. Tests replace it.</summary>
    public static Func<IntPtr, bool> IsVisible = IsWindowVisible;

    /// <summary>Makes the window the active one. Tests replace it.</summary>
    public static Action<IntPtr> Activate = h => SetForegroundWindow(h);

    /// <summary>Shows <paramref name="dialog"/> modally, then gives the focus back (see <see cref="GiveBack"/>).</summary>
    public static bool? ShowDialog(Window dialog) {
      IntPtr before = Foreground();
      try {
        return dialog.ShowDialog();
      } finally {
        GiveBack(before, dialog);
      }
    }

    /// <summary>Activates <paramref name="before"/> again when the dialog had no owner and that window is still shown.</summary>
    public static void GiveBack(IntPtr before, Window dialog) {
      if (dialog.Owner != null || before == IntPtr.Zero || !IsVisible(before)) {
        return;
      }
      Activate(before);
    }
  }
}
