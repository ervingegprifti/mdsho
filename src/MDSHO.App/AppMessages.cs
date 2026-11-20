using System.Windows;
using MDSHO.Common;
using MDSHO.Data;
using MDSHO.ViewModels;

namespace MDSHO {

  /// <summary>
  /// The app's messages, each in the one MDSHO dialog (<see cref="MdshoMessage"/>) with its wording, buttons, default and
  /// answer. Questions, destructive actions and errors get the warning icon; plain results get the info icon.
  /// </summary>
  public static class AppMessages {
    private const string ImportCaption = "Import MDSHO data";
    private const string ExitCaption = "Exit MDSHO";

    /// <summary>A second MDSHO was started.</summary>
    public static void AlreadyRunning() {
      MdshoMessage.Show(null, "MDSHO already running", "MDSHO seems to be running already. Please check the taskbar.", MessageIcon.Info);
    }

    /// <summary>A problem found while opening the data at startup.</summary>
    public static void StartupWarning(string message, string caption) {
      MdshoMessage.Show(null, caption, message, MessageIcon.Warning);
    }

    /// <summary>Yes / No / Cancel with a backup, OK / Cancel without one (the question says what each button does).</summary>
    public static DatabaseRecoveryChoice AskDatabaseRecovery(string question, bool hasBackup) {
      if (hasBackup) {
        MessageResult answer = MdshoMessage.Show(null, DatabaseStartup.RecoveryCaption, question, MessageIcon.Warning, MessageButtons.YesNoCancel, MessageResult.Yes);
        return answer == MessageResult.Yes ? DatabaseRecoveryChoice.RestoreBackup
          : answer == MessageResult.No ? DatabaseRecoveryChoice.StartEmpty : DatabaseRecoveryChoice.Exit;
      }
      MessageResult ok = MdshoMessage.Show(null, DatabaseStartup.RecoveryCaption, question, MessageIcon.Warning, MessageButtons.OKCancel, MessageResult.OK);
      return ok == MessageResult.OK ? DatabaseRecoveryChoice.StartEmpty : DatabaseRecoveryChoice.Exit;
    }

    /// <summary>What Start with Windows did.</summary>
    public static void AutoStart(string message) {
      MdshoMessage.Show(null, AutoStartController.MessageTitle, message, MessageIcon.Info);
    }

    /// <summary>Exit application from the tray: true to exit.</summary>
    public static bool ConfirmExit() {
      return MdshoMessage.Show(null, ExitCaption, "Are you sure you want to exit MDSHO?", MessageIcon.Warning, MessageButtons.YesNo, MessageResult.No) == MessageResult.Yes;
    }

    /// <summary>Exit while notes could not be saved: true to exit anyway.</summary>
    public static bool ConfirmExitOverUnsavedNotes(string question) {
      return MdshoMessage.Show(null, ExitCaption, question, MessageIcon.Warning, MessageButtons.YesNo, MessageResult.No) == MessageResult.Yes;
    }

    /// <summary>Export data finished.</summary>
    public static void ExportDone(string message) {
      MdshoMessage.Show(null, "Export MDSHO data", message, MessageIcon.Info);
    }

    /// <summary>Yes replaces the data, No adds the windows as new ones, Cancel (null) stops.</summary>
    public static ImportMode? AskImportMode(string question) {
      MessageResult answer = MdshoMessage.Show(null, ImportCaption, question, MessageIcon.Warning, MessageButtons.YesNoCancel, MessageResult.Yes);
      return answer == MessageResult.Yes ? ImportMode.Replace : answer == MessageResult.No ? ImportMode.AddAsNew : (ImportMode?)null;
    }

    /// <summary>Import while notes could not be saved: true to import anyway.</summary>
    public static bool ConfirmImportOverUnsavedNotes(string unsavedNotes) {
      string question = unsavedNotes + "\n\nImport anyway? What you typed in them since the last save will be lost.";
      return MdshoMessage.Show(null, ImportCaption, question, MessageIcon.Warning, MessageButtons.YesNo, MessageResult.No) == MessageResult.Yes;
    }

    /// <summary>Import data finished (MDSHO restarts next).</summary>
    public static void ImportDone(string message) {
      MdshoMessage.Show(null, ImportCaption, message, MessageIcon.Info);
    }

    /// <summary>Delete window asks twice; true only after Yes both times.</summary>
    public static bool ConfirmDeleteWindow(Window owner) {
      if (MdshoMessage.Show(owner, "Delete window confirmation", "This window and all its shortcuts will be deleted. Do you want to delete this window?",
          MessageIcon.Warning, MessageButtons.YesNo, MessageResult.No) != MessageResult.Yes) {
        return false;
      }
      return MdshoMessage.Show(owner, "Delete window warning", "One last warning. Do you want to delete this window? All its shortcuts will be gone.",
        MessageIcon.Warning, MessageButtons.YesNo, MessageResult.No) == MessageResult.Yes;
    }

    /// <summary>Virtual files (from a zip or a phone) were dropped on a panel.</summary>
    public static void VirtualFilesDropped() {
      string message = "To create this kind of shortcut please do:\n";
      message += "1. Drag to create a desktop shortcut first,\n";
      message += "2. Drag the shortcut from desktop to MDSHO";
      MdshoMessage.Show(null, "Information", message, MessageIcon.Info);
    }
  }
}
