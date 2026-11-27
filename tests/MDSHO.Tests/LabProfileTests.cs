using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MDSHO.Common;
using MDSHO.Data;
using MDSHO.ViewModels;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// The sync lab (developers only): <c>--profile &lt;name&gt;</c> runs a copy of MDSHO with its own data folder under
  /// <c>%LOCALAPPDATA%\MDSHO-lab\&lt;name&gt;</c>, its own single-instance mutex, device name and title, so several copies
  /// can sync side by side on one PC. <c>--sync-every</c> and <c>--sync-folder</c> are lab only too. The MSIX package
  /// ignores all of them.
  /// </summary>
  public class LabProfileTests : IDisposable {
    private readonly LabOptions old = LabOptions.Current;

    public void Dispose() { LabOptions.Current = old; }

    private static LabOptions Parse(bool packaged, params string[] args) => LabOptions.Parse(args, packaged);

    [Fact]
    public void A_profile_and_its_lab_options_are_read_from_the_command_line() {
      LabOptions lab = Parse(false, "--profile", "PC-A", "--sync-every", "3", "--sync-folder", @"C:\lab\sync");
      Assert.True(lab.IsLab);
      Assert.Equal("PC-A", lab.Profile);
      Assert.Equal(TimeSpan.FromSeconds(3), lab.SyncEvery);
      Assert.Equal(@"C:\lab\sync", lab.SyncFolder);
    }

    [Fact]
    public void The_package_ignores_every_lab_option() {
      LabOptions lab = Parse(true, "--profile", "PC-A", "--sync-every", "3", "--sync-folder", @"C:\lab\sync");
      Assert.False(lab.IsLab);
      Assert.Null(lab.Profile);
      Assert.Null(lab.SyncEvery);
      Assert.Null(lab.SyncFolder);
      Assert.Equal("", lab.CommandLine);
    }

    [Fact]
    public void No_options_is_a_normal_run() {
      LabOptions lab = Parse(false);
      Assert.False(lab.IsLab);
      Assert.Equal("", lab.TitleSuffix);
      Assert.Equal("Local\\MDSHO-x", lab.MutexName("Local\\MDSHO-x"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("PC A")]
    [InlineData("..")]
    [InlineData("a\\b")]
    [InlineData("a/b")]
    [InlineData("PC.A")]
    [InlineData("a-very-long-profile-name-that-goes-on-and-on")]
    public void Only_letters_digits_and_dashes_make_a_profile(string name) {
      Assert.False(LabOptions.IsValidName(name));
      Assert.False(Parse(false, "--profile", name).IsLab);
    }

    [Fact]
    public void A_profile_with_no_name_or_lab_options_without_a_profile_are_ignored() {
      Assert.False(Parse(false, "--profile").IsLab);
      LabOptions lab = Parse(false, "--sync-every", "3", "--sync-folder", @"C:\lab\sync");
      Assert.False(lab.IsLab);
      Assert.Null(lab.SyncEvery);
      Assert.Null(lab.SyncFolder);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("soon")]
    public void A_sync_interval_that_is_not_a_positive_number_is_ignored(string seconds) {
      LabOptions lab = Parse(false, "--profile", "PC-A", "--sync-every", seconds);
      Assert.True(lab.IsLab);
      Assert.Null(lab.SyncEvery);
    }

    [Fact]
    public void A_profile_has_its_own_mutex_title_and_device_name() {
      LabOptions lab = Parse(false, "--profile", "PC-B");
      Assert.Equal("Local\\MDSHO-x-lab-PC-B", lab.MutexName("Local\\MDSHO-x"));
      Assert.Equal(" [PC-B]", lab.TitleSuffix);
      Assert.NotEqual(Parse(false, "--profile", "PC-A").MutexName("Local\\MDSHO-x"), lab.MutexName("Local\\MDSHO-x"));
    }

    [Fact]
    public void A_profile_keeps_all_its_data_in_its_own_lab_folder() {
      string name = "test-" + Guid.NewGuid().ToString("N").Substring(0, 8);
      string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
      string expected = Path.Combine(local, LabOptions.LabFolderName, name);
      string normal = Paths.GetAppDataFolderPath();
      LabOptions.Current = Parse(false, "--profile", name);
      try {
        Assert.Equal(expected, LabOptions.Current.DataFolder);
        Assert.Equal(expected, Paths.GetAppDataFolderPath());
        string oldDb = Paths.DatabaseFilePathOverride, oldShortcuts = Paths.ShortcutsPathOverride, oldImport = Paths.ImportBackupsPathOverride;
        Paths.DatabaseFilePathOverride = null;
        Paths.ShortcutsPathOverride = null;
        Paths.ImportBackupsPathOverride = null;
        try {
          Assert.Equal(expected, Path.GetDirectoryName(Paths.GetAppDataLocalDatabaseFilePath()));
          Assert.Equal(expected, Path.GetDirectoryName(Paths.GetShortcutsPath()));
          Assert.Equal(expected, Path.GetDirectoryName(Paths.GetImportBackupsPath()));
        } finally {
          Paths.DatabaseFilePathOverride = oldDb;
          Paths.ShortcutsPathOverride = oldShortcuts;
          Paths.ImportBackupsPathOverride = oldImport;
        }
      } finally {
        LabOptions.Current = LabOptions.None;
        if (Directory.Exists(expected)) {
          Directory.Delete(expected, true);
        }
      }
      Assert.Equal(normal, Paths.GetAppDataFolderPath());
    }

    [Fact]
    public void The_device_name_defaults_to_the_profile_name() {
      using (TempDb db = new TempDb()) {
        LabOptions.Current = Parse(false, "--profile", "PC-A");
        Assert.Equal("PC-A", DeviceIdentity.Name);
        DeviceIdentity.Name = "Laptop";
        Assert.Equal("Laptop", DeviceIdentity.Name);
        LabOptions.Current = LabOptions.None;
        DeviceIdentity.Name = "";
        Assert.Equal(Environment.MachineName, DeviceIdentity.Name);
      }
    }

    [Fact]
    public void A_panel_title_shows_the_profile() {
      using (TempDb db = new TempDb()) {
        AppViewModel app = new AppViewModel();
        DesktopWindowViewModel win = app.AddWindow("TEST", 0, 0);
        Assert.DoesNotContain("[PC-A]", win.TitleDisplay);
        LabOptions.Current = Parse(false, "--profile", "PC-A");
        Assert.Contains(win.Appearance.Title + " [PC-A]", win.TitleDisplay);
      }
    }

    [Fact]
    public void A_restart_after_import_keeps_the_lab_options() {
      LabOptions lab = Parse(false, "--profile", "PC-A", "--sync-every", "3", "--sync-folder", @"C:\lab folder\sync");
      Assert.Equal("--profile PC-A --sync-every 3 --sync-folder \"C:\\lab folder\\sync\"", lab.CommandLine);
      RestartCommand c = RestartCommand.Build(false, null, @"C:\x\MDSHO.exe", lab.CommandLine);
      Assert.Equal(lab.CommandLine, c.Arguments);
      // The package starts by its app id, never with lab options.
      Assert.DoesNotContain("--profile", RestartCommand.Build(true, "MDSHO_abc", @"C:\x\MDSHO.exe", lab.CommandLine).Arguments);
    }

    [Fact]
    public void A_shorter_sync_interval_sets_the_check_delay_and_the_read_interval() {
      using (TempDb db = new TempDb()) {
        ScreenSetupTests.FakeTimer timer = new ScreenSetupTests.FakeTimer();
        string folder = Path.Combine(db.Folder, "Sync");
        Directory.CreateDirectory(folder);
        SyncSettings.Save(true, folder);
        int runs = 0;
        FakeClock clock = new FakeClock();
        SyncService sync = new SyncService(clock, "2.0.1", timer, action => { runs++; action(); }, null, null, null) {
          CheckDelay = TimeSpan.FromSeconds(2),
          ReadInterval = TimeSpan.FromSeconds(2)
        };
        sync.Start();
        Assert.Equal(TimeSpan.FromSeconds(2), timer.Delay);
        // No change: still read again after the short interval.
        clock.Now = clock.Now.AddSeconds(2);
        timer.Fire();
        Assert.Equal(2, runs);
        Assert.Equal(TimeSpan.FromSeconds(2), timer.Delay);
      }
    }

    [Fact]
    public void Normal_runs_keep_the_usual_sync_intervals() {
      SyncService sync = new SyncService(TimeProvider.System, "2.0.1", new ScreenSetupTests.FakeTimer(), action => action(), null, null, null);
      Assert.Equal(SyncService.CheckEvery, sync.CheckDelay);
      Assert.Equal(SyncService.ReadEvery, sync.ReadInterval);
    }
  }
}
