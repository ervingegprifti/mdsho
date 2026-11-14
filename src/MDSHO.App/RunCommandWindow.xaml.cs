using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using MDSHO.Helpers;
using MDSHO.ViewModels;

namespace MDSHO {

  /// <summary>
  /// The Run Command window: edits the name (in the title), program, working folder, arguments and options of one run
  /// command. Nothing is saved until Save; Cancel, the X button and Esc close it without saving.
  /// </summary>
  public partial class RunCommandWindow : Window {
    private readonly RunCommandViewModel viewModel;
    private readonly TitleRename titleRename;

    /// <summary>The tooltip of the command's title.</summary>
    public const string RenameTitleToolTip = "Double-click to rename the command";

    /// <summary>Creates the window for a run command.</summary>
    public RunCommandWindow(RunCommandViewModel viewModel) {
      InitializeComponent();
      this.viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
      this.DataContext = viewModel;
      // The title is the command's name; a rename from it is kept in the window until Save.
      ShowTitle();
      viewModel.PropertyChanged += (s, e) => {
        if (e.PropertyName == nameof(RunCommandViewModel.Name)) { ShowTitle(); }
      };
      this.titleRename = new TitleRename(this, this.TextTitle, this.TextTitleEdit, RenameTitleToolTip,
        () => this.viewModel.Name, name => this.viewModel.Name = name,
        () => { if (this.IsActive) { this.ComboProgram.Focus(); } });
      // The window icon is the run glyph of the command's window (solid or outline) and follows that setting while open.
      IconGlyphs icons = viewModel.Icons;
      this.Icon = GlyphIcon.Create(icons.Run);
      System.ComponentModel.PropertyChangedEventHandler iconsChanged = (s, e) => {
        if (e.PropertyName == nameof(IconGlyphs.Run)) {
          this.Icon = GlyphIcon.Create(icons.Run);
        }
      };
      icons.PropertyChanged += iconsChanged;
      this.Closed += (s, e) => icons.PropertyChanged -= iconsChanged;

      this.ButtonExamples.Click += (s, e) => ShowExamples();
      this.ButtonClose.Click += (s, e) => Close();
      this.ButtonBrowseProgram.Click += (s, e) => BrowseProgram();
      this.ButtonBrowseFolder.Click += (s, e) => BrowseFolder();
      this.ButtonTestRun.Click += (s, e) => this.viewModel.TestRun();
      this.ButtonSave.Click += ButtonSave_Click;
      this.ButtonCancel.Click += (s, e) => Close();
      this.PreviewKeyDown += Window_PreviewKeyDown;
      this.StateChanged += (s, e) => KeepContentInsideWhenMaximized();
      // Every outer margin of the fields and of the bottom bar equals the button height, like in Settings and the Note window.
      this.ButtonSave.SizeChanged += (s, e) => {
        Thickness margin = new Thickness(this.ButtonSave.ActualHeight);
        this.BodyContent.Margin = margin;
        this.FooterContent.Margin = margin;
      };
    }

    /// <summary>True while the command's title is being renamed.</summary>
    public bool IsRenamingTitle => this.titleRename.IsRenaming;

    /// <summary>Opens the edit box over the title, with the name selected.</summary>
    public void BeginRenameTitle() {
      this.titleRename.Begin();
    }

    /// <summary>Closes the title's edit box; with <paramref name="save"/> a changed, non-blank name is kept until Save.</summary>
    public void EndRenameTitle(bool save) {
      this.titleRename.End(save);
    }

    /// <summary>True when the user saved; false after Cancel, X or Esc.</summary>
    public bool Saved { get; private set; }

    /// <summary>
    /// Opens the window as a dialog for a run command, centred on the screen of the MDSHO window the command belongs to.
    /// </summary>
    /// <param name="shortcut">The run command.</param>
    /// <param name="isNewCommand">True when the command was just created (cmd.exe and "keep window open" are preset).</param>
    public static void ShowFor(ShortcutViewModel shortcut, bool isNewCommand) {
      try {
        Create(shortcut, isNewCommand, FindOwner(shortcut)).ShowDialog();
      } catch (Exception ex) {
        ErrorDialog.ShowDialog(ex);
      }
    }

