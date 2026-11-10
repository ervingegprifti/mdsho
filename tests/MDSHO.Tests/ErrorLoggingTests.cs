using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using MDSHO.ViewModels;
using Serilog.Events;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// Unexpected errors are logged (every error window logs its message and exception) and failed run commands are logged
  /// with the program name only: never their arguments or working folder, which can hold passwords or server names.
  /// </summary>
  public class ErrorLoggingTests : IDisposable {
    private const string Secret = "hunter2-password";
    private const string SecretFolder = @"\\secret-server\share";
    private readonly Action<ProcessStartInfo> oldStarter = ShortcutViewModel.ProcessStarter;
    private readonly Action<ErrorWindow> oldShow = ErrorDialog.ShowWindow;
    private readonly LogCapture log = new LogCapture();

    public void Dispose() {
      ShortcutViewModel.ProcessStarter = oldStarter;
      ErrorDialog.ShowWindow = oldShow;
      log.Dispose();
    }

    private static ProcessStartInfo SecretCommand() {
      return ShortcutViewModel.CreateRunCommandStartInfo(@"C:\Tools\sqlcmd.exe", "-S prod-db -P " + Secret, SecretFolder, false, null);
    }

    [Fact]
    public void A_run_command_that_fails_to_start_logs_the_program_but_not_its_arguments_or_folder() {
      // .NET puts the working folder into the exception message; simulate the worst case with the arguments too.
      ShortcutViewModel.ProcessStarter = psi =>
        throw new Win32Exception(2, "An error occurred trying to start process 'sqlcmd.exe " + psi.Arguments + "' with working directory '" + psi.WorkingDirectory + "'.");

      Assert.Throws<Win32Exception>(() => ShortcutViewModel.StartRunCommand(SecretCommand()));

      LogEvent e = log.Events.Single();
      Assert.Equal(LogEventLevel.Warning, e.Level);
      string text = log.AllText + string.Join(" ", e.Properties.Values.Select(v => v.ToString()));
      Assert.Contains(@"C:\Tools\sqlcmd.exe", text);
      Assert.Contains("2", text);
      Assert.DoesNotContain(Secret, text);
      Assert.DoesNotContain("prod-db", text);
      Assert.DoesNotContain("secret-server", text);
      Assert.Null(e.Exception);
    }

    [Fact]
    public void A_declined_administrator_prompt_is_information_without_arguments() {
      ShortcutViewModel.ProcessStarter = psi => throw new Win32Exception(ShortcutViewModel.ErrorCancelled);

      ShortcutViewModel.StartRunCommand(SecretCommand());

      LogEvent e = log.Events.Single();
      Assert.Equal(LogEventLevel.Information, e.Level);
      Assert.DoesNotContain(Secret, log.AllText);
      Assert.DoesNotContain("secret-server", log.AllText);
    }

    [Fact]
    public void A_run_command_that_starts_logs_nothing() {
      ShortcutViewModel.ProcessStarter = psi => { };
      ShortcutViewModel.StartRunCommand(SecretCommand());
      Assert.Empty(log.Events);
    }

    [Fact]
    public void The_error_window_logs_its_message_and_exception() {
      ErrorWindow shown = null;
      ErrorDialog.ShowWindow = w => shown = w;
      WpfAppHost.Run(() => {
        ErrorDialog.ShowDialog("The data could not be exported.", new InvalidOperationException("boom"));
        shown.Close();
      });
      Assert.NotNull(shown);
      LogEvent e = log.Events.Single();
      Assert.Equal(LogEventLevel.Error, e.Level);
      Assert.Contains("The data could not be exported.", log.Messages.Single());
      Assert.Equal("boom", e.Exception.Message);
    }

    [Fact]
    public void The_error_window_without_a_message_still_logs_the_exception() {
      ErrorWindow shown = null;
      ErrorDialog.ShowWindow = w => shown = w;
      WpfAppHost.Run(() => {
        ErrorDialog.ShowDialog(new DivideByZeroException());
        shown.Close();
      });
      Assert.IsType<DivideByZeroException>(log.Events.Single().Exception);
    }

    [Fact]
    public void An_unhandled_exception_logs_where_it_came_from_once() {
      ErrorWindow shown = null;
      ErrorDialog.ShowWindow = w => shown = w;
      WpfAppHost.Run(() => {
        ErrorDialog.ShowUnhandled("TaskScheduler.UnobservedTaskException", new InvalidOperationException("lost task"));
        shown.Close();
      });
      LogEvent e = log.Events.Single();
      Assert.Equal(LogEventLevel.Error, e.Level);
      Assert.Contains("TaskScheduler.UnobservedTaskException", log.Messages.Single());
      Assert.Equal("lost task", e.Exception.Message);
    }

    [Fact]
    public void Logging_still_happens_when_the_error_window_cannot_be_built() {
      // A window off the UI thread throws; the log line must already be written.
      Assert.ThrowsAny<Exception>(() => ErrorDialog.ShowDialog("off thread", new InvalidOperationException("boom")));
      Assert.Contains("off thread", log.Messages.Single());
    }
  }
}
