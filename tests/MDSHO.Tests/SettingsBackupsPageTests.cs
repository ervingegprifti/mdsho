using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using MDSHO.Data;
using MDSHO.Helpers;
using MDSHO.ViewModels;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>Settings > Backups: on/off, folder, keep count, last backup, Back up now and the failure line.</summary>
  public class SettingsBackupsPageTests {

    private sealed class Setup : IDisposable {
      public readonly TempDb Db = new TempDb();
      public readonly FakeAppServices Services = new FakeAppServices();
      public readonly FakeClock Clock = new FakeClock();
      public readonly string Profile;
      public readonly BackupService Backups;
      private readonly Func<string, string> originalPicker = SettingsWindow.PickFolder;

      public Setup() {
        Profile = Path.Combine(Db.Folder, "Profile");
        Directory.CreateDirectory(Profile);
        Backups = new BackupService(Clock, "HOMEPC", Profile, "1.12.0", new ScreenSetupTests.FakeTimer(), a => a());
        Services.Services.Backups = Backups;
        SettingsWindow.PickFolder = start => throw new InvalidOperationException("The folder picker was opened in a test.");
      }

      public string DefaultFolder => Path.Combine(Profile, AutoBackupNames.ExpectedFolderName);

      public void Dispose() {
        SettingsWindow.PickFolder = originalPicker;
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
      ((ListBox)settings.FindName("ListSettingsPages")).Items.Cast<ListBoxItem>().Single(i => (string)i.Content == "Backups").IsSelected = true;
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

    private static string Hex(object brush) {
      return Assert.IsType<SolidColorBrush>(brush).Color.ToString();
    }

    [Fact]
    public void The_page_shows_the_defaults() {
      using (Setup s = new Setup()) {
        WithSettings(window => {
          Assert.True(Named<StackPanel>(window, "PageBackups").IsVisible);
          CheckBox on = Named<CheckBox>(window, "CheckBackupOn");
          Assert.Equal("Back up my data every day", on.Content);
          Assert.True(on.IsChecked);
          TextBox folder = Named<TextBox>(window, "TextBackupFolder");
          Assert.True(folder.IsReadOnly);
          Assert.Equal(s.DefaultFolder, folder.Text);
          Assert.Equal("...", Named<Button>(window, "ButtonBackupFolderChange").Content);
          Assert.Equal("Open", Named<Button>(window, "ButtonBackupFolderOpen").Content);
          ComboBox keep = Named<ComboBox>(window, "ComboBackupKeep");
          Assert.Equal(new[] { 3, 5, 10, 20, 30 }, keep.Items.Cast<int>().ToArray());
          Assert.Equal(10, keep.SelectedItem);
          Assert.Equal("Last backup: Never", Named<TextBlock>(window, "TextLastBackup").Text);
          Assert.Equal("Back up now", Named<Button>(window, "ButtonBackUpNow").Content);
          Assert.Equal("Only when something changed.", Named<TextBlock>(window, "TextBackupOnHint").Text);
          Assert.False(Named<TextBlock>(window, "TextBackupError").IsVisible);
          Assert.False(Directory.Exists(s.DefaultFolder));
        });
      }
    }

    [Fact]
    public void Apply_saves_the_on_off_the_folder_and_the_keep_count() {
      using (Setup s = new Setup()) {
        string chosen = Path.Combine(s.Db.Folder, "Drive", "MDSHO backups");
        SettingsWindow.PickFolder = start => {
          Assert.Equal(s.DefaultFolder, start);
          return chosen;
        };
        WithSettings(window => {
          Named<CheckBox>(window, "CheckBackupOn").IsChecked = false;
          Click(window, "ButtonBackupFolderChange");
          Assert.Equal(chosen, Named<TextBox>(window, "TextBackupFolder").Text);
          Named<ComboBox>(window, "ComboBackupKeep").SelectedItem = 30;
          Assert.True(s.Backups.IsOn); // nothing saved before Apply
          Click(window, "ButtonApply");
          Assert.False(window.IsVisible);
        });
        Assert.False(s.Backups.IsOn);
        Assert.Equal(chosen, s.Backups.Folder);
        Assert.Equal(30, s.Backups.Keep);
      }
    }

    [Fact]
    public void Closing_without_Apply_saves_nothing() {
      using (Setup s = new Setup()) {
        SettingsWindow.PickFolder = start => Path.Combine(s.Db.Folder, "Elsewhere");
        WithSettings(window => {
          Named<CheckBox>(window, "CheckBackupOn").IsChecked = false;
          Click(window, "ButtonBackupFolderChange");
          Named<ComboBox>(window, "ComboBackupKeep").SelectedItem = 3;
        });
        Assert.True(s.Backups.IsOn);
        Assert.Equal(s.DefaultFolder, s.Backups.Folder);
        Assert.Equal(10, s.Backups.Keep);
        Assert.Null(DataStore.AppSettings.Get(BackupService.OnKey));
      }
    }

    [Fact]
    public void A_cancelled_folder_picker_keeps_the_folder() {
      using (Setup s = new Setup()) {
        SettingsWindow.PickFolder = start => null;
        WithSettings(window => {
          Click(window, "ButtonBackupFolderChange");
          Assert.Equal(s.DefaultFolder, Named<TextBox>(window, "TextBackupFolder").Text);
        });
      }
    }

    [Fact]
    public void Open_creates_the_folder_and_opens_it_in_Explorer() {
      using (Setup s = new Setup()) {
        WithSettings(window => {
          Click(window, "ButtonBackupFolderOpen");
          Assert.True(Directory.Exists(s.DefaultFolder));
          Assert.Equal(new[] { s.DefaultFolder }, s.Services.OpenedFolders.ToArray());
        });
      }
    }

    [Fact]
    public void Back_up_now_writes_a_file_in_the_folder_shown_and_updates_the_last_backup() {
      using (Setup s = new Setup()) {
        string chosen = Path.Combine(s.Db.Folder, "Chosen");
        SettingsWindow.PickFolder = start => chosen;
        WithSettings(window => {
          Click(window, "ButtonBackupFolderChange");
          Click(window, "ButtonBackUpNow");
          Assert.Equal(new[] { "MDSHO-backup-HOMEPC-2026-11-15-091204.zip" }, Directory.GetFiles(chosen).Select(Path.GetFileName).ToArray());
          Assert.Equal("Last backup: today 09:12", Named<TextBlock>(window, "TextLastBackup").Text);
          Assert.False(Named<TextBlock>(window, "TextBackupError").IsVisible);
          Assert.True(window.IsVisible);
        });
      }
    }

    [Fact]
    public void A_failed_backup_shows_a_red_line_until_the_next_success() {
      using (Setup s = new Setup()) {
        string blocker = Path.Combine(s.Db.Folder, "blocker.txt");
        File.WriteAllText(blocker, "x");
        string missing = Path.Combine(blocker, "MDSHO backups");
        s.Backups.Save(true, missing, 10);
        s.Backups.RunIfDue();
        WithSettings(window => {
          TextBlock error = Named<TextBlock>(window, "TextBackupError");
          Assert.True(error.IsVisible);
          Assert.Equal("The last backup failed: the folder " + missing + " can't be found.", error.Text);
          foreach (WindowThemeKind kind in new[] { WindowThemeKind.Light, WindowThemeKind.Dark }) {
            WindowThemes.Apply(kind);
            WpfAppHost.Pump();
            Assert.Equal(Hex(WindowThemes.ActiveDictionary["Theme.HintErrorText"]), Hex(error.Foreground));
          }
          SettingsWindow.PickFolder = start => s.DefaultFolder;
          Click(window, "ButtonBackupFolderChange");
          Click(window, "ButtonBackUpNow");
          Assert.False(error.IsVisible);
        });
      }
    }

    [Fact]
    public void The_page_shows_saved_settings() {
      using (Setup s = new Setup()) {
        string custom = Path.Combine(s.Db.Folder, "Custom");
        s.Backups.Save(false, custom, 5);
        WithSettings(window => {
          Assert.False(Named<CheckBox>(window, "CheckBackupOn").IsChecked);
          Assert.Equal(custom, Named<TextBox>(window, "TextBackupFolder").Text);
          Assert.Equal(5, Named<ComboBox>(window, "ComboBackupKeep").SelectedItem);
        });
      }
    }
  }
}
