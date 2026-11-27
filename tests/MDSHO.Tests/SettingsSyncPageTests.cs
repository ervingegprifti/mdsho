using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using MDSHO.Data;
using MDSHO.Helpers;
using MDSHO.ViewModels;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// 2.0.0, Settings > Sync: keep this PC in sync, the folder, this PC's name, the status line with Sync now and the
  /// table of the other PCs. On, folder and name are saved on Apply; turning on makes a backup first.
  /// </summary>
  public class SettingsSyncPageTests {

    private sealed class Setup : IDisposable {
      public readonly TempDb Db = new TempDb();
      public readonly FakeAppServices Services = new FakeAppServices();
      public readonly FakeClock Clock = new FakeClock();
      public readonly string Profile;
      public readonly string SyncFolder;
      public readonly BackupService Backups;
      public readonly SyncService Sync;
      private readonly Func<string, string> originalPicker = SettingsWindow.PickSyncFolder;

      public Setup() {
        Profile = Path.Combine(Db.Folder, "Profile");
        Directory.CreateDirectory(Profile);
        SyncFolder = Path.Combine(Db.Folder, "Drive", "MDSHO Sync");
        Directory.CreateDirectory(SyncFolder);
        Backups = new BackupService(Clock, "HOMEPC", Profile, "2.0.0", new ScreenSetupTests.FakeTimer(), a => a());
        Services.Services.Backups = Backups;
        Sync = new SyncService(Clock, "2.0.0", new ScreenSetupTests.FakeTimer(), a => a(), Backups, () => new HashSet<long>(), r => { });
        Services.Services.Sync = Sync;
        SettingsWindow.PickSyncFolder = start => throw new InvalidOperationException("The folder picker was opened in a test.");
      }

      public void Dispose() {
        SettingsWindow.PickSyncFolder = originalPicker;
        Services.Dispose();
        Db.Dispose();
      }
    }

    private static SettingsWindow Open() {
      DesktopWindowViewModel w = new AppViewModel().AddWindow("TEST", 100, 100);
      SettingsWindow settings = new SettingsWindow(w) {
        WindowStartupLocation = WindowStartupLocation.Manual,
        Left = -10000,
        Top = -10000,
        ShowActivated = false,
        ShowInTaskbar = false
      };
      settings.Show();
      WpfAppHost.Pump();
      ((ListBox)settings.FindName("ListSettingsPages")).Items.Cast<ListBoxItem>().Single(i => (string)i.Content == "Sync").IsSelected = true;
      WpfAppHost.Pump();
      return settings;
    }

    private static void WithSettings(Action<SettingsWindow> check) {
      WpfAppHost.Run(() => {
        SettingsWindow settings = Open();
        try {
          check(settings);
        } finally {
          if (settings.IsVisible) {
            settings.Close();
          }
          WindowThemes.Apply(WindowThemeKind.Light);
          WpfAppHost.Pump();
        }
      });
    }

    private static T Named<T>(FrameworkElement root, string name) where T : class {
      return Assert.IsAssignableFrom<T>(root.FindName(name));
    }

    private static void Click(SettingsWindow window, string button) {
      Named<Button>(window, button).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
      WpfAppHost.Pump();
    }

    [Fact]
    public void The_page_comes_after_Backups_and_shows_sync_off() {
      using (Setup s = new Setup()) {
        WithSettings(window => {
          ListBox pages = Named<ListBox>(window, "ListSettingsPages");
          Assert.Equal(new[] { "Backups", "Sync" }, pages.Items.Cast<ListBoxItem>().Select(i => (string)i.Content).Skip(4).ToArray());
          Assert.True(Named<StackPanel>(window, "PageSync").IsVisible);
          CheckBox on = Named<CheckBox>(window, "CheckSyncOn");
          Assert.Equal("Keep this PC in sync with my other PCs", on.Content);
          Assert.False(on.IsChecked);
          Assert.Equal("Panels, groups, buttons, notes, comments and snippets, with their text and colours. Window places and this PC's settings stay here.",
            Named<TextBlock>(window, "TextSyncOnHint").Text);
          TextBox folder = Named<TextBox>(window, "TextSyncFolder");
          Assert.True(folder.IsReadOnly);
          Assert.Equal("", folder.Text);
          Assert.Equal("...", Named<Button>(window, "ButtonSyncFolderChange").Content);
          Assert.Equal("Open", Named<Button>(window, "ButtonSyncFolderOpen").Content);
          Assert.Contains("Google Drive or OneDrive for desktop, Dropbox", Named<TextBlock>(window, "TextSyncFolderHint").Text);
          Assert.Contains("same one on each PC", Named<TextBlock>(window, "TextSyncFolderHint").Text);
          Assert.Equal(DeviceIdentity.Name, Named<TextBox>(window, "TextDeviceName").Text);
          Assert.Equal("Shown on your other PCs and in Text history.", Named<TextBlock>(window, "TextDeviceNameHint").Text);
          Assert.Equal("Sync is off.", Named<TextBlock>(window, "TextSyncStatus").Text);
          Assert.Equal("Sync now", Named<Button>(window, "ButtonSyncNow").Content);
          Assert.False(Named<Button>(window, "ButtonSyncNow").IsEnabled);
          Assert.False(Named<TextBlock>(window, "TextSyncBackupNotice").IsVisible);
        });
      }
    }

    [Fact]
    public void Apply_with_a_folder_turns_sync_on_after_a_backup_and_says_so_once() {
      using (Setup s = new Setup()) {
        SettingsWindow.PickSyncFolder = start => s.SyncFolder;
        WithSettings(window => {
          Click(window, "ButtonSyncFolderChange");
          Assert.Equal(s.SyncFolder, Named<TextBox>(window, "TextSyncFolder").Text);
          Named<CheckBox>(window, "CheckSyncOn").IsChecked = true;
          Assert.False(SyncSettings.IsOn); // nothing saved before Apply
          Click(window, "ButtonApply");
          Assert.False(window.IsVisible);
        });
        Assert.True(SyncSettings.IsOn);
        Assert.Equal(s.SyncFolder, SyncSettings.Folder);
        Assert.Single(Directory.GetFiles(s.Backups.Folder, "MDSHO-backup-HOMEPC-before-sync-*.zip"));
        Assert.True(File.Exists(Path.Combine(s.SyncFolder, DeviceIdentity.Id, "index.json")));
        WithSettings(window => {
          TextBlock notice = Named<TextBlock>(window, "TextSyncBackupNotice");
          Assert.True(notice.IsVisible);
          Assert.Contains("Import data restores it", notice.Text);
          Assert.True(Named<CheckBox>(window, "CheckSyncOn").IsChecked);
          Assert.StartsWith("Up to date.", Named<TextBlock>(window, "TextSyncStatus").Text);
          Assert.True(Named<Button>(window, "ButtonSyncNow").IsEnabled);
        });
        WithSettings(window => Assert.False(Named<TextBlock>(window, "TextSyncBackupNotice").IsVisible));
      }
    }

    [Fact]
    public void Ticking_the_box_without_a_folder_asks_for_one() {
      using (Setup s = new Setup()) {
        SettingsWindow.PickSyncFolder = start => null;
        WithSettings(window => {
          Named<CheckBox>(window, "CheckSyncOn").IsChecked = true;
          Click(window, "ButtonApply");
          Assert.True(window.IsVisible);
          Assert.True(Named<TextBlock>(window, "TextSyncError").IsVisible);
        });
        Assert.False(SyncSettings.IsOn);
      }
    }

    [Fact]
    public void Clearing_the_box_turns_sync_off_and_keeps_the_folder() {
      using (Setup s = new Setup()) {
        s.Sync.TurnOn(s.SyncFolder);
        WithSettings(window => {
          Named<CheckBox>(window, "CheckSyncOn").IsChecked = false;
          Click(window, "ButtonApply");
        });
        Assert.False(SyncSettings.IsOn);
        Assert.Equal(s.SyncFolder, SyncSettings.Folder);
        Assert.True(Directory.Exists(Path.Combine(s.SyncFolder, DeviceIdentity.Id)));
      }
    }

    [Fact]
    public void Apply_saves_this_PCs_name() {
      using (Setup s = new Setup()) {
        WithSettings(window => {
          Named<TextBox>(window, "TextDeviceName").Text = "  Home PC ";
          Click(window, "ButtonApply");
        });
        Assert.Equal("Home PC", DeviceIdentity.Name);
      }
    }

    [Fact]
    public void Closing_without_Apply_saves_nothing() {
      using (Setup s = new Setup()) {
        SettingsWindow.PickSyncFolder = start => s.SyncFolder;
        WithSettings(window => {
          Click(window, "ButtonSyncFolderChange");
          Named<CheckBox>(window, "CheckSyncOn").IsChecked = true;
          Named<TextBox>(window, "TextDeviceName").Text = "Other name";
        });
        Assert.False(SyncSettings.IsOn);
        Assert.Equal("", SyncSettings.Folder);
        Assert.NotEqual("Other name", DeviceIdentity.Name);
      }
    }

    [Fact]
    public void Sync_now_runs_a_round_and_the_table_lists_the_other_PCs() {
      using (Setup s = new Setup()) {
        string device = Guid.NewGuid().ToString();
        MDSHO.Data.SyncFolder.WriteIndex(MDSHO.Data.SyncFolder.DeviceFolder(s.SyncFolder, device),
          new SyncIndex { Device = device, Name = "Work PC", AppVersion = "2.0.0", Seq = 1, WrittenAt = s.Clock.GetUtcNow().ToUnixTimeMilliseconds() });
        SyncSettings.Save(true, s.SyncFolder);
        WithSettings(window => {
          Click(window, "ButtonSyncNow");
          Assert.Equal("Up to date. Last sync just now.", Named<TextBlock>(window, "TextSyncStatus").Text);
          ItemsControl peers = Named<ItemsControl>(window, "ListSyncPeers");
          SyncPeerRow row = Assert.IsType<SyncPeerRow>(peers.Items.Cast<object>().Single());
          Assert.Equal(("Work PC", "Today 09:12", "2.0.0"), (row.Name, row.LastSync, row.Version));
        });
      }
    }
  }
}
