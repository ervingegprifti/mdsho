using System;
using System.Collections.Generic;
using System.Linq;
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
using Serilog.Core;
using Serilog.Events;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// The composition root: the Generic Host with the data store, the Infrastructure services, logging, the app view model
  /// and the windows. The host is built for real; nothing is started or shown.
  /// </summary>
  public class AppHostTests {

    private sealed class MemorySink : ILogEventSink, IDisposable {
      public readonly List<LogEvent> Events = new List<LogEvent>();
      public bool Disposed;
      public void Emit(LogEvent logEvent) { Events.Add(logEvent); }
      public void Dispose() { Disposed = true; }
    }

    [Fact]
    public void Host_resolves_every_Infrastructure_service_as_one_shared_instance() {
      using (IHost host = AppHost.Build(null)) {
        IServiceProvider sp = host.Services;
        Assert.IsType<ShellLauncher>(sp.GetRequiredService<IShellLauncher>());
        Assert.IsType<PackageIdentity>(sp.GetRequiredService<IPackageIdentity>());
        Assert.IsType<AppDataLocator>(sp.GetRequiredService<IAppDataLocator>());
        Assert.IsType<AutoStartService>(sp.GetRequiredService<IAutoStartService>());
        Assert.IsType<ScreenService>(sp.GetRequiredService<IScreenService>());
        Assert.IsType<ClipboardService>(sp.GetRequiredService<IClipboardService>());
        Assert.IsType<UriLauncher>(sp.GetRequiredService<IUriLauncher>());
        Assert.IsType<ShellLinkWriter>(sp.GetRequiredService<IShellLinkWriter>());
        Assert.Same(sp.GetRequiredService<IShellLauncher>(), sp.GetRequiredService<IShellLauncher>());

        AppServices services = sp.GetRequiredService<AppServices>();
        Assert.Same(sp.GetRequiredService<IShellLauncher>(), services.Shell);
        Assert.Same(sp.GetRequiredService<IScreenService>(), services.Screens);
        Assert.Same(sp.GetRequiredService<IAutoStartService>(), services.AutoStart);
      }
    }

    [Fact]
    public void Host_resolves_the_data_store_and_its_repositories() {
      using (IHost host = AppHost.Build(null)) {
        IDataStore store = host.Services.GetRequiredService<IDataStore>();
        Assert.Same(store, host.Services.GetRequiredService<IDataStore>());
        Assert.Same(store.Windows, host.Services.GetRequiredService<IDesktopWindowRepository>());
        Assert.Same(store.Clones, host.Services.GetRequiredService<IDesktopWindowCloneRepository>());
        Assert.Same(store.Shortcuts, host.Services.GetRequiredService<IShortcutRepository>());
      }
    }

    [Fact]
    public void Host_resolves_the_store_actions_over_the_store_host() {
      using (IHost host = AppHost.Build(null)) {
        Assert.IsType<StoreActionsHost>(host.Services.GetRequiredService<IStoreActionsHost>());
        Assert.Same(host.Services.GetRequiredService<StoreActions>(), host.Services.GetRequiredService<StoreActions>());
      }
    }

    [Fact]
    public void Host_builds_the_app_view_model_from_the_database_once() {
      using (TempDb db = new TempDb()) {
        DesktopWindow w = DataStore.Windows.Create("TEST", 1, 2);
        using (IHost host = AppHost.Build(null)) {
          AppViewModel app = host.Services.GetRequiredService<AppViewModel>();
          Assert.Same(app, host.Services.GetRequiredService<AppViewModel>());
          Assert.Single(app.Windows, x => x.ID == w.ID);
        }
      }
    }

    [Fact]
    public void Host_registers_the_windows() {
      using (IHost host = AppHost.Build(null)) {
        IServiceProviderIsService isService = host.Services.GetRequiredService<IServiceProviderIsService>();
        Assert.True(isService.IsService(typeof(MainWindow)));
        Assert.True(isService.IsService(typeof(AboutWindow)));
        Assert.True(isService.IsService(typeof(WindowFactory)));
      }
    }

    [Fact]
    public void Host_sends_ILogger_events_to_the_Serilog_logger_and_disposes_it() {
      MemorySink sink = new MemorySink();
      Logger logger = new LoggerConfiguration().MinimumLevel.Information().WriteTo.Sink(sink).CreateLogger();
      using (IHost host = AppHost.Build(logger)) {
        host.Services.GetRequiredService<ILogger<AppHostTests>>().LogInformation("Hello {Name}", "MDSHO");
        Assert.False(sink.Disposed);
      }
      Assert.Contains(sink.Events, e => e.RenderMessage() == "Hello \"MDSHO\"");
      Assert.True(sink.Disposed);
    }

    [Fact]
    public void Composition_root_points_the_static_seams_at_the_container() {
      IDataStore oldStore = DataStore.Current;
      AppServices oldServices = AppServices.Current;
      Action<System.Diagnostics.ProcessStartInfo> oldStarter = ShortcutViewModel.ProcessStarter;
      Action<string> oldCopy = ShortcutViewModel.CopyText;
      IShellLinkWriter oldWriter = ShortcutFileService.LinkWriter;
      try {
        using (IHost host = AppHost.Build(null)) {
          AppHost.UseServices(host.Services);
          Assert.Same(host.Services.GetRequiredService<IDataStore>(), DataStore.Current);
          Assert.Same(host.Services.GetRequiredService<AppServices>(), AppServices.Current);
          Assert.Same(host.Services.GetRequiredService<IShellLinkWriter>(), ShortcutFileService.LinkWriter);
        }
      } finally {
        DataStore.Current = oldStore;
        AppServices.Current = oldServices;
        ShortcutViewModel.ProcessStarter = oldStarter;
        ShortcutViewModel.CopyText = oldCopy;
        ShortcutFileService.LinkWriter = oldWriter;
      }
    }
  }
}
