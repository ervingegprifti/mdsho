using System;
using System.IO;
using MDSHO.Common;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace MDSHO.Infrastructure.Logging {

  /// <summary>Sends <see cref="AppLog"/> events to a Serilog logger.</summary>
  public sealed class SerilogAppLogWriter : IAppLogWriter {
    private readonly Logger logger;

    /// <summary>Wraps a Serilog logger; <see cref="Close"/> disposes it.</summary>
    public SerilogAppLogWriter(Logger logger) {
      this.logger = logger;
    }

    /// <summary>Writes one event.</summary>
    public void Write(AppLogLevel level, Exception exception, string messageTemplate, object[] args) {
      this.logger.Write(ToSerilog(level), exception, messageTemplate, args);
    }

    /// <summary>Flushes and closes the log file.</summary>
    public void Close() {
      this.logger.Dispose();
    }

    private static LogEventLevel ToSerilog(AppLogLevel level) {
      switch (level) {
        case AppLogLevel.Warning: return LogEventLevel.Warning;
        case AppLogLevel.Error: return LogEventLevel.Error;
        default: return LogEventLevel.Information;
      }
    }
  }

  /// <summary>The settings of the log files.</summary>
  public sealed class AppLogFileOptions {
    /// <summary>Path with the date inserted before the extension by Serilog: mdsho-yyyyMMdd.log.</summary>
    public string PathTemplate { get; set; }
    /// <summary>A new file every day.</summary>
    public RollingInterval RollingInterval { get; set; }
    /// <summary>Largest size of one file.</summary>
    public long FileSizeLimitBytes { get; set; }
    /// <summary>Start a new file when the size limit is reached.</summary>
    public bool RollOnFileSizeLimit { get; set; }
    /// <summary>Most files kept.</summary>
    public int RetainedFileCountLimit { get; set; }
    /// <summary>Older files are deleted.</summary>
    public TimeSpan RetainedFileTimeLimit { get; set; }
    /// <summary>Whether other processes may write the same file.</summary>
    public bool Shared { get; set; }
    /// <summary>Lowest level written.</summary>
    public LogEventLevel MinimumLevel { get; set; }
  }

  /// <summary>Sets up the log files in the app data folder (the same folder "Browse app data" opens).</summary>
  public static class AppLogging {

    /// <summary>Name of the logs folder inside the app data folder.</summary>
    public const string LogsFolderName = "logs";

    /// <summary>One line per event: timestamp, level, message, then the exception (if any) on the next lines.</summary>
    public const string OutputTemplate = "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}";

    /// <summary>The logs folder in use, or null when file logging is off.</summary>
    public static string CurrentLogsFolder { get; private set; }

    /// <summary>The logs folder for an app data folder.</summary>
    public static string LogsFolder(string appDataFolder) {
      return Path.Combine(appDataFolder, LogsFolderName);
    }

    /// <summary>The file settings for a logs folder.</summary>
    public static AppLogFileOptions FileOptions(string logsFolder) {
      return new AppLogFileOptions {
        PathTemplate = Path.Combine(logsFolder, "mdsho-.log"),
        RollingInterval = RollingInterval.Day,
        FileSizeLimitBytes = 1024L * 1024L,
        RollOnFileSizeLimit = true,
        RetainedFileCountLimit = 14,
        RetainedFileTimeLimit = TimeSpan.FromDays(14),
        Shared = false,
        MinimumLevel = LogEventLevel.Information
      };
    }

    /// <summary>
    /// Creates the Serilog logger of the log files in <c>&lt;appDataFolder&gt;\logs</c>. <paramref name="ensureFolder"/>
    /// creates the folder (Directory.CreateDirectory in the app). Never throws: returns null when anything fails, and
    /// the app carries on without file logging. Events of Microsoft.* loggers (the host) below Warning are dropped.
    /// </summary>
    public static Logger CreateFileLogger(string appDataFolder, Action<string> ensureFolder) {
      try {
        string logsFolder = LogsFolder(appDataFolder);
        ensureFolder(logsFolder);
        AppLogFileOptions options = FileOptions(logsFolder);
        Logger logger = new LoggerConfiguration()
          .MinimumLevel.Is(options.MinimumLevel)
          .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
          .WriteTo.File(options.PathTemplate,
            outputTemplate: OutputTemplate,
            fileSizeLimitBytes: options.FileSizeLimitBytes,
            rollingInterval: options.RollingInterval,
            rollOnFileSizeLimit: options.RollOnFileSizeLimit,
            retainedFileCountLimit: options.RetainedFileCountLimit,
            retainedFileTimeLimit: options.RetainedFileTimeLimit,
            shared: options.Shared)
          .CreateLogger();
        CurrentLogsFolder = logsFolder;
        return logger;
      } catch (Exception) {
        CurrentLogsFolder = null;
        return null;
      }
    }

    /// <summary>
    /// Starts file logging: <see cref="CreateFileLogger"/>, then sends <see cref="AppLog"/> to it. Never throws.
    /// </summary>
    /// <returns>True when file logging started.</returns>
    public static bool Start(string appDataFolder, Action<string> ensureFolder) {
      Logger logger = CreateFileLogger(appDataFolder, ensureFolder);
      if (logger == null) {
        return false;
      }
      AppLog.Writer = new SerilogAppLogWriter(logger);
      return true;
    }

    /// <summary>
    /// Opens the logs folder in Explorer (through <paramref name="shell"/>), or the app data folder when there is no logs
    /// folder yet.
    /// </summary>
    public static void OpenLogsFolder(IAppDataLocator appDataLocator, IShellLauncher shell) {
      string folder = CurrentLogsFolder;
      if (folder == null || !Directory.Exists(folder)) {
        string appData = appDataLocator.Locate();
        folder = LogsFolder(appData);
        if (!Directory.Exists(folder)) {
          folder = appData;
        }
      }
      shell.OpenFolder(folder);
    }

    /// <summary>Logs the first line of a run: what is running and where its data is.</summary>
    public static void LogStartup(string version, string windowsVersion, bool is64Bit, bool packaged, string appDataFolder) {
      AppLog.Information("MDSHO {Version} started on {WindowsVersion}, 64-bit {Is64Bit}, packaged {Packaged}, app data folder {AppDataFolder}",
        version, windowsVersion, is64Bit ? "yes" : "no", packaged ? "yes" : "no", appDataFolder);
    }
  }
}
