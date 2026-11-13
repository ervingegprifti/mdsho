using System.Collections.ObjectModel;
using MDSHO.Core;
using MDSHO.Data;
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

// Drag & drop uses https://github.com/punker76/gong-wpf-dragdrop
// For the drag & drop to work make shure there is a backgrownd specified in the control expecting drag & drop. Transparent willl do.
// For external files drag & drop to work make shure Visual Studio is not run as Administrator.-->

namespace MDSHO.ViewModels {

  /// <summary>Lets a view model ask its window to close.</summary>
  public interface ICloseWindow {
    /// <summary>Set by the window; called by the view model to close it.</summary>
    Action CloseWindow { get; set; }
  }

  /// <summary>
  /// Represents the ViewModel of an DesktopWindow or an MDSHOClone object.
  /// </summary>
  public partial class DesktopWindowViewModel : ObservableObject, ICloseWindow {
    /// <summary>The version of the running application, shown after the title ("Show version in title").</summary>
    private static readonly string appVersion = VersionForTitle(Assembly.GetEntryAssembly()?.GetName().Version);
    /// <summary>
    /// "x.y.z", or "x.y.z.n" for a sideload test build (whose 4th part is its build number); null when unknown.
    /// </summary>
    public static string VersionForTitle(Version version) {
      if (version == null) { return null; }
      return version.Revision > 0 ? version.ToString(4) : version.ToString(3);
    }
    /// <summary>The version shown after the title, for example "v1.5.1", or "v1.5.1.7" for a sideload test build (empty when it can't be read).</summary>
    public static string VersionText => string.IsNullOrEmpty(appVersion) ? string.Empty : $"v{appVersion}";
    private DesktopWindowViewModel parentWindow = null;
    private long id;
    private long? parentWindowID;
    private bool isClone;
    /// <summary>Name of the screen the window is on.</summary>
    [ObservableProperty]
    private string screenDeviceName;
    /// <summary>Left edge of the window.</summary>
    [ObservableProperty]
    private long left;
    /// <summary>Top edge of the window.</summary>
    [ObservableProperty]
    private long top;
    /// <summary>Window width.</summary>
    [ObservableProperty]
    private long width;
    /// <summary>Window height.</summary>
    [ObservableProperty]
    private long height;
    private bool isDocked;
    private DesktopWindowAppearance appearance;
    private ObservableCollection<DesktopWindowViewModel> cloneWindows = new ObservableCollection<DesktopWindowViewModel>();
    private ObservableCollection<ShortcutViewModel> rootShortcuts = new ObservableCollection<ShortcutViewModel>();

