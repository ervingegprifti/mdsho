using System;
using System.Linq;
using System.Collections.ObjectModel;
using MDSHO.Core;
using MDSHO.Data;
using MDSHO.Common;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;


namespace MDSHO.ViewModels {

  /// <summary>Which text field of a shortcut is being edited in place.</summary>
  public enum EditField {
    /// <summary>Nothing is being edited.</summary>
    None,
    /// <summary>The name.</summary>
    Name,
    /// <summary>The program of a command shortcut.</summary>
    ProcessFileName,
    /// <summary>The arguments of a command shortcut.</summary>
    ProcessArguments
  }

  /// <summary>What a shortcut points at; picks the glyph of its first icon (the colour is always the window text colour).</summary>
  public enum ShortcutTargetKind {
    /// <summary>A folder.</summary>
    Folder,
    /// <summary>A file, or a target that is still being checked (network, removable or optical drive).</summary>
    File,
    /// <summary>A web address.</summary>
    WebLink,
    /// <summary>Runs a command.</summary>
    Command,
    /// <summary>A group of shortcuts.</summary>
    Group,
    /// <summary>A file or folder path that does not exist (any more).</summary>
    Missing
  }

  /// <summary>
  /// Represents the ViewModel of a Shortcut object.
  /// </summary>
  public partial class ShortcutViewModel : ObservableObject {
    /// <summary>Folder check; replaceable so tests need no real folders.</summary>
    public static Func<string, bool> DirectoryExists { get; set; } = Directory.Exists;
    /// <summary>File check; replaceable so tests need no real files.</summary>
    public static Func<string, bool> FileExists { get; set; } = File.Exists;
    /// <summary>Runs the slow (network path) folder check off the UI thread; replaceable for tests.</summary>
    public static Action<Action> BackgroundRunner { get; set; } = a => System.Threading.Tasks.Task.Run(a);
    /// <summary>True when a path may be slow to check (network/removable/optical drive, UNC); errors count as slow. Replaceable for tests.</summary>
    public static Func<string, bool> IsSlowPath { get; set; } = path => {
      if (path.StartsWith("\\\\") || path.StartsWith("//")) { return true; }
      try {
        DriveType type = new DriveInfo(Path.GetPathRoot(path)).DriveType;
        return type != DriveType.Fixed && type != DriveType.Ram;
      } catch {
        return true;
      }
    };
    private ShortcutTargetKind pathKind = ShortcutTargetKind.File;
    private int targetCheckVersion;
    private int pendingSlowCheckVersion;
    private long id;
    // The database saves of the observable properties below still happen in ShortcutViewModel_PropertyChanged, except
    // IsGroupExpanded, which saves in OnIsGroupExpandedChanged.
    /// <summary>The group this shortcut is in; null for a shortcut at the window root.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ResolvedProcessArguments), nameof(ToolTipText))]
    private ShortcutViewModel parentShortcut;
    /// <summary>ID of the window the shortcut sits directly in, or null.</summary>
    [ObservableProperty]
    private long? parentWindowID;
    /// <summary>The name shown in the tree.</summary>
    [ObservableProperty]
    private string name;
    private string target;
    private string ext;
    private string guid;
    private bool isGroup;
    /// <summary>True when the group is open in the tree; changes are saved at once.</summary>
    [ObservableProperty]
    private bool isGroupExpanded;
    /// <summary>True while the item is selected in the tree.</summary>
    [ObservableProperty]
    private bool isSelected;
    /// <summary>The comment on the shortcut, edited in the Comment window and shown in the tooltip.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasComment), nameof(CommentPreview), nameof(HasToolTip), nameof(ShowCommentDivider))]
    private string comment;
    /// <summary>True when the comment has more than spaces and new lines; the view then shows the comment marker.</summary>
    public bool HasComment => !string.IsNullOrWhiteSpace(this.comment);
    /// <summary>How many comment lines the tooltip shows; more are cut and marked with "…".</summary>
    public const int CommentPreviewLines = 10;
    /// <summary>The comment for the tooltip: at most <see cref="CommentPreviewLines"/> lines, then "…"; null without a comment.</summary>
    public string CommentPreview {
      get {
        if (!this.HasComment) {
          return null;
        }
        string[] lines = this.comment.Trim().Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        if (lines.Length <= CommentPreviewLines) {
          return string.Join(Environment.NewLine, lines);
        }
        return string.Join(Environment.NewLine, lines.Take(CommentPreviewLines)) + Environment.NewLine + "…";
      }
    }
    /// <summary>True when the tooltip has something to show: the target or command line, or the comment.</summary>
    public bool HasToolTip => !string.IsNullOrEmpty(this.ToolTipText) || this.HasComment;
    /// <summary>True when the tooltip shows both the target or command line and the comment, with a divider between them.</summary>
    public bool ShowCommentDivider => !string.IsNullOrEmpty(this.ToolTipText) && this.HasComment;
    /// <summary>True when the shortcut carries a colour mark.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowMark))]
    private bool isMarked;
    /// <summary>The mark colour.</summary>
    [ObservableProperty]
    private string markColor;
    private long position;
    private bool isProcessCommand;
    /// <summary>Program run by a command shortcut.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Target), nameof(ResolvedProcessArguments), nameof(ToolTipText))]
    private string processFileName;
    /// <summary>Arguments of the program of a command shortcut.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Target), nameof(ResolvedProcessArguments), nameof(ToolTipText))]
    private string processArguments;
    /// <summary>True when the run command always starts as administrator; saved at once.</summary>
    [ObservableProperty]
    private bool processRunAsAdmin;
    private string processWorkingFolder = "";
    private ObservableCollection<ShortcutViewModel> shortcuts = new ObservableCollection<ShortcutViewModel>();
    private bool suppressParentSave;
    private EditField editField;
    /// <summary>Text being edited; the edit box binds to this. Written to the field only by CommitEdit.</summary>
    [ObservableProperty]
    private string editText;

