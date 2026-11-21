using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using MDSHO.Common;
using MDSHO.Data;
using MDSHO.Helpers;
using MDSHO.ViewModels;

namespace MDSHO {

  /// <summary>
  /// A panel's Settings window, in pages chosen from the list on the left. Changes show live; closing without Apply undoes them.
  /// The shortcut, the screen setups setting, the window theme and the backup settings are app-wide and change only on Apply.
  /// </summary>
  public partial class SettingsWindow : Window {
    private readonly IconGlyphs icons;
    private bool closeWithoutApply = true;
    private readonly ShortcutRecorder recorder;

    /// <summary>The AppSettings key of the page Settings opens on: the page's name, for example "Keyboard".</summary>
    public const string PageSettingKey = "SettingsPage";

    /// <summary>Asks for a folder, starting in the one given; returns null when cancelled. Tests replace it.</summary>
    public static System.Func<string, string> PickFolder { get; set; } = start => {
      Microsoft.Win32.OpenFolderDialog dialog = new Microsoft.Win32.OpenFolderDialog { Title = "Backup folder", InitialDirectory = System.IO.Directory.Exists(start) ? start : "" };
      return dialog.ShowDialog() == true ? dialog.FolderName : null;
    };

    /// <summary>The modifier keys held now. Tests replace it.</summary>
    public static System.Func<System.Windows.Input.ModifierKeys> KeyboardModifiers { get; set; } = () => System.Windows.Input.Keyboard.Modifiers;

