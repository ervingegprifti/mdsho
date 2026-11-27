using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using MDSHO.Common;
using MDSHO.Data;

namespace MDSHO.ViewModels {

  /// <summary>Where sync stands, for the status line in Settings.</summary>
  public enum SyncState {
    /// <summary>Sync is off.</summary>
    Off,
    /// <summary>A round is running.</summary>
    Syncing,
    /// <summary>The last round worked.</summary>
    UpToDate,
    /// <summary>The folder could not be reached; changes wait.</summary>
    Unreachable,
    /// <summary>Another PC has a newer MDSHO.</summary>
    NewerPc
  }

  /// <summary>What turning sync on did.</summary>
  public sealed class SyncJoin {
    /// <summary>True when sync is on now.</summary>
    public bool Ok { get; set; }
    /// <summary>The backup made first.</summary>
    public string BackupPath { get; set; }
    /// <summary>Why it could not be turned on, for the user.</summary>
    public string Error { get; set; }
  }

  /// <summary>A row of the table of other PCs.</summary>
  public sealed class SyncPeerRow {
    /// <summary>The PC's name.</summary>
    public string Name { get; set; }
    /// <summary>"Today 08:41", "Yesterday 08:41", "3 days ago" or a date.</summary>
    public string LastSync { get; set; }
    /// <summary>Its MDSHO version.</summary>
    public string Version { get; set; }
  }

  /// <summary>
  /// Keeps this PC in sync with the user's other PCs through a folder. A round runs at start, at the first check (every
  /// 10 seconds) after a local change, every minute to read the other PCs, at exit and on Sync now; each round runs in
  /// the background, never on the UI thread. Turning sync on makes a backup first, then a plain merge. Turning it off
  /// only stops it: this PC keeps everything and the folder is not touched.
  /// </summary>
  public sealed class SyncService {
    /// <summary>How often the check runs.</summary>
    public static readonly TimeSpan CheckEvery = TimeSpan.FromSeconds(10);
    /// <summary>How often the other PCs are read when nothing changed here.</summary>
    public static readonly TimeSpan ReadEvery = TimeSpan.FromMinutes(1);

    private readonly TimeProvider clock;
    private readonly string appVersion;
    private readonly IOneShotTimer timer;
    private readonly Action<Action> runInBackground;
    private readonly BackupService backups;
    private readonly Func<ISet<long>> editing;
    private readonly Action<SyncResult> applied;
    private readonly Action<string, string> fixLink;
    private readonly Func<SyncOptions, SyncResult> run;
    private readonly Func<SyncPanelPlace> newPanelPlace;
    private readonly object gate = new object();
    private bool running;
    private DateTimeOffset? lastRound;
    private string lastStamp;
    private SyncState state = SyncState.Off;
    private List<SyncPeer> peers = new List<SyncPeer>();
    private string newerPc;

    /// <param name="clock">The time.</param>
    /// <param name="appVersion">This MDSHO's version, written to the index.</param>
    /// <param name="timer">Calls back for the next check.</param>
    /// <param name="runInBackground">Runs a round (Task.Run in the app).</param>
    /// <param name="backups">Makes the backup before joining.</param>
    /// <param name="editing">The buttons whose note, comment or snippet window is open (may be called on any thread).</param>
    /// <param name="applied">Gets the result of a round that changed this PC's data (on the round's thread).</param>
    /// <param name="fixLink">Points a .lnk file at a target on this PC, or null.</param>
    /// <param name="run">Runs one round (SyncEngine.Run; tests replace it).</param>
    /// <param name="newPanelPlace">Where panels from other PCs open (may be called on any thread), or null.</param>
    public SyncService(TimeProvider clock, string appVersion, IOneShotTimer timer, Action<Action> runInBackground, BackupService backups,
      Func<ISet<long>> editing, Action<SyncResult> applied, Action<string, string> fixLink = null, Func<SyncOptions, SyncResult> run = null,
      Func<SyncPanelPlace> newPanelPlace = null) {
      this.run = run ?? SyncEngine.Run;
      this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
      this.appVersion = appVersion;
      this.timer = timer ?? throw new ArgumentNullException(nameof(timer));
      this.runInBackground = runInBackground ?? throw new ArgumentNullException(nameof(runInBackground));
      this.backups = backups;
      this.editing = editing ?? (() => new HashSet<long>());
      this.applied = applied;
      this.fixLink = fixLink;
      this.newPanelPlace = newPanelPlace;
    }

