using System;
using MDSHO.Common;

namespace MDSHO.Infrastructure {

  /// <summary>Tells whether MDSHO runs from its installed MSIX package (package identity) and which package it is.</summary>
  public sealed class PackageIdentity : IPackageIdentity {

    /// <summary>True when the process has package identity (MSIX). Never throws.</summary>
    public bool IsPackaged => FamilyName != null;

    /// <summary>The package family name, or null when the process has no package identity (e.g. dotnet run). Never throws.</summary>
    public string FamilyName {
      get {
        try {
          // Throws InvalidOperationException when the process has no package identity.
          return global::Windows.ApplicationModel.Package.Current?.Id.FamilyName;
        } catch (Exception) {
          return null;
        }
      }
    }

    /// <summary>The package's LocalCache folder; throws when not packaged.</summary>
    public string LocalCacheFolder() {
      return global::Windows.Storage.ApplicationData.Current.LocalCacheFolder.Path;
    }
  }
}
