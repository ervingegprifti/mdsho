using System;
using System.Threading.Tasks;
using MDSHO.Common;
using Windows.ApplicationModel;

namespace MDSHO.Infrastructure {

  /// <summary>Real IAutoStartService over the MSIX StartupTask declared in Package.appxmanifest.</summary>
  public sealed class AutoStartService : IAutoStartService {
    private const string TaskId = "MDSHOStartup";
    private readonly Lazy<bool> isPackaged;

    /// <summary>Creates the service over a package check (read once).</summary>
    public AutoStartService(IPackageIdentity package) {
      isPackaged = new Lazy<bool>(() => package.IsPackaged);
    }

    /// <summary>True when MDSHO runs from the installed (MSIX) package, the only case where Start with Windows works.</summary>
    public bool IsSupported => isPackaged.Value;

    /// <summary>Reads whether the Windows startup task is enabled, or why it is not.</summary>
    public AutoStartState GetState() {
      if (!IsSupported) {
        return AutoStartState.NotPackaged;
      }
      return Map(GetTask().State);
    }

    /// <summary>Asks Windows to start MDSHO at sign-in.</summary>
    public AutoStartState Enable() {
      if (!IsSupported) {
        return AutoStartState.NotPackaged;
      }
      StartupTask task = GetTask();
      StartupTaskState state = Run(() => task.RequestEnableAsync().AsTask());
      return Map(state);
    }

    /// <summary>Stops MDSHO starting at sign-in.</summary>
    public void Disable() {
      if (IsSupported) {
        GetTask().Disable();
      }
    }

    private static StartupTask GetTask() {
      return Run(() => StartupTask.GetAsync(TaskId).AsTask());
    }

    // Off the UI thread so blocking on the WinRT call cannot deadlock.
    private static T Run<T>(Func<Task<T>> func) {
      return Task.Run(func).GetAwaiter().GetResult();
    }

    private static AutoStartState Map(StartupTaskState state) {
      switch (state) {
        case StartupTaskState.Enabled:
        case StartupTaskState.EnabledByPolicy:
          return AutoStartState.Enabled;
        case StartupTaskState.DisabledByUser:
          return AutoStartState.DisabledByUser;
        case StartupTaskState.DisabledByPolicy:
          return AutoStartState.DisabledByPolicy;
        default:
          return AutoStartState.Disabled;
      }
    }
  }
}