    /// <summary>
    /// How long exit and turning on wait for the folder (and exit for a round already running). Past it they give up and
    /// log it: a folder that hangs (a cloud drive offline, a network drive asleep) never keeps MDSHO or Windows from closing.
    /// </summary>
    public TimeSpan WaitLimit { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Whether a folder exists (Directory.Exists; tests replace it).</summary>
    public Func<string, bool> FolderExists { get; set; } = Directory.Exists;

    /// <summary>The place New panel uses on <paramref name="screen"/>: its centre, at the initial size.</summary>
    public static SyncPanelPlace CentreOf(ScreenInfo screen) {
      ScreenPoint centre = ScreenGeometry.CenterNewWindow(screen);
      return new SyncPanelPlace { ScreenDeviceName = screen.DeviceName ?? "", Left = (long)centre.X, Top = (long)centre.Y };
    }

    /// <summary>Raised when the state or the other PCs changed (maybe on a background thread).</summary>
    public event Action Changed;

    /// <summary>True when sync is on with a folder.</summary>
    public bool IsOn {
      get {
        try {
          return SyncSettings.IsOn;
        } catch (Exception ex) {
          AppLog.Warning(ex, "The sync setting could not be read");
          return false;
        }
      }
    }

    /// <summary>Where sync stands.</summary>
    public SyncState State => !IsOn ? SyncState.Off : this.state == SyncState.Off ? SyncState.Syncing : this.state;

    #region RUNNING
    /// <summary>Syncs now when on, then checks every 10 seconds.</summary>
    public void Start() {
      this.running = true;
      if (IsOn) {
        this.runInBackground(Round);
      }
      Arm();
    }

    /// <summary>Stops the checks.</summary>
    public void Stop() {
      this.running = false;
      this.timer.Stop();
    }

    private void Arm() {
      this.timer.Start(CheckEvery, () => {
        if (!this.running) {
          return;
        }
        try {
          if (IsDue()) {
            this.runInBackground(Round);
          }
        } catch (Exception ex) {
          AppLog.Warning(ex, "The sync check failed");
        }
        Arm();
      });
    }

    // A local change since the last round, or a minute since it.
    private bool IsDue() {
      if (!IsOn) {
        return false;
      }
      if (this.lastRound == null || this.clock.GetUtcNow() - this.lastRound.Value >= ReadEvery) {
        return true;
      }
      return SyncEngine.ChangeStamp() != this.lastStamp;
    }

    /// <summary>Sync now (Settings): a round in the background.</summary>
    public void SyncNow() {
      if (IsOn) {
        this.runInBackground(Round);
      }
    }

    /// <summary>
    /// When MDSHO closes: a last round, waiting at most <see cref="WaitLimit"/>, also for a round already running. Past
    /// that it is skipped and logged; the changes go at the next start. Never throws.
    /// </summary>
    public void RunOnExit() {
      try {
        if (!IsOn) {
          return;
        }
        // Read here (the UI thread at exit), so the round never waits for this thread.
        ISet<long> open = OpenWindows();
        if (open == null) {
          return;
        }
        System.Threading.Tasks.Task round = System.Threading.Tasks.Task.Run(() => Round(open, WaitLimit));
        if (!round.Wait(WaitLimit)) {
          AppLog.Warning(null, "The sync at exit took longer than {Seconds} s and was skipped; the changes go at the next start", WaitLimit.TotalSeconds);
        }
      } catch (Exception ex) {
        AppLog.Error(ex, "The sync at exit failed");
      }
    }

    // One round. Rounds never overlap. Never throws. The open text windows are read before the lock, so a round waiting
    // on the UI thread never holds it while the UI thread waits for the lock (at exit).
    private void Round() {
      ISet<long> open = OpenWindows();
      if (open != null) {
        Round(open, System.Threading.Timeout.InfiniteTimeSpan);
      }
    }

    // The buttons with unsaved typing, or null when they can't be read (the round then waits).
    private ISet<long> OpenWindows() {
      try {
        return this.editing() ?? new HashSet<long>();
      } catch (Exception ex) {
        AppLog.Warning(ex, "The open text windows could not be read; sync waits");
        return null;
      }
    }

    private void Round(ISet<long> open, TimeSpan wait) {
      if (!System.Threading.Monitor.TryEnter(this.gate, wait)) {
        AppLog.Warning(null, "A sync round is still running; this one is skipped");
        return;
      }
      try {
        try {
          if (!IsOn) {
            return;
          }
          this.state = SyncState.Syncing;
          Changed?.Invoke();
          SyncResult result = this.run(new SyncOptions {
            Folder = SyncSettings.Folder,
            DeviceId = DeviceIdentity.Id,
            DeviceName = DeviceIdentity.Name,
            AppVersion = this.appVersion,
            Editing = open,
            NowMs = () => this.clock.GetUtcNow().ToUnixTimeMilliseconds(),
            FixLink = this.fixLink,
            NewPanelPlace = this.newPanelPlace
          });
          this.lastRound = this.clock.GetUtcNow();
          this.lastStamp = SyncEngine.ChangeStamp();
          this.peers = result.Peers.ToList();
          this.newerPc = result.NewerPcs.FirstOrDefault();
          this.state = !result.FolderReachable ? SyncState.Unreachable : this.newerPc != null ? SyncState.NewerPc : SyncState.UpToDate;
          if (result.Applied) {
            this.applied?.Invoke(result);
          }
        } catch (Exception ex) {
          AppLog.Error(ex, "Sync failed");
          this.lastRound = this.clock.GetUtcNow();
          this.state = SyncState.Unreachable;
        }
      } finally {
        System.Threading.Monitor.Exit(this.gate);
      }
      Changed?.Invoke();
    }
    #endregion RUNNING

    #region TURNING ON AND OFF
    /// <summary>
    /// Apply in Settings with the box ticked: a backup of this PC first, then sync on with this folder and a plain merge
    /// (this PC's data is written, the other PCs' data merged in). No question is asked.
    /// </summary>
    public SyncJoin TurnOn(string folder) {
      if (string.IsNullOrWhiteSpace(folder) || !Path.IsPathFullyQualified(folder)) {
        return new SyncJoin { Error = "The sync folder can't be found: " + folder };
      }
      // A folder that hangs (a cloud drive offline) is given up after the time limit.
      System.Threading.Tasks.Task<bool> exists = System.Threading.Tasks.Task.Run(() => FolderExists(folder));
      if (!exists.Wait(WaitLimit)) {
        AppLog.Warning(null, "The sync folder did not answer in {Seconds} s: {Folder}", WaitLimit.TotalSeconds, folder);
        return new SyncJoin { Error = "The sync folder can't be reached: " + folder + ". Is the drive running?" };
      }
      if (!exists.Result) {
        return new SyncJoin { Error = "The sync folder can't be found: " + folder };
      }
      // The backup goes to the backup folder (Settings > Backups), not the sync folder; Back up now writes there the same way.
      string backup = this.backups?.BackUpForSync();
      if (this.backups != null && backup == null) {
        return new SyncJoin { Error = "Sync was not turned on: the backup before it failed. " + (this.backups.LastErrorText ?? "") };
      }
      SyncSettings.Save(true, folder);
      SyncSettings.BackupNotice = backup;
      AppLog.Information("Sync turned on with the folder {Folder}; backup {Backup}", folder, backup);
      this.lastRound = null;
      this.state = SyncState.Syncing;
      Changed?.Invoke();
      this.runInBackground(Round);
      return new SyncJoin { Ok = true, BackupPath = backup };
    }

    /// <summary>Apply with the box cleared: syncing stops. This PC keeps everything; the folder is not touched.</summary>
    public void TurnOff() {
      SyncSettings.Save(false, SyncSettings.Folder);
      this.state = SyncState.Off;
      AppLog.Information("Sync turned off");
      Changed?.Invoke();
    }

    /// <summary>Once after joining: "A backup ... was made first. Import data restores it."; then null.</summary>
    public string TakeBackupNotice() {
      string path = SyncSettings.BackupNotice;
      if (path == null) {
        return null;
      }
      SyncSettings.BackupNotice = null;
      return "A backup of this PC was made before it joined: " + Path.GetFileName(path) + ". Import data restores it.";
    }
    #endregion TURNING ON AND OFF

    #region STATUS
    /// <summary>The status line.</summary>
    public string StatusText {
      get {
        switch (State) {
          case SyncState.Off: return "Sync is off.";
          case SyncState.Syncing: return "Syncing...";
          case SyncState.Unreachable: return "Can't reach the folder. Will try again in a minute.";
          case SyncState.NewerPc: return this.newerPc + " has a newer MDSHO. Update this PC to sync with it.";
          default: return "Up to date. Last sync " + LastSyncText() + ".";
        }
      }
    }

    /// <summary>The grey line under the status, or null.</summary>
    public string StatusHint {
      get {
        switch (State) {
          case SyncState.Unreachable: return SyncSettings.Folder + " is not there (cloud drive not running?). Your changes are kept and sync later.";
          case SyncState.NewerPc: return "The other PCs keep syncing.";
          default: return null;
        }
      }
    }

    // "just now", "2 minutes ago", "today 08:41", "yesterday 08:41" or "15 Nov 2026 08:41".
    private string LastSyncText() {
      DateTimeOffset? last = SyncSettings.LastTime;
      if (last == null) {
        return "never";
      }
      TimeSpan ago = this.clock.GetUtcNow() - last.Value;
      if (ago < TimeSpan.FromMinutes(1)) {
        return "just now";
      }
      if (ago < TimeSpan.FromHours(1)) {
        int minutes = (int)ago.TotalMinutes;
        return minutes + (minutes == 1 ? " minute ago" : " minutes ago");
      }
      return When(last.Value, false);
    }

    // A time as "today 08:41" (or "Today 08:41" in the table).
    private string When(DateTimeOffset time, bool table) {
      DateTime local = TimeZoneInfo.ConvertTime(time, this.clock.LocalTimeZone).DateTime;
      DateTime today = this.clock.GetLocalNow().Date;
      string clockTime = local.ToString("HH:mm", CultureInfo.InvariantCulture);
      if (local.Date == today) {
        return (table ? "Today " : "today ") + clockTime;
      }
      if (local.Date == today.AddDays(-1)) {
        return (table ? "Yesterday " : "yesterday ") + clockTime;
      }
      int days = (today - local.Date).Days;
      if (table && days > 1 && days < 7) {
        return days + " days ago";
      }
      return local.ToString("d MMM yyyy HH:mm", CultureInfo.InvariantCulture);
    }

    /// <summary>The other PCs: name, last sync and MDSHO version.</summary>
    public IReadOnlyList<SyncPeerRow> PeerRows() {
      return this.peers.OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase).Select(p => new SyncPeerRow {
        Name = p.Name,
        LastSync = p.LastSync == null ? "" : When(p.LastSync.Value, true),
        Version = p.AppVersion
      }).ToList();
    }
    #endregion STATUS
  }
}
