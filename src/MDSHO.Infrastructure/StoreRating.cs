using System;
using System.Threading.Tasks;
using MDSHO.Common;
using Windows.ApplicationModel;
using Windows.Services.Store;

namespace MDSHO.Infrastructure {

  /// <summary>The Microsoft Store's in-app rating dialog (StoreContext), and whether MDSHO came from the Store.</summary>
  public sealed class StoreRating : IStoreRating {
    private readonly Func<IntPtr> ownerWindow;
    private readonly Lazy<string> installKind = new Lazy<string>(ReadInstallKind);

    /// <param name="ownerWindow">The handle of the MDSHO window the dialog belongs to (a WPF app must give one).</param>
    public StoreRating(Func<IntPtr> ownerWindow) {
      this.ownerWindow = ownerWindow;
    }

    /// <summary>"Store" for a Microsoft Store install, else the package signature kind, or "Not packaged". Never throws.</summary>
    public string InstallKind => this.installKind.Value;

    /// <summary>Shows the Store's rating dialog over the owner window.</summary>
    public async Task<StoreRatingResult> RequestRateAndReviewAsync() {
      StoreContext context = StoreContext.GetDefault();
      IntPtr owner = this.ownerWindow?.Invoke() ?? IntPtr.Zero;
      if (owner != IntPtr.Zero) {
        WinRT.Interop.InitializeWithWindow.Initialize(context, owner);
      }
      StoreRateAndReviewResult result = await context.RequestRateAndReviewAppAsync();
      switch (result.Status) {
        case StoreRateAndReviewStatus.Succeeded:
          return StoreRatingResult.Rated;
        case StoreRateAndReviewStatus.CanceledByUser:
          return StoreRatingResult.Cancelled;
        default:
          AppLog.Warning(result.ExtendedError, "The Store rating dialog ended with {Status}", result.Status);
          return StoreRatingResult.Failed;
      }
    }

    private static string ReadInstallKind() {
      try {
        // Throws InvalidOperationException when the process has no package identity.
        Package package = Package.Current;
        return package == null ? "Not packaged" : package.SignatureKind.ToString();
      } catch (Exception) {
        return "Not packaged";
      }
    }
  }
}
