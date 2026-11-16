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

  /// <summary>How the Store's in-app rating dialog ended.</summary>
  public enum StoreRatingResult {
    /// <summary>The user rated MDSHO.</summary>
    Rated,
    /// <summary>The user closed the dialog without rating.</summary>
    Cancelled,
    /// <summary>The dialog failed (an error or no network).</summary>
    Failed
  }

  /// <summary>The Microsoft Store's in-app rating dialog, and how MDSHO was installed.</summary>
  public interface IStoreRating {
    /// <summary>"Store" for a Microsoft Store install; otherwise the package signature ("Enterprise", "Developer", "None") or "Not packaged".</summary>
    string InstallKind { get; }
    /// <summary>Shows the Store's rating dialog.</summary>
    Task<StoreRatingResult> RequestRateAndReviewAsync();
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

    /// <summary>Creates the actions over a UI host and, for Store installs, the Store's rating dialog.</summary>
    public StoreActions(IStoreActionsHost host, IStoreRating rating = null) {
      this.host = host;
      RatingService = rating;
    }

    /// <summary>The Store's rating dialog, or null.</summary>
    public IStoreRating RatingService { get; }

    /// <summary>True when MDSHO was installed from the Microsoft Store.</summary>
    public bool IsStoreInstall => InstallKind == "Store";

    /// <summary>How MDSHO was installed (see <see cref="IStoreRating.InstallKind"/>).</summary>
    public string InstallKind => RatingService?.InstallKind ?? "Not packaged";

    /// <summary>Raised after a rating, or after the Store page opened when we can't know (the review prompt then never shows).</summary>
    public event Action Rated;

    /// <summary>
    /// Store installs: the Store's rating dialog; a cancel does nothing, and a failure opens the Store review page.
    /// Other installs: the Store review page. Never throws; errors are shown.
    /// </summary>
    public async Task RateAsync() {
      if (IsStoreInstall) {
        StoreRatingResult result;
        try {
          result = await RatingService.RequestRateAndReviewAsync();
        } catch (Exception ex) {
          AppLog.Warning(ex, "The Store rating dialog failed");
          result = StoreRatingResult.Failed;
        }
        if (result == StoreRatingResult.Rated) {
          RaiseRated();
          return;
        }
        if (result == StoreRatingResult.Cancelled) {
          return;
        }
      }
      try {
        await host.LaunchUriAsync(ReviewUri);
        RaiseRated();
      } catch (Exception ex) {
        host.ShowError(ex);
      }
    }

    private void RaiseRated() {
      try {
        Rated?.Invoke();
      } catch (Exception ex) {
        AppLog.Warning(ex, "Recording the rating failed");
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
