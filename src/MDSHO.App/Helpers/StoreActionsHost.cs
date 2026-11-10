using System;
using System.Threading.Tasks;
using System.Windows;
using MDSHO.Common;

namespace MDSHO.Helpers {

  /// <summary>Real IStoreActionsHost: the clipboard, browser and Store launcher services, plus the WPF message box and error dialog.</summary>
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

    /// <summary>Shows an information message box.</summary>
    public void ShowInformation(string message, string caption) {
      MessageBox.Show(message, caption, MessageBoxButton.OK, MessageBoxImage.Information);
    }

    /// <summary>Opens a web address in the default browser.</summary>
    public void OpenInBrowser(string url) {
      shell.OpenUrl(url);
    }

    /// <summary>Launches a URI with Windows (Microsoft Store pages).</summary>
    public async Task<bool> LaunchUriAsync(Uri uri) {
      return await uriLauncher.LaunchUriAsync(uri);
    }

    /// <summary>Shows the error dialog.</summary>
    public void ShowError(Exception exception) {
      ErrorDialog.ShowDialog(exception);
    }
  }
}
