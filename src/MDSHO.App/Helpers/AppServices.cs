using MDSHO.Common;
using MDSHO.Infrastructure;
using MDSHO.ViewModels;

namespace MDSHO.Helpers {

  /// <summary>
  /// The Windows services the UI code uses (windows, dialogs and the tray menu). The composition root sets
  /// <see cref="Current"/> once at startup; tests replace single services with fakes so nothing real is opened.
  /// </summary>
  public sealed class AppServices {

    /// <summary>The services in use. Starts as the real Infrastructure services.</summary>
    public static AppServices Current { get; set; } = Defaults();

    /// <summary>Processes, web links, Explorer folders.</summary>
    public IShellLauncher Shell { get; set; }
    /// <summary>MSIX package identity.</summary>
    public IPackageIdentity Package { get; set; }
    /// <summary>The folder "Browse app data" opens.</summary>
    public IAppDataLocator AppData { get; set; }
    /// <summary>Start with Windows.</summary>
    public IAutoStartService AutoStart { get; set; }
    /// <summary>Monitors and their DPI.</summary>
    public IScreenService Screens { get; set; }
    /// <summary>The clipboard.</summary>
    public IClipboardService Clipboard { get; set; }
    /// <summary>Microsoft Store pages.</summary>
    public IUriLauncher UriLauncher { get; set; }
    /// <summary>.lnk and .url files.</summary>
    public IShellLinkWriter LinkWriter { get; set; }
    /// <summary>Rate / Share MDSHO.</summary>
    public StoreActions StoreActions { get; set; }
    /// <summary>The windows of other apps, for "Bring other windows to this screen".</summary>
    public IOtherWindowsService OtherWindows { get; set; } = new OtherWindowsService();
    /// <summary>The global hotkey service.</summary>
    public IGlobalHotkeyService HotkeyService { get; set; } = new GlobalHotkeyService();
    /// <summary>The "Bring panels to front" shortcut. Made on first use.</summary>
    public BringToFrontShortcut BringToFront {
      get { return this.bringToFront ??= new BringToFrontShortcut(HotkeyService); }
      set { this.bringToFront = value; }
    }
    private BringToFrontShortcut bringToFront;
    /// <summary>Tells when the screens change.</summary>
    public IDisplayChangeWatcher DisplayChanges { get; set; } = new DisplayChangeWatcher();
    /// <summary>Places per screen setup. Made on first use.</summary>
    public ScreenSetupTracker ScreenSetups {
      get { return this.screenSetups ??= new ScreenSetupTracker(Screens, new DispatcherOneShotTimer()); }
      set { this.screenSetups = value; }
    }
    private ScreenSetupTracker screenSetups;
    /// <summary>The "Window theme" setting. Made on first use.</summary>
    public WindowThemeSetting WindowTheme {
      get { return this.windowTheme ??= new WindowThemeSetting(new WindowsAppMode()); }
      set { this.windowTheme = value; }
    }
    private WindowThemeSetting windowTheme;
    /// <summary>Automatic backups. Made on first use.</summary>
    public BackupService Backups {
      get {
        return this.backups ??= new BackupService(System.TimeProvider.System, System.Environment.MachineName,
          System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), Data.DataExporter.AppVersion(),
          new DispatcherOneShotTimer(), action => System.Threading.Tasks.Task.Run(action),
          System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments));
      }
      set { this.backups = value; }
    }
    private BackupService backups;
    /// <summary>The one-time "Enjoying MDSHO?" prompt (off in the Debug build). Made on first use.</summary>
    public ReviewPrompt Review {
      get {
        return this.review ??= new ReviewPrompt(System.TimeProvider.System, new DispatcherOneShotTimer(), () => ReviewWindow.IsDialogOpen(),
          ReviewWindow.ShowFor, StoreActions, IsDebugBuild);
      }
      set { this.review = value; }
    }
    private ReviewPrompt review;
#if DEBUG
    private const bool IsDebugBuild = true;
#else
    private const bool IsDebugBuild = false;
#endif
    /// <summary>The window the user was in before MDSHO, for snippets. Started by the app.</summary>
    public ForegroundWindowTracker ForegroundWindows { get; set; } = new ForegroundWindowTracker();
    /// <summary>Pastes snippet values. Made on first use.</summary>
    public ClipboardPaster Paster {
      get {
        return this.paster ??= new ClipboardPaster(ForegroundWindows, new ElevationCheck(), new UiaFocusedFieldCheck(),
          new PasteClipboard(), new PasteKeySender(), new TaskPasteDelay());
      }
      set { this.paster = value; }
    }
    private ClipboardPaster paster;
    /// <summary>"Bring panels to front". Null until the app has started.</summary>
    public PanelsToFront PanelsToFront { get; set; }

    /// <summary>Creates the set from its services.</summary>
    public AppServices(IShellLauncher shell, IPackageIdentity package, IAppDataLocator appData, IAutoStartService autoStart,
      IScreenService screens, IClipboardService clipboard, IUriLauncher uriLauncher, IShellLinkWriter linkWriter, StoreActions storeActions) {
      Shell = shell;
      Package = package;
      AppData = appData;
      AutoStart = autoStart;
      Screens = screens;
      Clipboard = clipboard;
      UriLauncher = uriLauncher;
      LinkWriter = linkWriter;
      StoreActions = storeActions;
    }

    /// <summary>A new set of the real Infrastructure services.</summary>
    public static AppServices Defaults() {
      PackageIdentity package = new PackageIdentity();
      ShellLauncher shell = new ShellLauncher();
      ClipboardService clipboard = new ClipboardService();
      UriLauncher uriLauncher = new UriLauncher();
      return new AppServices(shell, package, new AppDataLocator(package), new AutoStartService(package),
        new ScreenService(), clipboard, uriLauncher, new ShellLinkWriter(),
        new StoreActions(new StoreActionsHost(clipboard, shell, uriLauncher), new StoreRating(StoreActionsHost.OwnerWindowHandle)));
    }

    /// <summary>Gives the view models the services they use (process start, clipboard, shortcut files).</summary>
    public void ConnectViewModels() {
      ShortcutViewModel.ProcessStarter = Shell.Start;
      ShortcutViewModel.CopyText = Clipboard.SetText;
      ShortcutViewModel.PasteValue = value => Paster.PasteAsync(value);
      ShortcutFileService.LinkWriter = LinkWriter;
    }

    /// <summary>Opens Explorer on the app data folder.</summary>
    public void BrowseAppData() {
      Shell.OpenFolder(AppData.Locate());
    }
  }
}