    #region PROPERTIES
    /// <summary>The application view model this shortcut belongs to.</summary>
    public AppViewModel AppViewModel { get; private set; }
    /// <summary>The window this shortcut sits directly in; null when it is inside a group.</summary>
    public DesktopWindowViewModel ParentWindow {
      get {
        if (parentWindowID != null) {
          foreach (DesktopWindowViewModel desktopWindow in this.AppViewModel.Windows) {
            if (desktopWindow.ID == this.parentWindowID) {
              return desktopWindow;
            }
          }
        }
        return null;
      }
    }
    /// <summary>The real window the shortcut belongs to, found by walking up through its groups.</summary>
    public DesktopWindowViewModel OwningWindow {
      get {
        return ViewModelFactory.GetOwningWindow(this);
      }
    }
    /// <summary>Database ID.</summary>
    public long ID { get { return id; } set { id = value; } }
    partial void OnNameChanged(string value) {
      if (this.isGroup) {
        // The group value may have changed: the commands directly inside resolve their arguments again.
        foreach (ShortcutViewModel child in this.shortcuts) {
          child.RaiseCommandLineChanged();
        }
      }
    }
    /// <summary>What the shortcut opens; for a command shortcut the program and arguments as text.</summary>
    public string Target {
      get {
        if (this.isProcessCommand) {
          string fileName = string.IsNullOrEmpty(this.ProcessFileName) ? "ProcessFileName" : this.ProcessFileName;
          string arguments = string.IsNullOrEmpty(this.ProcessArguments) ? "ProcessArguments" : this.ProcessArguments;
          return $"{fileName} -> {arguments}";
        } else {
          return this.target;
        }
      }
      // Hand-written: the getter is worked out for a command shortcut, and a new target is checked again.
      set {
        if (SetProperty(ref this.target, value)) {
          UpdateTargetKind(false);
        }
      }
    }
    /// <summary>The target as stored in the database (for a run command not the "program -> arguments" text of <see cref="Target"/>).</summary>
    internal string StoredTarget { get { return this.target; } }
    /// <summary>What the shortcut points at: folder, file, web link, command or group.</summary>
    public ShortcutTargetKind TargetKind {
      get {
        if (this.isGroup) { return ShortcutTargetKind.Group; }
        if (this.isProcessCommand) { return ShortcutTargetKind.Command; }
        if (this.IsWebAddress) { return ShortcutTargetKind.WebLink; }
        return this.pathKind;
      }
    }
    // A .url file, or a .lnk whose target is a web address: no file system check.
    private bool IsWebAddress {
      get {
        if (string.Equals(this.ext, ".url", StringComparison.OrdinalIgnoreCase)) { return true; }
        return Uri.TryCreate(this.target?.Trim(), UriKind.Absolute, out Uri uri)
          && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeFtp);
      }
    }
    /// <summary>Checks the target again (e.g. it was deleted or restored since loading); slow paths are checked in the background.</summary>
    public void RefreshTargetKind() {
      UpdateTargetKind(true);
    }
    // Decides folder / file / missing from the target path (not the .lnk). Network, removable and optical paths are
    // checked in the background so a slow drive cannot freeze the UI: a new target shows as a file until known, a refresh
    // keeps the current kind until known, and a refresh does not queue another check while one is still running.
    // A result for an older target is dropped.
    private void UpdateTargetKind(bool refresh) {
      string path = this.target;
      if (this.isGroup || this.isProcessCommand || string.IsNullOrWhiteSpace(path) || this.IsWebAddress) {
        ++this.targetCheckVersion;
        SetPathKind(ShortcutTargetKind.File);
        return;
      }
      path = Environment.ExpandEnvironmentVariables(path.Trim());
      if (IsSlowPath(path)) {
        if (refresh && this.pendingSlowCheckVersion == this.targetCheckVersion) {
          return;
        }
        int version = ++this.targetCheckVersion;
        this.pendingSlowCheckVersion = version;
        if (!refresh) {
          SetPathKind(ShortcutTargetKind.File);
        }
        BackgroundRunner(() => {
          ShortcutTargetKind result = DetectPathKind(path);
          if (version == this.targetCheckVersion) {
            this.pendingSlowCheckVersion = 0;
            SetPathKind(result);
          }
        });
      } else {
        ++this.targetCheckVersion;
        SetPathKind(DetectPathKind(path));
      }
    }
    // A path ending in a separator can only be a folder. A failing check counts as a file (no warning icon).
    private static ShortcutTargetKind DetectPathKind(string path) {
      try {
        if (DirectoryExists(path)) { return ShortcutTargetKind.Folder; }
        bool folderOnly = path.EndsWith("\\") || path.EndsWith("/");
        if (!folderOnly && FileExists(path)) { return ShortcutTargetKind.File; }
        return ShortcutTargetKind.Missing;
      } catch {
        return ShortcutTargetKind.File;
      }
    }
    private void SetPathKind(ShortcutTargetKind kind) {
      if (this.pathKind == kind) {
        return;
      }
      this.pathKind = kind;
      OnPropertyChanged(nameof(TargetKind));
    }
    /// <summary>Extension of the shortcut file (.lnk or .url).</summary>
    public string Ext { get { return this.ext; } set { this.ext = value; } }
    /// <summary>Unique name of the shortcut file in the shortcuts folder.</summary>
    public string Guid { get { return this.guid; } set { this.guid = value; } }
    /// <summary>True for a group.</summary>
    public bool IsGroup {
      get { return this.isGroup; }
      set {
        this.isGroup = value;
        // Only a group can get new items or a paste.
        this.NewGroupShortcutCommand.NotifyCanExecuteChanged();
        this.NewProcessCommandShortcutCommand.NotifyCanExecuteChanged();
        this.PasteShortcutCommand.NotifyCanExecuteChanged();
      }
    }
    partial void OnIsGroupExpandedChanged(bool value) {
      DataStore.Shortcuts.UpdateIsGroupExpanded(this.ID, value);
    }
    /// <summary>The field whose edit box is open (None = not editing). Only one field at a time.</summary>
    public EditField EditField {
      // Hand-written: the setter is private, which a generated property cannot be.
      get { return this.editField; }
      private set {
        if (SetProperty(ref this.editField, value)) {
          OnPropertyChanged(nameof(IsEditing));
        }
      }
    }
    /// <summary>True while a name/process edit box of this item is open.</summary>
    public bool IsEditing { get { return this.editField != EditField.None; } }
    /// <summary>Replaceable so tests do not start processes.</summary>
    public Action<ShortcutViewModel, bool> StartProcessAction { get; set; }
    /// <summary>
    /// Starts a process for a shortcut or run command. The app sets it at startup (the Infrastructure shell launcher);
    /// tests replace it so no real process starts. Until it is set it throws.
    /// </summary>
    public static Action<ProcessStartInfo> ProcessStarter { get; set; } = psi =>
      throw new InvalidOperationException("ShortcutViewModel.ProcessStarter is not set.");
    /// <summary>Puts text on the clipboard. The app sets it at startup; tests replace it. Until it is set it throws.</summary>
    public static Action<string> CopyText { get; set; } = text =>
      throw new InvalidOperationException("ShortcutViewModel.CopyText is not set.");
    /// <summary>
    /// Opens the Run Command window for a run command: (command, isNewCommand). Set by the app at startup; null (nothing
    /// opens) in tests unless a test sets it.
    /// </summary>
    public static Action<ShortcutViewModel, bool> OpenRunCommandEditor { get; set; }
    /// <summary>Opens the Comment window for a button or group (set by the app; tests replace it).</summary>
    public static Action<ShortcutViewModel> OpenCommentEditor { get; set; }
    /// <summary>The Windows error code when the user answers No to the administrator (UAC) prompt.</summary>
    public const int ErrorCancelled = 1223;
    /// <summary>
    /// The start information of a run command: the arguments with <c>{}</c> filled with the group value and the lines
    /// joined for the program, "runas" when elevated, and the working folder (with <c>{}</c> filled) only when one is set.
    /// </summary>
    public static ProcessStartInfo CreateRunCommandStartInfo(string fileName, string storedArguments, string workingFolder, bool runAsAdministrator, string groupValue) {
      ProcessStartInfo startInfo = new ProcessStartInfo {
        UseShellExecute = true,
        FileName = fileName,
        Arguments = RunCommandLine.JoinLines(fileName, GroupValue.ResolveArguments(storedArguments, groupValue))
      };
      if (runAsAdministrator) {
        startInfo.Verb = "runas";
      }
      if (!string.IsNullOrWhiteSpace(workingFolder)) {
        startInfo.WorkingDirectory = GroupValue.ResolveArguments(workingFolder.Trim(), groupValue);
      }
      return startInfo;
    }
    /// <summary>
    /// Starts a run command through <see cref="ProcessStarter"/>; a declined administrator prompt is ignored silently.
    /// A failure is logged with the program name only (never the arguments or working folder, which can hold passwords
    /// or server names, and which .NET copies into the exception message) and rethrown.
    /// </summary>
    public static void StartRunCommand(ProcessStartInfo startInfo) {
      try {
        ProcessStarter(startInfo);
      } catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == ErrorCancelled) {
        // The user said No to the UAC prompt: nothing to do.
        Common.AppLog.Information("Run command {Program} was not started: the administrator prompt was declined", startInfo.FileName);
      } catch (System.ComponentModel.Win32Exception ex) {
        // The Windows text for the code alone, not ex.Message (it contains the working folder).
        Common.AppLog.Warning(null, "Run command {Program} could not be started: error {ErrorCode} {Error}",
          startInfo.FileName, ex.NativeErrorCode, new System.ComponentModel.Win32Exception(ex.NativeErrorCode).Message);
        throw;
      } catch (Exception ex) {
        Common.AppLog.Warning(null, "Run command {Program} could not be started: {ErrorType}", startInfo.FileName, ex.GetType().FullName);
        throw;
      }
    }
    /// <summary>The folder the run command starts in (<c>{}</c> is filled with the group value); empty = none. Saved at once.</summary>
    public string ProcessWorkingFolder {
      get {
        return this.processWorkingFolder;
      }
      // Hand-written: null is stored as "", which a generated property cannot do.
      set { SetProperty(ref this.processWorkingFolder, value ?? ""); }
    }
    /// <summary>The value of the direct parent group (see <see cref="GroupValue"/>), or null when there is none.</summary>
    public string ParentGroupValue {
      get {
        string value = null;
        if (this.parentShortcut != null && this.parentShortcut.IsGroup) {
          GroupValue.TryParse(this.parentShortcut.Name, out value);
        }
        return value;
      }
    }
    /// <summary>
    /// The arguments a run command starts with: its stored arguments with every <c>{}</c> replaced by the value of its
    /// direct parent group (see <see cref="GroupValue"/>), and the lines joined for the program (see <see cref="RunCommandLine.JoinLines"/>).
    /// </summary>
    public string ResolvedProcessArguments {
      get {
        return RunCommandLine.JoinLines(this.ProcessFileName, GroupValue.ResolveArguments(this.ProcessArguments, this.ParentGroupValue));
      }
    }
    /// <summary>
    /// The tooltip: for a run command the command line that will run (program, quoted when it contains a space, then the
    /// resolved arguments); for anything else the target.
    /// </summary>
    public string ToolTipText {
      get {
        if (!this.isProcessCommand || this.isGroup) {
          return this.Target;
        }
        string commandLine = RunCommandLine.CommandLine(this.ProcessFileName, this.ResolvedProcessArguments);
        return commandLine.Length == 0 ? null : commandLine;
      }
    }
    // The values above depend on the parent group's name, the parent itself and the command fields.
    private void RaiseCommandLineChanged() {
      OnPropertyChanged(nameof(this.ResolvedProcessArguments));
      OnPropertyChanged(nameof(this.ToolTipText));
    }
    /// <summary>Opens the edit box of one field (a field being edited is committed first) and selects the item.</summary>
    public void BeginEdit(EditField field) {
      if (field == EditField.None) { return; }
      CommitEdit();
      this.EditText = GetField(field);
      this.IsSelected = true;
      this.EditField = field;
    }
    /// <summary>Saves the edited text into the field (and the database) and closes the edit box.</summary>
    [RelayCommand]
    public void CommitEdit() {
      EditField field = this.editField;
      if (field == EditField.None) { return; }
      this.EditField = EditField.None;
      if ((GetField(field) ?? "") != (this.editText ?? "")) {
        if (field == EditField.Name) { this.Name = this.editText; }
        else if (field == EditField.ProcessFileName) { this.ProcessFileName = this.editText; }
        else { this.ProcessArguments = this.editText; }
      }
    }
    /// <summary>Closes the edit box and discards the edited text.</summary>
    [RelayCommand]
    public void CancelEdit() { this.EditField = EditField.None; }
    private string GetField(EditField field) {
      return field == EditField.Name ? this.Name : field == EditField.ProcessFileName ? this.ProcessFileName : this.ProcessArguments;
    }
    /// <summary>Single click: runs / toggles only in single-click mode, never while editing.</summary>
    public void Click(bool singleClickToOpen) {
      if (singleClickToOpen) { Activate(); }
    }
    /// <summary>Double click: runs / toggles only in double-click mode, never while editing.</summary>
    public void DoubleClick(bool singleClickToOpen) {
      if (!singleClickToOpen) { Activate(); }
    }
    private void Activate() {
      if (IsEditing) { return; }
      if (this.IsGroup) {
        this.IsGroupExpanded = !this.IsGroupExpanded;
      } else {
        this.Run();
      }
    }
    /// <summary>Order among the siblings, counting from 0.</summary>
    public long Position { get { return this.position; } set { this.position = value; } }
    /// <summary>True when the shortcut runs a command.</summary>
    public bool IsProcessCommand { get { return this.isProcessCommand; } set { this.isProcessCommand = value; } }
    /// <summary>The shortcuts inside a group (empty for a normal shortcut).</summary>
    public ObservableCollection<ShortcutViewModel> Children { get { return shortcuts; } set { shortcuts = value; } }
    // Shown or hidden (the views turn these into Visibility).
    /// <summary>True for a group.</summary>
    public bool ShowGroup => this.isGroup;
    /// <summary>True for a normal shortcut.</summary>
    public bool ShowSetNameTheSameAsTarget => !this.isGroup;
    /// <summary>True for a normal shortcut.</summary>
    public bool ShowCopyTargetToMemory => !this.isGroup;
    /// <summary>True for a file, folder or web link shortcut (not for groups and commands).</summary>
    public bool ShowShortcut => !this.isGroup && !this.isProcessCommand;
    /// <summary>True when the shortcut is marked.</summary>
    public bool ShowMark => this.isMarked;
    /// <summary>True for a command shortcut.</summary>
    public bool ShowCommand => !this.isGroup && this.isProcessCommand;
    /// <summary>True for a command shortcut in a trial, to show the PRO badge.</summary>
    public bool ShowPRO => !this.isGroup && this.isProcessCommand && this.AppViewModel.ShowPRO;
    /// <summary>True for a group (only groups can get new items).</summary>
    public bool ShowNewShortcut => this.isGroup;
    // Commands: generated from the [RelayCommand] methods below (NewGroupShortcutCommand, NewProcessCommandShortcutCommand,
    // SetRunCommandCommand, RenameCommand, SetNameTheSameAsTargetCommand, CopyTargetToMemoryCommand, CutShortcutCommand,
    // CopyShortcutCommand, PasteShortcutCommand, MarkShortcutCommand, DeleteShortcutCommand, CommitEditCommand, CancelEditCommand).
    #endregion PROPERTIES

    /// <summary>Creates the view model of a shortcut from its stored values.</summary>
    public ShortcutViewModel(AppViewModel appViewModel, ShortcutViewModel parentShortcut, long id, long? parentWindowID, string name, string target, string ext, string guid, bool isGroup, bool isGroupExpanded, string comment, bool isMarked, string markColor, long position, bool isProcessCommand, string processFileName, string processArguments, bool processRunAsAdmin = false, string processWorkingFolder = "") {
      this.AppViewModel = appViewModel;
      this.ParentShortcut = parentShortcut;
      this.id = id;
      this.parentWindowID = parentWindowID;
      this.name = name;
      this.target = target;
      this.ext = ext;
      this.guid = guid;
      this.isGroup = isGroup;
      this.isGroupExpanded = isGroupExpanded;
      this.comment = comment;
      this.isMarked = isMarked;
      this.markColor = markColor;
      this.position = position;
      this.isProcessCommand = isProcessCommand;
      this.processFileName = processFileName;
      this.processArguments = processArguments;
      this.processRunAsAdmin = processRunAsAdmin;
      this.processWorkingFolder = processWorkingFolder ?? "";
      UpdateTargetKind(false);
      // Events.
      this.PropertyChanged += ShortcutViewModel_PropertyChanged;
    }


    #region EVENTS
    /// <summary>Updates the parent in memory only (UI notifications, no database write). Used after the database was already updated in a transaction.</summary>
    public void SetParentInMemory(ShortcutViewModel parentShortcut, long? parentWindowID) {
      this.suppressParentSave = true;
      try {
        this.ParentShortcut = parentShortcut;
        this.ParentWindowID = parentWindowID;
      } finally {
        this.suppressParentSave = false;
      }
    }
    private void ShortcutViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e) {
      if (this.suppressParentSave) {
        return;
      }
      // Update the database.
      if (e.PropertyName == nameof(this.ParentShortcut)) {
        DataStore.Shortcuts.UpdateParentShortcutID(this.ID, this.ParentShortcut == null ? null : this.ParentShortcut.ID);
      } else if (e.PropertyName == nameof(this.ParentWindowID)) {
        DataStore.Shortcuts.UpdateParentWindowID(this.ID, this.ParentWindowID);
      } else if (e.PropertyName == nameof(this.Name)) {
        DataStore.Shortcuts.UpdateName(this.ID, this.Name);
      } else if (e.PropertyName == nameof(this.ProcessFileName)) {
        DataStore.Shortcuts.UpdateProcessFileName(this.ID, this.ProcessFileName);
      } else if (e.PropertyName == nameof(this.ProcessArguments)) {
        DataStore.Shortcuts.UpdateProcessArguments(this.ID, this.ProcessArguments);
      } else if (e.PropertyName == nameof(this.ProcessRunAsAdmin)) {
        DataStore.Shortcuts.UpdateProcessRunAsAdmin(this.ID, this.ProcessRunAsAdmin);
      } else if (e.PropertyName == nameof(this.ProcessWorkingFolder)) {
        DataStore.Shortcuts.UpdateProcessWorkingFolder(this.ID, this.ProcessWorkingFolder);
      } else if (e.PropertyName == nameof(this.IsMarked)) {
        DataStore.Shortcuts.UpdateIsMarked(this.ID, this.IsMarked);
      } else if (e.PropertyName == nameof(this.MarkColor)) {
        DataStore.Shortcuts.UpdateMarkColor(this.ID, this.MarkColor);
      } else if (e.PropertyName == nameof(this.Comment)) {
        DataStore.Shortcuts.UpdateComment(this.ID, this.Comment ?? "");
      } else if (e.PropertyName == nameof(this.ToolTipText)) {
        OnPropertyChanged(nameof(this.HasToolTip));
        OnPropertyChanged(nameof(this.ShowCommentDivider));
      }
      // MessageBox.Show($"Property '{e.PropertyName}' Changed"); // Debug.
    }
    #endregion EVENTS


    #region METHODS
    // Can Methods
    private bool CanExecuteNewGroupShortcut() {
      return this.isGroup;
    }
    private bool CanExecuteNewProcessCommandShortcut() {
      return this.isGroup;
    }
    private bool CanExecutePasteShortcut() {
      if (this.AppViewModel.CanPaste) {
        if (IsGroup && this.AppViewModel.CanPasteTo(this)) {
          return true;
        }
      }
      return false;
    }
    // Methods
    /// <summary>Opens the shortcut (or runs the command) as the normal user.</summary>
    public void Run() {
      this.RunShortcut(false);
    }
    /// <summary>Adds a new group inside this group.</summary>
    [RelayCommand(CanExecute = nameof(CanExecuteNewGroupShortcut))]
    private void NewGroupShortcut() {
      // This is not a Root Command Shortcut.
      ShortcutViewModel parentShortcut = this;
      DesktopWindowViewModel shortcutParentWindow = null;
      ShortcutViewModel shortcut = ViewModelFactory.NewGroupShortcut(this.AppViewModel, parentShortcut, shortcutParentWindow);
      parentShortcut.Children.Add(shortcut);
    }
    /// <summary>Adds a new command shortcut inside this group.</summary>
    [RelayCommand(CanExecute = nameof(CanExecuteNewProcessCommandShortcut))]
    private void NewProcessCommandShortcut() {
      // This is not a Root Command Shortcut.
      ShortcutViewModel parentShortcut = this;
      DesktopWindowViewModel shortcutParentWindow = null;
      ShortcutViewModel shortcut = ViewModelFactory.NewProcessCommandShortcut(this.AppViewModel, parentShortcut, shortcutParentWindow);
      parentShortcut.Children.Add(shortcut);
      // A new command opens the Run Command window; on Cancel the new command stays.
      OpenRunCommandEditor?.Invoke(shortcut, true);
    }
    /// <summary>Opens the Comment window for this button or group.</summary>
    [RelayCommand]
    private void EditComment() {
      OpenCommentEditor?.Invoke(this);
    }
    /// <summary>Opens the Run Command window for this run command.</summary>
    [RelayCommand]
    private void SetRunCommand() {
      OpenRunCommandEditor?.Invoke(this, false);
    }
    /// <summary>Starts renaming the shortcut.</summary>
    [RelayCommand]
    private void Rename() {
      this.BeginEdit(EditField.Name);
    }
    /// <summary>Renames the shortcut to the name of its target.</summary>
    [RelayCommand]
    private void SetNameTheSameAsTarget() {
      if (!string.IsNullOrEmpty(this.Target)) {
        this.Name = this.Target;
      }
    }
    /// <summary>Copies the target text to the clipboard.</summary>
    [RelayCommand]
    private void CopyTargetToMemory() {
      if (!string.IsNullOrEmpty(this.Target)) {
        CopyText(this.Target);
      }
    }
    /// <summary>Cuts the shortcut so it can be pasted elsewhere.</summary>
    [RelayCommand]
    private void CutShortcut() {
      this.AppViewModel.CutShortcut(copiedShortcut: this, cutFromParentShortcut: this.ParentShortcut, cutFromParentWindow: this.ParentWindow);
    }
    /// <summary>Copies the shortcut so a copy can be pasted elsewhere.</summary>
    [RelayCommand]
    private void CopyShortcut() {
      this.AppViewModel.CopyShortcut(this);
    }
    /// <summary>Pastes the cut or copied shortcut into this group.</summary>
    [RelayCommand(CanExecute = nameof(CanExecutePasteShortcut))]
    private void PasteShortcut() {
      this.AppViewModel.PasteShortcut(pasteToParentShortcut: this, pasteToParentWindow: null);
    }
    /// <summary>Marks or unmarks the shortcut.</summary>
    [RelayCommand]
    private void MarkShortcut(object parameter) {
      // The colour as "#AARRGGBB" (the mark menu item's Tag); empty = no mark.
      string colorString = parameter as string;
      if (string.IsNullOrEmpty(colorString)) {
        // UnMark the Shortcut.
        this.IsMarked = false;
      } else {
        // Mark the Shortcut.
        this.IsMarked = true;
        this.MarkColor = Common.ColorConversions.NormalizeHex(colorString);
      }
    }
    /// <summary>Deletes the shortcut (and its children) after asking.</summary>
    [RelayCommand]
    private void DeleteShortcut() {
      IList<ShortcutViewModel> siblings;
      if (this.ParentShortcut == null && this.ParentWindow != null) {
        // This is a root shortcut.
        siblings = this.ParentWindow.RootShortcuts;
      } else if (this.ParentShortcut != null && this.ParentWindow == null) {
        // This is not a root shortcut.
        siblings = this.ParentShortcut.Children;
      } else {
        throw new Exception("ParentShortcut and ParentWindow cannot be null or not null at the same time.");
      }
      string message, caption;
      if (this.IsGroup) {
        int shortcutCount = 0, groupCount = 0;
        CountDescendants(this, ref shortcutCount, ref groupCount);
        message = shortcutCount + groupCount == 0
          ? $"Delete group '{this.Name}'? It is empty."
          : $"Delete group '{this.Name}' and everything in it ({shortcutCount} {(shortcutCount == 1 ? "shortcut" : "shortcuts")}, {groupCount} {(groupCount == 1 ? "group" : "groups")})?";
        caption = "Delete group confirmation";
      } else {
        message = $"Delete shortcut '{this.Name}'?";
        caption = "Delete shortcut confirmation";
      }
      if (!Dialogs.Confirm(message, caption)) {
        return;
      }
      // Delete from the database and renumber the remaining siblings in one transaction.
      using (var tx = DataStore.BeginTransaction()) {
        DataStore.Shortcuts.Delete(this.ID);
        DataStore.Shortcuts.UpdatePositions(siblings.Where(s => !ReferenceEquals(s, this)).Select(s => s.ID));
        tx.Commit();
      }
      // Committed: update the view model, then delete the physical file/s.
      siblings.Remove(this);
      for (int i = 0; i < siblings.Count; i++) {
        siblings[i].Position = i;
      }
      this.AppViewModel.ForgetDeleted(this);
      ShortcutFileService.DeleteShortcutFiles(this);
    }
    private static void CountDescendants(ShortcutViewModel parent, ref int shortcutCount, ref int groupCount) {
      foreach (ShortcutViewModel child in parent.Children) {
        if (child.IsGroup) {
          groupCount++;
        } else {
          shortcutCount++;
        }
        CountDescendants(child, ref shortcutCount, ref groupCount);
      }
    }
    /// <summary>The message shown when a run command without a program is run.</summary>
    public const string NoProgramMessage = "Set a program first. Use Set Run Command... to choose one.";
    /// <summary>The caption of <see cref="NoProgramMessage"/>.</summary>
    public const string NoProgramCaption = "Run command";
    /// <summary>Opens the shortcut or runs the command, optionally elevated; shows a message when Windows cannot open it.</summary>
    public void RunShortcut(bool runAsAdministrator) {
      if (this.IsEditing) {
        return;
      }
      // The target may have been deleted or restored since the icon was drawn.
      RefreshTargetKind();
      if (this.StartProcessAction != null) {
        this.StartProcessAction(this, runAsAdministrator);
        return;
      }
      try {
        // Started through the shell (UseShellExecute) so control panel items and any file type open like in Explorer;
        // a plain Process.Start(path) does not work on .NET.
        if (this.IsGroup) {
          // Do nothing.
        } else {
          if (this.IsProcessCommand) {
            if (string.IsNullOrWhiteSpace(this.ProcessFileName)) {
              // Nothing went wrong: the user has not chosen a program yet. A plain hint, not an error.
              Dialogs.ShowMessage(NoProgramMessage, NoProgramCaption);
              return;
            }
            // Resolved now, so a renamed group takes effect at the next run.
            StartRunCommand(CreateRunCommandStartInfo(this.ProcessFileName, this.ProcessArguments, this.ProcessWorkingFolder,
              runAsAdministrator || this.ProcessRunAsAdmin, this.ParentGroupValue));
          } else {
            string shortcutsPath = Common.Paths.GetShortcutFilePath(this.Guid, this.Ext);
            ProcessStarter(new ProcessStartInfo { UseShellExecute = true, FileName = shortcutsPath });
          }
        }


      } catch (System.ComponentModel.Win32Exception ex) {
        string exceptionMessage = $"Error code: {ex.ErrorCode} \n";
        exceptionMessage += $"{ex.Message}. \n";
        exceptionMessage += "Please make sure the shortcut target is correct!";
        Dialogs.ShowMessage(exceptionMessage, "Exception");
      } catch (Exception ex) {
        Dialogs.ShowMessage(ex.ToString(), "Exception");
      }
    }
    #endregion METHODS
  }

}