    #region PROPERTIES
    /// <summary>The application view model this window belongs to.</summary>
    public AppViewModel AppViewModel { get; private set; }
    /// <summary>For a clone, the real window it copies; null for a real window.</summary>
    public DesktopWindowViewModel ParentWindow {
      get {
        if (this.parentWindow == null) {
          if (parentWindowID != null) {
            foreach (DesktopWindowViewModel desktopWindow in this.AppViewModel.Windows) {
              if (desktopWindow.ID == this.parentWindowID) {
                parentWindow = desktopWindow;
              }
            }
          }
        }
        return this.parentWindow;
      }
    }
    /// <summary>Database ID: of the window row, or of the clone row for a clone.</summary>
    public long ID { get { return id; } set { id = value; } }
    /// <summary>For a clone, the ID of its real window.</summary>
    public long? ParentWindowID { get { return parentWindowID; } set { parentWindowID = value; } }
    /// <summary>True for a clone of another window.</summary>
    public bool IsClone { get { return isClone; } set { isClone = value; } }
    /// <summary>
    /// True while the window is docked to the left or right screen edge. Docked windows have square corners,
    /// floating windows rounded ones.
    /// </summary>
    public bool IsDocked {
      // Hand-written: the setter is private, which a generated property cannot be.
      get { return this.isDocked; }
      private set { SetProperty(ref this.isDocked, value); }
    }
    /// <summary>Works out again whether the window is docked to a side of one of the screens, from its position and size.</summary>
    public void RefreshDocked(IEnumerable<MDSHO.Common.ScreenInfo> screens) {
      MDSHO.Common.ScreenRect rect = new MDSHO.Common.ScreenRect(Left, Top, Width, Height);
      IsDocked = screens != null && screens.Any(screen => MDSHO.Common.ScreenGeometry.IsDockedToSide(rect, screen));
    }
    /// <summary>
    /// Docks the window: saves left/top/height (window or clone row) in one transaction, then changes memory,
    /// which makes the corners square.
    /// </summary>
    public void ApplyDock(long left, long top, long height) {
      using (var tx = DataStore.BeginTransaction()) {
        if (IsClone) {
          DataStore.Clones.UpdateLeftTopHeight(ID, left, top, height);
        } else {
          DataStore.Windows.UpdateLeftTopHeight(ID, left, top, height);
        }
        tx.Commit();
      }
      Left = left;
      Top = top;
      Height = height;
      IsDocked = true;
    }
    /// <summary>
    /// The title as shown: with the suffix (Clone) for a clone, DEBUG in Debug builds, and the version when
    /// "Show version in title" is on.
    /// </summary>
    public string TitleDisplay {
      get {
        string title = IsClone ? $"{Appearance.Title} (Clone)" : Appearance.Title;
#if DEBUG
        title += " DEBUG";
#endif
        return Appearance.ShowVersionInTitle && VersionText.Length > 0 ? $"{title} {VersionText}" : title;
      }
    }
    // Shown or hidden (the views turn these into Visibility).
    /// <summary>True only while the window has no shortcuts, to show the drag items here hint.</summary>
    public bool ShowDragItemsHereInfo => RootShortcuts.Count == 0;
    /// <summary>True for a real window (only real windows can be cloned).</summary>
    public bool ShowCloneWindow => !this.isClone;
    /// <summary>True for a clone (a clone can be closed, a real window is deleted).</summary>
    public bool ShowCloseWindow => this.isClone;
    //
    /// <summary>Title, colours and settings, shared by a window and all its clones.</summary>
    public DesktopWindowAppearance Appearance { get { return appearance; } set { appearance = value; } }
    /// <summary>Checks every shortcut target again, so a target deleted or restored since loading gets the right icon.</summary>
    public void RefreshTargetKinds() {
      RefreshTargetKinds(this.rootShortcuts);
    }
    private static void RefreshTargetKinds(IEnumerable<ShortcutViewModel> shortcuts) {
      foreach (ShortcutViewModel shortcut in shortcuts) {
        shortcut.RefreshTargetKind();
        RefreshTargetKinds(shortcut.Children);
      }
    }
    /// <summary>The shortcuts and groups at the top level of the window (shared with the clones).</summary>
    public ObservableCollection<ShortcutViewModel> RootShortcuts {
      get {
        return this.rootShortcuts;
      }
      set {
        this.rootShortcuts = value;
        this.rootShortcuts.CollectionChanged += RootShortcuts_CollectionChanged;
      }
    }
    /// <summary>The clones of this window (empty for a clone).</summary>
    public ObservableCollection<DesktopWindowViewModel> Clones {
      get {
        return cloneWindows;
      }
      set {
        this.cloneWindows.CollectionChanged -= Clones_CollectionChanged;
        cloneWindows = value;
        this.cloneWindows.CollectionChanged += Clones_CollectionChanged;
      }
    }
    // Commands: NewGroupShortcutCommand, NewProcessCommandShortcutCommand, PasteShortcutCommand, BeginEditTitleCommand,
    // CommitEditTitleCommand and CancelEditTitleCommand are generated from the [RelayCommand] methods.
    /// <summary>Closes the window (for a clone: deletes the clone). Not generated: the CloseWindow name is taken by <see cref="ICloseWindow"/>.</summary>
    public IRelayCommand CloseWindowCommand { get; }

