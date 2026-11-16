using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using MDSHO.Common;
using MDSHO.Infrastructure;
using Serilog.Events;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>About shows how MDSHO was installed in brackets after the version, from the package signature.</summary>
  public class AboutInstallKindTests {

    /// <summary>An install kind that cannot be read.</summary>
    private sealed class ThrowingStoreRating : IStoreRating {
      public string InstallKind => throw new InvalidOperationException("no package info");
      public Task<StoreRatingResult> RequestRateAndReviewAsync() => Task.FromResult(StoreRatingResult.Failed);
    }

    private static StoreActions Actions(IStoreRating rating) {
      return new StoreActions(new ReviewPromptTests.StoreHost(), rating);
    }

    private static StoreActions Actions(string kind) {
      return Actions(new ReviewPromptTests.FakeStoreRating { InstallKind = kind });
    }

    [Theory]
    [InlineData("Store", "Microsoft Store")]
    [InlineData("Enterprise", "Sideload")]
    [InlineData("Developer", "Sideload")]
    [InlineData("None", "Developer build")]
    [InlineData("Not packaged", "Not packaged")]
    public void Each_install_kind_has_its_text(string kind, string text) {
      Assert.Equal(text, Actions(kind).InstallKindText);
    }

    [Fact]
    public void Without_a_rating_service_it_is_not_packaged() {
      Assert.Equal("Not packaged", new StoreActions(new ReviewPromptTests.StoreHost()).InstallKindText);
    }

    [Theory]
    [InlineData("Store", "1.14.1.0 (Microsoft Store)")]
    [InlineData("Enterprise", "1.14.1.0 (Sideload)")]
    [InlineData("Developer", "1.14.1.0 (Sideload)")]
    [InlineData("None", "1.14.1.0 (Developer build)")]
    [InlineData("Not packaged", "1.14.1.0 (Not packaged)")]
    public void The_version_gets_the_install_kind_in_brackets(string kind, string expected) {
      Assert.Equal(expected, Actions(kind).VersionWithInstallKind("1.14.1.0"));
    }

    [Fact]
    public void When_detection_fails_the_version_shows_alone_and_a_warning_is_logged() {
      using (LogCapture log = new LogCapture()) {
        Assert.Equal("1.14.1.0", Actions(new ThrowingStoreRating()).VersionWithInstallKind("1.14.1.0"));
        LogEvent warning = Assert.Single(log.Events, e => e.Level == LogEventLevel.Warning);
        Assert.IsType<InvalidOperationException>(warning.Exception);
      }
    }

    /// <summary>Shows a real About window over <paramref name="actions"/> off screen and returns the Version text.</summary>
    private static string AboutVersionText(StoreActions actions) {
      string text = null;
      WpfAppHost.Run(() => {
        AboutWindow window = new AboutWindow(actions, new ShellLauncher(psi => { })) {
          WindowStartupLocation = WindowStartupLocation.Manual,
          Left = -10000,
          Top = -10000,
          ShowActivated = false,
          ShowInTaskbar = false
        };
        window.Show();
        try {
          WpfAppHost.Pump();
          text = (string)((Label)window.FindName("LabelVersion")).Content;
        } finally {
          window.Close();
        }
      });
      return text;
    }

    private static string AppVersion => typeof(AboutWindow).Assembly.GetName().Version.ToString();

    [Theory]
    [InlineData("Store", "Microsoft Store")]
    [InlineData("Developer", "Sideload")]
    [InlineData("None", "Developer build")]
    [InlineData("Not packaged", "Not packaged")]
    public void About_shows_the_unchanged_version_then_the_install_kind(string kind, string text) {
      Assert.Equal(AppVersion + " (" + text + ")", AboutVersionText(Actions(kind)));
    }

    [Fact]
    public void About_shows_the_version_alone_when_detection_fails() {
      using (new LogCapture()) {
        Assert.Equal(AppVersion, AboutVersionText(Actions(new ThrowingStoreRating())));
      }
    }
  }
}
