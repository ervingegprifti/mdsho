using System;
using System.Threading.Tasks;

namespace MDSHO.Common {

  /// <summary>What the Rate / Share actions need from the UI (clipboard, message box, browser, store launcher).</summary>
  public interface IStoreActionsHost {
    /// <summary>Puts text on the clipboard.</summary>
    void CopyToClipboard(string text);
    /// <summary>Shows an information message.</summary>
    void ShowInformation(string message, string caption);
    /// <summary>Opens a web address in the default browser.</summary>
    void OpenInBrowser(string url);
    /// <summary>Launches a URI (here a Microsoft Store page).</summary>
    Task<bool> LaunchUriAsync(Uri uri);
    /// <summary>Reports an error to the user.</summary>
    void ShowError(Exception exception);
  }

  /// <summary>"Rate MDSHO" and "Share MDSHO", shared by the About window and the tray menu.</summary>
  public class StoreActions {
    /// <summary>Microsoft Store web page of MDSHO.</summary>
    public const string StoreWebUrl = "https://www.microsoft.com/store/productId/9P8DNCD0PPG8";
    /// <summary>Microsoft Store review page of MDSHO.</summary>
    public static readonly Uri ReviewUri = new Uri("ms-windows-store://review/?ProductId=9P8DNCD0PPG8");
    /// <summary>Microsoft Store product page of MDSHO.</summary>
    public static readonly Uri ProductPageUri = new Uri("ms-windows-store://pdp/?ProductId=9P8DNCD0PPG8");
    /// <summary>Caption of the Share message.</summary>
    public const string ShareCaption = "Share MDSHO";

    private readonly IStoreActionsHost host;

    /// <summary>Creates the actions over a UI host.</summary>
    public StoreActions(IStoreActionsHost host) {
      this.host = host;
    }

    /// <summary>Opens the Microsoft Store review page (never throws; errors are shown).</summary>
    public async Task RateAsync() {
      try {
        await host.LaunchUriAsync(ReviewUri);
      } catch (Exception ex) {
        host.ShowError(ex);
      }
    }

    /// <summary>
    /// Copies the store link to the clipboard, tells the user, opens it in the browser and opens the store product page
    /// (never throws; errors are shown). The store page opens even if the clipboard / browser step fails.
    /// </summary>
    public async Task ShareAsync() {
      try {
        host.CopyToClipboard(StoreWebUrl);
        host.ShowInformation("The following link has been copied into your clipboard. You can paste and share it with friends."
          + Environment.NewLine + Environment.NewLine + StoreWebUrl, ShareCaption);
        host.OpenInBrowser(StoreWebUrl);
      } catch (Exception ex) {
        host.ShowError(ex);
      }
      try {
        await host.LaunchUriAsync(ProductPageUri);
      } catch (Exception ex) {
        host.ShowError(ex);
      }
    }
  }
}
