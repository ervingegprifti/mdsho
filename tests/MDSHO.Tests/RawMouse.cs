using System;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace MDSHO.Tests {

  /// <summary>
  /// Mouse input as Windows delivers it to WPF: raw reports through the InputManager, so the MouseDevice counts the
  /// clicks itself (with its own double-click rules) and the controls' own handlers run, without moving the real mouse.
  /// The report types are internal to WPF, so they are made by reflection.
  /// </summary>
  internal static class RawMouse {

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern uint GetDoubleClickTime();

    /// <summary>The Windows double-click time in milliseconds.</summary>
    public static int DoubleClickTime => (int)GetDoubleClickTime();

    /// <summary>Clicks the left button <paramref name="clicks"/> times at a point of <paramref name="target"/>, <paramref name="gap"/> ms apart.</summary>
    public static void Clicks(FrameworkElement target, Point at, int clicks, int gap) {
      PresentationSource source = PresentationSource.FromVisual(target);
      (int x, int y) = ToDevice(target, source, at);
      // The mouse remembers the last click across tests: start well after it, so no run continues the one before.
      int time = Math.Max(Environment.TickCount, lastTime + 10 * DoubleClickTime);
      Report(source, "Activate, AbsoluteMove", x, y, time);
      for (int i = 0; i < clicks; i++) {
        time += i == 0 ? 10 : gap;
        Report(source, "Button1Press", x, y, time);
        Report(source, "Button1Release", x, y, time + 20);
        WpfAppHost.Pump();
      }
      lastTime = time + 20;
    }

    private static int lastTime;

    /// <summary>
    /// Presses the left button at <paramref name="from"/>, moves to <paramref name="to"/> with it held, and lets go there.
    /// </summary>
    public static void Drag(FrameworkElement target, Point from, Point to) {
      PresentationSource source = PresentationSource.FromVisual(target);
      (int x, int y) = ToDevice(target, source, from);
      (int x2, int y2) = ToDevice(target, source, to);
      int time = Math.Max(Environment.TickCount, lastTime + 10 * DoubleClickTime);
      Report(source, "Activate, AbsoluteMove", x, y, time);
      Report(source, "Button1Press", x, y, time + 10);
      WpfAppHost.Pump();
      // In steps, as a real mouse moves.
      for (int i = 1; i <= 4; i++) {
        Report(source, "AbsoluteMove", x + (x2 - x) * i / 4, y + (y2 - y) * i / 4, time + 10 + i * 20);
        WpfAppHost.Pump();
      }
      Report(source, "Button1Release", x2, y2, time + 200);
      WpfAppHost.Pump();
      lastTime = time + 200;
    }

    /// <summary>Runs <paramref name="action"/> with Shift held for this thread (the real keyboard is untouched).</summary>
    public static void WithShift(Action action) {
      byte[] keys = new byte[256];
      GetKeyboardState(keys);
      byte[] saved = (byte[])keys.Clone();
      keys[0x10] = keys[0xA0] = 0x80;
      SetKeyboardState(keys);
      try {
        action();
      } finally {
        SetKeyboardState(saved);
      }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool GetKeyboardState(byte[] keys);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool SetKeyboardState(byte[] keys);

    private static (int X, int Y) ToDevice(FrameworkElement target, PresentationSource source, Point at) {
      DpiScale dpi = VisualTreeHelper.GetDpi(target);
      Point p = target.TranslatePoint(at, (UIElement)source.RootVisual);
      return ((int)Math.Round(p.X * dpi.DpiScaleX), (int)Math.Round(p.Y * dpi.DpiScaleY));
    }

    private static void Report(PresentationSource source, string actions, int x, int y, int time) {
      Assembly core = typeof(InputManager).Assembly;
      object act = Enum.Parse(core.GetType("System.Windows.Input.RawMouseActions"), actions);
      object report = core.GetType("System.Windows.Input.RawMouseInputReport").GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)[0]
        .Invoke(new object[] { InputMode.Foreground, time, source, act, x, y, 0, IntPtr.Zero });
      InputEventArgs args = (InputEventArgs)core.GetType("System.Windows.Input.InputReportEventArgs").GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)[0]
        .Invoke(new object[] { Mouse.PrimaryDevice, report });
      args.RoutedEvent = (RoutedEvent)typeof(InputManager).GetField("PreviewInputReportEvent", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public).GetValue(null);
      InputManager.Current.ProcessInput(args);
    }
  }
}
