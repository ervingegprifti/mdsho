using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using MDSHO.Common;
using MDSHO.Data;
using MDSHO.ViewModels;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// The "Enjoying MDSHO?" review prompt rules: 14 days installed, 7 days of use, a minute after start, not over another
  /// dialog, ask again after 30 days, at most two showings, never after Rate, only for Store installs, never in the Debug
  /// build. Rate uses the Store's in-app rating dialog (a fake here).
  /// </summary>
  public class ReviewPromptTests : IDisposable {
    private readonly TempDb db = new TempDb();
    private readonly FakeClock clock = new FakeClock();
    private readonly ScreenSetupTests.FakeTimer timer = new ScreenSetupTests.FakeTimer();
    private readonly StoreHost host = new StoreHost();
    private readonly FakeStoreRating rating = new FakeStoreRating();
    private readonly StoreActions store;
    private readonly List<ReviewPrompt> shown = new List<ReviewPrompt>();
    private bool dialogOpen;

    public ReviewPromptTests() {
      store = new StoreActions(host, rating);
    }

    public void Dispose() { db.Dispose(); }

    internal sealed class StoreHost : IStoreActionsHost {
      public readonly List<Uri> Launched = new List<Uri>();
      public void CopyToClipboard(string text) { }
      public void ShowInformation(string message, string caption) { }
      public void OpenInBrowser(string url) { }
      public Task<bool> LaunchUriAsync(Uri uri) { Launched.Add(uri); return Task.FromResult(true); }
      public void ShowError(Exception exception) { throw exception; }
    }

    /// <summary>The Store's rating dialog: answers with <see cref="Result"/>, or throws.</summary>
    internal sealed class FakeStoreRating : IStoreRating {
      public string InstallKind { get; set; } = "Store";
      public StoreRatingResult Result = StoreRatingResult.Rated;
      public bool Throw;
      public int Calls;
      public Action OnCall;
      public Task<StoreRatingResult> RequestRateAndReviewAsync() {
        Calls++;
        OnCall?.Invoke();
        if (Throw) { return Task.FromException<StoreRatingResult>(new InvalidOperationException("Store dialog failed")); }
        return Task.FromResult(Result);
      }
    }

    private ReviewPrompt Prompt(bool debugBuild = false) {
      return new ReviewPrompt(clock, timer, () => dialogOpen, p => shown.Add(p), store, debugBuild);
    }

    /// <summary>Starts MDSHO once on each of <paramref name="days"/> days in a row from now, then moves to the next day.</summary>
    private void UseOnDays(int days) {
      for (int i = 0; i < days; i++) {
        Prompt().Start();
        timer.Stop();
        clock.Now = clock.Now.AddDays(1);
      }
    }

    /// <summary>Starts MDSHO and waits the first minute (no wait at all once it never shows again).</summary>
    private ReviewPrompt StartAndWaitAMinute() {
      ReviewPrompt prompt = Prompt();
      timer.Stop();
      timer.Delay = null;
      prompt.Start();
      if (timer.Running) {
        Assert.Equal(TimeSpan.FromMinutes(1), timer.Delay);
        timer.Fire();
      }
      return prompt;
    }

    private static string Setting(string key) { return DataStore.AppSettings.Get(key); }

    [Fact]
    public void The_first_start_is_recorded_once_as_a_local_date() {
      Prompt().Start();
      Assert.Equal("2026-11-15", Setting(ReviewPrompt.FirstStartKey));
      clock.Now = clock.Now.AddDays(3);
      Prompt().Start();
      Assert.Equal("2026-11-15", Setting(ReviewPrompt.FirstStartKey));
    }

    [Fact]
    public void A_day_counts_once_however_often_MDSHO_starts() {
      Prompt().Start();
      Prompt().Start();
      clock.Now = clock.Now.AddHours(5);
      Prompt().Start();
      Assert.Equal("1", Setting(ReviewPrompt.DaysUsedKey));
      clock.Now = clock.Now.AddDays(1);
      Prompt().Start();
      Assert.Equal("2", Setting(ReviewPrompt.DaysUsedKey));
    }

    [Fact]
    public void Shows_a_minute_after_start_after_14_days_and_7_days_of_use() {
      UseOnDays(7);
      clock.Now = clock.Now.AddDays(7); // day 14 after the first start
      ReviewPrompt prompt = StartAndWaitAMinute();
      Assert.Same(prompt, Assert.Single(shown));
      Assert.Equal("1", Setting(ReviewPrompt.ShownKey));
    }

    [Fact]
    public void Not_before_14_days_even_with_7_days_of_use() {
      UseOnDays(7);
      clock.Now = clock.Now.AddDays(6); // day 13
      StartAndWaitAMinute();
      Assert.Empty(shown);
    }

    [Fact]
    public void Not_with_only_6_days_of_use_even_after_a_month() {
      UseOnDays(5);
      clock.Now = clock.Now.AddDays(30);
      StartAndWaitAMinute(); // the 6th day of use, 35 days installed
      Assert.Empty(shown);
      clock.Now = clock.Now.AddDays(1);
      StartAndWaitAMinute(); // the 7th day of use
      Assert.Single(shown);
    }

    [Fact]
    public void An_existing_user_without_a_first_start_date_starts_counting_now() {
      DataStore.AppSettings.Set(ReviewPrompt.DaysUsedKey, "50");
      DataStore.AppSettings.Set(ReviewPrompt.LastDayKey, "2026-11-01");
      StartAndWaitAMinute();
      Assert.Empty(shown);
      Assert.Equal("2026-11-15", Setting(ReviewPrompt.FirstStartKey));
    }

    [Fact]
    public void Waits_while_a_dialog_is_open_and_tries_again_a_minute_later() {
      UseOnDays(14);
      dialogOpen = true;
      StartAndWaitAMinute();
      Assert.Empty(shown);
      Assert.True(timer.Running);
      Assert.Equal(TimeSpan.FromMinutes(1), timer.Delay);
      timer.Fire();
      Assert.Empty(shown);
      dialogOpen = false;
      timer.Fire();
      Assert.Single(shown);
    }

    [Fact]
    public void Rate_MDSHO_opens_the_Store_rating_dialog_and_after_a_rating_it_never_shows_again() {
      UseOnDays(14);
      ReviewPrompt prompt = StartAndWaitAMinute();
      prompt.RateAsync().GetAwaiter().GetResult();
      Assert.Equal(1, rating.Calls);
      Assert.Empty(host.Launched);
      Assert.Equal("Yes", Setting(ReviewPrompt.RatedKey));
      shown.Clear();
      clock.Now = clock.Now.AddDays(100);
      UseOnDays(10);
      StartAndWaitAMinute();
      Assert.Empty(shown);
      Assert.False(timer.Running);
    }

    [Fact]
    public void Cancel_in_the_Store_dialog_is_like_Later() {
      rating.Result = StoreRatingResult.Cancelled;
      UseOnDays(14);
      ReviewPrompt prompt = StartAndWaitAMinute();
      prompt.RateAsync().GetAwaiter().GetResult();
      Assert.Empty(host.Launched);
      Assert.Null(Setting(ReviewPrompt.RatedKey));
      clock.Now = clock.Now.AddDays(30);
      StartAndWaitAMinute().RateAsync().GetAwaiter().GetResult();
      Assert.Equal(2, shown.Count);
      clock.Now = clock.Now.AddDays(100);
      StartAndWaitAMinute();
      Assert.Equal(2, shown.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_failed_Store_dialog_opens_the_Store_page_and_counts_as_rated(bool throws) {
      rating.Result = StoreRatingResult.Failed;
      rating.Throw = throws;
      UseOnDays(14);
      StartAndWaitAMinute().RateAsync().GetAwaiter().GetResult();
      Assert.Equal(new[] { StoreActions.ReviewUri }, host.Launched);
      Assert.Equal("Yes", Setting(ReviewPrompt.RatedKey));
    }

    [Theory]
    [InlineData("Enterprise")]
    [InlineData("Developer")]
    [InlineData("None")]
    [InlineData("Not packaged")]
    public void Never_without_a_Microsoft_Store_install_and_logs_why_once(string installKind) {
      rating.InstallKind = installKind;
      using (LogCapture log = new LogCapture()) {
        ReviewPrompt prompt = Prompt();
        prompt.Start();
        Assert.False(timer.Running);
        string why = Assert.Single(log.Messages, m => m.Contains("review prompt"));
        Assert.Contains("not a Microsoft Store install", why);
        Assert.Contains(installKind, why);
      }
      UseOnDays(30);
      StartAndWaitAMinute();
      Assert.Empty(shown);
    }

    [Fact]
    public void Rate_from_the_panel_menu_or_About_follows_the_same_rules() {
      // The panel menu's Rate... and About's Rate link call the same StoreActions: a cancel records nothing.
      Prompt();
      rating.Result = StoreRatingResult.Cancelled;
      store.RateAsync().GetAwaiter().GetResult();
      Assert.Null(Setting(ReviewPrompt.RatedKey));
      rating.Result = StoreRatingResult.Rated;
      store.RateAsync().GetAwaiter().GetResult();
      Assert.Equal("Yes", Setting(ReviewPrompt.RatedKey));
      Assert.Empty(host.Launched);
    }

    [Fact]
    public void Rate_from_the_panel_menu_or_About_means_it_never_shows() {
      Prompt();
      // The panel menu's Rate... and About's Rate link call the same StoreActions.
      store.RateAsync().GetAwaiter().GetResult();
      UseOnDays(14);
      StartAndWaitAMinute();
      Assert.Empty(shown);
      Assert.Equal("Yes", Setting(ReviewPrompt.RatedKey));
    }

    [Fact]
    public void After_Later_it_asks_again_after_30_days() {
      UseOnDays(14);
      StartAndWaitAMinute(); // first showing; Later or X records nothing more
      shown.Clear();
      UseOnDays(29);
      StartAndWaitAMinute();
      Assert.Empty(shown);
      clock.Now = clock.Now.AddDays(1); // 30 days after the first showing
      StartAndWaitAMinute();
      Assert.Single(shown);
      Assert.Equal("2", Setting(ReviewPrompt.ShownKey));
    }

    [Fact]
    public void After_the_second_showing_it_never_shows_again() {
      UseOnDays(14);
      StartAndWaitAMinute();
      clock.Now = clock.Now.AddDays(30);
      StartAndWaitAMinute();
      Assert.Equal(2, shown.Count);
      clock.Now = clock.Now.AddDays(365);
      UseOnDays(10);
      StartAndWaitAMinute();
      Assert.Equal(2, shown.Count);
      Assert.False(timer.Running);
    }

    [Fact]
    public void Never_in_the_Debug_build() {
      UseOnDays(14);
      ReviewPrompt prompt = Prompt(debugBuild: true);
      prompt.Start();
      Assert.False(timer.Running);
      timer.Fire();
      Assert.Empty(shown);
    }

    [Fact]
    public void A_long_run_counts_new_days_every_hour_and_shows_when_due() {
      ReviewPrompt prompt = Prompt();
      prompt.Start();
      timer.Fire();
      Assert.Equal(TimeSpan.FromHours(1), timer.Delay);
      for (int hour = 0; hour < 24 * 14 && shown.Count == 0; hour++) {
        clock.Now = clock.Now.AddHours(1);
        timer.Fire();
      }
      Assert.Single(shown);
      Assert.Equal("15", Setting(ReviewPrompt.DaysUsedKey));
    }

    [Fact]
    public void Its_settings_are_not_exported() {
      UseOnDays(14);
      StartAndWaitAMinute();
      string zip = Path.Combine(db.Folder, "export.zip");
      DataExporter.Export(zip);
      using (ZipArchive archive = ZipFile.OpenRead(zip)) {
        foreach (ZipArchiveEntry entry in archive.Entries) {
          using (StreamReader reader = new StreamReader(entry.Open())) {
            Assert.DoesNotContain("Review", reader.ReadToEnd());
          }
        }
      }
    }

    [Fact]
    public void Every_key_is_in_the_database_docs() {
      string doc = File.ReadAllText(Path.Combine(ThirdPartyNoticesTests.RepoRoot(), "docs", "database.md"));
      foreach (string key in new[] { ReviewPrompt.FirstStartKey, ReviewPrompt.DaysUsedKey, ReviewPrompt.LastDayKey,
        ReviewPrompt.ShownKey, ReviewPrompt.LastShownKey, ReviewPrompt.RatedKey }) {
        Assert.Contains("| `" + key + "` |", doc);
        Assert.StartsWith("Review", key);
      }
    }
  }
}
