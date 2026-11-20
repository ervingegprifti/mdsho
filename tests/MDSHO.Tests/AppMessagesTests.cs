using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Controls;
using MDSHO.Common;
using MDSHO.Data;
using MDSHO.Helpers;
using MDSHO.ViewModels;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// Every message of the app goes through the one MDSHO dialog (no Windows message box is left): each keeps its wording,
  /// buttons, default and answer; questions, destructive actions and errors get the warning icon, plain results the info
  /// icon. The dialog's test hook answers, so no window opens.
  /// </summary>
  public class AppMessagesTests : IDisposable {
    private readonly Func<MessageWindow, MessageResult> oldShow = MdshoMessage.ShowWindow;
    private readonly List<MessageWindow> shown = new List<MessageWindow>();
    private readonly Queue<MessageResult> answers = new Queue<MessageResult>();

    public AppMessagesTests() {
      MdshoMessage.ShowWindow = w => {
        shown.Add(w);
        return answers.Count > 0 ? answers.Dequeue() : MessageRules.CancelResultOf(ButtonsOf(w));
      };
    }

    public void Dispose() {
      MdshoMessage.ShowWindow = oldShow;
    }

    private static MessageButtons ButtonsOf(MessageWindow w) {
      string labels = Labels(w);
      switch (labels) {
        case "OK,Cancel": return MessageButtons.OKCancel;
        case "Yes,No": return MessageButtons.YesNo;
        case "Yes,No,Cancel": return MessageButtons.YesNoCancel;
        default: return MessageButtons.OK;
      }
    }

    private static string Labels(MessageWindow w) {
      return string.Join(",", ((Panel)w.FindName("PanelButtons")).Children.OfType<Button>().Select(b => (string)b.Content));
    }

    private static string DefaultOf(MessageWindow w) {
      return (string)((Panel)w.FindName("PanelButtons")).Children.OfType<Button>().Single(b => b.IsDefault).Content;
    }

    private static string Heading(MessageWindow w) { return ((TextBlock)w.FindName("TextHeading")).Text; }
    private static string Text(MessageWindow w) { return ((TextBlock)w.FindName("TextMessage")).Text; }
    private static string Icon(MessageWindow w) { return ((TextBlock)w.FindName("TextIcon")).Text; }
    private static readonly IconGlyphs Glyphs = new IconGlyphs(true);

    private MessageWindow Only() {
      MessageWindow w = Assert.Single(shown);
      w.Close();
      return w;
    }

    [Fact]
    public void No_windows_message_box_is_left_in_the_app() {
      string src = Path.Combine(ThirdPartyNoticesTests.RepoRoot(), "src");
      string sep = Path.DirectorySeparatorChar.ToString();
      List<string> left = Directory.GetFiles(src, "*.cs", SearchOption.AllDirectories)
        .Where(f => !f.Contains(sep + "obj" + sep) && !f.Contains(sep + "bin" + sep))
        .Where(f => File.ReadAllText(f).Contains("MessageBox.Show"))
        .Select(f => f.Substring(src.Length + 1))
        .ToList();
      Assert.True(left.Count == 0, "MessageBox.Show is still used in: " + string.Join(", ", left));
    }

    [Fact]
    public void Already_running_is_info_with_ok() {
      WpfAppHost.Run(() => {
        AppMessages.AlreadyRunning();
        MessageWindow w = Only();
        Assert.StartsWith("MDSHO already running", w.Title);
        Assert.Equal("MDSHO seems to be running already. Please check the taskbar.", Heading(w));
        Assert.Equal(Glyphs.Info, Icon(w));
        Assert.Equal("OK", Labels(w));
      });
    }

    [Fact]
    public void A_startup_warning_is_a_warning_with_ok() {
      WpfAppHost.Run(() => {
        AppMessages.StartupWarning("The database is newer.\n\nUpdate MDSHO.", "MDSHO database");
        MessageWindow w = Only();
        Assert.StartsWith("MDSHO database", w.Title);
        Assert.Equal("The database is newer.", Heading(w));
        Assert.Equal("Update MDSHO.", Text(w));
        Assert.Equal(Glyphs.Warning, Icon(w));
        Assert.Equal("OK", Labels(w));
      });
    }

    [Theory]
    [InlineData(MessageResult.Yes, DatabaseRecoveryChoice.RestoreBackup)]
    [InlineData(MessageResult.No, DatabaseRecoveryChoice.StartEmpty)]
    [InlineData(MessageResult.Cancel, DatabaseRecoveryChoice.Exit)]
    public void Database_recovery_with_a_backup_asks_yes_no_cancel_with_yes_as_default(MessageResult answer, DatabaseRecoveryChoice expected) {
      WpfAppHost.Run(() => {
        answers.Enqueue(answer);
        Assert.Equal(expected, AppMessages.AskDatabaseRecovery("Restore the backup?", true));
        MessageWindow w = Only();
        Assert.StartsWith(DatabaseStartup.RecoveryCaption, w.Title);
        Assert.Equal("Yes,No,Cancel", Labels(w));
        Assert.Equal("Yes", DefaultOf(w));
        Assert.Equal(Glyphs.Warning, Icon(w));
      });
    }

    [Theory]
    [InlineData(MessageResult.OK, DatabaseRecoveryChoice.StartEmpty)]
    [InlineData(MessageResult.Cancel, DatabaseRecoveryChoice.Exit)]
    public void Database_recovery_without_a_backup_asks_ok_cancel_with_ok_as_default(MessageResult answer, DatabaseRecoveryChoice expected) {
      WpfAppHost.Run(() => {
        answers.Enqueue(answer);
        Assert.Equal(expected, AppMessages.AskDatabaseRecovery("Start empty?", false));
        MessageWindow w = Only();
        Assert.Equal("OK,Cancel", Labels(w));
        Assert.Equal("OK", DefaultOf(w));
        Assert.Equal(Glyphs.Warning, Icon(w));
      });
    }

    [Fact]
    public void Start_with_windows_messages_are_info() {
      WpfAppHost.Run(() => {
        AppMessages.AutoStart("Turned on in Settings.");
        MessageWindow w = Only();
        Assert.StartsWith(AutoStartController.MessageTitle, w.Title);
        Assert.Equal(Glyphs.Info, Icon(w));
        Assert.Equal("OK", Labels(w));
      });
    }

    [Theory]
    [InlineData(MessageResult.Yes, true)]
    [InlineData(MessageResult.No, false)]
    public void Exit_asks_yes_no_with_no_as_default(MessageResult answer, bool expected) {
      WpfAppHost.Run(() => {
        answers.Enqueue(answer);
        Assert.Equal(expected, AppMessages.ConfirmExit());
        MessageWindow w = Only();
        Assert.StartsWith("Exit MDSHO", w.Title);
        Assert.Equal("Are you sure you want to exit MDSHO?", Heading(w));
        Assert.Equal("Yes,No", Labels(w));
        Assert.Equal("No", DefaultOf(w));
        Assert.Equal(Glyphs.Warning, Icon(w));
      });
    }

    [Fact]
    public void Exit_over_unsaved_notes_asks_yes_no_with_no_as_default() {
      WpfAppHost.Run(() => {
        answers.Enqueue(MessageResult.Yes);
        Assert.True(AppMessages.ConfirmExitOverUnsavedNotes("A note could not be saved.\n\nExit anyway?"));
        MessageWindow w = Only();
        Assert.StartsWith("Exit MDSHO", w.Title);
        Assert.Equal("A note could not be saved.", Heading(w));
        Assert.Equal("Exit anyway?", Text(w));
        Assert.Equal("No", DefaultOf(w));
        Assert.Equal(Glyphs.Warning, Icon(w));
      });
    }

    [Fact]
    public void Export_done_is_info_with_ok() {
      WpfAppHost.Run(() => {
        AppMessages.ExportDone("Exported 1 windows to:\nD:/x.zip");
        MessageWindow w = Only();
        Assert.StartsWith("Export MDSHO data", w.Title);
        Assert.Equal("Exported 1 windows to:", Heading(w));
        Assert.Equal("D:/x.zip", Text(w));
        Assert.Equal(Glyphs.Info, Icon(w));
      });
    }

    [Theory]
    [InlineData(MessageResult.Yes, ImportMode.Replace)]
    [InlineData(MessageResult.No, ImportMode.AddAsNew)]
    [InlineData(MessageResult.Cancel, null)]
    public void Import_asks_yes_no_cancel_with_yes_as_default(MessageResult answer, ImportMode? expected) {
      WpfAppHost.Run(() => {
        answers.Enqueue(answer);
        Assert.Equal(expected, AppMessages.AskImportMode("Replace your data?"));
        MessageWindow w = Only();
        Assert.StartsWith("Import MDSHO data", w.Title);
        Assert.Equal("Yes,No,Cancel", Labels(w));
        Assert.Equal("Yes", DefaultOf(w));
        Assert.Equal(Glyphs.Warning, Icon(w));
      });
    }

    [Fact]
    public void Import_over_unsaved_notes_asks_yes_no_with_no_as_default_and_keeps_the_wording() {
      WpfAppHost.Run(() => {
        Assert.False(AppMessages.ConfirmImportOverUnsavedNotes("A note could not be saved."));
        MessageWindow w = Only();
        Assert.StartsWith("Import MDSHO data", w.Title);
        Assert.Equal("A note could not be saved.", Heading(w));
        Assert.Equal("Import anyway? What you typed in them since the last save will be lost.", Text(w));
        Assert.Equal("Yes,No", Labels(w));
        Assert.Equal("No", DefaultOf(w));
        Assert.Equal(Glyphs.Warning, Icon(w));
      });
    }

    [Fact]
    public void Import_done_is_info_with_ok() {
      WpfAppHost.Run(() => {
        AppMessages.ImportDone("Imported.\n\nMDSHO will now restart to load the imported data.");
        MessageWindow w = Only();
        Assert.StartsWith("Import MDSHO data", w.Title);
        Assert.Equal(Glyphs.Info, Icon(w));
        Assert.Equal("OK", Labels(w));
      });
    }

    [Fact]
    public void Deleting_a_window_asks_twice_with_no_as_default_and_stops_at_the_first_no() {
      WpfAppHost.Run(() => {
        answers.Enqueue(MessageResult.No);
        Assert.False(AppMessages.ConfirmDeleteWindow(null, true));
        MessageWindow first = Only();
        Assert.StartsWith("Delete window confirmation", first.Title);
        Assert.Equal("This window and all its shortcuts will be deleted. Do you want to delete this window?", Heading(first));
        Assert.Equal("No", DefaultOf(first));
        Assert.Equal(Glyphs.Warning, Icon(first));
      });
    }

    [Fact]
    public void Deleting_a_window_needs_yes_twice() {
      WpfAppHost.Run(() => {
        answers.Enqueue(MessageResult.Yes);
        answers.Enqueue(MessageResult.Yes);
        Assert.True(AppMessages.ConfirmDeleteWindow(null, true));
        Assert.Equal(2, shown.Count);
        MessageWindow second = shown[1];
        Assert.StartsWith("Delete window warning", second.Title);
        Assert.Equal("One last warning. Do you want to delete this window? All its shortcuts will be gone.", Heading(second));
        Assert.Equal("Yes,No", Labels(second));
        Assert.Equal("No", DefaultOf(second));
        Assert.Equal(Glyphs.Warning, Icon(second));
        shown.ForEach(w => w.Close());
      });
    }

    [Fact]
    public void Dropping_virtual_files_is_info() {
      WpfAppHost.Run(() => {
        AppMessages.VirtualFilesDropped(true);
        MessageWindow w = Only();
        Assert.StartsWith("Information", w.Title);
        Assert.Equal("To create this kind of shortcut please do:", Heading(w));
        Assert.Equal("1. Drag to create a desktop shortcut first,\n2. Drag the shortcut from desktop to MDSHO", Text(w));
        Assert.Equal(Glyphs.Info, Icon(w));
      });
    }

    [Fact]
    public void Store_information_is_info() {
      WpfAppHost.Run(() => {
        new StoreActionsHost(null, null, null).ShowInformation("Thanks.", "Support MDSHO");
        MessageWindow w = Only();
        Assert.StartsWith("Support MDSHO", w.Title);
        Assert.Equal(Glyphs.Info, Icon(w));
      });
    }

    [Fact]
    public void The_view_model_prompts_use_the_dialog() {
      WpfAppHost.Run(() => {
        Func<string, string, bool> oldConfirm = Dialogs.Confirm;
        Action<string, string> oldMessage = Dialogs.ShowMessage;
        Action<string, string, string> oldError = Dialogs.ShowError;
        Action<string, string, string> oldInfo = Dialogs.ShowInfo;
        try {
          WpfBridge.Connect();
          answers.Enqueue(MessageResult.Yes);
          Assert.True(Dialogs.Confirm("Delete this button?", "Delete"));
          Dialogs.ShowMessage("The file is gone.", "MDSHO");
          Assert.Equal(2, shown.Count);
          Assert.StartsWith("Delete", shown[0].Title);
          Assert.Equal("Yes,No", Labels(shown[0]));
          Assert.Equal("No", DefaultOf(shown[0]));
          Assert.Equal(Glyphs.Warning, Icon(shown[0]));
          Assert.Equal("OK", Labels(shown[1]));
          Assert.Equal(Glyphs.Warning, Icon(shown[1]));
          shown.ForEach(w => w.Close());
        } finally {
          Dialogs.Confirm = oldConfirm;
          Dialogs.ShowMessage = oldMessage;
          Dialogs.ShowError = oldError;
          Dialogs.ShowInfo = oldInfo;
        }
      });
    }
  }
}
