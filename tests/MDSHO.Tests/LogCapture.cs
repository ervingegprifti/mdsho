using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MDSHO.Common;
using MDSHO.Helpers;
using MDSHO.Infrastructure.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace MDSHO.Tests {

  /// <summary>
  /// Sends <see cref="AppLog"/> through a real Serilog logger into memory for one test (nothing is written to disk) and
  /// puts the previous writer back afterwards.
  /// </summary>
  public sealed class LogCapture : IDisposable, ILogEventSink {
    private readonly IAppLogWriter original = AppLog.Writer;

    /// <summary>Every event logged while the capture is active.</summary>
    public List<LogEvent> Events { get; } = new List<LogEvent>();

    public LogCapture() {
      Logger logger = new LoggerConfiguration().MinimumLevel.Information().WriteTo.Sink(this).CreateLogger();
      AppLog.Writer = new SerilogAppLogWriter(logger);
    }

    public void Emit(LogEvent logEvent) {
      Events.Add(logEvent);
    }

    /// <summary>Rendered messages, one per event, as the log file shows them (strings without quotes).</summary>
    public List<string> Messages => Events.Select(e => Render(e, "{Message:lj}")).ToList();

    private static string Render(LogEvent logEvent, string template) {
      using (StringWriter writer = new StringWriter()) {
        new Serilog.Formatting.Display.MessageTemplateTextFormatter(template).Format(logEvent, writer);
        return writer.ToString();
      }
    }

    /// <summary>Everything an event would put in a log file: the rendered line (with the file template) and the exception.</summary>
    public static string FullText(LogEvent logEvent) {
      return Render(logEvent, AppLogging.OutputTemplate);
    }

    /// <summary>All events as file text.</summary>
    public string AllText => string.Join(Environment.NewLine, Events.Select(FullText));

    public void Dispose() {
      AppLog.Writer = original;
    }
  }
}
