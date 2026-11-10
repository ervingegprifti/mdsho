using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using MDSHO.ViewModels;
using Serilog.Events;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>The open issues found by the 1.1.1 bug hunt: Alt+F4, Find Windows, a run command without a program.</summary>
  public class BugHuntFollowUpTests : IDisposable {
    private readonly TempDb db = new TempDb();
    private readonly AppViewModel app = new AppViewModel();
    private readonly DesktopWindowViewModel win;

    public BugHuntFollowUpTests() {
      win = app.AddWindow("TEST", 0, 0);
    }
    public void Dispose() { db.Dispose(); }

    // ---- 1. Alt+F4 ----

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
    private const int WM_SYSCOMMAND = 0x0112;
    private const int SC_CLOSE = 0xF060;

    /// <summary>What Alt+F4 (and the taskbar's Close window) sends to a window.</summary>
    private static void SendAltF4(Window view) {
      SendMessage(new WindowInteropHelper(view).Handle, WM_SYSCOMMAND, (IntPtr)SC_CLOSE, IntPtr.Zero);
      WpfAppHost.Pump();
    }

    private static DesktopWindowView ShowView(DesktopWindowViewModel vm, Action onClosed) {
      DesktopWindowView view = new DesktopWindowView(vm) { ShowActivated = false, ShowInTaskbar = false, Left = -10000, Top = -10000 };
      view.Closed += (s, e) => onClosed();
      view.Show();
      WpfAppHost.Pump();
      return view;
    }

    private long CloneRows(long id) { return db.Scalar("SELECT COUNT(*) FROM MDSHOWindowClones WHERE ID=" + id); }
    private long WindowRows(long id) { return db.Scalar("SELECT COUNT(*) FROM MDSHOWindows WHERE ID=" + id); }

    [Fact]
    public void Alt_F4_on_a_clone_closes_it_like_Close_this_Clone_and_frees_its_screen() {
      WpfAppHost.Run(() => {
        win.CloneWindow("TEST2", 0, 0);
        DesktopWindowViewModel clone = win.Clones.Single();
        bool closed = false;
        DesktopWindowView view = ShowView(clone, () => closed = true);
        try {
          Assert.Equal(1, CloneRows(clone.ID));
          SendAltF4(view);
          Assert.True(closed, "the clone window closes");
          Assert.Equal(0, CloneRows(clone.ID));
          Assert.DoesNotContain(clone, win.Clones);
          Assert.Equal(1, WindowRows(win.ID));
        } finally {
          if (!closed) { view.Close(); }
        }
      });
    }

    [Fact]
    public void Alt_F4_on_a_real_window_is_ignored() {
      WpfAppHost.Run(() => {
        bool closed = false;
        DesktopWindowView view = ShowView(win, () => closed = true);
        try {
          SendAltF4(view);
          Assert.False(closed, "Alt+F4 must not close a real window");
          Assert.True(view.IsVisible);
          Assert.Equal(1, WindowRows(win.ID));
          Assert.Contains(win, app.Windows);
        } finally {
          // Closing from code (what app exit does) still closes it, and deletes nothing.
          view.Close();
          WpfAppHost.Pump();
        }
        Assert.True(closed);
        Assert.Equal(1, WindowRows(win.ID));
      });
    }

    [Fact]
    public void Closing_windows_and_clones_from_code_like_app_exit_deletes_nothing() {
      WpfAppHost.Run(() => {
        win.CloneWindow("TEST2", 0, 0);
        DesktopWindowViewModel clone = win.Clones.Single();
        bool windowClosed = false, cloneClosed = false;
        DesktopWindowView windowView = ShowView(win, () => windowClosed = true);
        DesktopWindowView cloneView = ShowView(clone, () => cloneClosed = true);
        cloneView.Close();
        windowView.Close();
        WpfAppHost.Pump();
        Assert.True(windowClosed && cloneClosed);
        Assert.Equal(1, CloneRows(clone.ID));
        Assert.Equal(1, WindowRows(win.ID));
        Assert.Contains(clone, win.Clones);
      });
    }

    // ---- 2. Find Windows ----

    [Fact]
    public void Find_windows_saves_the_new_positions_of_windows_and_clones() {
      DesktopWindowViewModel second = app.AddWindow("GONE", 9000, 9000);
      win.CloneWindow("GONE2", 7000, 7000);
      DesktopWindowViewModel clone = win.Clones.Single();
      MDSHO.Common.ScreenRect bounds = new MDSHO.Common.ScreenRect(0, 0, 1920, 1080);
      MDSHO.Common.ScreenInfo primary = new MDSHO.Common.ScreenInfo("PRIMARY", bounds, bounds, 1.0, true);

      app.FindWindows(primary);

      // What a restart loads is where Find Windows put them.
      foreach (DesktopWindowViewModel w in new[] { win, second }) {
        MDSHO.Core.DesktopWindow row = MDSHO.Data.DataStore.Windows.Get(w.ID);
        Assert.Equal((w.Left, w.Top, w.Width, w.Height, "PRIMARY"), (row.Left, row.Top, row.Width, row.Height, row.ScreenDeviceName));
        Assert.True(bounds.Contains(new MDSHO.Common.ScreenPoint(w.Left, w.Top)), "window " + w.ID + " is on the primary screen");
      }
      MDSHO.Core.DesktopWindowClone cloneRow = MDSHO.Data.DataStore.Clones.Get(clone.ID);
      Assert.Equal((clone.Left, clone.Top, clone.Width, clone.Height, "PRIMARY"), (cloneRow.Left, cloneRow.Top, cloneRow.Width, cloneRow.Height, cloneRow.ScreenDeviceName));
      Assert.True(bounds.Contains(new MDSHO.Common.ScreenPoint(clone.Left, clone.Top)));
      Assert.Equal("PRIMARY", clone.ScreenDeviceName);
    }

    // ---- 3. Run command without a program ----

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Running_a_run_command_without_a_program_shows_a_plain_message_and_logs_no_error(string program) {
      ShortcutViewModel command = ViewModelFactory.NewProcessCommandShortcut(app, null, win);
      win.RootShortcuts.Add(command);
      command.ProcessFileName = program;
      command.ProcessArguments = "/K dir";
      List<(string Message, string Caption)> shown = new List<(string, string)>();
      Dialogs.ShowMessage = (message, caption) => shown.Add((message, caption)); // TempDb puts the original back
      List<ProcessStartInfo> started = new List<ProcessStartInfo>();
      Action<ProcessStartInfo> oldStarter = ShortcutViewModel.ProcessStarter;
      // Like the real shell launcher: Process.Start refuses an empty file name.
      ShortcutViewModel.ProcessStarter = psi => {
        if (string.IsNullOrWhiteSpace(psi.FileName)) { throw new InvalidOperationException("Cannot start process because a file name has not been provided."); }
        started.Add(psi);
      };
      try {
        using (LogCapture log = new LogCapture()) {
          command.RunShortcut(false);
          (string message, string caption) = Assert.Single(shown);
          Assert.Equal("Set a program first. Use Set Run Command... to choose one.", message);
          Assert.Equal("Run Command", caption);
          Assert.Empty(started);
          Assert.DoesNotContain(log.Events, e => e.Level >= LogEventLevel.Warning);
        }
      } finally {
        ShortcutViewModel.ProcessStarter = oldStarter;
      }
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Test_run_in_the_Run_Command_window_without_a_program_shows_the_same_plain_message_and_logs_no_warning(string program) {
      ShortcutViewModel command = ViewModelFactory.NewProcessCommandShortcut(app, null, win);
      win.RootShortcuts.Add(command);
      command.ProcessFileName = "cmd.exe";
      RunCommandViewModel editor = new RunCommandViewModel(command, false) { Program = program };
      List<(string Message, string Caption)> shown = new List<(string, string)>();
      Dialogs.ShowMessage = (message, caption) => shown.Add((message, caption)); // TempDb puts the original back
      List<ProcessStartInfo> started = new List<ProcessStartInfo>();
      Action<ProcessStartInfo> oldStarter = ShortcutViewModel.ProcessStarter;
      // Like the real shell launcher: Process.Start refuses an empty file name.
      ShortcutViewModel.ProcessStarter = psi => {
        if (string.IsNullOrWhiteSpace(psi.FileName)) { throw new InvalidOperationException("Cannot start process because a file name has not been provided."); }
        started.Add(psi);
      };
      try {
        using (LogCapture log = new LogCapture()) {
          editor.TestRunCommand.Execute(null);
          (string message, string caption) = Assert.Single(shown);
          Assert.Equal("Set a program first. Use Set Run Command... to choose one.", message);
          Assert.Equal("Run Command", caption);
          Assert.Empty(started);
          Assert.DoesNotContain(log.Events, e => e.Level >= LogEventLevel.Warning);
        }
      } finally {
        ShortcutViewModel.ProcessStarter = oldStarter;
      }
    }
  }
}
