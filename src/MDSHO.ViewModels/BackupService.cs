using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using MDSHO.Common;
using MDSHO.Data;

namespace MDSHO.ViewModels {

  /// <summary>
  /// Automatic backups: the same zip as Export data, written to a local folder (%USERPROFILE%\mdsho-backups unless the user
  /// picks another, such as a OneDrive or Google Drive folder). Checked at start and every hour: a backup is written when
  /// backups are on, the last one is 24 hours old or more, and the data changed. Keeps the last N regular backups of this
  /// PC. Settings are in AppSettings, on this PC only. A failure is logged and saved for Settings, never shown in a popup.
  /// </summary>
  public sealed class BackupService {
    /// <summary>AppSettings key: "On" or "Off". No row means on.</summary>
    public const string OnKey = "BackupOn";
    /// <summary>AppSettings key: the folder. No row or "" means the default folder in the user's profile folder.</summary>
    public const string FolderKey = "BackupFolder";
    /// <summary>AppSettings key: how many regular backups of this PC to keep (one of <see cref="KeepChoices"/>).</summary>
    public const string KeepKey = "BackupKeep";
    /// <summary>AppSettings key: when the last regular backup was written (UTC, round-trip format).</summary>
    public const string LastTimeKey = "BackupLastTime";
    /// <summary>AppSettings key: the data hash of the last regular backup.</summary>
    public const string LastHashKey = "BackupLastHash";
    /// <summary>AppSettings key: the reason the last backup failed; "" after a success.</summary>
    public const string LastErrorKey = "BackupLastError";

    /// <summary>The keep counts Settings offers.</summary>
    public static readonly int[] KeepChoices = { 3, 5, 10, 20, 30 };
    /// <summary>The keep count without a setting.</summary>
    public const int DefaultKeep = 10;
    /// <summary>How often the check runs.</summary>
    public static readonly TimeSpan CheckEvery = TimeSpan.FromHours(1);
    /// <summary>How old the last backup must be before the next one.</summary>
    public static readonly TimeSpan DueAfter = TimeSpan.FromHours(24);

    private readonly TimeProvider clock;
    private readonly string machineName;
    private readonly string pc;
    private readonly string profileFolder;
    private readonly string documentsFolder;
    private readonly string appVersion;
    private readonly IOneShotTimer timer;
    private readonly Action<Action> runInBackground;
    private readonly object writing = new object();
    private bool running;

    /// <param name="clock">The time.</param>
    /// <param name="machineName">The PC name (Environment.MachineName in the app).</param>
    /// <param name="profileFolder">The user's profile folder (%USERPROFILE%). Not Documents: ransomware shields and
    /// Controlled folder access block writes there.</param>
    /// <param name="appVersion">This MDSHO's version, for the name of the backup before a database upgrade.</param>
    /// <param name="timer">Calls back after the hour.</param>
    /// <param name="runInBackground">Runs the check (Task.Run in the app).</param>
    /// <param name="documentsFolder">The user's Documents folder, only to recognise the old default folder there.</param>
    public BackupService(TimeProvider clock, string machineName, string profileFolder, string appVersion, IOneShotTimer timer, Action<Action> runInBackground, string documentsFolder = null) {
      this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
      this.machineName = machineName;
      this.pc = BackupFiles.SafePcName(machineName);
      this.profileFolder = profileFolder ?? "";
      this.documentsFolder = documentsFolder;
      this.appVersion = appVersion;
      this.timer = timer ?? throw new ArgumentNullException(nameof(timer));
      this.runInBackground = runInBackground ?? throw new ArgumentNullException(nameof(runInBackground));
    }

    /// <summary>Raised after a backup was written or failed (maybe on a background thread).</summary>
    public event Action Changed;

    #region SETTINGS
    /// <summary>Back up my data every day. On unless turned off.</summary>
    public bool IsOn => Read(OnKey) != "Off";

