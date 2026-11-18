using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json.Nodes;
using MDSHO.Common;
using MDSHO.Core;
using MDSHO.Data;
using MDSHO.ViewModels;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>A clock for tests: the time is set by hand and local time is UTC.</summary>
  internal sealed class FakeClock : TimeProvider {
    public DateTimeOffset Now = new DateTimeOffset(2026, 11, 15, 9, 12, 4, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() { return Now; }
    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
  }

  /// <summary>Automatic backups: the export they write, when they are due, file names, keeping, and the upgrade backup.</summary>
  public class AutoBackupTests : IDisposable {
    private readonly TempDb db = new TempDb();
    private readonly FakeClock clock = new FakeClock();
    private readonly ScreenSetupTests.FakeTimer timer = new ScreenSetupTests.FakeTimer();
    private readonly string profile;
    private readonly string documents;

    public AutoBackupTests() {
      profile = Path.Combine(db.Folder, "Profile");
      Directory.CreateDirectory(profile);
      documents = Path.Combine(db.Folder, "Documents");
    }

    public void Dispose() { db.Dispose(); }

    private BackupService Service(string pc = "HOMEPC") {
      return new BackupService(clock, pc, profile, "1.12.0", timer, action => action(), documents);
    }

    private string DefaultFolder => Path.Combine(profile, AutoBackupNames.ExpectedFolderName);

    private static string[] Files(string folder) {
      return Directory.Exists(folder) ? Directory.GetFiles(folder).Select(Path.GetFileName).OrderBy(n => n, StringComparer.Ordinal).ToArray() : new string[0];
    }

    private static JsonNode Manifest(string zip) {
      using (ZipArchive archive = ZipFile.OpenRead(zip))
      using (StreamReader reader = new StreamReader(archive.GetEntry("manifest.json").Open())) {
        return JsonNode.Parse(reader.ReadToEnd());
      }
    }

    private static void ChangeData() {
      DesktopWindow w = DataStore.Windows.GetAll()[0];
      DataStore.Windows.UpdateTitle(w.ID, "Changed " + Guid.NewGuid().ToString("N"));
    }

    #region EXPORT
    [Fact]
    public void An_export_with_a_PC_name_has_it_in_the_manifest_and_Inspect_reads_it() {
      string zip = Path.Combine(db.Folder, "pc.zip");
      DataExporter.Export(zip, new ExportOptions { PcName = "HOMEPC" });
      Assert.Equal("HOMEPC", (string)Manifest(zip)["pcName"]);
      Assert.Equal(3, (int)Manifest(zip)["formatVersion"]);
      Assert.Equal("HOMEPC", DataImporter.Inspect(zip).PcName);
    }

    [Fact]
    public void An_export_without_a_PC_name_leaves_it_out_and_still_imports() {
      string zip = Path.Combine(db.Folder, "plain.zip");
      DataExporter.Export(zip);
      Assert.Null(Manifest(zip)["pcName"]);
      Assert.False(Manifest(zip).AsObject().ContainsKey("pcName"));
      Assert.Null(DataImporter.Inspect(zip).PcName);
      DataImporter.Import(zip, ImportMode.Replace);
    }

    [Fact]
    public void The_data_hash_ignores_the_export_time_and_changes_with_the_data_and_the_files() {
      string first = DataExporter.DataHash();
      Assert.Equal(first, DataExporter.DataHash());
      Assert.Matches("^[0-9a-f]{64}$", first);
      ChangeData();
      string second = DataExporter.DataHash();
      Assert.NotEqual(first, second);
      DesktopWindow w = DataStore.Windows.GetAll()[0];
      string guid = Identifiers.NewGuid();
      DataStore.Shortcuts.Create(null, w.ID, "a", @"C:\a.exe", ".lnk", guid);
      string file = Paths.GetShortcutFilePath(guid, ".lnk");
      File.WriteAllText(file, "one");
      string third = DataExporter.DataHash();
      File.WriteAllText(file, "two");
      Assert.NotEqual(third, DataExporter.DataHash());
    }
    #endregion EXPORT

    #region FILE NAMES
    [Fact]
    public void File_names_have_the_PC_name_made_safe_and_the_local_time() {
      DateTime t = new DateTime(2026, 11, 15, 9, 12, 4);
      Assert.Equal("MDSHO-backup-HOMEPC-2026-11-15-091204.zip", BackupFiles.RegularName("HOMEPC", t));
      Assert.Equal("MDSHO-backup-HOMEPC-before-1.12.0-2026-11-15-091204.zip", BackupFiles.UpgradeName("HOMEPC", "1.12.0", t));
      Assert.Equal("HOME_PC_1", BackupFiles.SafePcName("HOME PC/1"));
      Assert.Equal("A_B_C", BackupFiles.SafePcName("A:B*C"));
      Assert.Equal("PC", BackupFiles.SafePcName(""));
      Assert.Equal("PC", BackupFiles.SafePcName(null));
      Assert.True(BackupFiles.IsRegularOf("HOMEPC", "MDSHO-backup-HOMEPC-2026-11-15-091204.zip"));
      Assert.True(BackupFiles.IsRegularOf("HOMEPC", "mdsho-backup-homepc-2026-11-15-091204.ZIP"));
      Assert.False(BackupFiles.IsRegularOf("HOME", "MDSHO-backup-HOME-PC-2026-11-15-091204.zip"));
      Assert.False(BackupFiles.IsRegularOf("HOMEPC", "MDSHO-backup-HOMEPC-before-1.12.0-2026-11-15-091204.zip"));
      Assert.False(BackupFiles.IsRegularOf("HOMEPC", "MDSHO-backup-HOMEPC-2026-11-15-091204.zip.tmp"));
      Assert.False(BackupFiles.IsRegularOf("HOMEPC", "MDSHO-backup-HOMEPC-2026-11-15-0912.zip"));
    }
    #endregion FILE NAMES

    #region SETTINGS
    [Fact]
    public void Backups_are_on_by_default_in_the_profile_folder_keeping_10() {
      BackupService s = Service();
      Assert.True(s.IsOn);
      Assert.Equal(10, s.Keep);
      Assert.Equal(DefaultFolder, s.Folder);
      Assert.Equal(DefaultFolder, s.DefaultFolder);
      Assert.Equal(new[] { 3, 5, 10, 20, 30 }, BackupService.KeepChoices);
      Assert.Null(s.LastBackup);
      Assert.Null(s.LastErrorText);
      Assert.Equal("Last backup: Never", s.LastBackupText);
      Assert.False(Directory.Exists(DefaultFolder)); // created only when needed
    }

    [Fact]
    public void Save_keeps_the_settings_in_AppSettings_and_the_default_folder_as_no_value() {
      BackupService s = Service();
      string custom = Path.Combine(db.Folder, "Drive", "MDSHO backups");
      s.Save(false, custom, 3);
      Assert.Equal("Off", DataStore.AppSettings.Get(BackupService.OnKey));
      Assert.Equal(custom, DataStore.AppSettings.Get(BackupService.FolderKey));
      Assert.Equal("3", DataStore.AppSettings.Get(BackupService.KeepKey));
      BackupService again = Service();
      Assert.False(again.IsOn);
      Assert.Equal(custom, again.Folder);
      Assert.Equal(3, again.Keep);
      again.Save(true, DefaultFolder, 20);
      Assert.Equal("On", DataStore.AppSettings.Get(BackupService.OnKey));
      Assert.Equal("", DataStore.AppSettings.Get(BackupService.FolderKey));
      Assert.Equal(DefaultFolder, Service().Folder);
      Assert.Equal(20, Service().Keep);
    }

    [Fact]
    public void A_keep_count_that_is_not_a_choice_reads_as_10() {
      DataStore.AppSettings.Set(BackupService.KeepKey, "7");
      Assert.Equal(10, Service().Keep);
      DataStore.AppSettings.Set(BackupService.KeepKey, "x");
      Assert.Equal(10, Service().Keep);
    }

    [Fact]
    public void The_settings_are_not_exported() {
      BackupService s = Service();
      s.Save(false, Path.Combine(db.Folder, "x"), 5);
      s.BackUpNow();
      string zip = Path.Combine(db.Folder, "x", Files(Path.Combine(db.Folder, "x")).Single());
      using (ZipArchive archive = ZipFile.OpenRead(zip))
      using (StreamReader reader = new StreamReader(archive.GetEntry("data.json").Open())) {
        Assert.DoesNotContain("Backup", reader.ReadToEnd());
      }
    }
    #endregion SETTINGS

    #region WHEN
    [Fact]
    public void The_first_check_backs_up_and_records_the_time() {
      BackupService s = Service();
      Assert.True(s.RunIfDue());
      string[] files = Files(DefaultFolder);
      Assert.Equal(new[] { "MDSHO-backup-HOMEPC-2026-11-15-091204.zip" }, files);
      string zip = Path.Combine(DefaultFolder, files[0]);
      Assert.Equal("HOMEPC", DataImporter.Inspect(zip).PcName);
      Assert.Equal(clock.Now, s.LastBackup);
      Assert.Equal("Last backup: today 09:12", s.LastBackupText);
    }

    [Fact]
    public void A_backup_is_due_only_after_24_hours_and_only_when_the_data_changed() {
      BackupService s = Service();
      Assert.True(s.RunIfDue());
      ChangeData();
      clock.Now = clock.Now.AddHours(23).AddMinutes(59);
      Assert.False(s.RunIfDue()); // changed, but not 24 hours yet
      clock.Now = clock.Now.AddMinutes(1);
      Assert.True(s.RunIfDue()); // 24 hours and changed
      clock.Now = clock.Now.AddHours(30);
      Assert.False(s.RunIfDue()); // no change: nothing written
      Assert.Equal(2, Files(DefaultFolder).Length);
      ChangeData();
      Assert.True(s.RunIfDue());
      Assert.Equal(3, Files(DefaultFolder).Length);
    }

    [Fact]
    public void Nothing_is_written_when_backups_are_off() {
      BackupService s = Service();
      s.Save(false, DefaultFolder, 10);
      Assert.False(s.RunIfDue());
      Assert.Empty(Files(DefaultFolder));
    }

    [Fact]
    public void Back_up_now_writes_even_without_a_change() {
      BackupService s = Service();
      Assert.True(s.RunIfDue());
      clock.Now = clock.Now.AddMinutes(1);
      Assert.True(s.BackUpNow());
      Assert.Equal(2, Files(DefaultFolder).Length);
      Assert.Equal(clock.Now, s.LastBackup);
      // Back up now counts as the last backup: no change, so the next checks write nothing.
      clock.Now = clock.Now.AddDays(2);
      Assert.False(s.RunIfDue());
    }

    [Fact]
    public void Back_up_now_works_when_backups_are_off_and_can_use_another_folder_and_keep_count() {
      BackupService s = Service();
      s.Save(false, DefaultFolder, 10);
      string other = Path.Combine(db.Folder, "Other");
      Assert.True(s.BackUpNow(other, 3));
      Assert.Single(Files(other));
      Assert.Empty(Files(DefaultFolder));
    }

    [Fact]
    public void Start_checks_at_once_and_then_every_hour() {
      BackupService s = Service();
      s.Start();
      Assert.Single(Files(DefaultFolder));
      Assert.Equal(TimeSpan.FromHours(1), timer.Delay);
      Assert.True(timer.Running);
      ChangeData();
      clock.Now = clock.Now.AddHours(25);
      timer.Fire();
      Assert.Equal(2, Files(DefaultFolder).Length);
      Assert.True(timer.Running); // armed again
      s.Stop();
      Assert.False(timer.Running);
    }

    [Fact]
    public void Start_runs_the_check_in_the_background_runner() {
      List<Action> queued = new List<Action>();
      BackupService s = new BackupService(clock, "HOMEPC", profile, "1.12.0", timer, queued.Add);
      s.Start();
      Assert.Empty(Files(DefaultFolder));
      Assert.Single(queued);
      queued[0]();
      Assert.Single(Files(DefaultFolder));
    }

    [Fact]
    public void Changed_is_raised_after_a_backup() {
      BackupService s = Service();
      int changed = 0;
      s.Changed += () => changed++;
      s.BackUpNow();
      Assert.Equal(1, changed);
    }
    #endregion WHEN

    #region ON EXIT
    [Fact]
    public void On_exit_a_change_is_backed_up_even_within_24_hours() {
      BackupService s = Service();
      Assert.True(s.RunIfDue());
      ChangeData();
      clock.Now = clock.Now.AddMinutes(5);
      Assert.True(s.RunOnExit());
      Assert.Equal(2, Files(DefaultFolder).Length);
      Assert.Contains("MDSHO-backup-HOMEPC-2026-11-15-091704.zip", Files(DefaultFolder));
      Assert.Equal(clock.Now, s.LastBackup);
      Assert.Equal("Last backup: today 09:17", s.LastBackupText);
    }

    [Fact]
    public void On_exit_nothing_is_written_without_a_change() {
      BackupService s = Service();
      Assert.True(s.BackUpNow());
      clock.Now = clock.Now.AddMinutes(5);
      Assert.False(s.RunOnExit());
      Assert.Single(Files(DefaultFolder));
    }

    [Fact]
    public void On_exit_the_first_backup_is_written_when_there_is_none() {
      Assert.True(Service().RunOnExit());
      Assert.Single(Files(DefaultFolder));
    }

    [Fact]
    public void On_exit_nothing_is_written_when_backups_are_off() {
      BackupService s = Service();
      s.Save(false, DefaultFolder, 10);
      Assert.False(s.RunOnExit());
      Assert.Empty(Files(DefaultFolder));
    }

    [Fact]
    public void On_exit_old_backups_are_tidied_up() {
      BackupService s = Service();
      s.Save(true, DefaultFolder, 3);
      for (int i = 0; i < 5; i++) {
        ChangeData();
        clock.Now = clock.Now.AddMinutes(1);
        Assert.True(s.RunOnExit());
      }
      Assert.Equal(3, Files(DefaultFolder).Length);
    }

    [Fact]
    public void On_exit_a_failure_is_recorded_without_an_exception() {
      BackupService s = Service();
      string blocker = Path.Combine(db.Folder, "blocker.txt");
      File.WriteAllText(blocker, "x");
      s.Save(true, Path.Combine(blocker, "sub"), 10);
      Assert.False(s.RunOnExit());
      Assert.StartsWith("The last backup failed: the folder ", s.LastErrorText);
    }

    [Fact]
    public void Exiting_twice_writes_once() {
      BackupService s = Service();
      Assert.True(s.RunOnExit());
      Assert.False(s.RunOnExit()); // sign-out, then the app's own exit
      Assert.Single(Files(DefaultFolder));
    }

    [Fact]
    public void The_app_backs_up_on_exit_through_its_services() {
      using (FakeAppServices services = new FakeAppServices()) {
        services.Services.Backups = Service();
        App.BackUpOnExit();
        Assert.Single(Files(DefaultFolder));
      }
    }

    [Fact]
    public void The_app_backs_up_after_saving_notes_on_exit_and_on_sign_out() {
      string source = File.ReadAllText(Path.Combine(ThirdPartyNoticesTests.RepoRoot(), "src", "MDSHO.App", "App.xaml.cs")).Replace("\r\n", "\n");
      // In both, the notes are saved first and then the backup is made.
      foreach (string start in new[] { "this.SessionEnding += (s, args) => {", "    private void ExitApplication() {" }) {
        int begin = source.IndexOf(start, StringComparison.Ordinal);
        Assert.True(begin >= 0, start);
        int save = source.IndexOf("TextEditWindow.SaveOpenNotesForExit();", begin, StringComparison.Ordinal);
        int backup = source.IndexOf("BackUpOnExit();", begin, StringComparison.Ordinal);
        int end = source.IndexOf("\n    }", begin, StringComparison.Ordinal);
        Assert.True(begin < save && save < backup && backup < end, start);
      }
    }
    #endregion ON EXIT

    #region KEEPING
    [Fact]
    public void Only_the_last_N_regular_backups_of_this_PC_are_kept() {
      Directory.CreateDirectory(DefaultFolder);
      List<string> mine = new List<string>();
      for (int i = 1; i <= 12; i++) {
        string name = "MDSHO-backup-HOMEPC-2026-10-" + i.ToString("00") + "-080000.zip";
        File.WriteAllText(Path.Combine(DefaultFolder, name), "x");
        mine.Add(name);
      }
      string[] others = {
        "MDSHO-backup-WORKPC-2026-01-01-080000.zip",
        "MDSHO-backup-HOMEPC-before-1.11.0-2026-01-01-080000.zip",
        "MDSHO-backup-HOMEPC-2026-01-01-080000.zip.tmp",
        "MDSHO-backup-HOME-2026-01-01-080000.zip",
        "notes.txt"
      };
      foreach (string name in others) {
        File.WriteAllText(Path.Combine(DefaultFolder, name), "x");
      }
      BackupService s = Service();
      Assert.True(s.BackUpNow());
      string[] left = Files(DefaultFolder);
      foreach (string name in others) {
        Assert.Contains(name, left);
      }
      List<string> regular = left.Where(n => BackupFiles.IsRegularOf("HOMEPC", n)).ToList();
      Assert.Equal(10, regular.Count);
      Assert.Contains("MDSHO-backup-HOMEPC-2026-11-15-091204.zip", regular);
      // The three oldest went.
      Assert.DoesNotContain(mine[0], left);
      Assert.DoesNotContain(mine[1], left);
      Assert.DoesNotContain(mine[2], left);
      Assert.Contains(mine[3], left);
    }

    [Fact]
    public void The_keep_count_applies_to_the_next_backup() {
      BackupService s = Service();
      s.Save(true, DefaultFolder, 3);
      for (int i = 0; i < 5; i++) {
        clock.Now = clock.Now.AddMinutes(1);
        Assert.True(s.BackUpNow());
      }
      Assert.Equal(3, Files(DefaultFolder).Length);
    }
    #endregion KEEPING

    #region FAILURES
    [Fact]
    public void A_failure_is_recorded_without_an_exception_and_cleared_by_the_next_success() {
      BackupService s = Service();
      string blocker = Path.Combine(db.Folder, "blocker.txt");
      File.WriteAllText(blocker, "a file, not a folder");
      string missing = Path.Combine(blocker, "MDSHO backups");
      s.Save(true, missing, 10);
      Assert.False(s.RunIfDue());
      Assert.Equal("The last backup failed: the folder " + missing + " can't be found.", s.LastErrorText);
      Assert.Null(s.LastBackup);
      // The next hourly check tries again.
      Assert.False(s.RunIfDue());
      Assert.NotNull(s.LastErrorText);
      Assert.Equal(s.LastErrorText, Service().LastErrorText); // saved
      s.Save(true, DefaultFolder, 10);
      Assert.True(s.RunIfDue());
      Assert.Null(s.LastErrorText);
      Assert.Null(Service().LastErrorText);
    }

    [Fact]
    public void Back_up_now_records_a_failure_and_returns_false() {
      BackupService s = Service();
      string blocker = Path.Combine(db.Folder, "blocker.txt");
      File.WriteAllText(blocker, "x");
      Assert.False(s.BackUpNow(Path.Combine(blocker, "sub"), 10));
      Assert.StartsWith("The last backup failed: ", s.LastErrorText);
    }
    #endregion FAILURES

    #region OLD DEFAULTS
    [Theory]
    [InlineData("Profile", "MDSHO backups")]
    [InlineData("Profile", "MDSHO backups (debug)")]
    [InlineData("Documents", "MDSHO backups")]
    [InlineData("Documents", "MDSHO backups (debug)")]
    [InlineData("profile", "mdsho BACKUPS")]
    public void An_old_default_folder_counts_as_no_folder_chosen(string parent, string name) {
      DataStore.AppSettings.Set(BackupService.FolderKey, Path.Combine(db.Folder, parent, name));
      Assert.Equal(DefaultFolder, Service().Folder);
      DataStore.AppSettings.Set(BackupService.FolderKey, Path.Combine(db.Folder, parent, name) + Path.DirectorySeparatorChar);
      Assert.Equal(DefaultFolder, Service().Folder);
    }

    [Fact]
    public void The_new_default_is_mdsho_backups_in_the_profile_folder() {
      Assert.Equal(Path.Combine(profile, AutoBackupNames.ExpectedFolderName), Service().DefaultFolder);
      Assert.EndsWith(AutoBackupNames.ExpectedFolderName, Service().Folder);
    }

    [Fact]
    public void Files_in_an_old_default_folder_are_left_alone() {
      string old = Path.Combine(documents, "MDSHO backups");
      Directory.CreateDirectory(old);
      string[] names = { "MDSHO-backup-HOMEPC-2026-01-01-080000.zip", "MDSHO-backup-HOMEPC-2026-01-02-080000.zip" };
      foreach (string name in names) {
        File.WriteAllText(Path.Combine(old, name), "x");
      }
      DataStore.AppSettings.Set(BackupService.FolderKey, old);
      BackupService s = Service();
      s.Save(true, s.Folder, 3);
      for (int i = 0; i < 4; i++) {
        clock.Now = clock.Now.AddMinutes(1);
        Assert.True(s.BackUpNow());
      }
      Assert.Equal(names, Files(old));
      Assert.Equal(3, Files(DefaultFolder).Length);
    }

    [Fact]
    public void Choosing_the_default_or_an_old_default_saves_no_folder() {
      BackupService s = Service();
      s.Save(true, DefaultFolder.ToUpperInvariant() + Path.DirectorySeparatorChar, 10);
      Assert.Equal("", DataStore.AppSettings.Get(BackupService.FolderKey));
      s.Save(true, Path.Combine(documents, "MDSHO backups"), 10);
      Assert.Equal("", DataStore.AppSettings.Get(BackupService.FolderKey));
      string custom = Path.Combine(db.Folder, "Drive", "mdsho-backups");
      s.Save(true, custom, 10);
      Assert.Equal(custom, DataStore.AppSettings.Get(BackupService.FolderKey));
    }
    #endregion OLD DEFAULTS

    #region BLOCKED
    private const string BlockedEnd = ". Allow MDSHO there, or pick another folder.";

    [Fact]
    public void The_app_default_folder_is_in_the_user_profile_not_Documents() {
      string folder = MDSHO.Helpers.AppServices.Defaults().Backups.DefaultFolder;
      Assert.Equal(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), AutoBackupNames.ExpectedFolderName), folder);
      Assert.False(folder.StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void A_folder_already_chosen_stays() {
      string custom = Path.Combine(db.Folder, "Elsewhere", "MDSHO backups");
      DataStore.AppSettings.Set(BackupService.FolderKey, custom);
      Assert.Equal(custom, Service().Folder);
    }

    [Fact]
    public void A_blocked_write_says_the_antivirus_or_Windows_blocked_it() {
      BackupService s = Service();
      Directory.CreateDirectory(DefaultFolder);
      // A read-only file where the backup goes: moving the new file there is denied, like a ransomware shield does.
      string target = Path.Combine(DefaultFolder, "MDSHO-backup-HOMEPC-2026-11-15-091204.zip");
      File.WriteAllText(target, "x");
      File.SetAttributes(target, FileAttributes.ReadOnly);
      try {
        Assert.False(s.RunIfDue());
        Assert.Equal("The last backup failed: your antivirus or Windows blocked MDSHO from writing to " + DefaultFolder + BlockedEnd, s.LastErrorText);
      } finally {
        File.SetAttributes(target, FileAttributes.Normal);
      }
    }

    [Theory]
    [InlineData(unchecked((int)0x80070005))] // ERROR_ACCESS_DENIED
    [InlineData(unchecked((int)0x800700E1))] // ERROR_VIRUS_INFECTED
    [InlineData(unchecked((int)0x800700E2))] // ERROR_VIRUS_DELETED
    public void Access_and_virus_errors_are_blocked_too(int hresult) {
      Assert.Equal("your antivirus or Windows blocked MDSHO from writing to X:/B. Allow MDSHO there, or pick another folder",
        BackupService.FailureReason(new IOException("denied", hresult), "X:/B"));
      Assert.Equal("your antivirus or Windows blocked MDSHO from writing to X:/B. Allow MDSHO there, or pick another folder",
        BackupService.FailureReason(new UnauthorizedAccessException("denied"), "X:/B"));
    }

    [Fact]
    public void Other_failures_keep_their_messages() {
      Assert.Equal("the disk is full", BackupService.FailureReason(new IOException("full", unchecked((int)0x80070070)), "X:/B"));
      Assert.Equal("Something odd", BackupService.FailureReason(new InvalidOperationException("Something odd."), "X:/B"));
    }

    [Fact]
    public void A_blocked_delete_while_tidying_up_keeps_the_backup_without_red() {
      BackupService s = Service();
      s.Save(true, DefaultFolder, 3);
      Directory.CreateDirectory(DefaultFolder);
      string oldest = Path.Combine(DefaultFolder, "MDSHO-backup-HOMEPC-2026-10-01-080000.zip");
      for (int i = 1; i <= 3; i++) {
        File.WriteAllText(Path.Combine(DefaultFolder, "MDSHO-backup-HOMEPC-2026-10-0" + i + "-080000.zip"), "x");
      }
      File.SetAttributes(oldest, FileAttributes.ReadOnly);
      try {
        Assert.True(s.BackUpNow());
        Assert.Null(s.LastErrorText);
        Assert.Equal(clock.Now, s.LastBackup);
        Assert.True(File.Exists(Path.Combine(DefaultFolder, "MDSHO-backup-HOMEPC-2026-11-15-091204.zip")));
        Assert.True(File.Exists(oldest));
      } finally {
        File.SetAttributes(oldest, FileAttributes.Normal);
      }
    }
    #endregion BLOCKED

    #region LAST BACKUP TEXT
    [Fact]
    public void Last_backup_says_today_yesterday_or_the_date() {
      BackupService s = Service();
      s.BackUpNow();
      Assert.Equal("Last backup: today 09:12", s.LastBackupText);
      clock.Now = clock.Now.AddDays(1);
      Assert.Equal("Last backup: yesterday 09:12", s.LastBackupText);
      clock.Now = clock.Now.AddDays(1);
      Assert.Equal("Last backup: 15 Nov 2026 09:12", s.LastBackupText);
    }
    #endregion LAST BACKUP TEXT

    #region BEFORE AN UPGRADE
    // A database from an older MDSHO: version 9, before the note colour columns.
    private void MakeOlderDatabase() {
      DesktopWindow w = DataStore.Windows.GetAll()[0];
      DataStore.Shortcuts.Create(null, w.ID, "old", @"C:\old.exe", ".lnk", Identifiers.NewGuid());
      DataStore.Clones.Create(w.ID, "S2", 1, 2);
      Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
      db.Exec("ALTER TABLE Shortcuts DROP COLUMN NoteTextColor; PRAGMA user_version = 9;");
      Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
    }

    [Fact]
    public void Before_an_upgrade_the_old_data_is_backed_up_even_when_backups_are_off() {
      Service().Save(false, DefaultFolder, 3);
      MakeOlderDatabase();
      string path = Service().BackUpBeforeUpgrade();
      Assert.Equal(Path.Combine(DefaultFolder, "MDSHO-backup-HOMEPC-before-1.12.0-2026-11-15-091204.zip"), path);
      Assert.True(File.Exists(path));
      JsonNode manifest = Manifest(path);
      Assert.Equal(9, (int)manifest["schemaVersion"]);
      Assert.Equal("HOMEPC", (string)manifest["pcName"]);
      Assert.Equal(1, (int)manifest["counts"]["clones"]);
      Assert.Equal(1, (int)manifest["counts"]["shortcuts"]);
      Assert.Equal(9, db.Scalar("PRAGMA user_version")); // nothing upgraded
      // After the upgrade the file imports.
      DataStore.Upgrade();
      Assert.Equal(AppConstants.DatabaseVersion, db.Scalar("PRAGMA user_version"));
      DataImporter.Import(path, ImportMode.Replace);
      Assert.Equal("old", DataStore.Shortcuts.GetAll().Single().Name);
    }

    [Fact]
    public void An_upgrade_backup_is_never_deleted_by_keeping() {
      MakeOlderDatabase();
      Service().BackUpBeforeUpgrade();
      DataStore.Upgrade();
      BackupService s = Service();
      s.Save(true, DefaultFolder, 3);
      for (int i = 0; i < 5; i++) {
        clock.Now = clock.Now.AddMinutes(1);
        s.BackUpNow();
      }
      Assert.Contains("MDSHO-backup-HOMEPC-before-1.12.0-2026-11-15-091204.zip", Files(DefaultFolder));
      Assert.Equal(4, Files(DefaultFolder).Length);
    }

    [Fact]
    public void No_upgrade_backup_when_the_database_is_up_to_date() {
      Assert.Null(Service().BackUpBeforeUpgrade());
      Assert.Empty(Files(DefaultFolder));
    }

    [Fact]
    public void No_upgrade_backup_and_no_database_on_a_new_install() {
      using (TempDb fresh = new TempDb(false)) {
        Assert.Null(Service().BackUpBeforeUpgrade());
        Assert.False(File.Exists(fresh.DbPath));
      }
    }

    [Fact]
    public void A_failed_upgrade_backup_returns_null_without_an_exception() {
      MakeOlderDatabase();
      string blocker = Path.Combine(db.Folder, "blocker.txt");
      File.WriteAllText(blocker, "x");
      Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
      db.Exec("INSERT INTO AppSettings (Key, Value) VALUES ('" + BackupService.FolderKey + "', '" + Path.Combine(blocker, "sub").Replace("'", "''") + "')");
      Assert.Null(Service().BackUpBeforeUpgrade());
    }

    [Fact]
    public void The_database_says_which_version_it_upgrades_from() {
      Assert.Null(DataStore.VersionBeforeUpgrade());
      MakeOlderDatabase();
      Assert.Equal(9, DataStore.VersionBeforeUpgrade());
    }
    #endregion BEFORE AN UPGRADE

    #region IMPORT
    [Fact]
    public void The_import_question_first_says_where_the_file_is_from() {
      ExportManifest m = new ExportManifest { PcName = "HOMEPC", ExportedAtUtc = new DateTime(2026, 11, 15, 9, 12, 0, DateTimeKind.Utc), AppVersion = "1.12.0" };
      m.Counts.Windows = 4;
      m.Counts.Clones = 2;
      m.Counts.Shortcuts = 52;
      string text = ImportQuestion.Text(m, TimeZoneInfo.Utc);
      Assert.StartsWith("This file is from HOMEPC, 15 Nov 2026 09:12: 4 panels, 2 clones, 52 buttons.\n\n", text);
      Assert.Contains("Yes = Replace", text);
      Assert.Contains("No = Add as new windows", text);
      Assert.Contains("Cancel = do nothing.", text);
      m.PcName = null;
      m.Counts.Windows = 1;
      m.Counts.Clones = 0;
      m.Counts.Shortcuts = 1;
      Assert.StartsWith("This file is from 15 Nov 2026 09:12: 1 panel, 0 clones, 1 button.\n\n", ImportQuestion.Text(m, TimeZoneInfo.Utc));
    }

    [Fact]
    public void The_import_dialog_opens_in_the_backup_folder_when_it_exists() {
      BackupService s = Service();
      Assert.Null(s.ImportStartFolder());
      s.BackUpNow();
      Assert.Equal(DefaultFolder, s.ImportStartFolder());
    }
    #endregion IMPORT
  }

  /// <summary>The backup folder name each build uses.</summary>
  internal static class AutoBackupNames {
#if DEBUG
    public const string ExpectedFolderName = "mdsho-backups-debug";
#else
    public const string ExpectedFolderName = "mdsho-backups";
#endif
  }
}
