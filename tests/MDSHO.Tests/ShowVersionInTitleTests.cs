using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using MDSHO.ViewModels;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// "Show version in title" (a panel's Settings, shared with its clones, on by default): the panel's title bar shows the
  /// MDSHO version after the title, for example "MDSHO v1.5.1".
  /// </summary>
  public class ShowVersionInTitleTests {

    [Fact]
    public void The_version_text_has_three_parts_after_a_v() {
      string version = DesktopWindowViewModel.VersionText;
      if (version.Length > 0) {
        Assert.Matches(@"^v\d+\.\d+\.\d+$", version);
      }
    }

    [Fact]
    public void The_title_shows_the_version_while_the_setting_is_on() {
      using (TempDb db = new TempDb()) {
        DesktopWindowViewModel w = new AppViewModel().AddWindow("TEST", 100, 100);
        w.Appearance.Title = "Work";
        Assert.True(w.Appearance.ShowVersionInTitle);
        Assert.StartsWith("Work", w.TitleDisplay);
        if (DesktopWindowViewModel.VersionText.Length > 0) {
          Assert.EndsWith(" " + DesktopWindowViewModel.VersionText, w.TitleDisplay);
        }
        w.Appearance.ShowVersionInTitle = false;
        Assert.StartsWith("Work", w.TitleDisplay);
        if (DesktopWindowViewModel.VersionText.Length > 0) {
          Assert.DoesNotContain(DesktopWindowViewModel.VersionText, w.TitleDisplay);
        }
      }
    }

    [Fact]
    public void Turning_it_off_refreshes_the_title_and_is_saved_once_for_the_panel_and_its_clones() {
      using (TempDb db = new TempDb()) {
        AppViewModel app = new AppViewModel();
        DesktopWindowViewModel w = app.AddWindow("TEST", 100, 100);
        w.CloneWindow("TEST", 5, 5);
        DesktopWindowViewModel clone = w.Clones.Single();
        Assert.Same(w.Appearance, clone.Appearance);
        List<string> raised = new List<string>();
        w.PropertyChanged += (s, e) => raised.Add(e.PropertyName);
        clone.Appearance.ShowVersionInTitle = false;
        Assert.Contains(nameof(w.TitleDisplay), raised);
        Assert.Equal(0, db.Scalar("SELECT ShowVersionInTitle FROM MDSHOWindows WHERE ID=" + w.ID));
        // Read back as it starts next time.
        DesktopWindowViewModel reloaded = ViewModelFactory.WindowToViewModel(app, MDSHO.Data.DataStore.Windows.Get(w.ID));
        Assert.False(reloaded.Appearance.ShowVersionInTitle);
      }
    }

    [Fact]
    public void Settings_has_the_checkbox_below_reserve_screen_space_and_closing_without_Apply_puts_it_back() {
      using (TempDb db = new TempDb()) {
        WpfAppHost.Run(() => {
          DesktopWindowViewModel w = new AppViewModel().AddWindow("TEST", 100, 100);
          SettingsWindow settings = new SettingsWindow(w) { WindowStartupLocation = WindowStartupLocation.Manual, Left = -10000, Top = -10000, ShowActivated = false, ShowInTaskbar = false };
          settings.Show();
          try {
            WpfAppHost.Pump();
            CheckBox reserve = (CheckBox)settings.FindName("CheckReserveDockedSpace");
            CheckBox version = Assert.IsType<CheckBox>(settings.FindName("CheckShowVersionInTitle"));
            Assert.Equal("Show version in title", version.Content);
            Assert.False(string.IsNullOrEmpty(version.ToolTip as string));
            StackPanel other = (StackPanel)reserve.Parent;
            Assert.Same(other, version.Parent);
            Assert.Equal(other.Children.IndexOf(reserve) + 1, other.Children.IndexOf(version));
            Assert.True(version.IsChecked);
            version.IsChecked = false;
            WpfAppHost.Pump();
            Assert.False(w.Appearance.ShowVersionInTitle);
          } finally {
            settings.Close();
            WpfAppHost.Pump();
          }
          Assert.True(w.Appearance.ShowVersionInTitle);
        });
      }
    }
  }
}