    /// <summary>Whether a window is the active one. Tests replace it, since off-screen test windows are never activated.</summary>
    public static Func<Window, bool> IsActiveWindow = window => window.IsActive;

    /// <summary>
    /// The window to open over, which is the MDSHO window the user clicked in: the active one of the command's window and
    /// its clones (a clone shows the same buttons and can be on another screen). Else the command's own window, else any
    /// open clone of it, else the active window, else null.
    /// </summary>
    public static Window FindOwner(ShortcutViewModel shortcut) {
      Window[] open = Application.Current?.Windows.OfType<Window>().Where(w => w.IsVisible).ToArray() ?? new Window[0];
      DesktopWindowViewModel owningWindow = shortcut?.OwningWindow;
      if (owningWindow != null) {
        Window[] family = open.Where(w => w.DataContext is DesktopWindowViewModel vm && (vm == owningWindow || owningWindow.Clones.Contains(vm))).ToArray();
        Window found = family.FirstOrDefault(IsActiveWindow)
          ?? family.FirstOrDefault(w => w.DataContext == owningWindow)
          ?? family.FirstOrDefault();
        if (found != null) {
          return found;
        }
      }
      return open.FirstOrDefault(IsActiveWindow);
    }

    /// <summary>
    /// Creates the window for a run command without showing it. With an owner it is centred in the work area of the
    /// owner's screen; without one it is centred on the primary screen.
    /// </summary>
    public static RunCommandWindow Create(ShortcutViewModel shortcut, bool isNewCommand, Window owner) {
      RunCommandWindow window = new RunCommandWindow(new RunCommandViewModel(shortcut, isNewCommand));
      if (owner != null) {
        DialogPlacement.CenterOnScreenOf(window, owner);
      } else {
        window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
      }
      return window;
    }

    /// <summary>Creates the Examples popup, owned by this window and centred on this window's screen, without showing it.</summary>
    public RunCommandExamplesWindow CreateExamples() {
      RunCommandExamplesWindow examples = new RunCommandExamplesWindow(this.viewModel);
      // Centred on this window, at 80 % of its size (never below the popup's minimum size).
      DialogPlacement.CenterOnWindow(examples, this, ExamplesSizeRatio);
      return examples;
    }

    /// <summary>The Examples popup opens at this share of the Run command window's width and height.</summary>
    public const double ExamplesSizeRatio = 0.8;

    private void ShowTitle() {
      this.Title = this.viewModel.Name;
#if DEBUG
      this.Title += " DEBUG";
#endif
    }

    private void ButtonSave_Click(object sender, RoutedEventArgs e) {
      // A name still being typed in the title counts.
      EndRenameTitle(save: true);
      this.viewModel.Save();
      this.Saved = true;
      Close();
    }

    // Esc works like Cancel (a ComboBox list that is open closes first). While renaming the title, Enter and Esc only
    // close the edit box.
    private void Window_PreviewKeyDown(object sender, KeyEventArgs e) {
      if (this.titleRename.HandleKey(e)) {
        return;
      }
      if (e.Key == Key.Escape && !this.ComboProgram.IsDropDownOpen) {
        e.Handled = true;
        Close();
      }
    }

    // Maximising is a double-click on the title bar (there is no maximise button).
    private void KeepContentInsideWhenMaximized() {
      bool maximized = this.WindowState == WindowState.Maximized;
      // A maximised borderless window reaches past the screen edges by the resize border: keep the content inside.
      this.RootGrid.Margin = maximized ? new Thickness(7) : new Thickness(0);
    }

    private void ShowExamples() {
      CreateExamples().ShowDialog();
    }

    private void BrowseProgram() {
      Microsoft.Win32.OpenFileDialog dialog = new Microsoft.Win32.OpenFileDialog {
        Title = "Choose a program",
        Filter = "Programs (*.exe)|*.exe|All files (*.*)|*.*",
        CheckFileExists = true
      };
      if (dialog.ShowDialog(this) == true) {
        this.viewModel.Program = dialog.FileName;
      }
    }

    private void BrowseFolder() {
      Microsoft.Win32.OpenFolderDialog dialog = new Microsoft.Win32.OpenFolderDialog { Title = "Choose the working folder" };
      if (dialog.ShowDialog(this) == true) {
        this.viewModel.WorkingFolder = dialog.FolderName;
      }
    }
  }
}
