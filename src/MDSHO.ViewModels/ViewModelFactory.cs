using MDSHO.Core;
using MDSHO.Data;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using MDSHO.Common;
using System.Collections;
using System.Linq;

namespace MDSHO.ViewModels {

  /// <summary>Builds view models from database records (and back) and creates new shortcuts, files included.</summary>
  public static class ViewModelFactory {


    #region DesktopWindow
    /// <summary>Builds the view model of a clone and adds it to its window; the clone shares the window's appearance and shortcuts.</summary>
    public static void CloneWindow(AppViewModel appViewModel, DesktopWindowViewModel parentWindow, DesktopWindowClone cloneRecord) {
      DesktopWindowViewModel cloneWindow = new DesktopWindowViewModel(
        appViewModel: appViewModel,
        id: cloneRecord.ID,
        parentWindowID: parentWindow.ID,
        isClone: true,
        screenDeviceName: cloneRecord.ScreenDeviceName,
        left: cloneRecord.Left,
        top: cloneRecord.Top,
        width: cloneRecord.Width,
        height: cloneRecord.Height,
        appearance: parentWindow.Appearance);
      cloneWindow.RootShortcuts = parentWindow.RootShortcuts;
      parentWindow.Clones.Add(cloneWindow);
    }
    /// <summary>Builds the view model of a window with its shortcuts and clones.</summary>
    public static DesktopWindowViewModel WindowToViewModel(AppViewModel appViewModel, DesktopWindow windowRecord) {
      DesktopWindowAppearance appearance = new DesktopWindowAppearance(
        appViewModel: appViewModel,
        parentWindowID: windowRecord.ID,
        title: windowRecord.Title,
        borderColor: ColorConversions.NormalizeHex(windowRecord.BorderColor),
        backgroundColor: ColorConversions.NormalizeHex(windowRecord.BackgroundColor),
        textColor: ColorConversions.NormalizeHex(windowRecord.TextColor),
        titleBackgroundColor: ColorConversions.NormalizeHex(windowRecord.TitleBackgroundColor),
        titleTextColor: ColorConversions.NormalizeHex(windowRecord.TitleTextColor),
        notes: windowRecord.Notes,
        solidIcons: windowRecord.SolidIcons,
        singleClickToOpen: windowRecord.SingleClickToOpen);
      appearance.CommentWrapText = windowRecord.CommentWrapText;
      appearance.ShowVersionInTitle = windowRecord.ShowVersionInTitle;

      DesktopWindowViewModel desktopWindow = new DesktopWindowViewModel(
        appViewModel: appViewModel,
        id: windowRecord.ID,
        parentWindowID: null,
        isClone: false,
        screenDeviceName: windowRecord.ScreenDeviceName,
        left: windowRecord.Left,
        top: windowRecord.Top,
        width: windowRecord.Width,
        height: windowRecord.Height,
        appearance: appearance);

      desktopWindow.RootShortcuts = RootShortcutsToViewModels(appViewModel, windowRecord.RootShortcuts);

      foreach (DesktopWindowClone cloneRecord in windowRecord.Clones) {
        CloneWindow(appViewModel: appViewModel, parentWindow: desktopWindow, cloneRecord: cloneRecord);
      }
      return desktopWindow;
    }
    /// <summary>Builds the view models of several windows.</summary>
    public static ObservableCollection<DesktopWindowViewModel> WindowsToViewModels(AppViewModel appViewModel, List<DesktopWindow> windowRecords) {
      ObservableCollection<DesktopWindowViewModel> windows = new ObservableCollection<DesktopWindowViewModel>();
      foreach (DesktopWindow windowRecord in windowRecords) {
        DesktopWindowViewModel desktopWindow = WindowToViewModel(
          appViewModel: appViewModel,
          windowRecord: windowRecord);
        windows.Add(desktopWindow);
      }
      return windows;
    }
    #endregion DesktopWindow


