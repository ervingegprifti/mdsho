using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using MDSHO.Common;
using MDSHO.Infrastructure;
using MDSHO.ViewModels;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// The Windows services in MDSHO.Infrastructure, behind interfaces in MDSHO.Common. Nothing here starts a process,
  /// opens Explorer or touches the clipboard: the process start is a recorder and the package is a fake.
  /// </summary>
  public class InfrastructureTests {

    private sealed class FakePackage : IPackageIdentity {
      public bool IsPackaged { get; set; }
      public string FamilyName { get; set; }
      public string Cache { get; set; }
      public string LocalCacheFolder() { return Cache ?? throw new InvalidOperationException("not packaged"); }
    }

    #region PROJECT
    [Fact]
    public void Infrastructure_has_no_WPF_UI_reference() {
      string[] names = typeof(ShellLauncher).Assembly.GetReferencedAssemblies().Select(a => a.Name).ToArray();
      Assert.DoesNotContain("PresentationFramework", names);
      Assert.DoesNotContain("MDSHO", names);
      Assert.DoesNotContain("MDSHO.ViewModels", names);
    }

    [Fact]
    public void Infrastructure_services_implement_the_Common_interfaces() {
      Assert.IsAssignableFrom<IShellLauncher>(new ShellLauncher());
      Assert.IsAssignableFrom<IPackageIdentity>(new PackageIdentity());
      Assert.IsAssignableFrom<IAppDataLocator>(new AppDataLocator(new FakePackage()));
      Assert.IsAssignableFrom<IAutoStartService>(new AutoStartService(new FakePackage()));
      Assert.IsAssignableFrom<IScreenService>(new ScreenService());
      Assert.IsAssignableFrom<IClipboardService>(new ClipboardService());
      Assert.IsAssignableFrom<IUriLauncher>(new UriLauncher());
      Assert.IsAssignableFrom<IShellLinkWriter>(new ShellLinkWriter());
    }
    #endregion PROJECT

    #region SHELL LAUNCHER
    [Fact]
    public void Shell_launcher_starts_urls_folders_and_programs_through_the_shell() {
      List<ProcessStartInfo> started = new List<ProcessStartInfo>();
      ShellLauncher shell = new ShellLauncher(psi => started.Add(psi));
      shell.OpenUrl("https://example.org/a?b=1");
      shell.OpenFolder(@"C:\Some Folder\MDSHO");
      shell.Run("explorer.exe", @"shell:AppsFolder\Family!App");
      shell.Start(new ProcessStartInfo("cmd.exe", "/c") { UseShellExecute = false });

      Assert.Equal(4, started.Count);
      Assert.Equal("https://example.org/a?b=1", started[0].FileName);
      Assert.Equal("", started[0].Arguments);
      Assert.True(started[0].UseShellExecute);
      Assert.Equal("explorer.exe", started[1].FileName);
      Assert.Equal("\"C:\\Some Folder\\MDSHO\"", started[1].Arguments);
      Assert.True(started[1].UseShellExecute);
      Assert.Equal(@"shell:AppsFolder\Family!App", started[2].Arguments);
      Assert.True(started[2].UseShellExecute);
      Assert.Equal("cmd.exe", started[3].FileName);
      Assert.False(started[3].UseShellExecute);
    }
    #endregion SHELL LAUNCHER

    #region PACKAGE, APP DATA, AUTO START
    [Fact]
    public void The_test_host_has_no_package_identity() {
      PackageIdentity package = new PackageIdentity();
      Assert.False(package.IsPackaged);
      Assert.Null(package.FamilyName);
    }

    [Fact]
    public void Auto_start_is_supported_only_when_packaged() {
      Assert.False(new AutoStartService(new FakePackage { IsPackaged = false }).IsSupported);
      Assert.Equal(AutoStartState.NotPackaged, new AutoStartService(new FakePackage { IsPackaged = false }).GetState());
      Assert.True(new AutoStartService(new FakePackage { IsPackaged = true }).IsSupported);
    }

    [Fact]
    public void App_data_locator_returns_the_folder_holding_the_database_and_shortcuts() {
      using (TempDb db = new TempDb(create: false)) {
        AppDataLocator locator = new AppDataLocator(new FakePackage { IsPackaged = false });
        Assert.Equal(db.Folder, locator.Locate());
      }
    }

    [Fact]
    public void App_data_locator_prefers_the_redirected_package_folder_when_it_exists() {
      using (TempDb db = new TempDb(create: false)) {
        string cache = Path.Combine(db.Folder, "LocalCache");
        string redirected = Path.Combine(cache, "Local", AppConstants.ApplicationFolderName);
        Directory.CreateDirectory(redirected);
        AppDataLocator locator = new AppDataLocator(new FakePackage { IsPackaged = true, Cache = cache });
        Assert.Equal(redirected, locator.Locate());
      }
    }
    #endregion PACKAGE, APP DATA, AUTO START

    #region SCREENS
    [Fact]
    public void Screen_service_reads_the_monitors_with_one_primary() {
      ScreenService screens = new ScreenService();
      IList<ScreenInfo> all = screens.AllScreens();
      Assert.NotEmpty(all);
      Assert.Single(all, s => s.IsPrimary);
      Assert.Equal(all.Single(s => s.IsPrimary).DeviceName, screens.PrimaryScreen().DeviceName);
      Assert.True(screens.PrimaryScreen().Scale > 0);
    }
    #endregion SCREENS

    #region SHELL LINKS
    [Fact]
    public void Url_file_is_written_as_an_internet_shortcut() {
      using (TempDb db = new TempDb(create: false)) {
        string path = Path.Combine(db.Folder, "a.url");
        new ShellLinkWriter().CreateUrl(path, "https://example.org/");
        string[] lines = File.ReadAllLines(path);
        Assert.Equal(new[] { "[InternetShortcut]", "URL=https://example.org/", "IDList=", "HotKey=", "IconFile=", "IconIndex=" }, lines);
      }
    }

    [Fact]
    public void Lnk_file_is_written_by_the_shell_link_writer() {
      using (TempDb db = new TempDb(create: false)) {
        string path = Path.Combine(db.Folder, "a.lnk");
        new ShellLinkWriter().CreateLnk(path, db.Folder, db.Folder);
        Assert.True(File.Exists(path));
      }
    }

    private sealed class RecordingLinkWriter : IShellLinkWriter {
      public readonly List<string> Calls = new List<string>();
      public void CreateLnk(string lnkFilePath, string targetPath, string workingFolder) { Calls.Add("lnk|" + lnkFilePath + "|" + targetPath + "|" + workingFolder); }
      public void CreateUrl(string urlFilePath, string url) { Calls.Add("url|" + urlFilePath + "|" + url); }
    }

    [Fact]
    public void Shortcut_file_service_writes_its_files_through_the_link_writer() {
      IShellLinkWriter old = ShortcutFileService.LinkWriter;
      RecordingLinkWriter writer = new RecordingLinkWriter();
      try {
        using (TempDb db = new TempDb(create: false)) {
          ShortcutFileService.LinkWriter = writer;
          ShortcutFileService.CreateLnkFile("G1", @"C:\Target");
          ShortcutFileService.CreateUrlFile("G2", "https://example.org/");
          string folder = Paths.GetShortcutsPath();
          Assert.Equal(new[] {
            "lnk|" + Path.Combine(folder, "G1.lnk") + "|C:\\Target|" + folder,
            "url|" + Path.Combine(folder, "G2.url") + "|https://example.org/"
          }, writer.Calls);
        }
      } finally {
        ShortcutFileService.LinkWriter = old;
      }
    }
    #endregion SHELL LINKS

    #region LOGGING
    [Fact]
    public void Logging_setup_lives_in_Infrastructure() {
      Assert.Equal("MDSHO.Infrastructure", typeof(MDSHO.Infrastructure.Logging.AppLogging).Assembly.GetName().Name);
      Assert.Equal("MDSHO.Infrastructure", typeof(MDSHO.Infrastructure.Logging.SerilogAppLogWriter).Assembly.GetName().Name);
    }

    [Fact]
    public void Open_logs_folder_falls_back_to_the_app_data_folder_and_opens_Explorer_through_the_launcher() {
      List<ProcessStartInfo> started = new List<ProcessStartInfo>();
      using (TempDb db = new TempDb(create: false)) {
        AppDataLocator locator = new AppDataLocator(new FakePackage());
        ShellLauncher shell = new ShellLauncher(psi => started.Add(psi));
        MDSHO.Infrastructure.Logging.AppLogging.OpenLogsFolder(locator, shell);
        Directory.CreateDirectory(MDSHO.Infrastructure.Logging.AppLogging.LogsFolder(db.Folder));
        MDSHO.Infrastructure.Logging.AppLogging.OpenLogsFolder(locator, shell);
      }
      Assert.Equal(2, started.Count);
      Assert.EndsWith("\"", started[0].Arguments);
      Assert.DoesNotContain("logs", started[0].Arguments);
      Assert.EndsWith("logs\"", started[1].Arguments);
    }
    #endregion LOGGING

    #region APP SERVICES
    [Fact]
    public void App_services_default_to_the_Infrastructure_implementations() {
      Assert.IsType<ShellLauncher>(Helpers.AppServices.Defaults().Shell);
      Assert.IsType<ScreenService>(Helpers.AppServices.Defaults().Screens);
      Assert.IsType<ClipboardService>(Helpers.AppServices.Defaults().Clipboard);
    }
    #endregion APP SERVICES
  }
}
