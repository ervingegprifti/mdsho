using System;
using System.IO;
using System.Linq;
using MDSHO.Common;
using MDSHO.Helpers;
using MDSHO.Infrastructure.Logging;
using Serilog.Events;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>The logging seam (AppLog in MDSHO.Common) and the Serilog set-up in the app.</summary>
  public class AppLogTests {

    private sealed class ThrowingWriter : IAppLogWriter {
      public bool Closed;
      public void Write(AppLogLevel level, Exception exception, string messageTemplate, object[] args) {
        throw new IOException("disk full");
      }
      public void Close() {
        Closed = true;
        throw new IOException("disk full");
      }
    }

    [Fact]
    public void Without_a_writer_logging_does_nothing_and_never_throws() {
      IAppLogWriter original = AppLog.Writer;
      try {
        AppLog.Writer = null;
        AppLog.Information("Hello {Name}", "x");
        AppLog.Warning(null, "Careful {Name}", "x");
        AppLog.Error(new InvalidOperationException("boom"), "Failed {Name}", "x");
        AppLog.CloseAndFlush();
      } finally {
        AppLog.Writer = original;
      }
    }

    [Fact]
    public void Events_reach_Serilog_with_level_message_properties_and_exception() {
      using (LogCapture log = new LogCapture()) {
        AppLog.Information("Started {Version}", "1.2.3");
        AppLog.Warning(null, "Odd {Thing}", "x");
        AppLog.Error(new InvalidOperationException("boom"), "Failed {Step}", "export");

        Assert.Equal(3, log.Events.Count);
        Assert.Equal(LogEventLevel.Information, log.Events[0].Level);
        Assert.Equal("Started 1.2.3", log.Messages[0]);
        Assert.Equal(LogEventLevel.Warning, log.Events[1].Level);
        Assert.Equal(LogEventLevel.Error, log.Events[2].Level);
        Assert.Equal("boom", log.Events[2].Exception.Message);
      }
    }

    [Fact]
    public void A_failing_writer_never_crashes_the_app() {
      IAppLogWriter original = AppLog.Writer;
      ThrowingWriter writer = new ThrowingWriter();
      try {
        AppLog.Writer = writer;
        AppLog.Information("Hello");
        AppLog.Error(new Exception("x"), "Failed");
        AppLog.CloseAndFlush();
        Assert.True(writer.Closed);
        Assert.Null(AppLog.Writer);
      } finally {
        AppLog.Writer = original;
      }
    }

    [Fact]
    public void Logs_folder_is_logs_inside_the_app_data_folder() {
      Assert.Equal(Path.Combine(@"X:\Data\MDSHO", "logs"), AppLogging.LogsFolder(@"X:\Data\MDSHO"));
    }

    [Fact]
    public void File_options_roll_daily_and_at_1_MB_and_keep_14_files() {
      AppLogFileOptions options = AppLogging.FileOptions(@"X:\Data\MDSHO\logs");
      Assert.Equal(@"X:\Data\MDSHO\logs\mdsho-.log", options.PathTemplate);
      Assert.Equal(Serilog.RollingInterval.Day, options.RollingInterval);
      Assert.Equal(1024L * 1024L, options.FileSizeLimitBytes);
      Assert.True(options.RollOnFileSizeLimit);
      Assert.Equal(14, options.RetainedFileCountLimit);
      Assert.Equal(TimeSpan.FromDays(14), options.RetainedFileTimeLimit);
      Assert.False(options.Shared);
      Assert.Equal(LogEventLevel.Information, options.MinimumLevel);
    }

    [Fact]
    public void Output_template_has_timestamp_level_message_and_exception() {
      using (LogCapture log = new LogCapture()) {
        AppLog.Error(new InvalidOperationException("boom"), "Export failed");
        string line = LogCapture.FullText(log.Events.Single());
        Assert.Matches(@"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3} [+-]\d{2}:\d{2} \[ERR\] Export failed", line);
        Assert.Contains("System.InvalidOperationException: boom", line);
      }
    }

    [Fact]
    public void When_the_logs_folder_cannot_be_created_the_app_carries_on_without_file_logging() {
      IAppLogWriter original = AppLog.Writer;
      try {
        AppLog.Writer = null;
        bool started = AppLogging.Start(@"X:\Data\MDSHO", folder => throw new UnauthorizedAccessException("no access"));
        Assert.False(started);
        Assert.Null(AppLog.Writer);
        AppLog.Information("still fine");
      } finally {
        AppLog.Writer = original;
      }
    }

    [Fact]
    public void Startup_line_has_version_windows_bitness_packaging_and_folder() {
      using (LogCapture log = new LogCapture()) {
        AppLogging.LogStartup("1.1.1", "Microsoft Windows NT 10.0.19045.0", true, false, @"X:\Data\MDSHO");
        string message = log.Messages.Single();
        Assert.Contains("1.1.1", message);
        Assert.Contains("10.0.19045", message);
        Assert.Contains("64-bit yes", message);
        Assert.Contains("packaged no", message);
        Assert.Contains(@"X:\Data\MDSHO", message);
      }
    }
  }
}