    #region Shortcuts
    /// <summary>Builds the view model of a shortcut record, including the view models of all its children.</summary>
    public static ShortcutViewModel ShortcutToViewModel(AppViewModel appViewModel, ShortcutViewModel parentShortcut, Shortcut shortcutRecord) {
      ShortcutViewModel shortcut = new ShortcutViewModel(
        appViewModel: appViewModel,
        parentShortcut: parentShortcut,
        id: shortcutRecord.ID,
        parentWindowID: shortcutRecord.ParentWindowID,
        name: shortcutRecord.Name,
        target: shortcutRecord.Target,
        ext: shortcutRecord.Ext,
        guid: shortcutRecord.Guid,
        isGroup: Convert.ToBoolean(shortcutRecord.IsGroup),
        isGroupExpanded: Convert.ToBoolean(shortcutRecord.IsGroupExpanded),
        comment: shortcutRecord.Comment,
        isMarked: Convert.ToBoolean(shortcutRecord.IsMarked),
        markColor: ColorConversions.NormalizeHex(shortcutRecord.MarkColor),
        position: shortcutRecord.Position,
        isProcessCommand: Convert.ToBoolean(shortcutRecord.IsProcessCommand),
        processFileName: shortcutRecord.ProcessFileName,
        processArguments: shortcutRecord.ProcessArguments,
        processRunAsAdmin: Convert.ToBoolean(shortcutRecord.ProcessRunAsAdmin),
        processWorkingFolder: shortcutRecord.ProcessWorkingFolder,
        isNote: Convert.ToBoolean(shortcutRecord.IsNote),
        noteText: shortcutRecord.NoteText,
        noteColors: new NoteColors(shortcutRecord.NoteBorderColor, shortcutRecord.NoteTitleBackgroundColor, shortcutRecord.NoteTitleTextColor,
          shortcutRecord.NoteBackgroundColor, shortcutRecord.NoteTextColor));
      foreach (Shortcut childRecord in shortcutRecord.Shortcuts) {
        ShortcutViewModel child = ShortcutToViewModel(appViewModel: appViewModel, parentShortcut: shortcut, shortcutRecord: childRecord);
        shortcut.Children.Add(child);
      }
      return shortcut;
    }
    /// <summary>Builds the view models of a window's root shortcuts.</summary>
    public static ObservableCollection<ShortcutViewModel> RootShortcutsToViewModels(AppViewModel appViewModel, List<Shortcut> rootShortcutRecords) {
      ObservableCollection<ShortcutViewModel> rootShortcuts = new ObservableCollection<ShortcutViewModel>();
      foreach (Shortcut rootShortcutRecord in rootShortcutRecords) {
        ShortcutViewModel rootShortcut = ShortcutToViewModel(appViewModel: appViewModel, parentShortcut: null, shortcutRecord: rootShortcutRecord);
        rootShortcuts.Add(rootShortcut);
      }
      return rootShortcuts;
    }
    #endregion Shortcuts


    #region DEEP CLONE
    /// <summary>Copies a shortcut (and all its children) into the database and returns the view model of the copy. The caller owns the transaction.</summary>
    public static ShortcutViewModel DeepCloneShortcut(ShortcutViewModel shortcut, ShortcutViewModel parentShortcut, DesktopWindowViewModel parentWindow, Action<string> fileCreated = null) {
      Shortcut copyRecord = CreateShortcutCopy(shortcut, parentShortcut, parentWindow, fileCreated);
      ShortcutViewModel copy = ShortcutToViewModel(shortcut.AppViewModel, parentShortcut, copyRecord);
      foreach (ShortcutViewModel child in shortcut.Children) {
        ShortcutViewModel childCopy = DeepCloneShortcut(child, copy, null, fileCreated);
        copy.Children.Add(childCopy);
      }
      return copy;
    }
    /// <summary>
    /// Deep clones (copy/paste) a shortcut or group as ONE database transaction. The physical files copied for the clone are
    /// removed again if anything fails, so a failed copy leaves neither rows nor orphan files.
    /// </summary>
    public static ShortcutViewModel CopyShortcut(ShortcutViewModel shortcut, ShortcutViewModel parentShortcut, DesktopWindowViewModel parentWindow) {
      List<string> createdFiles = new List<string>();
      try {
        using (var tx = DataStore.BeginTransaction()) {
          ShortcutViewModel deepCloneShortcut = DeepCloneShortcut(shortcut, parentShortcut, parentWindow, createdFiles.Add);
          tx.Commit();
          return deepCloneShortcut;
        }
      } catch (Exception) {
        foreach (string file in createdFiles) {
          try { System.IO.File.Delete(file); } catch (Exception) { }
        }
        throw;
      }
    }
    #endregion DEEP CLONE


