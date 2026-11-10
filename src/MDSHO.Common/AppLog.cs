using System;

namespace MDSHO.Common {

  /// <summary>How important a log event is.</summary>
  public enum AppLogLevel {
    /// <summary>Normal events (startup, exit, export, migrations).</summary>
    Information,
    /// <summary>Something went wrong but the app can carry on.</summary>
    Warning,
    /// <summary>An unexpected error.</summary>
    Error
  }

  /// <summary>Where <see cref="AppLog"/> sends its events. The app plugs in Serilog; tests plug in memory.</summary>
  public interface IAppLogWriter {
    /// <summary>Writes one event. <paramref name="messageTemplate"/> uses Serilog's {Name} placeholders.</summary>
    void Write(AppLogLevel level, Exception exception, string messageTemplate, object[] args);
    /// <summary>Flushes and closes the log.</summary>
    void Close();
  }

  /// <summary>
  /// A small static logging seam, so MDSHO.Common, Core, Data and ViewModels can log without a reference to Serilog.
  /// With no <see cref="Writer"/> nothing is logged. Logging never throws.
  /// </summary>
  public static class AppLog {

    /// <summary>The current writer; null = logging is off.</summary>
    public static IAppLogWriter Writer { get; set; }

    /// <summary>Logs a normal event.</summary>
    public static void Information(string messageTemplate, params object[] args) {
      Write(AppLogLevel.Information, null, messageTemplate, args);
    }

    /// <summary>Logs a problem the app can carry on from; <paramref name="exception"/> may be null.</summary>
    public static void Warning(Exception exception, string messageTemplate, params object[] args) {
      Write(AppLogLevel.Warning, exception, messageTemplate, args);
    }

    /// <summary>Logs an unexpected error.</summary>
    public static void Error(Exception exception, string messageTemplate, params object[] args) {
      Write(AppLogLevel.Error, exception, messageTemplate, args);
    }

    /// <summary>Flushes and closes the log and turns logging off.</summary>
    public static void CloseAndFlush() {
      IAppLogWriter writer = Writer;
      Writer = null;
      try {
        writer?.Close();
      } catch (Exception) {
        // Logging must never crash the app.
      }
    }

    private static void Write(AppLogLevel level, Exception exception, string messageTemplate, object[] args) {
      try {
        Writer?.Write(level, exception, messageTemplate, args ?? Array.Empty<object>());
      } catch (Exception) {
        // Logging must never crash the app.
      }
    }
  }
}
