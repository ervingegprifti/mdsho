# Third-Party Notices

MDSHO uses the following third-party packages. Each license is compatible with distributing MDSHO under the GPLv3.

## Shipped with the application

The Microsoft Store / sideload MSIX is self-contained, so it also ships the .NET and WPF runtimes. This list is the one shown under "Third Party Notices" in the About window (`src/MDSHO.App/ThirdPartyNotices.cs`); `ThirdPartyNoticesTests` fails when the two drift apart or when a shipped package is missing.

| Package | Version | License | Included packages / files | License text | Project URL | GPLv3-compatible |
| :--- | :--- | :--- | :--- | :--- | :--- | :---: |
| .NET Runtime | 10.0.14 | MIT | Microsoft.NETCore.App runtime pack (win-x64) | https://github.com/dotnet/runtime/blob/main/LICENSE.TXT | https://github.com/dotnet/runtime | Yes |
| WPF (Windows Desktop Runtime) | 10.0.14 | MIT | Microsoft.WindowsDesktop.App runtime pack (win-x64) | https://github.com/dotnet/wpf/blob/main/LICENSE.TXT | https://github.com/dotnet/wpf | Yes |
| Windows SDK for .NET (C#/WinRT projection) | 10.0.19041.57 | Microsoft Windows SDK License | Microsoft.Windows.SDK.NET.Ref: `Microsoft.Windows.SDK.NET.dll`, `WinRT.Runtime.dll` (C#/WinRT, MIT: https://github.com/microsoft/CsWinRT) | https://aka.ms/WinSDKLicenseURL | https://aka.ms/WinSDKProjectURL | Redistributable Windows component used only to call the MSIX StartupTask API (see note below) |
| Microsoft.Data.Sqlite | 10.0.14 | MIT | Microsoft.Data.Sqlite, Microsoft.Data.Sqlite.Core | https://github.com/dotnet/efcore/blob/main/LICENSE.txt | https://github.com/dotnet/efcore | Yes |
| SQLitePCLRaw | 2.1.13 | Apache-2.0 | SQLitePCLRaw.bundle_e_sqlite3, .core, .provider.e_sqlite3, .lib.e_sqlite3 | https://github.com/ericsink/SQLitePCL.raw/blob/master/LICENSE.TXT | https://github.com/ericsink/SQLitePCL.raw | Yes (Apache-2.0 is compatible with GPLv3) |
| SQLite | 3.53.3 | Public domain | native `e_sqlite3.dll`, inside SQLitePCLRaw.lib.e_sqlite3 | https://www.sqlite.org/copyright.html | https://www.sqlite.org/ | Yes |
| CommunityToolkit.Mvvm | 8.4.2 | MIT | CommunityToolkit.Mvvm (MVVM base classes, commands and source generators) | https://github.com/CommunityToolkit/dotnet/blob/main/License.md | https://github.com/CommunityToolkit/dotnet | Yes |
| Dapper | 2.1.89 | Apache-2.0 | Dapper | https://github.com/DapperLib/Dapper/blob/main/License.txt | https://github.com/DapperLib/Dapper | Yes (Apache-2.0 is compatible with GPLv3) |
| gong-wpf-dragdrop | 4.0.0 | BSD-3-Clause | gong-wpf-dragdrop | https://github.com/punker76/gong-wpf-dragdrop/blob/develop/LICENSE | https://github.com/punker76/gong-wpf-dragdrop | Yes |
| PixiEditor.ColorPicker | 3.4.2.3 | MIT | PixiEditor.ColorPicker, PixiEditor.ColorPicker.Models 1.0.11 | https://github.com/PixiEditor/ColorPicker/blob/master/LICENSE.md | https://github.com/PixiEditor/ColorPicker | Yes |
| Microsoft.Xaml.Behaviors.Wpf | 1.1.31 | MIT | Microsoft.Xaml.Behaviors.Wpf (dependency of PixiEditor.ColorPicker) | https://github.com/microsoft/XamlBehaviorsWpf/blob/master/LICENSE | https://github.com/microsoft/XamlBehaviorsWpf | Yes |
| Serilog | 4.4.0 | Apache-2.0 | Serilog (writes MDSHO's log files) | https://github.com/serilog/serilog/blob/dev/LICENSE | https://github.com/serilog/serilog | Yes (Apache-2.0 is compatible with GPLv3) |
| Serilog.Sinks.File | 7.0.0 | Apache-2.0 | Serilog.Sinks.File | https://github.com/serilog/serilog-sinks-file/blob/dev/LICENSE | https://github.com/serilog/serilog-sinks-file | Yes (Apache-2.0 is compatible with GPLv3) |
| Serilog.Extensions.Logging | 10.0.0 | Apache-2.0 | Serilog.Extensions.Logging (sends Microsoft.Extensions.Logging to the log files) | https://github.com/serilog/serilog-extensions-logging/blob/dev/LICENSE | https://github.com/serilog/serilog-extensions-logging | Yes (Apache-2.0 is compatible with GPLv3) |
| Microsoft.Extensions.Hosting | 10.0.13 | MIT | Microsoft.Extensions.Hosting and its dependencies: .Hosting.Abstractions, .DependencyInjection(.Abstractions), .Logging(.Abstractions, .Configuration, .Console, .Debug, .EventLog, .EventSource), .Configuration(.Abstractions, .Binder, .CommandLine, .EnvironmentVariables, .FileExtensions, .Json, .UserSecrets), .Diagnostics(.Abstractions), .FileProviders.Abstractions, .FileProviders.Physical, .FileSystemGlobbing, .Options(.ConfigurationExtensions), .Primitives (the app's service container) | https://github.com/dotnet/runtime/blob/main/LICENSE.TXT | https://github.com/dotnet/runtime | Yes |

Note on the Windows SDK projection: it comes with the `net10.0-windows10.0.19041.0` target framework and is a standard, freely redistributable part of every .NET app that calls Windows Runtime APIs; MDSHO's own source does not include it.

Build-time only (not shipped): `Microsoft.Windows.SDK.BuildTools` is referenced by the MSIX packaging project with `PrivateAssets="all"` and only provides the packaging tools.

## Tests only (not shipped)

| Package | Version | License | Project URL |
| :--- | :--- | :--- | :--- |
| xunit | 2.9.3 | Apache-2.0 | https://github.com/xunit/xunit |
| xunit.runner.visualstudio | 3.1.4 | MIT | https://github.com/xunit/visualstudio.xunit |
| Microsoft.NET.Test.Sdk | 17.14.1 | MIT | https://github.com/microsoft/vstest |

The full license texts are included in each NuGet package and in the shipped build output. Copyright remains with the respective authors.