    #region HIERARCHY
    /// <summary>Loads everything from the database and builds the application view model.</summary>
    public static AppViewModel CreateAppViewModel() {
      AppViewModel appViewModel = new AppViewModel();
      List<DesktopWindow> windowRecords = null;
      string errorMessage = "";
      try {
        windowRecords = DataStore.Windows.LoadAll();
      } catch (Exception ex) {
        errorMessage = ex.ToString();
      }
      if (windowRecords == null) {
        throw new Exception("Database must be updated. " + errorMessage);
      } else {
        appViewModel.Windows = WindowsToViewModels(appViewModel: appViewModel, windowRecords: windowRecords);
      }
      return appViewModel;
    }
    /// <summary>The real window a shortcut belongs to: the window of its top level shortcut or group (null if that is not attached to a window).</summary>
    public static DesktopWindowViewModel GetOwningWindow(ShortcutViewModel shortcut) {
      ShortcutViewModel rootShortcut = GetRootShortcut(shortcut);
      if (rootShortcut != null) {
        return rootShortcut.ParentWindow;
      }
      return null;
    }
    /// <summary>Walks up through the groups to the top level shortcut or group (the shortcut itself when it is top level).</summary>
    public static ShortcutViewModel GetRootShortcut(ShortcutViewModel shortcut) {
      while (shortcut.ParentShortcut != null) {
        shortcut = shortcut.ParentShortcut;
      }
      return shortcut;
    }
    #endregion HIERARCHY


