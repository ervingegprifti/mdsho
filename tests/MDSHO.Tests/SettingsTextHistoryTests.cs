using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using MDSHO.Core;
using MDSHO.Data;
using MDSHO.Helpers;
using MDSHO.ViewModels;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>Settings > General > Text history: "[10] earlier texts to keep", 0 to 50, saved on Apply.</summary>
  public class SettingsTextHistoryTests {

    private sealed class Setup : IDisposable {
      public readonly TempDb Db = new TempDb();
      public readonly FakeAppServices Services = new FakeAppServices();
      private readonly Func<string, string> originalPicker = SettingsWindow.PickFolder;

      public Setup() {
        string profile = Path.Combine(Db.Folder, "Profile");
        Directory.CreateDirectory(profile);
        Services.Services.Backups = new BackupService(new FakeClock(), "HOMEPC", profile, "1.22.0", new ScreenSetupTests.FakeTimer(), a => a());
        SettingsWindow.PickFolder = start => throw new InvalidOperationException("The folder picker was opened in a test.");
      }

      public void Dispose() {
        SettingsWindow.PickFolder = originalPicker;
        Services.Dispose();
        Db.Dispose();
      }
    }

    private static void WithGeneralPage(Action<SettingsWindow, AppViewModel> check) {
      WpfAppHost.Run(() => {
        AppViewModel app = new AppViewModel();
        DesktopWindowViewModel w = app.AddWindow("TEST", 100, 100);
        SettingsWindow settings = new SettingsWindow(w) {
          WindowStartupLocation = WindowStartupLocation.Manual,
          Left = -10000,
          Top = -10000,
          ShowActivated = false,
          ShowInTaskbar = false
        };
        settings.Show();
        WpfAppHost.Pump();
        ((ListBox)settings.FindName("ListSettingsPages")).Items.Cast<ListBoxItem>().Single(i => (string)i.Content == "General").IsSelected = true;
        WpfAppHost.Pump();
        try {
          check(settings, app);
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

    [Fact]
    public void The_general_page_has_the_text_history_group_with_0_to_50_and_10_chosen() {
      using (Setup s = new Setup()) {
        WithGeneralPage((window, app) => {
          StackPanel page = Named<StackPanel>(window, "PageGeneral");
          Assert.Contains(page.Children.OfType<Label>(), l => (string)l.Content == "Text history");
          ComboBox combo = Named<ComboBox>(window, "ComboTextHistoryKeep");
          Assert.Equal(Enumerable.Range(0, 51), combo.Items.Cast<int>());
          Assert.Equal(10, combo.SelectedItem);
          Assert.True(combo.IsVisible);
          Assert.Equal("Earlier texts to keep", System.Windows.Automation.AutomationProperties.GetName(combo));
          Assert.Equal("earlier texts to keep", Named<TextBlock>(window, "TextTextHistoryKeep").Text);
          Assert.Equal("For notes, snippets and comments. 0 turns it off.", Named<TextBlock>(window, "TextTextHistoryHint").Text);
        });
      }
    }

    [Fact]
    public void Apply_saves_the_number_and_trims_the_history_at_once() {
      using (Setup s = new Setup()) {
        TextHistorySetting.SetKeep(25);
        WithGeneralPage((window, app) => {
          DesktopWindowViewModel win = app.Windows.Single();
          win.NewNoteShortcutCommand.Execute(null);
          ShortcutViewModel note = win.RootShortcuts.Last();
          note.CancelEdit();
          DateTime now = new DateTime(2026, 11, 21, 9, 0, 0);
          foreach (string t in new[] { "1", "2", "3" }) {
            now = now.AddMinutes(1);
            note.KeepHistory(HistoryFields.Text, t, now);
          }
          ComboBox combo = Named<ComboBox>(window, "ComboTextHistoryKeep");
          Assert.Equal(25, combo.SelectedItem);
          combo.SelectedItem = 1;
          Assert.Equal(25, TextHistorySetting.Keep);
          Named<Button>(window, "ButtonApply").RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
          WpfAppHost.Pump();
          Assert.Equal(1, TextHistorySetting.Keep);
          Assert.Equal(new[] { "3" }, note.History(HistoryFields.Text).Select(h => h.Text).ToArray());
        });
      }
    }
  }
}
