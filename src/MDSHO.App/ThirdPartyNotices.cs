using System;
using System.Collections.Generic;

namespace MDSHO {

  /// <summary>One third-party component shown in the About window (and listed in THIRD-PARTY-NOTICES.md).</summary>
  public sealed class ThirdPartyNotice {
    public ThirdPartyNotice(string name, string version, string license, string licenseUrl, string projectUrl, params string[] packages) {
      Name = name;
      Version = version;
      License = license;
      LicenseUrl = new Uri(licenseUrl);
      ProjectUrl = new Uri(projectUrl);
      Packages = packages;
    }

    /// <summary>Display name.</summary>
    public string Name { get; }
    /// <summary>Shipped version.</summary>
    public string Version { get; }
    /// <summary>License name (SPDX id where there is one).</summary>
    public string License { get; }
    public Uri LicenseUrl { get; }
    public Uri ProjectUrl { get; }
    /// <summary>NuGet package ids (or runtime packs) this entry covers.</summary>
    public IReadOnlyList<string> Packages { get; }
  }

  /// <summary>The third-party components that ship with MDSHO. Tests keep this list in sync with the package references.</summary>
  public static class ThirdPartyNotices {

    /// <summary>Year of the first MDSHO release; the copyright line runs from here to the current year.</summary>
    public const int FirstYear = 2022;