    #region NEW
    /// <summary>Inserts a copy of a shortcut (new GUID, last position) under a group or window and copies its file; the caller owns the transaction.</summary>
    public static Shortcut CreateShortcutCopy(ShortcutViewModel shortcut, ShortcutViewModel parentShortcut, DesktopWindowViewModel parentWindow, Action<string> fileCreated = null) {
      RequireOneParent(parentShortcut, parentWindow);
      long? parentShortcutID = parentShortcut?.ID;
      long? parentWindowID = parentWindow?.ID;
      // The copy gets its own Guid and is appended as the last child of the new parent.
      Shortcut template = new Shortcut(
        parentShortcutID: parentShortcutID,
        parentWindowID: parentWindowID,
        name: shortcut.Name,
        target: shortcut.StoredTarget ?? "",
        ext: shortcut.Ext,
        guid: Identifiers.NewGuid(),
        isGroup: Convert.ToInt64(shortcut.IsGroup),
        isGroupExpanded: Convert.ToInt64(shortcut.IsGroupExpanded),
        comment: shortcut.Comment,
        isMarked: Convert.ToInt64(shortcut.IsMarked),
        markColor: shortcut.MarkColor,
        position: 0,
        isProcessCommand: Convert.ToInt64(shortcut.IsProcessCommand),
        processFileName: shortcut.ProcessFileName,
        processArguments: shortcut.ProcessArguments,
        processRunAsAdmin: Convert.ToInt64(shortcut.ProcessRunAsAdmin),
        processWorkingFolder: shortcut.ProcessWorkingFolder,
        isNote: Convert.ToInt64(shortcut.IsNote),
        noteText: shortcut.NoteText ?? "");
      var colors = shortcut.NoteColors.Stored;
      template.NoteBorderColor = colors.Border;
      template.NoteTitleBackgroundColor = colors.TitleBackground;
      template.NoteTitleTextColor = colors.TitleText;
      template.NoteBackgroundColor = colors.Background;
      template.NoteTextColor = colors.Text;
      Shortcut savedRecord = DataStore.Shortcuts.Append(template);
      // Copy the physical file after the insert, so a failed insert (rollback) never leaves an orphan file behind.
      // Notes, groups and run commands have no shortcut file.
      if (!shortcut.IsGroup && !shortcut.IsProcessCommand && !shortcut.IsNote) {
        string shortcutPath = Paths.GetShortcutFilePath(shortcut.Guid, shortcut.Ext);
        if (System.IO.File.Exists(shortcutPath)) {
          string copyPath = Paths.GetShortcutFilePath(savedRecord.Guid, savedRecord.Ext);
          System.IO.File.Copy(shortcutPath, copyPath);
          fileCreated?.Invoke(copyPath);
        }
      }
      return savedRecord;
    }
    // A new shortcut goes either into a group or to a window root, never both or neither.
    private static void RequireOneParent(ShortcutViewModel parentShortcut, DesktopWindowViewModel parentWindow) {
      if (parentShortcut == null && parentWindow == null) {
        throw new Exception("Both parentShortcut and parentWindow cannot be null.");
      } else if (parentShortcut != null && parentWindow != null) {
        throw new Exception("Both parentShortcut and parentWindow cannot be NOT null.");
      }
    }
    /// <summary>Creates a new empty group under a group or window root.</summary>
    public static ShortcutViewModel NewGroupShortcut(AppViewModel appViewModel, ShortcutViewModel parentShortcut, DesktopWindowViewModel parentWindow) {
      RequireOneParent(parentShortcut, parentWindow);
      long? parentShortcutID = parentShortcut?.ID;
      long? parentWindowID = parentWindow?.ID;
      Shortcut shortcutRecord = DataStore.Shortcuts.CreateGroup(parentShortcutID, parentWindowID);
      ShortcutViewModel shortcut = ShortcutToViewModel(appViewModel, parentShortcut, shortcutRecord);
      return shortcut;
    }
    /// <summary>Creates a new shortcut and its .lnk or .url file under a group or window root.</summary>
    public static ShortcutViewModel NewShortcut(AppViewModel appViewModel, ShortcutViewModel parentShortcut, DesktopWindowViewModel parentWindow, string name, string target, string ext, string guid) {
      RequireOneParent(parentShortcut, parentWindow);
      long? parentShortcutID = parentShortcut?.ID;
      long? parentWindowID = parentWindow?.ID;
      Shortcut shortcutRecord = DataStore.Shortcuts.Create(parentShortcutID, parentWindowID, name, target, ext, guid);
      ShortcutViewModel shortcut = ShortcutToViewModel(appViewModel, parentShortcut, shortcutRecord);

      // IMPORTANT INFO
      // In case of control panel items we first create the link
      // then COPY the link in Shortcuts folder and execute from there
      // In case of normal cases we CREATE the link in Shortcuts folder and execute from there
      // If the target does not exists then there is nothing to create in the Shortcuts folder
      // (we are talking not in the case of a drag androp event, but in just opening the application and the shortcut in Shortcuts folder has been deleted)

      // Write the shortcut (not groups) to physical user local folder C:\Users\USER_NAME\AppData\Local.
      // LocalApplicationData (applies to current user - local only). No need for Administrator access to write on that folder.
      // File.Exists & Directory.Exists are time consuming in case of \\server... For that reason we just let it fail silently.

      try {
        if (shortcut.Ext.Equals(AppConstants.LnkExtension, StringComparison.OrdinalIgnoreCase)) {
          ShortcutFileService.CreateLnkFile(shortcut.Guid, shortcut.Target);
        } else if (ext.Equals(AppConstants.UrlExtension, StringComparison.OrdinalIgnoreCase)) {
          ShortcutFileService.CreateUrlFile(shortcut.Guid, shortcut.Target);
        }
      } catch (Exception) {
        // Let it fail silently.
      }
      return shortcut;
    }
    /// <summary>Creates a new run-command shortcut under a group or window root.</summary>
    public static ShortcutViewModel NewProcessCommandShortcut(AppViewModel appViewModel, ShortcutViewModel parentShortcut, DesktopWindowViewModel parentWindow) {
      RequireOneParent(parentShortcut, parentWindow);
      long? parentShortcutID = parentShortcut?.ID;
      long? parentWindowID = parentWindow?.ID;
      Shortcut shortcutRecord = DataStore.Shortcuts.CreateProcessCommand(parentShortcutID, parentWindowID);
      ShortcutViewModel shortcut = ShortcutToViewModel(appViewModel, parentShortcut, shortcutRecord);
      return shortcut;
    }
    /// <summary>Creates a new empty note named "New note" under a group or a window (exactly one of the two).</summary>
    public static ShortcutViewModel NewNoteShortcut(AppViewModel appViewModel, ShortcutViewModel parentShortcut, DesktopWindowViewModel parentWindow) {
      RequireOneParent(parentShortcut, parentWindow);
      Shortcut shortcutRecord = DataStore.Shortcuts.CreateNote(parentShortcut?.ID, parentWindow?.ID);
      return ShortcutToViewModel(appViewModel, parentShortcut, shortcutRecord);
    }
    #endregion NEW