    #region TITLE EDIT
    private bool isEditingTitle;
    /// <summary>Text being edited; the edit box binds to this. Written to the Title only by CommitEditTitle.</summary>
    [ObservableProperty]
    private string titleEditText;
    /// <summary>True while the header title edit box is open (this window only; the title itself is shared with the clones).</summary>
    public bool IsEditingTitle {
      // Hand-written: the setter is private, which a generated property cannot be.
      get { return this.isEditingTitle; }
      private set { SetProperty(ref this.isEditingTitle, value); }
    }
    /// <summary>Opens the title edit box with the current title.</summary>
    [RelayCommand]
    public void BeginEditTitle() {
      this.TitleEditText = this.Appearance.Title;
      this.IsEditingTitle = true;
    }
    /// <summary>Closes the edit box; writes the Title (shared appearance, saved once by the real window) only if it changed.</summary>
    [RelayCommand]
    public void CommitEditTitle() {
      if (!this.isEditingTitle) { return; }
      this.IsEditingTitle = false;
      if ((this.Appearance.Title ?? "") != (this.titleEditText ?? "")) {
        this.Appearance.Title = this.titleEditText;
      }
    }
    /// <summary>Closes the edit box and discards the edited text.</summary>
    [RelayCommand]
    public void CancelEditTitle() {
      this.IsEditingTitle = false;
    }
    #endregion TITLE EDIT
    #endregion PROPERTIES

    /// <summary>Creates the view model of a window or clone from its stored values.</summary>
    public DesktopWindowViewModel(AppViewModel appViewModel, long id, long? parentWindowID, bool isClone, string screenDeviceName, long left, long top, long width, long height, DesktopWindowAppearance appearance) {
      this.AppViewModel = appViewModel;
      this.id = id;
      this.parentWindowID = parentWindowID;
      this.isClone = isClone;
      this.screenDeviceName = screenDeviceName;
      this.left = left;
      this.top = top;
      this.width = width;
      this.height = height;
      this.appearance = appearance;
      // Events.
      this.appearance.PropertyChanged += Appearance_PropertyChanged;
      this.cloneWindows.CollectionChanged += Clones_CollectionChanged;
      // Commands.
      this.CloseWindowCommand = new RelayCommand<object>(CloseThisWindow);
    }




