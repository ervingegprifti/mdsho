using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace MDSHO.Common {

  /// <summary>Starts things through Windows: processes, web links, Explorer folders and programs.</summary>
  public interface IShellLauncher {
    /// <summary>Starts a process exactly as described.</summary>
    void Start(ProcessStartInfo startInfo);
    /// <summary>Opens a web address in the default browser.</summary>
    void OpenUrl(string url);
    /// <summary>Opens a folder in Explorer.</summary>
    void OpenFolder(string path);
    /// <summary>Starts a program with arguments through the shell.</summary>
    void Run(string fileName, string arguments);
  }

  /// <summary>Whether MDSHO runs from its installed MSIX package, and which package.</summary>
  public interface IPackageIdentity {
    /// <summary>True when the process has package identity. Never throws.</summary>
    bool IsPackaged { get; }
    /// <summary>The package family name, or null when not packaged. Never throws.</summary>
    string FamilyName { get; }
    /// <summary>The package's LocalCache folder. Throws when not packaged.</summary>
    string LocalCacheFolder();
  }

  /// <summary>Finds the folder that really holds the database and the shortcuts (see <see cref="AppDataFolder"/>).</summary>
  public interface IAppDataLocator {
    /// <summary>The folder "Browse app data" opens.</summary>
    string Locate();
  }

  /// <summary>
  /// Reserves strips of the screen for docked windows through Windows' app bars (the way the taskbar reserves its space):
  /// desktop icons and maximised programs stay out of a reserved strip. Positions are physical pixels.
  /// </summary>
  public interface IAppBarService {
    /// <summary>The message Windows sends a window that holds a strip when the strips change (wParam 1: position changed).</summary>
    int CallbackMessage { get; }
    /// <summary>
    /// Reserves (or moves) the window's strip on the <paramref name="side"/> edge of its screen, as wide as the window.
    /// The window must already be on screen. Returns the strip Windows gave, or null when it refused.
    /// </summary>
    ScreenRect? Reserve(IntPtr window, DockSide side);
    /// <summary>Gives the window's strip back. Does nothing for a window that holds none.</summary>
    void Release(IntPtr window);
    /// <summary>Gives every strip back (MDSHO exits).</summary>
    void ReleaseAll();
    /// <summary>Where the window is on the screen.</summary>
    ScreenRect WindowRect(IntPtr window);
  }

  /// <summary>Reads the connected monitors.</summary>
  public interface IScreenService {
    /// <summary>Every connected screen.</summary>
    IList<ScreenInfo> AllScreens();
    /// <summary>The primary screen.</summary>
    ScreenInfo PrimaryScreen();
    /// <summary>The screen a window (by its handle) is mostly on.</summary>
    ScreenInfo ScreenFromHandle(IntPtr windowHandle);
    /// <summary>The screen the mouse pointer is on.</summary>
    ScreenInfo ScreenAtCursor();
  }

  /// <summary>The Windows clipboard.</summary>
  public interface IClipboardService {
    /// <summary>Puts text on the clipboard.</summary>
    void SetText(string text);
  }

  /// <summary>Launches URIs with Windows (here Microsoft Store pages).</summary>
  public interface IUriLauncher {
    /// <summary>Launches the URI; true when Windows started something for it.</summary>
    Task<bool> LaunchUriAsync(Uri uri);
  }

  /// <summary>Writes Windows shortcut files: .lnk (file or folder) and .url (web address).</summary>
  public interface IShellLinkWriter {
    /// <summary>Writes a .lnk file pointing at the target, opening in a normal window, working folder as given.</summary>
    void CreateLnk(string lnkFilePath, string targetPath, string workingFolder);
    /// <summary>Writes a .url (Internet shortcut) file for the address.</summary>
    void CreateUrl(string urlFilePath, string url);
  }
}
