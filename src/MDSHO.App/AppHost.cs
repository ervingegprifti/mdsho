using System;
using MDSHO.Common;
using MDSHO.Core;
using MDSHO.Data;
using MDSHO.Helpers;
using MDSHO.Infrastructure;
using MDSHO.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;

namespace MDSHO {

  /// <summary>
  /// The composition root: builds the Generic Host with every service MDSHO uses. <see cref="App"/> builds it once at
  /// startup and disposes it on exit. The host has no hosted services, so it is not started; it is the container and owns
  /// the log file.
  /// </summary>
  public static class AppHost {

    /// <summary>
    /// Builds the host. <paramref name="fileLogger"/> is the Serilog logger of the log files (null = no file logging);
    /// the host owns it and disposes it with the container.
    /// </summary>
    public static IHost Build(Serilog.ILogger fileLogger) {
      // No default configuration sources or logging providers (no console, debug or Windows event log output).
      HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings { ApplicationName = AppConstants.ApplicationName });
      IServiceCollection services = builder.Services;

      // Logging: Microsoft.Extensions.Logging goes to the same Serilog logger as AppLog.
      builder.Logging.ClearProviders();
      if (fileLogger != null) {
        builder.Logging.AddSerilog(fileLogger, dispose: true);
      }

      // Data store and repositories (one SQLite store; the repositories are its own).
      services.AddSingleton<IDataStore>(_ => DataStore.CreateSqlite());
      services.AddSingleton(sp => sp.GetRequiredService<IDataStore>().Windows);
      services.AddSingleton(sp => sp.GetRequiredService<IDataStore>().Clones);
      services.AddSingleton(sp => sp.GetRequiredService<IDataStore>().Shortcuts);

      // Infrastructure (Windows services).
      services.AddSingleton<IShellLauncher, ShellLauncher>(_ => new ShellLauncher());
      services.AddSingleton<IPackageIdentity, PackageIdentity>();
      services.AddSingleton<IAppDataLocator, AppDataLocator>();
      services.AddSingleton<IAutoStartService, AutoStartService>();
      services.AddSingleton<IScreenService, ScreenService>();
      services.AddSingleton<IClipboardService, ClipboardService>();
      services.AddSingleton<IUriLauncher, UriLauncher>();
      services.AddSingleton<IShellLinkWriter, ShellLinkWriter>();

      // UI services.
      services.AddSingleton<IStoreActionsHost, StoreActionsHost>();
      services.AddSingleton<StoreActions>();
      services.AddSingleton<AppServices>();

      // View models: the app view model is loaded from the database (after the schema upgrade) on first use.
      services.AddSingleton(_ => ViewModelFactory.CreateAppViewModel());

      // Windows.
      services.AddSingleton<MainWindow>();
      services.AddTransient<AboutWindow>();
      services.AddSingleton<WindowFactory>();

      // Fail at build time, not at first use, when a registration cannot be resolved.
      builder.ConfigureContainer(new DefaultServiceProviderFactory(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true }));
      return builder.Build();
    }

    /// <summary>
    /// Points the static seams of the layers built before DI (the DataStore facade, the view models and the UI code) at
    /// the container's services. Called once, right after <see cref="Build"/>.
    /// </summary>
    public static void UseServices(IServiceProvider services) {
      DataStore.Current = services.GetRequiredService<IDataStore>();
      AppServices appServices = services.GetRequiredService<AppServices>();
      AppServices.Current = appServices;
      appServices.ConnectViewModels();
    }
  }
}