    /// <summary>The default folder: %USERPROFILE%\mdsho-backups.</summary>
    public string DefaultFolder {
      get {
#if DEBUG
        return Path.Combine(this.profileFolder, AppConstants.BackupsDebugFolderName);
#else
        return Path.Combine(this.profileFolder, AppConstants.BackupsFolderName);
#endif
      }
    }

    /// <summary>The folder the backups go to.</summary>
    public string Folder {
      get {
        string saved = Read(FolderKey);
        return IsDefault(saved) ? DefaultFolder : saved;
      }
    }

    /// <summary>How many regular backups of this PC are kept.</summary>
    public int Keep => ValidKeep(int.TryParse(Read(KeepKey), NumberStyles.Integer, CultureInfo.InvariantCulture, out int keep) ? keep : DefaultKeep);

    /// <summary>When the last regular backup was written, or null.</summary>
    public DateTimeOffset? LastBackup {
      get {
        return DateTimeOffset.TryParse(Read(LastTimeKey), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTimeOffset t) ? t : null;
      }
    }

    /// <summary>"Last backup: today 09:12", "yesterday 09:12", "15 Nov 2026 09:12" or "Never".</summary>
    public string LastBackupText {
      get {
        DateTimeOffset? last = LastBackup;
        if (last == null) {
          return "Last backup: Never";
        }
        DateTime local = TimeZoneInfo.ConvertTime(last.Value, this.clock.LocalTimeZone).DateTime;
        DateTime today = this.clock.GetLocalNow().Date;
        string time = local.ToString("HH:mm", CultureInfo.InvariantCulture);
        if (local.Date == today) {
          return "Last backup: today " + time;
        }
        if (local.Date == today.AddDays(-1)) {
          return "Last backup: yesterday " + time;
        }
        return "Last backup: " + local.ToString("d MMM yyyy HH:mm", CultureInfo.InvariantCulture);
      }
    }

    /// <summary>"The last backup failed: reason." after a failure, null after a success.</summary>
    public string LastErrorText {
      get {
        string reason = Read(LastErrorKey);
        return string.IsNullOrEmpty(reason) ? null : "The last backup failed: " + reason + ".";
      }
    }

    /// <summary>Saves the settings (Apply in Settings). The default folder is saved as no value, so it follows the profile folder.</summary>
    public void Save(bool on, string folder, int keep) {
      DataStore.AppSettings.Set(OnKey, on ? "On" : "Off");
      DataStore.AppSettings.Set(FolderKey, IsDefault(folder) ? "" : folder);
      DataStore.AppSettings.Set(KeepKey, ValidKeep(keep).ToString(CultureInfo.InvariantCulture));
      AppLog.Information("Backups set to {On}, keeping {Keep}", on ? "on" : "off", ValidKeep(keep));
    }

    /// <summary>The folder Import data's file dialog opens in: the backup folder when it exists, else null.</summary>
    public string ImportStartFolder() {
      string folder = Folder;
      return Directory.Exists(folder) ? folder : null;
    }

    /// <summary>
    /// The newest backup (regular or before an upgrade) of this PC in the backup folder, else the newest of any PC, else
    /// null. Used when the database can't be opened: then the saved folder can't be read either and the default is used.
    /// </summary>
    public string LatestBackup() {
      string[] backups = new[] { Folder, DefaultFolder }.Distinct(StringComparer.OrdinalIgnoreCase).Where(Directory.Exists)
        .SelectMany(folder => Directory.GetFiles(folder, "MDSHO-backup-*.zip"))
        .OrderByDescending(File.GetLastWriteTimeUtc).ToArray();
      string mine = "MDSHO-backup-" + this.pc + "-";
      return backups.FirstOrDefault(f => Path.GetFileName(f).StartsWith(mine, StringComparison.OrdinalIgnoreCase)) ?? backups.FirstOrDefault();
    }
    #endregion SETTINGS

    #region BACKING UP
    /// <summary>Checks now, then every hour, each check in the background.</summary>
    public void Start() {
      this.running = true;
      this.runInBackground(() => RunIfDue());
      Arm();
    }

