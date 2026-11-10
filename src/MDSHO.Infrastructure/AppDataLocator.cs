using System.IO;
using MDSHO.Common;

namespace MDSHO.Infrastructure {

  /// <summary>
  /// Finds the folder that really holds the database and the shortcuts (see <see cref="AppDataFolder"/>): the package's
  /// redirected folder when Windows redirected it, otherwise the folder that holds both.
  /// </summary>
  public sealed class AppDataLocator : IAppDataLocator {
    private readonly IPackageIdentity package;

    /// <summary>Creates the locator over a package check.</summary>
    public AppDataLocator(IPackageIdentity package) {
      this.package = package;
    }

    /// <summary>The folder "Browse app data" opens.</summary>
    public string Locate() {
      return AppDataFolder.Locate(package.IsPackaged, package.LocalCacheFolder, Directory.Exists,
        Paths.GetAppDataLocalDatabaseFilePath(), Paths.GetShortcutsPath());
    }
  }
}
