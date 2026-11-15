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
  /// The shortcut, the screen setups setting and the window theme are app-wide and change only on Apply.
  /// </summary>
  public partial class SettingsWindow : Window {
    private readonly IconGlyphs icons;
    private bool closeWithoutApply = true;
    private readonly ShortcutRecorder recorder;

    /// <summary>The AppSettings key of the page Settings opens on: the page's name, for example "Keyboard".</summary>
    public const string PageSettingKey = "SettingsPage";

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
      // Every outer margin of the content and of the Apply bar equals the Apply button height, like in the Note window.
      this.ButtonApply.SizeChanged += (s, e) => {
        Thickness margin = new Thickness(this.ButtonApply.ActualHeight);
        this.PanelBody.Margin = margin;
        this.FooterContent.Margin = margin;
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
