using System;
using System.Threading.Tasks;
using MDSHO.Common;

namespace MDSHO.Infrastructure {

  /// <summary>Launches URIs with Windows (Microsoft Store pages).</summary>
  public sealed class UriLauncher : IUriLauncher {
    /// <summary>Launches the URI; true when Windows started something for it.</summary>
    public async Task<bool> LaunchUriAsync(Uri uri) {
      return await global::Windows.System.Launcher.LaunchUriAsync(uri);
    }
  }
}
