using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MDSHO.Common;
using MDSHO.Core;
using MDSHO.Data;
using MDSHO.ViewModels;
using Serilog.Events;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>What MDSHO writes to its log: database upgrade steps, a too-new database, export and import.</summary>
  public class LoggingEventsTests : IDisposable {
    private readonly TempDb db = new TempDb();
    private readonly LogCapture log = new LogCapture();

    public void Dispose() {
      log.Dispose();
      db.Dispose();
    }

    private string Work(string name) {
      string folder = Path.Combine(db.Folder, "work");
      Directory.CreateDirectory(folder);
      return Path.Combine(folder, name);
    }

    [Fact]
    public void Migrations_log_the_backup_and_each_step() {
      List<DbMigration> list = SchemaManager.Migrations.ToList();
      int next = AppConstants.DatabaseVersion + 1;
      list.Add(new DbMigration(next, "test step", conn => { }));

      SchemaManager.ApplyMigrations(list);

      string backup = Directory.GetFiles(db.Folder, "test.sqlite3.bak-v*").Single();
      Assert.Contains(log.Messages, m => m.Contains("backed up") && m.Contains(backup) && m.Contains("version " + AppConstants.DatabaseVersion));
      Assert.Contains(log.Messages, m => m.Contains("migration " + next) && m.Contains("test step"));
      Assert.Contains(log.Messages, m => m == "Database version " + next);
    }

    [Fact]
    public void An_up_to_date_database_logs_only_its_version() {
      SchemaManager.ApplyMigrations(SchemaManager.Migrations);
      Assert.Equal(new[] { "Database version " + AppConstants.DatabaseVersion }, log.Messages);
    }

    [Fact]
    public void A_database_from_a_newer_MDSHO_is_logged_as_a_warning() {
      bool proceed = DatabaseStartup.TryUpgrade(() => throw new DatabaseTooNewException(99, 5), (message, caption) => { });
      Assert.False(proceed);
      LogEvent e = log.Events.Single();
      Assert.Equal(LogEventLevel.Warning, e.Level);
      Assert.IsType<DatabaseTooNewException>(e.Exception);
    }

    [Fact]
    public void Export_logs_start_and_counts() {
      DataStore.Windows.Create("SCREEN1", 1, 1);
      int windows = DataStore.Windows.GetAll().Count;
      string zip = Work("export.zip");
      DataExporter.Export(zip);
      Assert.Contains(log.Messages, m => m.StartsWith("Export started", StringComparison.Ordinal) && m.Contains(zip));
      Assert.Contains(log.Messages, m => m.StartsWith("Export finished", StringComparison.Ordinal) && m.Contains(windows + " windows, 0 clones, 0 shortcuts"));
    }

    [Fact]
    public void A_failed_export_is_logged_as_an_error_and_still_thrown() {
      Assert.Throws<DirectoryNotFoundException>(() => DataExporter.Export(Path.Combine(db.Folder, "nodir", "x.zip")));
      LogEvent e = log.Events.Last();
      Assert.Equal(LogEventLevel.Error, e.Level);
      Assert.IsType<DirectoryNotFoundException>(e.Exception);
      Assert.StartsWith("Export failed", log.Messages.Last());
    }

    [Fact]
    public void Import_logs_start_mode_counts_and_backup() {
      DataStore.Windows.Create("SCREEN1", 1, 1);
      int windows = DataStore.Windows.GetAll().Count;
      string zip = Work("export.zip");
      DataExporter.Export(zip);
      int before = log.Events.Count;

      ImportResult result = DataImporter.Import(zip, ImportMode.Replace);

      List<string> messages = log.Messages.Skip(before).ToList();
      Assert.Contains(messages, m => m.StartsWith("Import started", StringComparison.Ordinal) && m.Contains("Replace") && m.Contains(zip));
      Assert.Contains(messages, m => m.StartsWith("Import finished", StringComparison.Ordinal) && m.Contains(windows + " windows, 0 clones, 0 shortcuts"));
      Assert.Contains(messages, m => m.Contains(result.BackupPath));
    }

    [Fact]
    public void An_invalid_import_file_is_logged_as_a_warning_and_still_thrown() {
      string zip = Work("not-a-zip.zip");
      File.WriteAllText(zip, "not a zip");
      Assert.Throws<DataPortabilityException>(() => DataImporter.Import(zip, ImportMode.AddAsNew));
      LogEvent e = log.Events.Last();
      Assert.Equal(LogEventLevel.Warning, e.Level);
      Assert.IsType<DataPortabilityException>(e.Exception);
      Assert.StartsWith("Import failed", log.Messages.Last());
    }
  }
}