    /// <summary>Stops the hourly check.</summary>
    public void Stop() {
      this.running = false;
      this.timer.Stop();
    }

    private void Arm() {
      this.timer.Start(CheckEvery, () => {
        if (!this.running) {
          return;
        }
        this.runInBackground(() => RunIfDue());
        Arm();
      });
    }

    /// <summary>
    /// Writes a backup when one is due: backups on, the last one 24 hours old or more (or none), and the data changed.
    /// Returns true when a file was written. Never throws: a failure is saved for Settings and logged.
    /// </summary>
    public bool RunIfDue() {
      return RunIfChanged(true);
    }

    /// <summary>
    /// When MDSHO exits (or Windows signs out): writes a backup when backups are on and the data changed since the last
    /// one, whatever its age. Returns true when a file was written. Never throws: a failure is saved for Settings and logged.
    /// </summary>
    public bool RunOnExit() {
      return RunIfChanged(false);
    }

    private bool RunIfChanged(bool dailyOnly) {
      lock (this.writing) {
        string folder = null;
        try {
          if (!IsOn) {
            return false;
          }
          DateTimeOffset? last = LastBackup;
          if (dailyOnly && last != null && this.clock.GetUtcNow() - last.Value < DueAfter) {
            return false;
          }
          string hash = DataExporter.DataHash();
          if (hash == Read(LastHashKey)) {
            return false;
          }
          folder = Folder;
          Write(folder, Keep, hash);
          return true;
        } catch (Exception ex) {
          Failed(ex, folder ?? Folder);
          return false;
        }
      }
    }

    /// <summary>Back up now (Settings): writes a backup at once, even with no change, to the folder given (else the saved
    /// one). Returns false after a failure, which is saved for Settings and logged.</summary>
    public bool BackUpNow(string folder = null, int? keep = null) {
      lock (this.writing) {
        folder ??= Folder;
        try {
          Write(folder, ValidKeep(keep ?? Keep), DataExporter.DataHash());
          return true;
        } catch (Exception ex) {
          Failed(ex, folder);
          return false;
        }
      }
    }

    /// <summary>
    /// Before a database upgrade: backs up the old data, whatever the on/off setting. Returns the file, or null when no
    /// upgrade is due or the backup failed (logged; MDSHO starts anyway). These files are never deleted by MDSHO.
    /// </summary>
    public string BackUpBeforeUpgrade() {
      lock (this.writing) {
        try {
          int? from = DataStore.VersionBeforeUpgrade();
          if (from == null) {
            return null;
          }
          string folder = Folder;
          EnsureFolder(folder);
          string path = Path.Combine(folder, BackupFiles.UpgradeName(this.pc, this.appVersion, this.clock.GetLocalNow().DateTime));
          DataExporter.Export(path, new ExportOptions { PcName = this.machineName, BeforeUpgrade = true });
          AppLog.Information("Backup before the database upgrade from version {Version} written to {Path}", from.Value, path);
          return path;
        } catch (Exception ex) {
          AppLog.Error(ex, "The backup before the database upgrade failed; MDSHO starts anyway");
          return null;
        }
      }
    }

    /// <summary>
    /// Before this PC joins sync: backs up its data to the backup folder, whatever the on/off setting, as
    /// MDSHO-backup-PC-before-sync-....zip, which MDSHO never deletes. Returns the file, or null after a failure (saved
    /// for Settings and logged).
    /// </summary>
    public string BackUpForSync() {
      lock (this.writing) {
        string folder = Folder;
        try {
          EnsureFolder(folder);
          string path = Path.Combine(folder, BackupFiles.SyncName(this.pc, this.clock.GetLocalNow().DateTime));
          DataExporter.Export(path, new ExportOptions { PcName = this.machineName });
          AppLog.Information("Backup before joining sync written to {Path}", path);
          return path;
        } catch (Exception ex) {
          Failed(ex, folder);
          return null;
        }
      }
    }

