using System;
using MDSHO.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace MDSHO {

  /// <summary>Creates the app's top-level windows from the container (windows that need a view model get it here).</summary>
  public sealed class WindowFactory {
    private readonly IServiceProvider services;

    /// <summary>Creates the factory over the container.</summary>
    public WindowFactory(IServiceProvider services) {
      this.services = services;
    }

    /// <summary>The hidden main window (one per run).</summary>
    public MainWindow Main() {
      return services.GetRequiredService<MainWindow>();
    }

    /// <summary>A new About window.</summary>
    public AboutWindow About() {
      return services.GetRequiredService<AboutWindow>();
    }

    /// <summary>A new view of a window or clone.</summary>
    public DesktopWindowView DesktopWindow(DesktopWindowViewModel desktopWindow) {
      return new DesktopWindowView(desktopWindow);
    }
  }
}
