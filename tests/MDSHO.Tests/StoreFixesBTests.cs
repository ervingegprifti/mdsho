using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using MDSHO.Common;
using MDSHO.Core;
using MDSHO.Data;
using MDSHO.ViewModels;
using Serilog.Events;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// Robustness before the Store release: errors caught off the UI thread, plain messages when a button can't run, and
  /// shortcut files that can't be deleted or that nothing uses any more.
  /// </summary>
  public class StoreFixesBTests : IDisposable {
    private readonly TempDb db = new TempDb();
    private readonly LogCapture log = new LogCapture();
    private readonly Action<ProcessStartInfo> oldStarter = ShortcutViewModel.ProcessStarter;
    private readonly Action<ErrorWindow> oldShow = ErrorDialog.ShowWindow;
    private readonly Action<string, string, string> oldShowError = Dialogs.ShowError;
    private readonly List<(string Caption, string Error, string Detail)> errors = new List<(string, string, string)>();
    private readonly AppViewModel app = new AppViewModel();
    private readonly DesktopWindowViewModel win;

    public StoreFixesBTests() {
      Dialogs.ShowError = (caption, error, detail) => errors.Add((caption, error, detail));
      win = app.AddWindow("TEST", 0, 0);
    }

    public void Dispose() {
      ShortcutViewModel.ProcessStarter = oldStarter;
      ErrorDialog.ShowWindow = oldShow;
      Dialogs.ShowError = oldShowError;
      log.Dispose();
      db.Dispose();
    }

    // ---- 4: the global handlers run on any thread ----

    [Fact]
    public void An_unhandled_exception_off_the_UI_thread_shows_the_window_on_the_UI_thread() {
      int uiThread = 0;
      WpfAppHost.Run(() => uiThread = Environment.CurrentManagedThreadId);
      int shownOn = 0;
      ErrorWindow shown = null;
      ErrorDialog.ShowWindow = w => { shownOn = Environment.CurrentManagedThreadId; shown = w; };
      Exception thrown = null;
      // Like TaskScheduler.UnobservedTaskException, which runs on the finalizer thread (not STA).
      Thread worker = new Thread(() => {
        try { ErrorDialog.ShowUnhandled("TaskScheduler.UnobservedTaskException", new InvalidOperationException("lost task")); } catch (Exception ex) { thrown = ex; }
      });
      worker.SetApartmentState(ApartmentState.MTA);
      worker.Start();
      worker.Join();
      // Lets the queued window run on the UI thread.
      WpfAppHost.Run(() => { });
      Assert.Null(thrown);
      Assert.NotNull(shown);
      Assert.Equal(uiThread, shownOn);
      WpfAppHost.Run(() => shown.Close());
      Assert.Contains("TaskScheduler.UnobservedTaskException", log.Messages.Single());
    }

    [Fact]
    public void An_unhandled_exception_never_throws_when_the_window_fails() {
      WpfAppHost.Run(() => { });
      ErrorDialog.ShowWindow = w => throw new InvalidOperationException("window failed");
      Exception thrown = null;
      Thread worker = new Thread(() => {
        try { ErrorDialog.ShowUnhandled("AppDomain.CurrentDomain.UnhandledException", new InvalidOperationException("pool")); } catch (Exception ex) { thrown = ex; }
      });
      worker.Start();
      worker.Join();
      WpfAppHost.Run(() => { });
      Assert.Null(thrown);
      Assert.Contains(log.Events, e => e.Exception?.Message == "window failed");
    }

    // ---- 10: a button that can't run shows a short plain message ----

    private ShortcutViewModel FileShortcut(ShortcutViewModel parent, string name) {
      string guid = Identifiers.NewGuid();
      ShortcutViewModel s = ViewModelFactory.NewShortcut(app, parent, parent == null ? win : null, name, @"C:\Target\" + name + ".exe", ".lnk", guid);
      (parent == null ? win.RootShortcuts : parent.Children).Add(s);
      File.WriteAllText(Paths.GetShortcutFilePath(guid, ".lnk"), "content of " + name);
      return s;
    }

    private static void AssertPlain(string text) {
      Assert.DoesNotContain("   at ", text);
      Assert.DoesNotContain("-2147467259", text);
      Assert.DoesNotContain("Exception", text);
    }

    [Fact]
    public void A_shortcut_Windows_cannot_open_shows_the_Windows_reason_without_the_HRESULT() {
      ShortcutViewModel s = FileShortcut(null, "Tool");
      ShortcutViewModel.ProcessStarter = psi => throw new Win32Exception(2);
      s.RunShortcut(false);
      var e = Assert.Single(errors);
      Assert.Equal("Tool", e.Caption);
      Assert.Equal("Windows could not open this file: " + new Win32Exception(2).Message, e.Error);
      AssertPlain(e.Error + e.Detail);
      Assert.Contains(log.Events, x => x.Level == LogEventLevel.Warning);
    }

    [Fact]
    public void Any_other_failure_shows_a_plain_message_and_logs_the_details() {
      ShortcutViewModel s = FileShortcut(null, "Tool");
      ShortcutViewModel.ProcessStarter = psi => throw new InvalidOperationException("No application is associated.");
      s.RunShortcut(false);
      var e = Assert.Single(errors);
      Assert.Equal("Windows could not open this file: No application is associated.", e.Error);
      AssertPlain(e.Error + e.Detail);
      Assert.Contains(log.Events, x => x.Exception is InvalidOperationException);
    }

    [Fact]
    public void A_run_command_that_cannot_start_shows_the_Windows_reason() {
      ShortcutViewModel s = ViewModelFactory.NewProcessCommandShortcut(app, null, win);
      win.RootShortcuts.Add(s);
      s.ProcessFileName = "missing.exe";
      ShortcutViewModel.ProcessStarter = psi => throw new Win32Exception(2);
      s.RunShortcut(false);
      var e = Assert.Single(errors);
      Assert.Equal("Windows could not start the program: " + new Win32Exception(2).Message, e.Error);
      AssertPlain(e.Error + e.Detail);
    }

    [Fact]
    public void Test_run_that_cannot_start_shows_the_Windows_reason() {
      ShortcutViewModel s = ViewModelFactory.NewProcessCommandShortcut(app, null, win);
      win.RootShortcuts.Add(s);
      s.ProcessFileName = "missing.exe";
      RunCommandViewModel vm = new RunCommandViewModel(s, false);
      ShortcutViewModel.ProcessStarter = psi => throw new Win32Exception(2);
      vm.TestRun();
      var e = Assert.Single(errors);
      Assert.Equal("Windows could not start the program: " + new Win32Exception(2).Message, e.Error);
      AssertPlain(e.Error + e.Detail);
    }

    // ---- 15: shortcut files that can't be deleted, and files nothing uses ----

    [Fact]
    public void A_locked_file_does_not_fail_a_delete_that_was_saved() {
      ShortcutViewModel group = ViewModelFactory.NewGroupShortcut(app, null, win);
      win.RootShortcuts.Add(group);
      ShortcutViewModel a = FileShortcut(group, "a"), b = FileShortcut(group, "b");
      string fileA = Paths.GetShortcutFilePath(a.Guid, a.Ext), fileB = Paths.GetShortcutFilePath(b.Guid, b.Ext);
      using (new FileStream(fileA, FileMode.Open, FileAccess.Read, FileShare.None)) {
        group.DeleteShortcutCommand.Execute(null);
      }
      Assert.Empty(win.RootShortcuts);
      Assert.Null(DataStore.Shortcuts.Get(group.ID));
      Assert.True(File.Exists(fileA), "the locked file stays (swept at the next start)");
      Assert.False(File.Exists(fileB), "the other files are still deleted");
      Assert.Contains(log.Events, e => e.Level == LogEventLevel.Warning && e.Exception is IOException);
    }

    [Fact]
    public void Startup_deletes_shortcut_files_that_no_button_uses() {
      ShortcutViewModel used = FileShortcut(null, "used");
      string dir = Paths.GetShortcutsPath();
      string orphanLnk = Path.Combine(dir, Identifiers.NewGuid() + ".lnk");
      string orphanUrl = Path.Combine(dir, Identifiers.NewGuid() + ".url");
      string other = Path.Combine(dir, "readme.txt");
      foreach (string f in new[] { orphanLnk, orphanUrl, other }) { File.WriteAllText(f, "x"); }

      Assert.Equal(2, ShortcutFileService.DeleteUnusedFiles());

      Assert.True(File.Exists(Paths.GetShortcutFilePath(used.Guid, used.Ext)));
      Assert.False(File.Exists(orphanLnk));
      Assert.False(File.Exists(orphanUrl));
      Assert.True(File.Exists(other), "only .lnk and .url files are touched");
    }

    [Fact]
    public void Startup_deletes_nothing_when_there_are_no_buttons() {
      // An empty database may be a fresh start after a problem: keep the files in case the data comes back.
      foreach (Shortcut s in DataStore.Shortcuts.GetAll()) { DataStore.Shortcuts.Delete(s.ID); }
      string orphan = Path.Combine(Paths.GetShortcutsPath(), Identifiers.NewGuid() + ".lnk");
      File.WriteAllText(orphan, "x");
      Assert.Equal(0, ShortcutFileService.DeleteUnusedFiles());
      Assert.True(File.Exists(orphan));
    }

    [Fact]
    public void A_locked_unused_file_is_only_logged() {
      FileShortcut(null, "used");
      string orphan = Path.Combine(Paths.GetShortcutsPath(), Identifiers.NewGuid() + ".lnk");
      File.WriteAllText(orphan, "x");
      using (new FileStream(orphan, FileMode.Open, FileAccess.Read, FileShare.None)) {
        Assert.Equal(0, ShortcutFileService.DeleteUnusedFiles());
      }
      Assert.Contains(log.Events, e => e.Level == LogEventLevel.Warning);
    }
  }
}