    private void Write(string folder, int keep, string hash) {
      EnsureFolder(folder);
      string path = Path.Combine(folder, BackupFiles.RegularName(this.pc, this.clock.GetLocalNow().DateTime));
      DataExporter.Export(path, new ExportOptions { PcName = this.machineName });
      DataStore.AppSettings.Set(LastTimeKey, this.clock.GetUtcNow().ToString("o", CultureInfo.InvariantCulture));
      DataStore.AppSettings.Set(LastHashKey, hash);
      DataStore.AppSettings.Set(LastErrorKey, "");
      AppLog.Information("Backup written to {Path}", path);
      DeleteOld(folder, keep);
      Changed?.Invoke();
    }

    // Deletes the oldest regular backups of this PC past the keep count. Other files are never touched. A delete that
    // fails (an antivirus blocking it, say) is only logged: the new backup is written, so it isn't a failed backup.
    private void DeleteOld(string folder, int keep) {
      string[] mine = Directory.GetFiles(folder).Select(Path.GetFileName).Where(n => BackupFiles.IsRegularOf(this.pc, n))
        .OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToArray();
      foreach (string name in mine.Take(Math.Max(0, mine.Length - keep))) {
        try {
          File.Delete(Path.Combine(folder, name));
          AppLog.Information("Old backup deleted: {Name}", name);
        } catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) {
          AppLog.Warning(ex, "An old backup could not be deleted: {Name}", name);
        }
      }
    }

    private void Failed(Exception ex, string folder) {
      string reason = FailureReason(ex, folder);
      AppLog.Warning(ex, "Backup failed: {Reason}", reason);
      try {
        DataStore.AppSettings.Set(LastErrorKey, reason);
      } catch (Exception saveEx) {
        AppLog.Warning(saveEx, "The backup failure could not be saved");
      }
      Changed?.Invoke();
    }

    /// <summary>A short reason for Settings, without the final full stop.</summary>
    public static string FailureReason(Exception ex, string folder) {
      if (ex is FolderNotFoundException) {
        return "the folder " + folder + " can't be found";
      }
      // Access denied, also from Controlled folder access and antivirus ransomware shields.
      if (ex is UnauthorizedAccessException || (ex is IOException && (ex.HResult == unchecked((int)0x80070005) ||
          ex.HResult == unchecked((int)0x800700E1) || ex.HResult == unchecked((int)0x800700E2)))) {
        return "your antivirus or Windows blocked MDSHO from writing to " + folder + ". Allow MDSHO there, or pick another folder";
      }
      // ERROR_DISK_FULL and ERROR_HANDLE_DISK_FULL
      if (ex is IOException && (ex.HResult == unchecked((int)0x80070070) || ex.HResult == unchecked((int)0x80070027))) {
        return "the disk is full";
      }
      return ex.Message.Trim().TrimEnd('.');
    }

    // Creates the folder when needed; a folder that can't be made (a missing drive, say) "can't be found".
    private static void EnsureFolder(string folder) {
      if (string.IsNullOrWhiteSpace(folder) || !Path.IsPathFullyQualified(folder)) {
        throw new FolderNotFoundException();
      }
      if (Directory.Exists(folder)) {
        return;
      }
      try {
        Directory.CreateDirectory(folder);
      } catch (Exception ex) when (ex is IOException || ex is ArgumentException || ex is NotSupportedException) {
        throw new FolderNotFoundException(ex);
      }
    }

    private sealed class FolderNotFoundException : Exception {
      public FolderNotFoundException() : base("The backup folder can't be found.") { }
      public FolderNotFoundException(Exception inner) : base("The backup folder can't be found.", inner) { }
    }
    #endregion BACKING UP

    private static int ValidKeep(int keep) {
      return KeepChoices.Contains(keep) ? keep : DefaultKeep;
    }

