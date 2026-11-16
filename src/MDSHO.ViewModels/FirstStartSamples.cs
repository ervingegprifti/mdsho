using System;
using System.IO;
using MDSHO.Common;
using MDSHO.Core;
using MDSHO.Data;

namespace MDSHO.ViewModels {

  /// <summary>The "Getting started" group of sample buttons a brand-new database starts with.</summary>
  public static class FirstStartSamples {
    /// <summary>The text of the welcome note.</summary>
    public const string NoteText =
      "Welcome to MDSHO.\r\n" +
      "\r\n" +
      "Drag files, folders or web links onto a panel to make buttons.\r\n" +
      "Right-click anything for more: new groups, run commands, notes, colours.\r\n" +
      "Press Ctrl+Shift+Alt+M to bring your panels to the front.\r\n" +
      "\r\n" +
      "These samples are yours to try. Delete the group when you are done.\r\n" +
      "\r\n" +
      "MDSHO is developed in spare time. If it saves you time, you can support its development: right-click a panel's title and choose Support MDSHO.";

    /// <summary>Adds the group and its samples to the window and returns the welcome note's ID (null when it was not added).</summary>
    public static long? AddTo(long windowID, string documentsFolder) {
      Shortcut group;
      try {
        group = DataStore.Shortcuts.Append(Record(null, windowID, "Getting started", "", isGroup: true));
      } catch (Exception ex) {
        AppLog.Warning(ex, "The Getting started group was not added");
        return null;
      }
      long? noteID = Add("Welcome to MDSHO", () => {
        Shortcut note = Record(group.ID, null, "Welcome to MDSHO", "Opens this note. Notes save themselves as you type.");
        note.IsNote = 1;
        note.NoteText = NoteText;
        return DataStore.Shortcuts.Append(note).ID;
      });
      Add("Documents", () => {
        if (string.IsNullOrEmpty(documentsFolder) || !Directory.Exists(documentsFolder)) {
          throw new DirectoryNotFoundException("The Documents folder was not found.");
        }
        return AppendWithFile(Record(group.ID, null, "Documents",
          "A shortcut to your Documents folder. Drag your own files and folders here to add more.",
          target: documentsFolder, ext: AppConstants.LnkExtension));
      });
      Add("Show my IP address", () => {
        Shortcut command = Record(group.ID, null, "Show my IP address",
          "A run command. Right-click it and choose Edit run command... to see how it works.");
        command.IsProcessCommand = 1;
        command.ProcessFileName = RunCommandLine.DefaultProgram;
        command.ProcessArguments = "/K ipconfig";
        return DataStore.Shortcuts.Append(command).ID;
      });
      Add("MDSHO guide", () => AppendWithFile(Record(group.ID, null, "MDSHO guide",
        "A web link to the MDSHO guide. Drag links from your browser to add more.",
        target: AppConstants.DocsUrl, ext: AppConstants.UrlExtension)));
      return noteID;
    }

    private static long? Add(string name, Func<long> add) {
      try {
        return add();
      } catch (Exception ex) {
        AppLog.Warning(ex, "First start sample {Name} was not added", name);
        return null;
      }
    }

    // Writes the .lnk or .url file first, the same way a drop does; a failed insert removes it again.
    private static long AppendWithFile(Shortcut record) {
      if (record.Ext == AppConstants.LnkExtension) {
        ShortcutFileService.CreateLnkFile(record.Guid, record.Target);
      } else {
        ShortcutFileService.CreateUrlFile(record.Guid, record.Target);
      }
      try {
        return DataStore.Shortcuts.Append(record).ID;
      } catch (Exception) {
        File.Delete(Paths.GetShortcutFilePath(record.Guid, record.Ext));
        throw;
      }
    }

    private static Shortcut Record(long? parentShortcutID, long? parentWindowID, string name, string comment,
        string target = "", string ext = "", bool isGroup = false) {
      return new Shortcut(parentShortcutID, parentWindowID, name, target, ext, Identifiers.NewGuid(),
        isGroup: isGroup ? 1 : 0, isGroupExpanded: isGroup ? 1 : 0, comment: comment, isMarked: 0, markColor: "#FFFFFFFF",
        position: 0, isProcessCommand: 0, processFileName: "", processArguments: "");
    }
  }

}
