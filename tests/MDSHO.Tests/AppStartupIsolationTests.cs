using System.Reflection;
using System.Windows;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// WPF raises Application.OnStartup as soon as the dispatcher runs, even without Application.Run. The test host creates
  /// the real App, so without a switch the real startup would run inside the tests: take the single instance lock, rename
  /// and upgrade the real app data, open the real log and replace the test services. It only stayed harmless on a PC where
  /// MDSHO was running and held the lock.
  /// </summary>
  public class AppStartupIsolationTests {
    [Fact]
    public void The_test_host_never_runs_the_real_app_startup() {
      object mutex = null;
      object host = null;
      WpfAppHost.Run(() => { });
      WpfAppHost.Run(() => {
        App app = (App)Application.Current;
        mutex = typeof(App).GetField("singleInstanceMutex", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(app);
        host = typeof(App).GetField("host", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(app);
      });
      Assert.False(App.StartupEnabled);
      Assert.Null(mutex);
      Assert.Null(host);
    }

    [Fact]
    public void The_real_app_runs_its_startup_by_default() {
      // Only the test host switches it off (WpfAppHost); the default must stay on for MDSHO.exe.
      PropertyInfo p = typeof(App).GetProperty(nameof(App.StartupEnabled), BindingFlags.Public | BindingFlags.Static);
      Assert.NotNull(p);
      string source = System.IO.File.ReadAllText(System.IO.Path.Combine(ThirdPartyNoticesTests.RepoRoot(), "src", "MDSHO.App", "App.xaml.cs"));
      Assert.Contains("public static bool StartupEnabled { get; set; } = true;", source);
    }
  }
}