    // No folder, the default, or an old default (so a later default applies). Full paths, ignoring case.
    private bool IsDefault(string folder) {
      if (string.IsNullOrWhiteSpace(folder) || SameFolder(folder, DefaultFolder)) {
        return true;
      }
      foreach (string parent in new[] { this.profileFolder, this.documentsFolder }) {
        if (!string.IsNullOrEmpty(parent) && AppConstants.OldBackupsFolderNames.Any(name => SameFolder(folder, Path.Combine(parent, name)))) {
          return true;
        }
      }
      return false;
    }

    private static bool SameFolder(string a, string b) {
      if (string.IsNullOrWhiteSpace(a)) {
        return true;
      }
      return string.Equals(a.TrimEnd('\\', '/'), b.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase);
    }

    // A setting, or null when it can't be read (an old database without AppSettings, say).
    private static string Read(string key) {
      try {
        return DataStore.AppSettings.Get(key);
      } catch (Exception ex) {
        AppLog.Warning(ex, "The backup setting {Key} could not be read", key);
        return null;
      }
    }
  }

  /// <summary>Backup file names: MDSHO-backup-PC-yyyy-MM-dd-HHmmss.zip and MDSHO-backup-PC-before-1.12.0-yyyy-MM-dd-HHmmss.zip.</summary>
  public static class BackupFiles {
    private const string Stamp = "yyyy-MM-dd-HHmmss";

    /// <summary>The PC name with only letters, digits, - and _ (others become _); "PC" when empty.</summary>
    public static string SafePcName(string name) {
      return FileNames.SafePcName(name);
    }

    /// <summary>The name of a regular backup.</summary>
    public static string RegularName(string pc, DateTime local) {
      return "MDSHO-backup-" + pc + "-" + local.ToString(Stamp, CultureInfo.InvariantCulture) + ".zip";
    }

    /// <summary>The name of the backup before a database upgrade to <paramref name="version"/>.</summary>
    public static string UpgradeName(string pc, string version, DateTime local) {
      return "MDSHO-backup-" + pc + "-before-" + version + "-" + local.ToString(Stamp, CultureInfo.InvariantCulture) + ".zip";
    }

    /// <summary>The name of the backup made before this PC joins sync.</summary>
    public static string SyncName(string pc, DateTime local) {
      return "MDSHO-backup-" + pc + "-before-sync-" + local.ToString(Stamp, CultureInfo.InvariantCulture) + ".zip";
    }

    /// <summary>True only for a regular backup of this PC with exactly that pattern.</summary>
    public static bool IsRegularOf(string pc, string fileName) {
      return Regex.IsMatch(fileName ?? "", "^MDSHO-backup-" + Regex.Escape(pc) + @"-\d{4}-\d{2}-\d{2}-\d{6}\.zip$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }
  }

  /// <summary>The question Import data asks before Replace or Add as new windows.</summary>
  public static class ImportQuestion {
    /// <summary>"This file is from HOMEPC, 15 Nov 2026 09:12: 4 panels, 2 clones, 52 buttons." and the choices.</summary>
    public static string Text(ExportManifest manifest, TimeZoneInfo zone) {
      DateTime when = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(manifest.ExportedAtUtc, DateTimeKind.Utc), zone);
      string date = when.ToString("d MMM yyyy HH:mm", CultureInfo.InvariantCulture);
      string from = string.IsNullOrEmpty(manifest.PcName) ? date : manifest.PcName + ", " + date;
      return "This file is from " + from + ": " + Count(manifest.Counts.Windows, "panel") + ", " + Count(manifest.Counts.Clones, "clone") + ", " +
        Count(manifest.Counts.Shortcuts, "button") + ".\n\n" +
        "Yes = Replace: replace ALL current windows and shortcuts with this file. A backup of your current data is saved first.\n" +
        "No = Add as new windows: keep your current data and add the imported windows next to it.\n" +
        "Cancel = do nothing.";
    }

    private static string Count(int count, string what) {
      return count + " " + what + (count == 1 ? "" : "s");
    }
  }
}
