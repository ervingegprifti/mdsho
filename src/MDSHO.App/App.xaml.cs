using MDSHO.Data;
using MDSHO.Helpers;
using MDSHO.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

// Creating a background application with WPF
// https://www.thomasclaudiushuber.com/2015/08/22/creating-a-background-application-with-wpf/

namespace MDSHO
{
  /// <summary>
  /// Interaction logic for App.xaml
  /// </summary>
  public partial class App : Application {
    private System.Windows.Forms.NotifyIcon notifyIcon;
    private bool exitApplication;
    private IHost host;
    private WindowFactory windows;

    /// <summary>Connects the WPF-free view models to WPF (command requery, message boxes) once per process.</summary>
    static App() {
      WpfBridge.Connect();
    }

    /// <summary>
    /// The application datacontext.
    /// </summary>
    public AppViewModel DataContext { get; set; }

    /// <summary>
    /// False only in the test host: WPF raises OnStartup as soon as the dispatcher runs, even without Application.Run, and
    /// the real startup must never run inside the tests (it would rename and open the real app data and replace the test services).
    /// </summary>
    public static bool StartupEnabled { get; set; } = true;

    /// <summary>Starts MDSHO: makes sure only one copy runs, upgrades the database, opens every saved window and creates the tray icon.</summary>
    protected override void OnStartup(StartupEventArgs e) {
      base.OnStartup(e);
      if (!StartupEnabled) {
        return;
      }
      try {
        // Events
        UnhandledExceptionsEvents();

        if (!AcquireSingleInstanceMutex()) {
          string message = "MDSHO seems to be running already. Please check the taskbar.";
          MessageBox.Show(message, "MDSHO already running", MessageBoxButton.OK, MessageBoxImage.Information);
          Application.Current.Shutdown();
          return;
        }

        // Data saved under the names used before 1.1.2 (MDSHO.sqlite3, MDSHOShortcuts) gets the new names first: before
        // the logger looks for the app data folder (that creates the new shortcuts folder) and before the database opens.
        Data.DataNamesReport renamedData = Data.DataNamesUpgrade.RenameOldNamesInAppDataFolder();

        // The composition root: the host owns the services and the log file.
        this.host = AppHost.Build(CreateFileLogger());
        AppHost.UseServices(this.host.Services);
        this.windows = this.host.Services.GetRequiredService<WindowFactory>();
        LogStartup();
        renamedData.WriteToLog();

        if (!DatabaseUpdate()) {
          // The database was created by a newer MDSHO: the user was told to update. Exit without touching the data.
          Application.Current.Shutdown();
          return;
        }

        // New run commands and "Set run command..." open the Run Command window.
        ShortcutViewModel.OpenRunCommandEditor = RunCommandWindow.ShowFor;
        ShortcutViewModel.OpenCommentEditor = TextEditWindow.ShowCommentFor;
        ShortcutViewModel.OpenNoteEditor = TextEditWindow.ShowNoteFor;
        ShortcutViewModel.ToggleNoteEditor = TextEditWindow.ToggleNoteFor;

        // Get the data context for the application.
        this.DataContext = this.host.Services.GetRequiredService<AppViewModel>();

        SetWindows();

        // Note windows that were open come back where they were; at exit (also when Windows shuts down or restarts)
        // unsaved typing in them is saved and they stay marked as open.
        TextEditWindow.ReopenOpenNotes(this.DataContext);
        this.SessionEnding += (s, args) => TextEditWindow.SaveOpenNotesForExit();

        this.MainWindow = this.windows.Main();
        MainWindow.Closing += MainWindow_Closing;
        Current.ShutdownMode = ShutdownMode.OnMainWindowClose;

        this.notifyIcon = new System.Windows.Forms.NotifyIcon();
        this.notifyIcon.DoubleClick += (s, args) => ShowAboutWindow();
        this.notifyIcon.Icon = MDSHO.Properties.Resources.logo;
        this.notifyIcon.Visible = true;

        CreateContextMenu();
      } catch (Exception ex) {
        ErrorDialog.ShowDialog(ex);
      }
    }


