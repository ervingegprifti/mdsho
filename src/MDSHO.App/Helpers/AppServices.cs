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
    /// <summary>The windows of other apps, for "Find Other Lost Windows".</summary>
    public IOtherWindowsService OtherWindows { get; set; } = new OtherWindowsService();

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
        new StoreActions(new StoreActionsHost(clipboard, shell, uriLauncher)));
    }

    /// <summary>Gives the view models the services they use (process start, clipboard, shortcut files).</summary>
    public void ConnectViewModels() {
      ShortcutViewModel.ProcessStarter = Shell.Start;
      ShortcutViewModel.CopyText = Clipboard.SetText;
      ShortcutFileService.LinkWriter = LinkWriter;
    }

    /// <summary>Opens Explorer on the app data folder.</summary>
    public void BrowseAppData() {
      Shell.OpenFolder(AppData.Locate());
    }
  }
}
