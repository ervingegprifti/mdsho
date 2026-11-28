using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using MDSHO.Data;
using MDSHO.Helpers;
using MDSHO.ViewModels;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// 2.1.0, Settings > Sync: "Sync through" Folder, Google Drive or OneDrive. Folder shows the folder row as before; Google
  /// Drive and OneDrive show the account with Connect... and Disconnect, and that every PC must use the same choice.
  /// </summary>
  public class SettingsSyncCloudTests {

    private sealed class Setup : IDisposable {
      public readonly TempDb Db = new TempDb();
      public readonly FakeAppServices Services = new FakeAppServices();
      public readonly FakeClock Clock = new FakeClock();
      public readonly SyncService Sync;
      public readonly FakeHttp Tokens = new FakeHttp(r => FakeHttp.Status(HttpStatusCode.OK));
      private readonly Func<string, string> originalPicker = SettingsWindow.PickSyncFolder;

      public Setup(bool setUp) {
        string profile = Path.Combine(Db.Folder, "Profile");
        Directory.CreateDirectory(profile);
        BackupService backups = new BackupService(Clock, "HOMEPC", profile, "2.1.0", new ScreenSetupTests.FakeTimer(), a => a());
        Services.Services.Backups = backups;
        Sync = new SyncService(Clock, "2.1.0", new ScreenSetupTests.FakeTimer(), a => a(), backups, () => new HashSet<long>(), r => { }) {
          ProviderFor = kind => kind == SyncKinds.GoogleDrive ? OAuthProviders.Google(setUp ? "gid" : "", "s")
            : kind == SyncKinds.OneDrive ? OAuthProviders.Microsoft(setUp ? "mid" : "") : null,
          ClientFor = p => new OAuthClient(p, Tokens),
          CloudHandler = new FakeGoogleDrive().Http()
        };
        Services.Services.Sync = Sync;
        SettingsWindow.PickSyncFolder = start => throw new InvalidOperationException("The folder picker was opened in a test.");
      }

      public void Dispose() {
        SettingsWindow.PickSyncFolder = originalPicker;
        Services.Dispose();
        Db.Dispose();
      }
    }

    private static void WithSettings(Action<SettingsWindow> check) {
      WpfAppHost.Run(() => {
        DesktopWindowViewModel w = new AppViewModel().AddWindow("TEST", 100, 100);
        SettingsWindow settings = new SettingsWindow(w) {
          WindowStartupLocation = WindowStartupLocation.Manual, Left = -10000, Top = -10000, ShowActivated = false, ShowInTaskbar = false
        };
        settings.Show();
        WpfAppHost.Pump();
        ((ListBox)settings.FindName("ListSettingsPages")).Items.Cast<ListBoxItem>().Single(i => (string)i.Content == "Sync").IsSelected = true;
        WpfAppHost.Pump();
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

    private static T Named<T>(FrameworkElement root, string name) where T : class => Assert.IsAssignableFrom<T>(root.FindName(name));

    private static void Click(SettingsWindow window, string button) {
      Named<Button>(window, button).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
      WpfAppHost.Pump();
    }

    private static void Choose(SettingsWindow window, string kind) {
      ComboBox list = Named<ComboBox>(window, "ComboSyncKind");
      list.SelectedItem = list.Items.Cast<ComboBoxItem>().Single(i => (string)i.Content == kind);
      WpfAppHost.Pump();
      window.UpdateLayout();
      WpfAppHost.Pump();
    }

    [Fact]
    public void Sync_through_lists_the_three_choices_and_starts_on_Folder() {
      using (new Setup(setUp: false)) {
        WithSettings(window => {
          ComboBox list = Named<ComboBox>(window, "ComboSyncKind");
          Assert.Equal(new[] { "Folder", "Google Drive", "OneDrive" }, list.Items.Cast<ComboBoxItem>().Select(i => (string)i.Content).ToArray());
          Assert.Equal("Folder", ((ComboBoxItem)list.SelectedItem).Content);
          Assert.Equal("Sync through", Named<TextBlock>(window, "TextSyncKindLabel").Text);
          Assert.True(Named<Grid>(window, "GridSyncFolder").IsVisible);
          Assert.False(Named<Grid>(window, "GridSyncAccount").IsVisible);
        });
      }
    }

    [Fact]
    public void A_cloud_shows_its_account_row_and_says_every_PC_must_use_the_same_choice() {
      using (new Setup(setUp: true)) {
        WithSettings(window => {
          Choose(window, "Google Drive");
          Assert.False(Named<Grid>(window, "GridSyncFolder").IsVisible);
          Assert.True(Named<Grid>(window, "GridSyncAccount").IsVisible);
          Assert.Equal("Not connected", Named<TextBox>(window, "TextSyncAccount").Text);
          Assert.Equal("Connect...", Named<Button>(window, "ButtonSyncConnect").Content);
          Assert.True(Named<Button>(window, "ButtonSyncConnect").IsEnabled);
          Assert.False(Named<Button>(window, "ButtonSyncDisconnect").IsEnabled);
          Assert.Contains("Every PC must use the same choice", Named<TextBlock>(window, "TextSyncFolderHint").Text);
          Choose(window, "Folder");
          Assert.Contains("same one on each PC", Named<TextBlock>(window, "TextSyncFolderHint").Text);
        });
      }
    }

    [Fact]
    public void Connect_says_when_a_choice_is_not_set_up_yet() {
      using (new Setup(setUp: false)) {
        WithSettings(window => {
          Choose(window, "OneDrive");
          Click(window, "ButtonSyncConnect");
          WpfAppHost.Pump();
          TextBlock error = Named<TextBlock>(window, "TextSyncError");
          Assert.True(error.IsVisible);
          Assert.Equal("OneDrive is not set up in this version of MDSHO yet.", error.Text);
        });
      }
    }

    [Fact]
    public void A_connected_cloud_shows_the_account_and_Disconnect_forgets_it() {
      using (Setup s = new Setup(setUp: true)) {
        SyncSettings.SetRefreshToken(SyncKinds.GoogleDrive, "rt");
        SyncSettings.SetAccount(SyncKinds.GoogleDrive, "ann@example.com");
        SyncSettings.Kind = SyncKinds.GoogleDrive;
        WithSettings(window => {
          Assert.Equal("Google Drive", ((ComboBoxItem)Named<ComboBox>(window, "ComboSyncKind").SelectedItem).Content);
          Assert.Equal("Connected: ann@example.com", Named<TextBox>(window, "TextSyncAccount").Text);
          Assert.True(Named<Button>(window, "ButtonSyncDisconnect").IsEnabled);
          Click(window, "ButtonSyncDisconnect");
          for (int i = 0; i < 50 && SyncSettings.RefreshToken(SyncKinds.GoogleDrive) != null; i++) {
            System.Threading.Thread.Sleep(20);
            WpfAppHost.Pump();
          }
          WpfAppHost.Pump();
          Assert.Null(SyncSettings.RefreshToken(SyncKinds.GoogleDrive));
          Assert.Equal("Not connected", Named<TextBox>(window, "TextSyncAccount").Text);
          Assert.Contains(s.Tokens.Requests, r => r.Url.Contains("revoke"));
        });
      }
    }

    [Fact]
    public void Apply_on_a_cloud_that_is_not_connected_asks_to_connect_first() {
      using (new Setup(setUp: true)) {
        WithSettings(window => {
          Choose(window, "Google Drive");
          Named<CheckBox>(window, "CheckSyncOn").IsChecked = true;
          Click(window, "ButtonApply");
          Assert.True(window.IsVisible);
          Assert.Equal("Connect to Google Drive first.", Named<TextBlock>(window, "TextSyncError").Text);
          Assert.False(SyncSettings.IsOn);
        });
      }
    }

    [Fact]
    public void Apply_on_a_connected_cloud_turns_sync_on_there() {
      using (new Setup(setUp: true)) {
        SyncSettings.SetRefreshToken(SyncKinds.GoogleDrive, "rt");
        WithSettings(window => {
          Choose(window, "Google Drive");
          Named<CheckBox>(window, "CheckSyncOn").IsChecked = true;
          Click(window, "ButtonApply");
          Assert.False(window.IsVisible);
          Assert.True(SyncSettings.IsOn);
          Assert.Equal(SyncKinds.GoogleDrive, SyncSettings.Kind);
        });
      }
    }
  }
}