    public static readonly IReadOnlyList<ThirdPartyNotice> All = new[] {
      // Runtime shipped inside the self-contained MSIX.
      new ThirdPartyNotice(".NET Runtime", "10.0.14", "MIT",
        "https://github.com/dotnet/runtime/blob/main/LICENSE.TXT", "https://github.com/dotnet/runtime",
        "Microsoft.NETCore.App.Runtime.win-x64"),
      new ThirdPartyNotice("WPF (Windows Desktop Runtime)", "10.0.14", "MIT",
        "https://github.com/dotnet/wpf/blob/main/LICENSE.TXT", "https://github.com/dotnet/wpf",
        "Microsoft.WindowsDesktop.App.Runtime.win-x64"),
      new ThirdPartyNotice("Windows SDK for .NET (C#/WinRT projection)", "10.0.19041.57", "Microsoft Windows SDK License",
        "https://aka.ms/WinSDKLicenseURL", "https://aka.ms/WinSDKProjectURL",
        "Microsoft.Windows.SDK.NET.Ref"),
      // NuGet packages (direct and transitive).
      new ThirdPartyNotice("Microsoft.Data.Sqlite", "10.0.14", "MIT",
        "https://github.com/dotnet/efcore/blob/main/LICENSE.txt", "https://github.com/dotnet/efcore",
        "Microsoft.Data.Sqlite", "Microsoft.Data.Sqlite.Core"),
      new ThirdPartyNotice("SQLitePCLRaw", "2.1.13", "Apache-2.0",
        "https://github.com/ericsink/SQLitePCL.raw/blob/master/LICENSE.TXT", "https://github.com/ericsink/SQLitePCL.raw",
        "SQLitePCLRaw.bundle_e_sqlite3", "SQLitePCLRaw.core", "SQLitePCLRaw.provider.e_sqlite3", "SQLitePCLRaw.lib.e_sqlite3"),
      new ThirdPartyNotice("SQLite", "3.53.3", "Public domain",
        "https://www.sqlite.org/copyright.html", "https://www.sqlite.org/"),
      new ThirdPartyNotice("CommunityToolkit.Mvvm", "8.4.2", "MIT",
        "https://github.com/CommunityToolkit/dotnet/blob/main/License.md", "https://github.com/CommunityToolkit/dotnet",
        "CommunityToolkit.Mvvm"),
      new ThirdPartyNotice("Dapper", "2.1.89", "Apache-2.0",
        "https://github.com/DapperLib/Dapper/blob/main/License.txt", "https://github.com/DapperLib/Dapper",
        "Dapper"),
      new ThirdPartyNotice("gong-wpf-dragdrop", "4.0.0", "BSD-3-Clause",
        "https://github.com/punker76/gong-wpf-dragdrop/blob/develop/LICENSE", "https://github.com/punker76/gong-wpf-dragdrop",
        "gong-wpf-dragdrop"),
      new ThirdPartyNotice("PixiEditor.ColorPicker", "3.4.2.3", "MIT",
        "https://github.com/PixiEditor/ColorPicker/blob/master/LICENSE.md", "https://github.com/PixiEditor/ColorPicker",
        "PixiEditor.ColorPicker", "PixiEditor.ColorPicker.Models"),
      new ThirdPartyNotice("Microsoft.Xaml.Behaviors.Wpf", "1.1.31", "MIT",
        "https://github.com/microsoft/XamlBehaviorsWpf/blob/master/LICENSE", "https://github.com/microsoft/XamlBehaviorsWpf",
        "Microsoft.Xaml.Behaviors.Wpf"),
      new ThirdPartyNotice("Serilog", "4.4.0", "Apache-2.0",
        "https://github.com/serilog/serilog/blob/dev/LICENSE", "https://github.com/serilog/serilog",
        "Serilog"),
      new ThirdPartyNotice("Serilog.Sinks.File", "7.0.0", "Apache-2.0",
        "https://github.com/serilog/serilog-sinks-file/blob/dev/LICENSE", "https://github.com/serilog/serilog-sinks-file",
        "Serilog.Sinks.File"),
      new ThirdPartyNotice("Serilog.Extensions.Logging", "10.0.0", "Apache-2.0",
        "https://github.com/serilog/serilog-extensions-logging/blob/dev/LICENSE", "https://github.com/serilog/serilog-extensions-logging",
        "Serilog.Extensions.Logging"),
      new ThirdPartyNotice("Microsoft.Extensions.Hosting", "10.0.13", "MIT",
        "https://github.com/dotnet/runtime/blob/main/LICENSE.TXT", "https://github.com/dotnet/runtime",
        "Microsoft.Extensions.Hosting", "Microsoft.Extensions.Hosting.Abstractions", "Microsoft.Extensions.DependencyInjection",
        "Microsoft.Extensions.DependencyInjection.Abstractions", "Microsoft.Extensions.Logging", "Microsoft.Extensions.Logging.Abstractions",
        "Microsoft.Extensions.Logging.Configuration", "Microsoft.Extensions.Logging.Console", "Microsoft.Extensions.Logging.Debug",
        "Microsoft.Extensions.Logging.EventLog", "Microsoft.Extensions.Logging.EventSource", "Microsoft.Extensions.Configuration",
        "Microsoft.Extensions.Configuration.Abstractions", "Microsoft.Extensions.Configuration.Binder", "Microsoft.Extensions.Configuration.CommandLine",
        "Microsoft.Extensions.Configuration.EnvironmentVariables", "Microsoft.Extensions.Configuration.FileExtensions", "Microsoft.Extensions.Configuration.Json",
        "Microsoft.Extensions.Configuration.UserSecrets", "Microsoft.Extensions.Diagnostics", "Microsoft.Extensions.Diagnostics.Abstractions",
        "Microsoft.Extensions.FileProviders.Abstractions", "Microsoft.Extensions.FileProviders.Physical", "Microsoft.Extensions.FileSystemGlobbing",
        "Microsoft.Extensions.Options", "Microsoft.Extensions.Options.ConfigurationExtensions", "Microsoft.Extensions.Primitives"),
    };

    /// <summary>Copyright line shown in the About window for the given year.</summary>
    public static string CopyrightFor(int currentYear) {
      string years = currentYear > FirstYear ? FirstYear + "–" + currentYear : FirstYear.ToString();
      return "© " + years + " Ervin Gegprifti. Licensed under GPLv3 or a commercial licence.";
    }

    /// <summary>Copyright line for the current year.</summary>
    public static string Copyright => CopyrightFor(DateTime.Now.Year);
  }
}
