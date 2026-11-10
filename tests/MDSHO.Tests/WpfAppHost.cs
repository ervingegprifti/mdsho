using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;


namespace MDSHO.Tests {

  /// <summary>
  /// One long-lived STA thread that owns the WPF <see cref="Application"/> (there can be only one per process), so several
  /// tests can build the real views. <see cref="Run"/> executes a test body on that thread and rethrows its failure.
  /// </summary>
  internal static class WpfAppHost {
    private static readonly object gate = new object();
    private static Dispatcher dispatcher;

    private static Dispatcher Start() {
      lock (gate) {
        if (dispatcher != null) {
          return dispatcher;
        }
        using (ManualResetEventSlim ready = new ManualResetEventSlim()) {
          Exception startFailure = null;
          Thread thread = new Thread(() => {
            try {
              // Relative pack URIs (e.g. resources/logo.ico) resolve against the entry assembly, which is the test host here.
              typeof(Application).GetField("_resourceAssembly", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                .SetValue(null, typeof(App).Assembly);
              // The real startup (single instance lock, real app data, real log) must never run in the tests.
              App.StartupEnabled = false;
              Application app = Application.Current ?? new App();
              app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
              ((App)app).InitializeComponent();
              dispatcher = Dispatcher.CurrentDispatcher;
            } catch (Exception ex) {
              startFailure = ex;
            } finally {
              ready.Set();
            }
            if (startFailure == null) {
              Dispatcher.Run();
            }
          });
          thread.IsBackground = true;
          thread.SetApartmentState(ApartmentState.STA);
          thread.Start();
          ready.Wait();
          if (startFailure != null) {
            throw new Exception("WPF application setup failed: " + startFailure, startFailure);
          }
          return dispatcher;
        }
      }
    }

    /// <summary>Runs <paramref name="body"/> on the WPF thread and waits; an exception inside is rethrown here.</summary>
    public static void Run(Action body) {
      Exception failure = null;
      Start().Invoke(() => {
        try { body(); } catch (Exception ex) { failure = ex; }
      });
      if (failure != null) {
        ExceptionDispatchInfo.Capture(failure).Throw();
      }
    }

    /// <summary>Lets queued bindings and layout run.</summary>
    public static void Pump() {
      Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
      Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.Background);
    }

    public static IEnumerable<T> VisualDescendants<T>(DependencyObject root) where T : DependencyObject {
      int count = VisualTreeHelper.GetChildrenCount(root);
      for (int i = 0; i < count; i++) {
        DependencyObject child = VisualTreeHelper.GetChild(root, i);
        if (child is T match) {
          yield return match;
        }
        foreach (T nested in VisualDescendants<T>(child)) {
          yield return nested;
        }
      }
    }
  }
}
