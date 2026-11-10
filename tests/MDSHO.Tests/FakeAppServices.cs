using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using MDSHO.Common;
using MDSHO.Helpers;
using MDSHO.Infrastructure;
using MDSHO.ViewModels;

namespace MDSHO.Tests {

  /// <summary>
  /// Swaps <see cref="AppServices.Current"/> for fakes during a test: processes are recorded (never started), the clipboard
  /// is a list, the package is "not packaged" and Store launches are recorded. Screens stay real (read only).
  /// </summary>
  public sealed class FakeAppServices : IDisposable {
    private readonly AppServices original = AppServices.Current;
    public readonly List<ProcessStartInfo> Started = new List<ProcessStartInfo>();
    public readonly List<string> Copied = new List<string>();
    public readonly List<Uri> Launched = new List<Uri>();
    public AppServices Services { get; }

    public FakeAppServices() {
      FakePackage package = new FakePackage();
      ShellLauncher shell = new ShellLauncher(psi => Started.Add(psi));
      FakeClipboard clipboard = new FakeClipboard(Copied);
      FakeUriLauncher uriLauncher = new FakeUriLauncher(Launched);
      Services = new AppServices(shell, package, new AppDataLocator(package), new AutoStartService(package), new ScreenService(),
        clipboard, uriLauncher, new ShellLinkWriter(), new StoreActions(new StoreActionsHost(clipboard, shell, uriLauncher)));
      AppServices.Current = Services;
    }

    /// <summary>The folders opened in Explorer (the quotes removed).</summary>
    public List<string> OpenedFolders {
      get {
        List<string> folders = new List<string>();
        foreach (ProcessStartInfo psi in Started) {
          if (psi.FileName == "explorer.exe") {
            folders.Add(psi.Arguments.Trim('"'));
          }
        }
        return folders;
      }
    }

    public void Dispose() {
      AppServices.Current = original;
    }

    private sealed class FakePackage : IPackageIdentity {
      public bool IsPackaged => false;
      public string FamilyName => null;
      public string LocalCacheFolder() { throw new InvalidOperationException("not packaged"); }
    }

    private sealed class FakeClipboard : IClipboardService {
      private readonly List<string> copied;
      public FakeClipboard(List<string> copied) { this.copied = copied; }
      public void SetText(string text) { copied.Add(text); }
    }

    private sealed class FakeUriLauncher : IUriLauncher {
      private readonly List<Uri> launched;
      public FakeUriLauncher(List<Uri> launched) { this.launched = launched; }
      public Task<bool> LaunchUriAsync(Uri uri) { launched.Add(uri); return Task.FromResult(true); }
    }
  }

  /// <summary>Test-run set-up: the shortcut files are written for real (into temp folders), processes are never started.</summary>
  internal static class TestServicesSetup {
    [ModuleInitializer]
    internal static void Init() {
      ShortcutFileService.LinkWriter = new ShellLinkWriter();
      ShortcutViewModel.ProcessStarter = psi => throw new InvalidOperationException("A process was started in a test: " + psi.FileName);
      ShortcutViewModel.CopyText = text => throw new InvalidOperationException("The clipboard was used in a test.");
      AppServices.Current = new FakeAppServices().Services;
    }
  }
}