    #region Update Shortcuts Position
    /// <summary>Saves the list order as the Position of each shortcut.</summary>
    public static void UpdateShortcutsPosition(IList shortcuts) {
      for (int i = 0; i < shortcuts.Count; i++) {
        if (shortcuts[i] is ShortcutViewModel) {
          ShortcutViewModel shortcut = (ShortcutViewModel)shortcuts[i];
          DataStore.Shortcuts.UpdatePosition(shortcut.ID, i);
        }
      }
    }
    /// <summary>
    /// Moves/reorders shortcuts (and/or inserts newly created ones) as ONE database transaction, then applies the
    /// in-memory collection changes. If anything fails nothing is committed, the collections and the view models are untouched
    /// and the files of newly created shortcuts are removed.
    /// </summary>
    /// <param name="sourceList">The list the moved shortcuts currently live in (null if none).</param>
    /// <param name="destinationList">The list the shortcuts are moved or added to.</param>
    /// <param name="insertIndex">Index in destinationList BEFORE the moved items are removed from it.</param>
    /// <param name="newParentShortcut">The group the shortcuts go into (null when they go to a window root).</param>
    /// <param name="newParentWindow">The real window whose root the shortcuts go to (null when they go into a group).</param>
    /// <param name="moved">The existing shortcuts that are moved.</param>
    /// <param name="create">Optional: creates new shortcuts (DB rows + files) and adds them to the given list.</param>
    public static void ApplyShortcutMove(IList sourceList, IList destinationList, int insertIndex, ShortcutViewModel newParentShortcut, DesktopWindowViewModel newParentWindow, IList<ShortcutViewModel> moved, Action<List<ShortcutViewModel>> create) {
      List<ShortcutViewModel> created = new List<ShortcutViewModel>();
      // Work out the final order on copies, without touching the real collections.
      List<object> finalDest = destinationList.Cast<object>().ToList();
      bool sameList = sourceList != null && ReferenceEquals(sourceList, destinationList);
      List<object> finalSource = (sourceList != null && !sameList) ? sourceList.Cast<object>().ToList() : null;
      List<object> workSource = sameList ? finalDest : finalSource;
      List<ShortcutViewModel> removals = new List<ShortcutViewModel>();
      foreach (ShortcutViewModel m in moved) {
        int index = workSource == null ? -1 : workSource.IndexOf(m);
        if (index != -1) {
          workSource.RemoveAt(index);
          removals.Add(m);
          if (sameList && index < insertIndex) {
            --insertIndex;
          }
        }
      }
      bool setParent = (newParentShortcut == null) != (newParentWindow == null);
      List<ShortcutViewModel> toAdd;
      try {
        using (var tx = DataStore.BeginTransaction()) {
          create?.Invoke(created);
          toAdd = moved.Concat(created).ToList();
          int at = insertIndex;
          foreach (ShortcutViewModel vm in toAdd) {
            finalDest.Insert(at++, vm);
          }
          if (setParent) {
            foreach (ShortcutViewModel m in moved) {
              DataStore.Shortcuts.UpdateParentShortcutIDAndParentWindowID(m.ID, newParentShortcut?.ID, newParentWindow?.ID);
            }
          }
          DataStore.Shortcuts.UpdatePositions(finalDest.OfType<ShortcutViewModel>().Select(s => s.ID));
          if (finalSource != null) {
            DataStore.Shortcuts.UpdatePositions(finalSource.OfType<ShortcutViewModel>().Select(s => s.ID));
          }
          tx.Commit();
        }
      } catch (Exception) {
        // Rolled back: remove the files created for the new shortcuts.
        foreach (ShortcutViewModel c in created) {
          try { ShortcutFileService.DeleteShortcutFiles(c); } catch (Exception) { }
        }
        throw;
      }
      // Committed: apply the in-memory changes (none of them writes to the database).
      foreach (ShortcutViewModel m in removals) {
        sourceList.Remove(m);
      }
      int insertAt = insertIndex;
      foreach (ShortcutViewModel vm in toAdd) {
        destinationList.Insert(insertAt++, vm);
      }
      if (setParent) {
        foreach (ShortcutViewModel m in moved) {
          m.SetParentInMemory(newParentShortcut, newParentWindow?.ID);
        }
      }
      SetPositionsInMemory(destinationList);
      if (sourceList != null && !sameList) {
        SetPositionsInMemory(sourceList);
      }
      // A move can change where a cut group may be pasted (not into itself or its own children).
      moved.FirstOrDefault()?.AppViewModel?.NotifyPasteTargetsChanged();
    }
    private static void SetPositionsInMemory(IList shortcuts) {
      for (int i = 0; i < shortcuts.Count; i++) {
        if (shortcuts[i] is ShortcutViewModel) {
          ((ShortcutViewModel)shortcuts[i]).Position = i;
        }
      }
    }
    #endregion Update Shortcuts Position


  }

}