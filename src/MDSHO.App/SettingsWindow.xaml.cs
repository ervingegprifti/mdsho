using System.Windows;
using MDSHO.Helpers;
using MDSHO.ViewModels;

namespace MDSHO {

  /// <summary>
  /// A panel's Settings window. Changes show live; closing without Apply undoes them.
  /// The shortcut and the screen setups setting are app-wide and change only on Apply.
  /// </summary>
  public partial class SettingsWindow : Window {
    private readonly IconGlyphs icons;
    private bool closeWithoutApply = true;
    private readonly ShortcutRecorder recorder;

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

      this.ButtonApply.Click += (s, e) => {
        try {
          // Another app holds the new shortcut: stay open and show the red hint.
          if (!this.recorder.Apply()) {
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
        this.closeWithoutApply = false;
        Close();
      };
    }

    // While recording, every key goes to the recorder, so Space or Enter don't click the box again.
    private void ShortcutBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e) {
      (Common.HotkeyModifiers modifiers, int key) = KeyInput.ToHotkey(e.Key, e.SystemKey, KeyboardModifiers());
      if (this.recorder.KeyPressed(modifiers, key)) {
        e.Handled = true;
      }
    }

    // The window icon and the title bar's icon (the gear) and close glyph, in the window's icon set.
    private void ShowIcons() {
      this.Icon = GlyphIcon.Create(this.icons.Settings);
      this.TitleBar.Glyph = this.icons.Settings;
      this.TitleBar.CloseGlyph = this.icons.Times;
    }
  }
}