    /// <summary>Creates the Settings window for a panel window and remembers the current settings so closing without Apply restores them.</summary>
    public SettingsWindow(DesktopWindowViewModel desktopWindow) {
      InitializeComponent();
      DesktopWindowAppearance a = desktopWindow.Appearance;
      this.Title = "Settings: " + a.Title;
#if DEBUG
      this.Title += " DEBUG";
#endif
      this.DataContext = desktopWindow;
      // Opens on the page used last, on this PC.
      ShowPage(ReadSavedPage());
      this.Closed += (s, e) => SavePage();
      // Every outer margin of the page and of the Apply bar equals the Apply button height, like Run command and Colours.
      // The list starts at the same height, so its first item lines up with the page title.
      this.ButtonApply.SizeChanged += (s, e) => {
        Thickness margin = new Thickness(this.ButtonApply.ActualHeight);
        this.PanelBody.Margin = margin;
        this.FooterContent.Margin = margin;
        this.ListSettingsPages.Padding = new Thickness(0, margin.Top, 0, margin.Top);
      };
      // The window icon and the title bar's gear follow the window's Solid icons setting, also while it is changed here.
      this.icons = a.Icons;
      ShowIcons();
      System.ComponentModel.PropertyChangedEventHandler iconsChanged = (s, e) => ShowIcons();
      this.icons.PropertyChanged += iconsChanged;
      this.Closed += (s, e) => this.icons.PropertyChanged -= iconsChanged;

      // Solid icons, Single click to open and Show version in title save themselves when they change; closing without
      // Apply puts them back (which saves the old values again).
      (bool solidIcons, bool singleClickToOpen, bool showVersionInTitle) old = (a.SolidIcons, a.SingleClickToOpen, a.ShowVersionInTitle);
      this.Closing += (s, e) => {
        if (this.closeWithoutApply) {
          a.SolidIcons = old.solidIcons;
          a.SingleClickToOpen = old.singleClickToOpen;
          a.ShowVersionInTitle = old.showVersionInTitle;
        }
      };
      // Keyboard shortcut: nothing changes until Apply.
      this.recorder = new ShortcutRecorder(AppServices.Current.BringToFront);
      this.PanelShortcut.DataContext = this.recorder;
      this.ButtonShortcutBox.Click += (s, e) => {
        this.recorder.StartRecording();
        this.ButtonShortcutBox.Focus();
      };
      this.ButtonShortcutBox.PreviewKeyDown += ShortcutBox_PreviewKeyDown;
      // A click elsewhere stops recording.
      this.ButtonShortcutBox.LostKeyboardFocus += (s, e) => this.recorder.Cancel();
      this.ButtonShortcutDefault.Click += (s, e) => this.recorder.UseDefault();
      this.ButtonShortcutOff.Click += (s, e) => this.recorder.TurnOff();
      // Closing while recording makes the saved shortcut work again.
      this.Closed += (s, e) => this.recorder.Cancel();
      // Screen setups: saved only on Apply.
      this.CheckRememberScreenSetups.IsChecked = AppServices.Current.ScreenSetups.Enabled;
      // Window theme: shown at once, saved on Apply; closing without Apply goes back.
      ShowWindowTheme(AppServices.Current.WindowTheme.Choice);
      RoutedEventHandler preview = (s, e) => AppServices.Current.WindowTheme.Preview(ChosenWindowTheme());
      this.RadioThemeSameAsWindows.Checked += preview;
      this.RadioThemeLight.Checked += preview;
      this.RadioThemeDark.Checked += preview;
      this.Closed += (s, e) => AppServices.Current.WindowTheme.Preview(null);
      // Backups: on/off, folder and keep count are saved on Apply; Back up now acts at once, with the folder shown.
      BackupService backups = AppServices.Current.Backups;
      this.CheckBackupOn.IsChecked = backups.IsOn;
      this.TextBackupFolder.Text = backups.Folder;
      foreach (int keep in BackupService.KeepChoices) {
        this.ComboBackupKeep.Items.Add(keep);
      }
      this.ComboBackupKeep.SelectedItem = backups.Keep;
      // Text history: how many earlier texts each note, snippet and comment keeps; saved on Apply.
      foreach (int keep in TextHistorySetting.Choices) {
        this.ComboTextHistoryKeep.Items.Add(keep);
      }
      this.ComboTextHistoryKeep.SelectedItem = TextHistorySetting.Keep;
      ShowBackupState(backups);
      this.ButtonBackupFolderChange.Click += (s, e) => {
        string chosen = PickFolder(this.TextBackupFolder.Text);
        if (!string.IsNullOrEmpty(chosen)) {
          this.TextBackupFolder.Text = chosen;
        }
      };
      this.ButtonBackupFolderOpen.Click += (s, e) => {
        try {
          System.IO.Directory.CreateDirectory(this.TextBackupFolder.Text);
          AppServices.Current.Shell.OpenFolder(this.TextBackupFolder.Text);
        } catch (System.Exception ex) {
          ErrorDialog.ShowDialog("The backup folder could not be opened.", ex);
        }
      };
      this.ButtonBackUpNow.Click += (s, e) => {
        backups.BackUpNow(this.TextBackupFolder.Text, (int)this.ComboBackupKeep.SelectedItem);
        ShowBackupState(backups);
      };
      // An hourly backup can finish while Settings is open, on another thread.
      System.Action backupsChanged = () => this.Dispatcher.BeginInvoke(new System.Action(() => ShowBackupState(backups)));
      backups.Changed += backupsChanged;
      this.Closed += (s, e) => backups.Changed -= backupsChanged;

      this.ButtonApply.Click += (s, e) => {
        try {
          // Another app holds the new shortcut: stay open and show the red hint.
          if (!this.recorder.Apply()) {
            this.PageItemKeyboard.IsSelected = true;
            return;
          }
        } catch (System.Exception ex) {
          ErrorDialog.ShowDialog(ex);
          return;
        }
        try {
          bool remember = this.CheckRememberScreenSetups.IsChecked == true;
          if (remember != AppServices.Current.ScreenSetups.Enabled) {
            AppServices.Current.ScreenSetups.SetEnabled(remember);
          }
        } catch (System.Exception ex) {
          ErrorDialog.ShowDialog(ex);
          return;
        }
        try {
          WindowThemeChoice theme = ChosenWindowTheme();
          if (theme != AppServices.Current.WindowTheme.Choice) {
            AppServices.Current.WindowTheme.SetChoice(theme);
          } else {
            AppServices.Current.WindowTheme.Preview(null);
          }
        } catch (System.Exception ex) {
          ErrorDialog.ShowDialog(ex);
          return;
        }
        try {
          bool on = this.CheckBackupOn.IsChecked == true;
          string folder = this.TextBackupFolder.Text;
          int keep = (int)this.ComboBackupKeep.SelectedItem;
          if (on != backups.IsOn || folder != backups.Folder || keep != backups.Keep) {
            backups.Save(on, folder, keep);
          }
        } catch (System.Exception ex) {
          ErrorDialog.ShowDialog(ex);
          return;
        }
        try {
          int keep = (int)this.ComboTextHistoryKeep.SelectedItem;
          if (keep != TextHistorySetting.Keep) {
            TextHistorySetting.SetKeep(keep);
          }
        } catch (System.Exception ex) {
          ErrorDialog.ShowDialog(ex);
          return;
        }
        this.closeWithoutApply = false;
        Close();
      };
    }

