using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MDSHO.Core;
using MDSHO.Data;


namespace MDSHO.ViewModels {

  /// <summary>
  /// The main ViewModel class.
  /// </summary>
  public class AppViewModel : ObservableObject {

    private ShortcutViewModel copiedShortcut;
    private ShortcutViewModel cutFromParentShortcut;
    private DesktopWindowViewModel cutFromParentWindow;
    private bool isCut;

    #region PROPERTIES
    /// <summary>True when something was cut or copied and can be pasted.</summary>
    public bool CanPaste {
      get {
        return copiedShortcut != null;
      }
    }
    /// <summary>All real windows (not clones); each one holds its own clones.</summary>
    public ObservableCollection<DesktopWindowViewModel> Windows { get; set; } = new ObservableCollection<DesktopWindowViewModel>();
    #endregion PROPERTIES

    /// <summary>Creates an empty application view model.</summary>
    public AppViewModel() {

    }

    #region Methods
    /// <summary>Creates a new window in the database, adds its view model to Windows and returns it.</summary>
    public DesktopWindowViewModel AddWindow(string screenDeviceName, long left, long top) {
      // Create the new MDSHO window into the database.
      DesktopWindow windowRecord = DataStore.Windows.Create(screenDeviceName, left, top);
      // Get the view model for the newly created MDSHO window.
      DesktopWindowViewModel desktopWindow = ViewModelFactory.WindowToViewModel(this, windowRecord);
      // Add the view model into the AppViewModel Windows list.
      this.Windows.Add(desktopWindow);
      //
      return desktopWindow;
    }
    /// <summary>
    /// Find MDSHO lost windows: gathers every window and clone to the middle of <paramref name="screen"/> (the primary screen) at the
    /// initial size, each one slightly offset from the previous, so none is lost off screen.
    /// </summary>
    public void FindWindows(MDSHO.Common.ScreenInfo screen) {
      // Bounds in DIPs (screen bounds are physical pixels).
      MDSHO.Common.ScreenRect bounds = screen.BoundsDip;
      int offset = 0;
      const int offsetStep = 40;
      // Saved too (one transaction), so a restart opens them where they were found, not off screen again.
      using (ITransactionScope tx = DataStore.BeginTransaction()) {
        foreach (DesktopWindowViewModel desktopWindow in this.Windows) {
          PlaceFoundWindow(desktopWindow, bounds, offset, screen.DeviceName);
          offset += offsetStep;
          foreach (DesktopWindowViewModel cloneWindow in desktopWindow.Clones) {
            PlaceFoundWindow(cloneWindow, bounds, offset, screen.DeviceName);
            offset += offsetStep;
          }
        }
        tx.Commit();
      }
    }
    private static void PlaceFoundWindow(DesktopWindowViewModel window, MDSHO.Common.ScreenRect bounds, int offset, string screenDeviceName) {
      window.Width = MDSHO.Common.AppConstants.InitialWindowWidth;
      window.Height = MDSHO.Common.AppConstants.InitialWindowHeight;
      window.Left = (int)bounds.Left + (((int)bounds.Width - (int)window.Width) / 2) + offset;
      window.Top = (int)bounds.Top + (((int)bounds.Height - (int)window.Height) / 2) + offset;
      window.ScreenDeviceName = screenDeviceName;
      // The same saves as moving (position and screen) and centring (position and size) a window by hand.
      if (window.IsClone) {
        DataStore.Clones.UpdateLeftTopWidthHeight(window.ID, window.Left, window.Top, window.Width, window.Height);
        DataStore.Clones.UpdateLeftTopScreenDeviceName(window.ID, window.Left, window.Top, screenDeviceName);
      } else {
        DataStore.Windows.UpdateLeftTopWidthHeight(window.ID, window.Left, window.Top, window.Width, window.Height);
        DataStore.Windows.UpdateLeftTopScreenDeviceName(window.ID, window.Left, window.Top, screenDeviceName);
      }
    }
    /// <summary>Number of real windows (clones not counted).</summary>
    public int CountWindows() {
      int counter = 0;
      foreach (DesktopWindowViewModel desktopWindow in Windows) {
        if (!desktopWindow.IsClone) {
          counter += 1;
        }
      }
      return counter;
    }
    /// <summary>Remembers a shortcut to move on the next paste, and where it came from.</summary>
    public void CutShortcut(ShortcutViewModel copiedShortcut, ShortcutViewModel cutFromParentShortcut, DesktopWindowViewModel cutFromParentWindow) {
      this.copiedShortcut = copiedShortcut;
      this.cutFromParentShortcut = cutFromParentShortcut;
      this.cutFromParentWindow = cutFromParentWindow;
      this.isCut = true;
      NotifyPasteTargetsChanged();
    }
    /// <summary>Remembers a shortcut to copy on the next paste.</summary>
    public void CopyShortcut(ShortcutViewModel copiedShortcut) {
      this.copiedShortcut = copiedShortcut;
      this.isCut = false;
      NotifyPasteTargetsChanged();
    }
    /// <summary>True when the remembered shortcut may be pasted into the given group (a cut group cannot go into itself or its own children).</summary>
    public bool CanPasteTo(ShortcutViewModel pasteToParentShortcut) {
      if (this.copiedShortcut == null) {
        return false;
      }
      // A cut shortcut cannot be pasted into itself or into one of its descendants (it would create a cycle).
      if (this.isCut && pasteToParentShortcut != null && IsSelfOrDescendant(this.copiedShortcut, pasteToParentShortcut)) {
        return false;
      }
      return true;
    }
    /// <summary>
    /// Forgets the cut or copied shortcut when it, or a group it is in, was just deleted, so Paste cannot bring back a
    /// deleted item (or copy one whose files are gone).
    /// </summary>
    public void ForgetDeleted(ShortcutViewModel deleted) {
      if (this.copiedShortcut == null || deleted == null || !IsSelfOrDescendant(deleted, this.copiedShortcut)) {
        return;
      }
      this.copiedShortcut = null;
      this.cutFromParentShortcut = null;
      this.cutFromParentWindow = null;
      this.isCut = false;
      NotifyPasteTargetsChanged();
    }
    private static bool IsSelfOrDescendant(ShortcutViewModel rootShortcut, ShortcutViewModel shortcut) {
      // Walk the subtree of rootShortcut (iteratively, any depth) looking for shortcut.
      Stack<ShortcutViewModel> stack = new Stack<ShortcutViewModel>();
      stack.Push(rootShortcut);
      while (stack.Count > 0) {
        ShortcutViewModel current = stack.Pop();
        if (ReferenceEquals(current, shortcut) || current.ID == shortcut.ID) {
          return true;
        }
        foreach (ShortcutViewModel child in current.Children) {
          stack.Push(child);
        }
      }
      return false;
    }
    /// <summary>Pastes the remembered shortcut into a group or onto a window root: moves it after a cut, copies it (with its files) after a copy.</summary>
    public void PasteShortcut(ShortcutViewModel pasteToParentShortcut, DesktopWindowViewModel pasteToParentWindow) {
      if (!this.CanPasteTo(pasteToParentShortcut)) {
        // Invalid target (e.g. cut group pasted into itself or a descendant): do nothing and keep the clipboard.
        return;
      }
      if (this.copiedShortcut != null) {
        if (this.isCut) {
          // Cut: the parent change and the position renumbering are one transaction; the collections change only after it committed.
          // Where the item is now: it may have been dragged elsewhere since it was cut.
          IList sourceList = (IList)this.copiedShortcut.ParentShortcut?.Children ?? this.copiedShortcut.ParentWindow?.RootShortcuts;
          if (sourceList != null) {
            // Found from the item itself.
          } else if (cutFromParentShortcut != null && cutFromParentWindow == null) {
            sourceList = cutFromParentShortcut.Children;
          } else if (cutFromParentShortcut == null && cutFromParentWindow != null) {
            sourceList = cutFromParentWindow.RootShortcuts;
          }
          ShortcutViewModel[] moved = new[] { this.copiedShortcut };
          if (pasteToParentShortcut != null && pasteToParentWindow == null) {
            // Paste to Shortcut.
            ViewModelFactory.ApplyShortcutMove(sourceList, pasteToParentShortcut.Children, pasteToParentShortcut.Children.Count, pasteToParentShortcut, null, moved, null);
          } else if (pasteToParentShortcut == null && pasteToParentWindow != null) {
            // Paste to Window (a clone pastes into its parent window).
            DesktopWindowViewModel targetWindow = (pasteToParentWindow.IsClone) ? pasteToParentWindow.ParentWindow : pasteToParentWindow;
            ViewModelFactory.ApplyShortcutMove(sourceList, pasteToParentWindow.RootShortcuts, pasteToParentWindow.RootShortcuts.Count, null, targetWindow, moved, null);
          }
        } else {
          // Copy: the whole subtree is inserted in one transaction; it is added to the collection only after it committed.
          if (pasteToParentShortcut != null && pasteToParentWindow == null) {
            // Paste to Shortcut.
            ShortcutViewModel deepCloneShortcut;
            using (var tx = DataStore.BeginTransaction()) {
              deepCloneShortcut = ViewModelFactory.CopyShortcut(copiedShortcut, pasteToParentShortcut, pasteToParentWindow);
              tx.Commit();
            }
            pasteToParentShortcut.Children.Add(deepCloneShortcut);
          } else if (pasteToParentShortcut == null && pasteToParentWindow != null) {
            // Paste to Window.
            DesktopWindowViewModel toParentWindow = (pasteToParentWindow.IsClone) ? pasteToParentWindow.ParentWindow : pasteToParentWindow;
            ShortcutViewModel deepCloneShortcut;
            using (var tx = DataStore.BeginTransaction()) {
              deepCloneShortcut = ViewModelFactory.CopyShortcut(copiedShortcut, pasteToParentShortcut, toParentWindow);
              tx.Commit();
            }
            toParentWindow.RootShortcuts.Add(deepCloneShortcut);
          }
        }
      }
      // Reset.
      this.copiedShortcut = null;
      this.cutFromParentShortcut = null;
      this.cutFromParentWindow = null;
      this.isCut = false;
      NotifyPasteTargetsChanged();
    }
    /// <summary>
    /// Tells every Paste command (each window, clone and group) to ask again whether it can run. Called when the cut or
    /// copied shortcut changes and after a move, so Paste menu items enable and disable at once.
    /// </summary>
    public void NotifyPasteTargetsChanged() {
      Stack<ShortcutViewModel> groups = new Stack<ShortcutViewModel>();
      foreach (DesktopWindowViewModel window in this.Windows) {
        window.PasteShortcutCommand.NotifyCanExecuteChanged();
        foreach (DesktopWindowViewModel clone in window.Clones) {
          clone.PasteShortcutCommand.NotifyCanExecuteChanged();
        }
        foreach (ShortcutViewModel shortcut in window.RootShortcuts) {
          groups.Push(shortcut);
        }
      }
      while (groups.Count > 0) {
        ShortcutViewModel shortcut = groups.Pop();
        if (!shortcut.IsGroup) {
          continue;
        }
        shortcut.PasteShortcutCommand.NotifyCanExecuteChanged();
        foreach (ShortcutViewModel child in shortcut.Children) {
          groups.Push(child);
        }
      }
    }
    #endregion Methods


  }

}