    #region GLOBAL EXCEPTION HANDLING
    // https://stackoverflow.com/questions/1472498/wpf-global-exception-handler
    // https://stackoverflow.com/questions/793100/globally-catch-exceptions-in-a-wpf-application
    private void UnhandledExceptionsEvents() {
      AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
      // Only one UI-thread handler: Dispatcher.UnhandledException and Application.DispatcherUnhandledException fire for the same exception.
      Application.Current.DispatcherUnhandledException += Current_DispatcherUnhandledException;
      TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
    }
    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e) {
      try {
        LogUnhandledException("AppDomain.CurrentDomain.UnhandledException", e.ExceptionObject as Exception ?? new Exception(Convert.ToString(e.ExceptionObject)));
      } finally {
        if (e.IsTerminating) {
          // The process is about to end: write what is buffered.
          Common.AppLog.CloseAndFlush();
        }
      }
    }
    private void Current_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e) {
      LogUnhandledException("Application.Current.DispatcherUnhandledException", e.Exception);
      e.Handled = true;
    }
    private void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e) {
      LogUnhandledException("TaskScheduler.UnobservedTaskException", e.Exception);
      e.SetObserved();
    }
    private void LogUnhandledException(string source, Exception exception) {
      // Logs the source and the exception, then shows "Something went wrong." with the exception's message.
      ErrorDialog.ShowUnhandled(source, exception);
    }
    #endregion GLOBAL EXCEPTION HANDLING

    /// <summary>
    /// Creates the logger of the log files in the app data folder's logs folder (the folder "Browse app data" opens) and
    /// sends AppLog to it. Never stops startup: without a logs folder MDSHO runs without file logging (returns null).
    /// </summary>
    private static Serilog.Core.Logger CreateFileLogger() {
      try {
        Common.IPackageIdentity package = new Infrastructure.PackageIdentity();
        string appDataFolder = new Infrastructure.AppDataLocator(package).Locate();
        Serilog.Core.Logger logger = Infrastructure.Logging.AppLogging.CreateFileLogger(appDataFolder, folder => System.IO.Directory.CreateDirectory(folder));
        if (logger != null) {
          Common.AppLog.Writer = new Infrastructure.Logging.SerilogAppLogWriter(logger);
        }
        return logger;
      } catch (Exception ex) {
        Debug.WriteLine(ex);
        return null;
      }
    }

    /// <summary>Writes the startup line: what is running and where its data is.</summary>
    private static void LogStartup() {
      try {
        string appDataFolder = AppServices.Current.AppData.Locate();
        Infrastructure.Logging.AppLogging.LogStartup(typeof(App).Assembly.GetName().Version.ToString(), Environment.OSVersion.VersionString,
          Environment.Is64BitProcess, AppServices.Current.Package.IsPackaged, appDataFolder);
      } catch (Exception ex) {
        Debug.WriteLine(ex);
      }
    }


    /// <summary>Upgrades the database. Returns false when the database is newer than this app (startup must stop).</summary>
    private bool DatabaseUpdate() {
      try {
        return DatabaseStartup.TryUpgrade(
          () => {
            this.host.Services.GetRequiredService<Core.IDataStore>().Upgrade();
            // Stored paths of dropped .lnk/.url files still pointing into the old shortcuts folder.
            Data.DataNamesUpgrade.FixShortcutTargetsInAppDataFolder();
          },
          (message, caption) => MessageBox.Show(message, caption, MessageBoxButton.OK, MessageBoxImage.Warning));
      } catch (Exception ex) {
        ErrorDialog.ShowDialog(ex);
        return true;
      }
    }

    private void SetWindows() {
      try {
        foreach (DesktopWindowViewModel desktopWindow in DataContext.Windows) {
          DesktopWindowView windowView = this.windows.DesktopWindow(desktopWindow);
          windowView.Show();
          // Create the DesktopWindowView clones for this DesktopWindowView if any.
          foreach (DesktopWindowViewModel cloneWindow in desktopWindow.Clones) {
            DesktopWindowView cloneView = this.windows.DesktopWindow(cloneWindow);
            cloneView.Show();
          }
        }
      } catch (Exception ex) {
        ErrorDialog.ShowDialog(ex);
      }
    }

    private void MainWindow_Closing(object sender, CancelEventArgs e) {
      try {
        if (!exitApplication) {
          e.Cancel = true;
          // A hidden window can be shown again, a closed one not.
          MainWindow.Hide();
        }
      } catch (Exception ex) {
        ErrorDialog.ShowDialog(ex);
      }
    }


    #region CONTEXT MENU
    /// <summary>The icon set (solid or outline) of the first desktop window, which the tray menu and About from the tray follow; solid when there is no window.</summary>
    public static IconGlyphs FirstWindowIcons(AppViewModel appViewModel) {
      DesktopWindowViewModel first = appViewModel?.Windows.FirstOrDefault();
      return first?.Appearance?.Icons ?? new IconGlyphs(true);
    }
    /// <summary>The font icon of each tray menu item, by its text. Start with Windows has none: Windows draws its check mark in the icon spot.</summary>
    private static readonly (string Text, Func<IconGlyphs, string> Glyph)[] TrayIcons = {
      ("New window", g => g.Plus),
      ("Find MDSHO lost windows", g => g.Desktop),
      ("Find other lost windows", g => g.Desktop),
      ("Export data...", g => g.Send),
      ("Import data...", g => g.Database),
      ("Browse app data", g => g.Folder),
      ("About", g => g.Info),
      ("Exit application", g => g.Times),
    };
    /// <summary>Draws the tray menu's icons again from <paramref name="icons"/> (solid or outline); items already showing the right glyph are left alone.</summary>
    public static void UpdateTrayIcons(System.Windows.Forms.ContextMenuStrip menu, IconGlyphs icons) {
      icons ??= new IconGlyphs(true);
      foreach ((string text, Func<IconGlyphs, string> glyphOf) in TrayIcons) {
        System.Windows.Forms.ToolStripItem item = menu.Items.Cast<System.Windows.Forms.ToolStripItem>().Single(i => i.Text == text);
        string glyph = glyphOf(icons);
        if (item.Image != null && Helpers.GlyphIcon.GlyphOf(item.Image) == glyph) {
          continue;
        }
        System.Drawing.Image old = item.Image;
        item.Image = Helpers.GlyphIcon.ToBitmap(glyph, menu.ImageScalingSize.Height);
        old?.Dispose();
      }
    }
    /// <summary>The tray menu; its icons follow the first desktop window's solid/outline setting.</summary>
    public static System.Windows.Forms.ContextMenuStrip CreateTrayMenu(App app) {
      return CreateTrayMenu(app, () => FirstWindowIcons((Application.Current as App)?.DataContext));
    }
    /// <summary>
    /// Builds the tray menu. Static so tests can check its items without a running App; the click handlers use <paramref name="app"/>.
    /// <paramref name="icons"/> gives the icon set for the menu icons; it is asked again each time the menu opens.
    /// </summary>
    public static System.Windows.Forms.ContextMenuStrip CreateTrayMenu(App app, Func<IconGlyphs> icons) {
      System.Windows.Forms.ContextMenuStrip menu = new System.Windows.Forms.ContextMenuStrip();
      menu.Items.Add("New window").Click += (s, e) => app.NewWindow(null);
      menu.Items.Add("Find MDSHO lost windows").Click += (s, e) => app.FindWindows();
      menu.Items.Add("Find other lost windows").Click += (s, e) => FindOtherLostWindows();
      Common.AutoStartController autoStartController = new Common.AutoStartController(AppServices.Current.AutoStart, message => MessageBox.Show(message, Common.AutoStartController.MessageTitle, MessageBoxButton.OK, MessageBoxImage.Information));
      System.Windows.Forms.ToolStripMenuItem autoStartMenuItem = new System.Windows.Forms.ToolStripMenuItem("Start with Windows") { CheckOnClick = false };
      autoStartMenuItem.Click += (s, e) => autoStartController.Toggle();
      menu.Opening += (s, e) => autoStartMenuItem.Checked = autoStartController.IsChecked();
      menu.Items.Add(autoStartMenuItem);
      menu.Items.Add("Export data...").Click += (s, e) => app.ExportData();
      menu.Items.Add("Import data...").Click += (s, e) => app.ImportData();
      menu.Items.Add("Browse app data").Click += (s, e) => BrowseAppData();
      System.Windows.Forms.ToolStripItem aboutMenuItem = menu.Items.Add("About");
      aboutMenuItem.Click += (s, e) => app.ShowAboutWindow();
      menu.Items.Add("Exit application").Click += (s, e) => app.ExitApplication(silent: false);
      UpdateTrayIcons(menu, icons());
      menu.Opening += (s, e) => UpdateTrayIcons(menu, icons());
      return menu;
    }
    /// <summary>Opens Explorer on the folder that really holds the database and the shortcuts (the package's private folder when Windows redirected it).</summary>
    public static void BrowseAppData() {
      try {
        AppServices.Current.BrowseAppData();
      } catch (Exception ex) {
        ErrorDialog.ShowDialog(ex);
      }
    }
    private void CreateContextMenu() {
      try {
        this.notifyIcon.ContextMenuStrip = CreateTrayMenu(this);
      } catch (Exception ex) {
        ErrorDialog.ShowDialog(ex);
      }
    }
    /// <summary>Creates a new window (saved in the database) in the centre of the given screen, or of the primary screen when null, and shows it.</summary>
    public void NewWindow(Common.ScreenInfo screen) {
      try {
        AppViewModel appViewModel = ((App)Application.Current).DataContext;
        if (screen == null) {
          screen = AppServices.Current.Screens.PrimaryScreen();
        }
        string screenDeviceName = screen.DeviceName;
        // Screen.Bounds are physical pixels, the window values are DIPs: convert with that monitor's own scale.
        Common.ScreenPoint center = Common.ScreenGeometry.CenterNewWindow(screen);
        int centerLeft = (int)center.X;
        int centerTop = (int)center.Y;
        DesktopWindowViewModel desktopWindow = appViewModel.AddWindow(screenDeviceName, centerLeft, centerTop);
        DesktopWindowView windowView = this.windows.DesktopWindow(desktopWindow);
        windowView.Show();
      } catch (Exception ex) {
        ErrorDialog.ShowDialog(ex);
      }
    }
    /// <summary>
    /// Opens the About dialog. Its icon uses <paramref name="icons"/> (the set of the window whose title menu opened it),
    /// or the first window's set when null (opened from the tray).
    /// </summary>
    public void ShowAboutWindow(IconGlyphs icons = null) {
      try {
        AboutWindow aboutWindow = this.windows.About();
        aboutWindow.UseIcons(icons ?? FirstWindowIcons(this.DataContext));
        aboutWindow.ShowDialog();
      } catch (Exception ex) {
        ErrorDialog.ShowDialog(ex);
      }
    }
    /// <summary>Gathers every window and clone to the middle of the primary screen, slightly offset from each other, so none is lost off screen.</summary>
    public void FindWindows() {
      try {
        AppViewModel appViewModel = ((App)Application.Current).DataContext;
        foreach (DesktopWindowViewModel desktopWindow in appViewModel.Windows) {
          RestoreWindowState(desktopWindow);
          foreach (DesktopWindowViewModel cloneWindow in desktopWindow.Clones) {
            RestoreWindowState(cloneWindow);
          }
        }
        appViewModel.FindWindows(AppServices.Current.Screens.PrimaryScreen());
      } catch (Exception ex) {
        ErrorDialog.ShowDialog(ex);
      }
    }
    /// <summary>
    /// Find other lost windows: moves every window of another app whose title bar can't be grabbed on any screen to the
    /// screen with the mouse pointer, then says how many moved and how many Windows refused.
    /// </summary>
    public static void FindOtherLostWindows() {
      try {
        AppServices services = AppServices.Current;
        Common.LostWindowsResult result = Common.LostWindows.Rescue(services.OtherWindows, services.Screens.AllScreens(), services.Screens.ScreenAtCursor());
        Common.AppLog.Information("Find other lost windows moved {Moved} windows and skipped {Skipped}", result.Moved, result.Skipped);
        Dialogs.ShowMessage(result.Message, "Find other lost windows");
      } catch (Exception ex) {
        ErrorDialog.ShowDialog(ex);
      }
    }
    /// <summary>A minimized or maximized window goes back to normal, so the position Find MDSHO lost windows gives it shows.</summary>
    private static void RestoreWindowState(DesktopWindowViewModel desktopWindow) {
      Window window = WindowLookup.GetViewFromViewModel(desktopWindow);
      if (window != null) {
        window.WindowState = WindowState.Normal;
      }
    }
    /// <summary>
    /// Exit the application.
    /// </summary>
    /// <param name="silent">If true then do not disturb the user with the confirmation question.</param>
    public void ExitApplication(bool silent) {
      try {
        if (silent) {
          ExitApplication();
        } else {
          MessageBoxResult messageBoxResult = MessageBox.Show("Are you sure you want to exit MDSHO?", "Exit MDSHO", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
          if (messageBoxResult == MessageBoxResult.Yes) {
            ExitApplication();
          }
        }
      } catch (Exception ex) {
        ErrorDialog.ShowDialog(ex);
      }
    }
    #endregion CONTEXT MENU

    #region EXPORT / IMPORT
    private bool restartAfterExit;

    /// <summary>Asks for a zip file and exports all MDSHO data (windows, clones, shortcuts and shortcut files) to it.</summary>
    public void ExportData() {
      try {
        Microsoft.Win32.SaveFileDialog dialog = new Microsoft.Win32.SaveFileDialog {
          Title = "Export MDSHO data",
          Filter = "MDSHO export (*.zip)|*.zip",
          DefaultExt = ".zip",
          AddExtension = true,
          FileName = DataExporter.DefaultFileName(DateTime.Now)
        };
        if (dialog.ShowDialog() != true) {
          return;
        }
        ExportManifest manifest = DataExporter.Export(dialog.FileName);
        string message = "Exported " + manifest.Counts.Windows + " windows, " + manifest.Counts.Clones + " clones and " + manifest.Counts.Shortcuts + " shortcuts to:\n" + dialog.FileName;
        if (manifest.MissingFiles.Count > 0) {
          message += "\n\n" + manifest.MissingFiles.Count + " shortcut file(s) did not exist and were not included.";
        }
        MessageBox.Show(message, "Export MDSHO data", MessageBoxButton.OK, MessageBoxImage.Information);
      } catch (Exception ex) {
        ErrorDialog.ShowDialog("The data could not be exported.", ex);
      }
    }

    /// <summary>Asks for a zip file made by Export data, lets the user pick Replace or Add as new windows, imports and restarts MDSHO.</summary>
    public void ImportData() {
      try {
        Microsoft.Win32.OpenFileDialog dialog = new Microsoft.Win32.OpenFileDialog {
          Title = "Import MDSHO data",
          Filter = "MDSHO export (*.zip)|*.zip",
          CheckFileExists = true
        };
        if (dialog.ShowDialog() != true) {
          return;
        }
        ExportManifest manifest;
        try {
          manifest = DataImporter.Inspect(dialog.FileName);
        } catch (DataPortabilityException ex) {
          ErrorDialog.ShowDialog(ex.Message, ex);
          return;
        }
        string question =
          "This file contains " + manifest.Counts.Windows + " windows, " + manifest.Counts.Clones + " clones, " + manifest.Counts.Shortcuts + " shortcuts, exported " +
          manifest.ExportedAtUtc.ToLocalTime().ToString("g") + " from MDSHO " + manifest.AppVersion + ".\n\n" +
          "Yes = Replace: replace ALL current windows and shortcuts with this file. A backup of your current data is saved first.\n" +
          "No = Add as new windows: keep your current data and add the imported windows next to it.\n" +
          "Cancel = do nothing.";
        MessageBoxResult answer = MessageBox.Show(question, "Import MDSHO data", MessageBoxButton.YesNoCancel, MessageBoxImage.Question, MessageBoxResult.Yes);
        if (answer == MessageBoxResult.Cancel || answer == MessageBoxResult.None) {
          return;
        }
        ImportMode mode = answer == MessageBoxResult.Yes ? ImportMode.Replace : ImportMode.AddAsNew;
        ImportResult result;
        try {
          result = DataImporter.Import(dialog.FileName, mode);
        } catch (DataPortabilityException ex) {
          ErrorDialog.ShowDialog(ex.Message, ex);
          return;
        } catch (Exception ex) {
          ErrorDialog.ShowDialog("The data could not be imported. Nothing was changed.", ex);
          return;
        }
        string done = "The data was imported.";
        if (result.BackupPath != null) {
          done += "\nYour previous data was saved to:\n" + result.BackupPath;
        }
        done += "\n\nMDSHO will now restart to load the imported data.";
        MessageBox.Show(done, "Import MDSHO data", MessageBoxButton.OK, MessageBoxImage.Information);
        RestartApplication();
      } catch (Exception ex) {
        ErrorDialog.ShowDialog(ex);
      }
    }

    /// <summary>Exits and starts the same program again once this instance has released the single instance mutex (see OnExit).</summary>
    private void RestartApplication() {
      this.restartAfterExit = true;
      ExitApplication();
    }
    #endregion EXPORT / IMPORT


    private void ExitApplication() {
      TextEditWindow.SaveOpenNotesForExit();
      this.notifyIcon.Dispose();
      this.notifyIcon = null;
      this.exitApplication = true;
      MainWindow.Close();
    }

    #region SINGLE INSTANCE
    // One instance per user session and per database (Debug and Release use different databases), regardless of the version.
#if DEBUG
    private const string SingleInstanceMutexName = @"Local\MDSHO-Debug-6F1C2A4E-8B3D-4E5F-9A7B-2C4D6E8F0A1B";
#else
    private const string SingleInstanceMutexName = @"Local\MDSHO-3E9B7D21-5C4A-4F86-B1D0-7A2E9C5F8B63";
#endif
    private Mutex singleInstanceMutex;
    private bool ownsSingleInstanceMutex;

    /// <summary>
    /// Try to acquire the single instance mutex. It is held for the application lifetime and released in OnExit.
    /// </summary>
    /// <returns>Return true if this is the only running instance, false if another instance already owns the mutex.</returns>
    private bool AcquireSingleInstanceMutex() {
      this.singleInstanceMutex = new Mutex(false, SingleInstanceMutexName);
      try {
        this.ownsSingleInstanceMutex = this.singleInstanceMutex.WaitOne(0, false);
      } catch (AbandonedMutexException) {
        // The previous instance ended without releasing the mutex; this instance now owns it.
        this.ownsSingleInstanceMutex = true;
      }
      return this.ownsSingleInstanceMutex;
    }

    /// <summary>Releases the single instance lock and, after an import, starts MDSHO again.</summary>
    protected override void OnExit(ExitEventArgs e) {
      Common.AppLog.Information(this.restartAfterExit ? "MDSHO exited and restarts to load the imported data" : "MDSHO exited");
      // Close the log file before a restarted MDSHO opens it, then dispose the host (and the services it owns).
      Common.AppLog.CloseAndFlush();
      Common.IShellLauncher shell = AppServices.Current.Shell;
      Common.IPackageIdentity package = AppServices.Current.Package;
      try {
        this.host?.Dispose();
      } catch (Exception ex) {
        Debug.WriteLine(ex);
      }
      this.host = null;
      if (this.singleInstanceMutex != null) {
        if (this.ownsSingleInstanceMutex) {
          this.singleInstanceMutex.ReleaseMutex();
          this.ownsSingleInstanceMutex = false;
        }
        this.singleInstanceMutex.Dispose();
        this.singleInstanceMutex = null;
      }
      if (this.restartAfterExit) {
        this.restartAfterExit = false;
        try {
          string family = package.FamilyName;
          Common.RestartCommand command = Common.RestartCommand.Build(family != null, family, Environment.ProcessPath);
          if (command != null) {
            shell.Run(command.FileName, command.Arguments);
          }
        } catch (Exception ex) {
          Debug.WriteLine(ex);
        }
      }
      base.OnExit(e);
    }
    #endregion SINGLE INSTANCE

  }

}
