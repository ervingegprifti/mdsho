using System;
using System.Globalization;
using System.Threading.Tasks;
using MDSHO.Common;
using MDSHO.Data;

namespace MDSHO.ViewModels {

  /// <summary>
  /// The one-time "Enjoying MDSHO?" prompt. It shows when MDSHO has been installed for 14 days and used on 7 different
  /// days, a minute after start, never over another MDSHO dialog. Later or X on the first showing asks again after 30
  /// days, and so does a cancel in the Store's rating dialog; after the second showing, or after a rating, it never shows
  /// again. Only for Microsoft Store installs; off in the Debug build. Kept in AppSettings, on this PC only.
  /// </summary>
  public sealed class ReviewPrompt {
    /// <summary>AppSettings key: the local date of the first start this PC recorded (yyyy-MM-dd).</summary>
    public const string FirstStartKey = "ReviewFirstStart";
    /// <summary>AppSettings key: on how many different local days MDSHO ran.</summary>
    public const string DaysUsedKey = "ReviewDaysUsed";
    /// <summary>AppSettings key: the last local day counted (yyyy-MM-dd).</summary>
    public const string LastDayKey = "ReviewLastDay";
    /// <summary>AppSettings key: how many times the prompt was shown (0, 1 or 2).</summary>
    public const string ShownKey = "ReviewShown";
    /// <summary>AppSettings key: the local date of the last showing (yyyy-MM-dd).</summary>
    public const string LastShownKey = "ReviewLastShown";
    /// <summary>AppSettings key: "Yes" once the user used any Rate action.</summary>
    public const string RatedKey = "ReviewRated";

    /// <summary>Days installed before the first showing.</summary>
    public const int InstalledDays = 14;
    /// <summary>Different days of use before the first showing.</summary>
    public const int UsedDays = 7;
    /// <summary>Days after the first showing before the second.</summary>
    public const int AskAgainDays = 30;
    /// <summary>Showings in total.</summary>
    public const int MaxShowings = 2;
    /// <summary>The wait after start, and again while a dialog is open.</summary>
    public static readonly TimeSpan Wait = TimeSpan.FromMinutes(1);
    /// <summary>How often a long run checks again (new days count too).</summary>
    public static readonly TimeSpan CheckEvery = TimeSpan.FromHours(1);

    private const string DateFormat = "yyyy-MM-dd";

    private readonly TimeProvider clock;
    private readonly IOneShotTimer timer;
    private readonly Func<bool> isDialogOpen;
    private readonly Action<ReviewPrompt> show;

    /// <param name="clock">The time.</param>
    /// <param name="timer">Calls back after the wait.</param>
    /// <param name="isDialogOpen">True while another MDSHO dialog is open.</param>
    /// <param name="show">Shows the window.</param>
    /// <param name="store">The Rate action and the install kind; a rating through it (panel menu, About) means the prompt never shows.</param>
    /// <param name="debugBuild">True in the Debug build: the prompt never shows.</param>
    public ReviewPrompt(TimeProvider clock, IOneShotTimer timer, Func<bool> isDialogOpen, Action<ReviewPrompt> show, StoreActions store, bool debugBuild) {
      this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
      this.timer = timer ?? throw new ArgumentNullException(nameof(timer));
      this.isDialogOpen = isDialogOpen ?? throw new ArgumentNullException(nameof(isDialogOpen));
      this.show = show ?? throw new ArgumentNullException(nameof(show));
      Store = store ?? throw new ArgumentNullException(nameof(store));
      DebugBuild = debugBuild;
      Store.Rated += RecordRated;
    }

    /// <summary>The Rate action.</summary>
    public StoreActions Store { get; }

    /// <summary>True in the Debug build.</summary>
    public bool DebugBuild { get; }

    /// <summary>At start: counts today and checks again a minute later. Never throws.</summary>
    public void Start() {
      if (DebugBuild) {
        return;
      }
      try {
        if (!Store.IsStoreInstall) {
          AppLog.Information("The review prompt is off: MDSHO is not a Microsoft Store install ({InstallKind})", Store.InstallKind);
          return;
        }
        CountToday();
        if (!IsFinished) {
          this.timer.Start(Wait, Check);
        }
      } catch (Exception ex) {
        AppLog.Warning(ex, "The review prompt could not start");
      }
    }

    /// <summary>Drops a waiting check.</summary>
    public void Stop() {
      this.timer.Stop();
    }

    /// <summary>Records that the user rated (or the Store page opened): the prompt never shows again. Never throws.</summary>
    public void RecordRated() {
      try {
        DataStore.AppSettings.Set(RatedKey, "Yes");
      } catch (Exception ex) {
        AppLog.Warning(ex, "The review prompt could not save the rating");
      }
    }

    /// <summary>Rate MDSHO in the prompt: the Store's rating dialog (a cancel counts as Later).</summary>
    public Task RateAsync() {
      return Store.RateAsync();
    }

    private void Check() {
      try {
        CountToday();
        if (IsFinished || !Store.IsStoreInstall) {
          return;
        }
        if (!IsDue) {
          this.timer.Start(CheckEvery, Check);
          return;
        }
        if (this.isDialogOpen()) {
          this.timer.Start(Wait, Check);
          return;
        }
        Set(ShownKey, (Number(ShownKey) + 1).ToString(CultureInfo.InvariantCulture));
        Set(LastShownKey, Format(Today));
        AppLog.Information("The review prompt was shown");
        if (!IsFinished) {
          this.timer.Start(CheckEvery, Check);
        }
        this.show(this);
      } catch (Exception ex) {
        AppLog.Warning(ex, "The review prompt check failed");
      }
    }

    // Rated, or shown the most times.
    private bool IsFinished => Get(RatedKey) == "Yes" || Number(ShownKey) >= MaxShowings;

    // Today qualifies: installed and used long enough, and 30 days after the first showing.
    private bool IsDue {
      get {
        DateTime today = Today;
        DateTime? firstStart = Date(FirstStartKey);
        if (firstStart == null || today < firstStart.Value.AddDays(InstalledDays) || Number(DaysUsedKey) < UsedDays) {
          return false;
        }
        if (Number(ShownKey) == 0) {
          return true;
        }
        DateTime? lastShown = Date(LastShownKey);
        return lastShown == null || today >= lastShown.Value.AddDays(AskAgainDays);
      }
    }

    // Records the first start once and counts today once.
    private void CountToday() {
      DateTime today = Today;
      if (Date(FirstStartKey) == null) {
        Set(FirstStartKey, Format(today));
      }
      DateTime? lastDay = Date(LastDayKey);
      if (lastDay == null || today > lastDay.Value) {
        Set(DaysUsedKey, (Number(DaysUsedKey) + 1).ToString(CultureInfo.InvariantCulture));
        Set(LastDayKey, Format(today));
      }
    }

    private DateTime Today => this.clock.GetLocalNow().Date;

    private static string Format(DateTime date) { return date.ToString(DateFormat, CultureInfo.InvariantCulture); }

    private static DateTime? Date(string key) {
      return DateTime.TryParseExact(Get(key), DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime d) ? d : null;
    }

    private static int Number(string key) {
      return int.TryParse(Get(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) ? n : 0;
    }

    private static string Get(string key) { return DataStore.AppSettings.Get(key); }

    private static void Set(string key, string value) { DataStore.AppSettings.Set(key, value); }
  }
}
