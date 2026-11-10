namespace MDSHO.Common {

  /// <summary>The program and arguments that start MDSHO again.</summary>
  public sealed class RestartCommand {
    /// <summary>The Application Id declared in Package.appxmanifest.</summary>
    public const string PackagedApplicationId = "App";

    /// <summary>The program to start.</summary>
    public string FileName { get; private set; }
    /// <summary>The command line arguments to start it with.</summary>
    public string Arguments { get; private set; }

    /// <summary>
    /// Packaged (MSIX) builds are started through the shell by their AppUserModelId so the new process keeps its package
    /// identity; otherwise the exe is started directly. Returns null when there is nothing to start.
    /// </summary>
    public static RestartCommand Build(bool isPackaged, string packageFamilyName, string exePath) {
      if (isPackaged && !string.IsNullOrWhiteSpace(packageFamilyName)) {
        return new RestartCommand { FileName = "explorer.exe", Arguments = @"shell:AppsFolder\" + packageFamilyName + "!" + PackagedApplicationId };
      }
      if (string.IsNullOrEmpty(exePath)) {
        return null;
      }
      return new RestartCommand { FileName = exePath, Arguments = "" };
    }
  }
}