    #region EVENTS
    private void Appearance_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e) {
      if (e.PropertyName == nameof(this.Appearance.Title) || e.PropertyName == nameof(this.Appearance.ShowVersionInTitle)) {
        OnPropertyChanged(nameof(this.TitleDisplay)); // So to refresh the Title View with the new change.
      }
      // DesktopWindowAppearance is shared between the window and its clones. Only the window (non clone) saves it, so it is saved once and against the Windows table ID.
      // For a clone this.ID is the clones table ID and must not be used with the Windows repository update methods.
      if (this.IsClone) {
        return;
      }
      long windowID = this.Appearance.ParentWindowID;
      // Save to Database. The colours are not saved here as they change too many times while the user picks one: SettingsWindow saves them.
      if (e.PropertyName == nameof(this.Appearance.Title)) {
        DataStore.Windows.UpdateTitle(windowID, title: this.Appearance.Title);
      } else if (e.PropertyName == nameof(this.Appearance.SolidIcons)) {
        DataStore.Windows.UpdateSolidIcons(windowID, this.Appearance.SolidIcons);
      } else if (e.PropertyName == nameof(this.Appearance.SingleClickToOpen)) {
        DataStore.Windows.UpdateSingleClickToOpen(windowID, this.Appearance.SingleClickToOpen);
      } else if (e.PropertyName == nameof(this.Appearance.CommentWrapText)) {
        DataStore.Windows.UpdateCommentWrapText(windowID, this.Appearance.CommentWrapText);
      } else if (e.PropertyName == nameof(this.Appearance.ShowVersionInTitle)) {
        DataStore.Windows.UpdateShowVersionInTitle(windowID, this.Appearance.ShowVersionInTitle);
      }
    }
    private void Clones_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) {
      // A clone removed from its window (closed/deleted) stops listening to the shared objects of the window.
      if (e.OldItems != null) {
        foreach (DesktopWindowViewModel cloneWindow in e.OldItems) {
          cloneWindow.DetachFromSharedObjects();
        }
      }
    }
    private void RootShortcuts_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) {
      OnPropertyChanged(nameof(ShowDragItemsHereInfo));
    }
    #endregion EVENTS


    #region METHODS
    // Can Methods
    private bool CanExecutePasteShortcut() {
      return this.AppViewModel.CanPaste;
    }
    // Methods
    /// <summary>Adds a new group at the window root.</summary>
    [RelayCommand]
    private void NewGroupShortcut() {
      // This is a Root Command Shortcut.
      ShortcutViewModel parentShortcut = null;
      DesktopWindowViewModel shortcutParentWindow = this.IsClone ? this.ParentWindow : this;
      ShortcutViewModel shortcut = ViewModelFactory.NewGroupShortcut(this.AppViewModel, parentShortcut, shortcutParentWindow);
      shortcutParentWindow.RootShortcuts.Add(shortcut);
    }
    /// <summary>Adds a new run-command shortcut at the window root.</summary>
    [RelayCommand]
    private void NewProcessCommandShortcut() {
      // This is a Root Command Shortcut.
      ShortcutViewModel parentShortcut = null;
      DesktopWindowViewModel shortcutParentWindow = this.IsClone ? this.ParentWindow : this;
      ShortcutViewModel shortcut = ViewModelFactory.NewProcessCommandShortcut(this.AppViewModel, parentShortcut, shortcutParentWindow);
      shortcutParentWindow.RootShortcuts.Add(shortcut);
      // A new command opens the Run Command window; on Cancel the new command stays.
      ShortcutViewModel.OpenRunCommandEditor?.Invoke(shortcut, true);
    }
    /// <summary>Adds a new note at the window root and opens the Note window; on Cancel the new note stays.</summary>
    [RelayCommand]
    private void NewNoteShortcut() {
      DesktopWindowViewModel shortcutParentWindow = this.IsClone ? this.ParentWindow : this;
      ShortcutViewModel note = ViewModelFactory.NewNoteShortcut(this.AppViewModel, null, shortcutParentWindow);
      shortcutParentWindow.RootShortcuts.Add(note);
      ShortcutViewModel.OpenNoteEditor?.Invoke(note);
    }
    /// <summary>Pastes the cut or copied shortcut at the window root.</summary>
    [RelayCommand(CanExecute = nameof(CanExecutePasteShortcut))]
    private void PasteShortcut() {
      this.AppViewModel.PasteShortcut(pasteToParentShortcut: null, pasteToParentWindow: this);
    }

    /// <summary>What Enter does on a non-group shortcut. Replaceable so tests do not start processes.</summary>
    public Action<ShortcutViewModel> RunShortcutAction { get; set; } = s => s.Run();

    /// <summary>
    /// Keyboard shortcuts on the selected shortcut/group: F2 rename, Delete delete (confirmed), Ctrl+X/C/V cut/copy/paste,
    /// Enter run (or expand/collapse a group). Returns true when the key was handled. Nothing is handled while a name is being edited.
    /// </summary>
    public bool HandleKey(ShortcutKey key, ShortcutModifiers modifiers, ShortcutViewModel selected, bool isEditing) {
      if (isEditing || (selected != null && selected.IsEditing)) {
        return false;
      }
      bool ctrl = modifiers == ShortcutModifiers.Control;
      bool none = modifiers == ShortcutModifiers.None;
      if (ctrl && key == ShortcutKey.V) {
        // Into the selected group; a selected item goes to its parent group, or the window root when it is at root. Nothing selected: window root.
        ShortcutViewModel target = selected == null ? null : selected.IsGroup ? selected : selected.ParentShortcut;
        if (target != null) {
          if (target.PasteShortcutCommand.CanExecute(null)) {
            target.PasteShortcutCommand.Execute(null);
          }
        } else if (this.AppViewModel.CanPasteTo(null)) {
          this.PasteShortcutCommand.Execute(null);
        }
        return true;
      }
      if (selected == null) {
        return false;
      }
      if (none && key == ShortcutKey.F2) {
        if (selected.IsNote) {
          ShortcutViewModel.RenameNoteEditor?.Invoke(selected);
        } else if (selected.ShowCommand) {
          ShortcutViewModel.RenameRunCommandEditor?.Invoke(selected);
        } else {
          selected.BeginEdit(EditField.Name);
        }
      } else if (none && key == ShortcutKey.Delete) {
        selected.DeleteShortcutCommand.Execute(null);
      } else if (ctrl && key == ShortcutKey.X) {
        selected.CutShortcutCommand.Execute(null);
      } else if (ctrl && key == ShortcutKey.C) {
        selected.CopyShortcutCommand.Execute(null);
      } else if (none && key == ShortcutKey.Enter) {
        if (selected.IsGroup) {
          selected.IsGroupExpanded = !selected.IsGroupExpanded;
        } else {
          RunShortcutAction(selected);
        }
      } else {
        return false;
      }
      return true;
    }
    // Close Window from its view model.
    /// <summary>Set by the window; called to close it.</summary>
    public Action CloseWindow { get; set; }
    /// <summary>
    /// The implementation of CloseThisWindow is at DesktopWindowView_Loaded.
    /// </summary>
    public void CloseThisWindow(object parameter) {
      CloseWindow?.Invoke();
    }
    /// <summary>
    /// Deletes this clone: first from the database (a failure leaves the view models untouched), then from its window's
    /// Clones, which frees its screen for "Clone to all screens". The window closes its view afterwards.
    /// </summary>
    public void DeleteClone() {
      if (!this.isClone) {
        throw new InvalidOperationException("Only a clone can be deleted with DeleteClone.");
      }
      DataStore.Clones.Delete(this.ID);
      this.ParentWindow?.Clones.Remove(this);
    }
    /// <summary>
    /// Unsubscribe from the objects shared with the parent window (used when a clone is removed).
    /// </summary>
    private void DetachFromSharedObjects() {
      this.appearance.PropertyChanged -= Appearance_PropertyChanged;
      this.rootShortcuts.CollectionChanged -= RootShortcuts_CollectionChanged;
    }
    #endregion METHODS


    #region FUNCTIONS
    /// <summary>
    /// Deletes this (non-clone) window and all its shortcuts from the database in one transaction, then updates the view models
    /// and deletes the physical shortcut files. Its clones must already be closed.
    /// </summary>
    public void DeleteWindowAndShortcuts() {
      List<ShortcutViewModel> rootShortcutsToDelete = this.RootShortcuts.ToList();
      using (var tx = DataStore.BeginTransaction()) {
        foreach (ShortcutViewModel shortcut in rootShortcutsToDelete) {
          DataStore.Shortcuts.Delete(shortcut.ID);
        }
        DataStore.Windows.Delete(this.ID);
        tx.Commit();
      }
      this.RootShortcuts.Clear();
      this.AppViewModel.Windows.Remove(this);
      foreach (ShortcutViewModel shortcut in rootShortcutsToDelete) {
        this.AppViewModel.ForgetDeleted(shortcut);
        ShortcutFileService.DeleteShortcutFiles(shortcut);
      }
    }
    /// <summary>Creates a clone of this window on the given screen at the given position, saves it and adds it to Clones.</summary>
    public void CloneWindow(string screenDeviceName, int left, int top) {
      DesktopWindowClone cloneRecord = DataStore.Clones.Create(this.ID, screenDeviceName, left, top);
      ViewModelFactory.CloneWindow(appViewModel: this.AppViewModel, parentWindow: this, cloneRecord: cloneRecord);
    }
    #endregion FUNCTIONS


    #region DRAG & DROP
    /// <summary>
    /// Makes shortcuts for files and folders dropped from outside (Explorer): a dropped .lnk or .url file is copied into
    /// the shortcuts folder, anything else gets a new .lnk. Each new shortcut is added to <paramref name="created"/>.
    /// The view's drop handler calls this inside the drop's transaction.
    /// </summary>
    public void AddDroppedFiles(IEnumerable<string> droppedFilePaths, ShortcutViewModel shortcutParentShortcut, DesktopWindowViewModel shortcutParentWindow, List<ShortcutViewModel> created) {
      string guid;
      foreach (string droppedFilePath in droppedFilePaths) {
        // Each dropped item gets its own GUID (copied file name and shortcut row).
        guid = Common.Identifiers.NewGuid();
        string name = Path.GetFileName(droppedFilePath);

        DirectoryInfo directoryInfo = new DirectoryInfo(droppedFilePath);
        if (directoryInfo.Parent == null) {
          // This is the root. In that case name is empty so we need to set the name differently.
          name = RootName(droppedFilePath);
        }

        ShortcutViewModel shortcutToAdd = null;
        string copiedFilePath = null;

        try {
          if (Path.GetExtension(droppedFilePath).Equals(Common.AppConstants.LnkExtension, StringComparison.InvariantCultureIgnoreCase)) {
            // If the dropped file is a .lnk file. We just copy the .lnk files to the sortcuts folder and use them from there.
            string shortcutLnkFilePath = Common.Paths.GetShortcutFilePath(guid, Common.AppConstants.LnkExtension);
            File.Copy(droppedFilePath, shortcutLnkFilePath);
            copiedFilePath = shortcutLnkFilePath;
            shortcutToAdd = ViewModelFactory.NewShortcut(this.AppViewModel, shortcutParentShortcut, shortcutParentWindow, name, shortcutLnkFilePath, Common.AppConstants.LnkExtension, guid);
          } else if (Path.GetExtension(droppedFilePath).Equals(Common.AppConstants.UrlExtension, StringComparison.InvariantCultureIgnoreCase)) {
            // If the dropped file is a .url file. We just copy the .url files to the sortcuts folder and use them from there.
            string shortcutUrlFilePath = Common.Paths.GetShortcutFilePath(guid, Common.AppConstants.UrlExtension);
            File.Copy(droppedFilePath, shortcutUrlFilePath);
            copiedFilePath = shortcutUrlFilePath;
            shortcutToAdd = ViewModelFactory.NewShortcut(this.AppViewModel, shortcutParentShortcut, shortcutParentWindow, name, shortcutUrlFilePath, Common.AppConstants.UrlExtension, guid);
          } else {
            shortcutToAdd = ViewModelFactory.NewShortcut(this.AppViewModel, shortcutParentShortcut, shortcutParentWindow, name, droppedFilePath, Common.AppConstants.LnkExtension, guid);
          }
        } catch (Exception) {
          // The copied file has no shortcut row yet (so it is not in "created"): remove it.
          if (copiedFilePath != null && File.Exists(copiedFilePath)) {
            File.Delete(copiedFilePath);
          }
          throw;
        }

        if (shortcutToAdd != null) {
          created.Add(shortcutToAdd);
        }
      }
    }
    // The name of a dropped drive or share root: "Local Disk (C:)", "Drive (D:)" when it has no label or is not ready,
    // and the share path itself (\\server\share), which has no drive letter.
    private static string RootName(string root) {
      if (root.StartsWith(@"\\") || root.StartsWith("//")) {
        return root.TrimEnd('\\', '/');
      }
      DriveInfo driveInfo = new DriveInfo(root);
      string volumeLabel;
      try {
        volumeLabel = driveInfo.VolumeLabel;
      } catch (IOException) {
        volumeLabel = null;
      } catch (UnauthorizedAccessException) {
        volumeLabel = null;
      }
      if (string.IsNullOrEmpty(volumeLabel)) {
        volumeLabel = "Drive";
      }
      return $"{volumeLabel} ({driveInfo.Name.Replace(@"\", "")})";
    }
    /// <summary>Makes a web shortcut for dropped text when it is an absolute address; other text is ignored.</summary>
    public void AddDroppedText(string dataText, ShortcutViewModel shortcutParentShortcut, DesktopWindowViewModel shortcutParentWindow, List<ShortcutViewModel> created) {
      Uri uri;
      bool isAbsoluteUri = Uri.TryCreate(dataText, UriKind.Absolute, out uri);
      if (isAbsoluteUri) {
        string guid = Common.Identifiers.NewGuid();
        ShortcutViewModel shortcutToAdd = ViewModelFactory.NewShortcut(this.AppViewModel, shortcutParentShortcut, shortcutParentWindow, uri.AbsoluteUri, uri.AbsoluteUri, Common.AppConstants.UrlExtension, guid);

        if (shortcutToAdd != null) {
          created.Add(shortcutToAdd);
        }
      }
    }
    #endregion


  }

}