    private IEnumerable<ListBoxItem> PageItems => this.ListSettingsPages.Items.OfType<ListBoxItem>();

    // Selects the page with this name, else the first page (Appearance).
    private void ShowPage(string name) {
      this.ListSettingsPages.SelectedItem = PageItems.FirstOrDefault(i => (string)i.Content == name) ?? PageItems.First();
    }

    private static string ReadSavedPage() {
      try {
        return DataStore.AppSettings.Get(PageSettingKey);
      } catch (System.Exception ex) {
        AppLog.Warning(ex, "The Settings page could not be read; Settings opens on Appearance");
        return null;
      }
    }

    private void SavePage() {
      try {
        if (this.ListSettingsPages.SelectedItem is ListBoxItem item) {
          DataStore.AppSettings.Set(PageSettingKey, (string)item.Content);
        }
      } catch (System.Exception ex) {
        AppLog.Warning(ex, "The Settings page could not be saved");
      }
    }

    // While recording, every key goes to the recorder, so Space or Enter don't click the box again.
    private void ShortcutBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e) {
      (Common.HotkeyModifiers modifiers, int key) = KeyInput.ToHotkey(e.Key, e.SystemKey, KeyboardModifiers());
      if (this.recorder.KeyPressed(modifiers, key)) {
        e.Handled = true;
      }
    }

    // "Last backup: ..." and, after a failure, the red line.
    private void ShowBackupState(BackupService backups) {
      this.TextLastBackup.Text = backups.LastBackupText;
      string error = backups.LastErrorText;
      this.TextBackupError.Text = error ?? "";
      this.TextBackupError.ToolTip = error;
      this.TextBackupError.Visibility = error == null ? Visibility.Collapsed : Visibility.Visible;
    }

    private void ShowWindowTheme(WindowThemeChoice choice) {
      this.RadioThemeSameAsWindows.IsChecked = choice == WindowThemeChoice.SameAsWindows;
      this.RadioThemeLight.IsChecked = choice == WindowThemeChoice.Light;
      this.RadioThemeDark.IsChecked = choice == WindowThemeChoice.Dark;
    }

    private WindowThemeChoice ChosenWindowTheme() {
      if (this.RadioThemeLight.IsChecked == true) {
        return WindowThemeChoice.Light;
      }
      return this.RadioThemeDark.IsChecked == true ? WindowThemeChoice.Dark : WindowThemeChoice.SameAsWindows;
    }

    // The window icon and the title bar's icon (the gear) and close glyph, in the window's icon set.
    private void ShowIcons() {
      this.Icon = GlyphIcon.Create(this.icons.Settings);
      this.TitleBar.Glyph = this.icons.Settings;
      this.TitleBar.CloseGlyph = this.icons.Times;
    }
  }
}
