using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Input;

namespace MDSHO.Tests {

  /// <summary>Raises left mouse presses and releases on a window title (no real mouse), for the title rename tests.</summary>
  internal static class TitleMouse {

    [DllImport("user32.dll")]
    private static extern uint GetDoubleClickTime();

    /// <summary>The system double-click time in milliseconds.</summary>
    public static int DoubleClickTime => (int)GetDoubleClickTime();

    private static void Raise(UIElement element, RoutedEvent routedEvent, int clickCount) {
      MouseButtonEventArgs args = new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = routedEvent };
      // ClickCount has an internal setter; WPF sets it from the real mouse.
      typeof(MouseButtonEventArgs).GetProperty(nameof(MouseButtonEventArgs.ClickCount)).GetSetMethod(true).Invoke(args, new object[] { clickCount });
      element.RaiseEvent(args);
      WpfAppHost.Pump();
    }

    /// <summary>A press and release; <paramref name="clickCount"/> 2 is the second click of a double click.</summary>
    public static void Click(UIElement element, int clickCount = 1) {
      Raise(element, UIElement.MouseLeftButtonDownEvent, clickCount);
      Raise(element, UIElement.MouseLeftButtonUpEvent, clickCount);
    }

    /// <summary>A fast double click: two clicks, the second with click count 2.</summary>
    public static void DoubleClick(UIElement element) {
      Click(element, 1);
      Click(element, 2);
    }

    /// <summary>Waits a while, pumping the UI thread so timers run.</summary>
    public static void Wait(int ms) {
      Stopwatch watch = Stopwatch.StartNew();
      while (watch.ElapsedMilliseconds < ms) {
        WpfAppHost.Pump();
        Thread.Sleep(10);
      }
    }

    /// <summary>Waits up to <paramref name="ms"/>, pumping, until <paramref name="done"/>.</summary>
    public static bool WaitFor(Func<bool> done, int ms) {
      Stopwatch watch = Stopwatch.StartNew();
      while (watch.ElapsedMilliseconds < ms) {
        WpfAppHost.Pump();
        if (done()) { return true; }
        Thread.Sleep(10);
      }
      return done();
    }
  }
}
