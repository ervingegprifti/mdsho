using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using MDSHO.Common;

namespace MDSHO.Helpers {

  /// <summary>Real IStoreActionsHost: the clipboard, browser and Store launcher services, plus the MDSHO message dialog and error dialog.</summary>
  public sealed class StoreActionsHost : IStoreActionsHost {
    private readonly IClipboardService clipboard;
    private readonly IShellLauncher shell;
    private readonly IUriLauncher uriLauncher;

    /// <summary>Creates the host over the clipboard, browser and Store launcher services.</summary>
    public StoreActionsHost(IClipboardService clipboard, IShellLauncher shell, IUriLauncher uriLauncher) {
      this.clipboard = clipboard;
      this.shell = shell;
      this.uriLauncher = uriLauncher;
    }

    /// <summary>Puts text on the clipboard.</summary>
    public void CopyToClipboard(string text) {
      clipboard.SetText(text);
    }

    /// <summary>Shows an information message in the MDSHO dialog.</summary>
    public void ShowInformation(string message, string caption) {
      MdshoMessage.Show(null, caption, message, MessageIcon.Info);
    }

    /// <summary>Opens a web address in the default browser.</summary>
    public void OpenInBrowser(string url) {
      shell.OpenUrl(url);
    }

    /// <summary>Launches a URI with Windows (Microsoft Store pages).</summary>
    public async Task<bool> LaunchUriAsync(Uri uri) {
      return await uriLauncher.LaunchUriAsync(uri);
    }

    /// <summary>
    /// The window the Store's rating dialog belongs to: the active MDSHO window (About, a panel), else the panel used
    /// last, else a shown panel, else the main window. Zero when there is none.
    /// </summary>
    public static IntPtr OwnerWindowHandle() {
      Application app = Application.Current;
      if (app == null) {
        return IntPtr.Zero;
      }
      Window owner = app.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive && w.IsVisible && !(w is ReviewWindow))
        ?? DesktopWindowView.LastUsed
        ?? app.Windows.OfType<DesktopWindowView>().FirstOrDefault(w => w.IsVisible)
        ?? app.MainWindow;
      return owner == null ? IntPtr.Zero : new WindowInteropHelper(owner).EnsureHandle();
    }

    /// <summary>Shows the error dialog.</summary>
    public void ShowError(Exception exception) {
      ErrorDialog.ShowDialog(exception);
    }
  }
}
