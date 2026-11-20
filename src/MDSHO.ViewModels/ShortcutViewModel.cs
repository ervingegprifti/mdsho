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
    /// <summary>A note button: its text is the note.</summary>
    Note,
    /// <summary>A file or folder path that does not exist (any more).</summary>
    Missing,
    /// <summary>A snippet: pastes its text.</summary>
    Snippet
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
    [NotifyPropertyChangedFor(nameof(ResolvedProcessArguments), nameof(ToolTipText), nameof(RunCommandError), nameof(HasRunCommandError))]
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
    public bool HasComment => !this.isNote && !string.IsNullOrWhiteSpace(this.comment);
    /// <summary>True when the button can have a comment: every button and group except notes (a note is text already).</summary>
    public bool ShowComment => !this.isNote;
    /// <summary>How many comment lines the tooltip shows; more are cut and marked with "…".</summary>
    public const int CommentPreviewLines = 10;
    /// <summary>The comment for the tooltip: at most <see cref="CommentPreviewLines"/> lines, then "…"; null without a comment.</summary>
    public string CommentPreview {
      get {
        if (!this.HasComment) {
          return null;
        }
        return Preview(this.comment);
      }
    }
    /// <summary>At most <see cref="CommentPreviewLines"/> lines of a text, then "…"; null for a text of only spaces.</summary>
    private static string Preview(string text) {
      if (string.IsNullOrWhiteSpace(text)) {
        return null;
      }
      string[] lines = text.Trim().Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
      if (lines.Length <= CommentPreviewLines) {
        return string.Join(Environment.NewLine, lines);
      }
      return string.Join(Environment.NewLine, lines.Take(CommentPreviewLines)) + Environment.NewLine + "…";
    }
    /// <summary>True when the tooltip has something to show: the target or command line, or the comment.</summary>
    public bool HasToolTip => !string.IsNullOrEmpty(this.ToolTipText) || this.HasComment || this.HasRunCommandError;
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
    private bool isNote;
    private bool isSnippet;
    /// <summary>The text of a snippet (empty for other buttons). Saved at once.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ToolTipText))]
    private string snippetText;
    /// <summary>The text of a note button (empty for other buttons).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ToolTipText))]
    private string noteText;
    /// <summary>Program run by a command shortcut.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Target), nameof(ResolvedProcessArguments), nameof(ToolTipText))]
    private string processFileName;
    /// <summary>Arguments of the program of a command shortcut.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Target), nameof(ResolvedProcessArguments), nameof(ToolTipText), nameof(RunCommandError), nameof(HasRunCommandError))]
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
      if (this.isSnippet) {
        // The tooltip starts with the name.
        OnPropertyChanged(nameof(this.ToolTipText));
      }
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
        if (this.isNote) { return ShortcutTargetKind.Note; }
        if (this.isSnippet) { return ShortcutTargetKind.Snippet; }
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
      if (this.isGroup || this.isProcessCommand || this.isNote || this.isSnippet || string.IsNullOrWhiteSpace(path) || this.IsWebAddress) {
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
        this.NewSnippetShortcutCommand.NotifyCanExecuteChanged();
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
    /// <summary>Opens the Note window for a note button (set by the app; tests replace it).</summary>
    public static Action<ShortcutViewModel> OpenNoteEditor { get; set; }
    /// <summary>
    /// Called after a button opens something. The app uses it to send raised panels back.
    /// </summary>
    public static Action Opened { get; set; }
    /// <summary>
    /// Opens the Note window for a note button, or closes it when it is open: triggering the button (click or double click,
    /// or Enter) shows and hides its note. Set by the app; when null, <see cref="OpenNoteEditor"/> is used.
    /// </summary>
    public static Action<ShortcutViewModel> ToggleNoteEditor { get; set; }
    /// <summary>
    /// Called after a button, group or panel was deleted, with each deleted top item: closes the open note windows of the
    /// notes in it (set by the app), so nothing is typed into a note that is gone.
    /// </summary>
    public static Action<ShortcutViewModel> NotesDeleted { get; set; }
    /// <summary>The Windows error code when the user answers No to the administrator (UAC) prompt.</summary>
    public const int ErrorCancelled = 1223;
    /// <summary>
    /// The start information of a run command: the arguments with the group values filled in and the lines joined for the
    /// program, "runas" when elevated, and the working folder (values filled in) only when one is set.
    /// </summary>
    public static ProcessStartInfo CreateRunCommandStartInfo(string fileName, string storedArguments, string workingFolder, bool runAsAdministrator, IReadOnlyList<GroupValueItem> groupValues) {
      ProcessStartInfo startInfo = new ProcessStartInfo {
        UseShellExecute = true,
        FileName = fileName,
        Arguments = RunCommandLine.JoinLines(fileName, GroupValue.Fill(storedArguments, groupValues))
      };
      if (runAsAdministrator) {
        startInfo.Verb = "runas";
      }
      if (!string.IsNullOrWhiteSpace(workingFolder)) {
        startInfo.WorkingDirectory = GroupValue.Fill(workingFolder.Trim(), groupValues);
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
    /// <summary>The folder the run command starts in (group values are filled in); empty = none. Saved at once.</summary>
    public string ProcessWorkingFolder {
      get {
        return this.processWorkingFolder;
      }
      // Hand-written: null is stored as "", which a generated property cannot do.
      set {
        if (SetProperty(ref this.processWorkingFolder, value ?? "")) {
          OnPropertyChanged(nameof(this.RunCommandError));
          OnPropertyChanged(nameof(this.HasRunCommandError));
        }
      }
    }
    /// <summary>The values of the direct parent group (see <see cref="GroupValue"/>); empty when there are none.</summary>
    public IReadOnlyList<GroupValueItem> ParentGroupValues {
      get {
        if (this.parentShortcut != null && this.parentShortcut.IsGroup) {
          return GroupValue.Parse(this.parentShortcut.Name);
        }
        return System.Array.Empty<GroupValueItem>();
      }
    }
    /// <summary>The group-value syntax error of a run command's arguments or working folder, or null.</summary>
    public string RunCommandError {
      get {
        if (!this.isProcessCommand || this.isGroup) {
          return null;
        }
        return GroupValue.FindError(this.ProcessArguments, this.ProcessWorkingFolder, this.ParentGroupValues);
      }
    }
    /// <summary>True when the run command has a syntax error; it then does not run.</summary>
    public bool HasRunCommandError => this.RunCommandError != null;
    /// <summary>The line below the error when a run command with an error is started.</summary>
    public const string RunCommandNotRunText = "The command did not run. Right-click it and choose Edit run command... to fix it.";
    /// <summary>
    /// The arguments a run command starts with: its stored arguments with the values of its direct parent group filled in
    /// (none while it has an error), and the lines joined for the program (see <see cref="RunCommandLine.JoinLines"/>).
    /// </summary>
    public string ResolvedProcessArguments {
      get {
        IReadOnlyList<GroupValueItem> values = this.HasRunCommandError ? null : this.ParentGroupValues;
        return RunCommandLine.JoinLines(this.ProcessFileName, GroupValue.Fill(this.ProcessArguments, values));
      }
    }
    /// <summary>
    /// The tooltip: for a run command the command line that will run (program, quoted when it contains a space, then the
    /// resolved arguments); for a note the first lines of the note; for anything else the target.
    /// </summary>
    public string ToolTipText {
      get {
        if (this.isNote) {
          return Preview(this.noteText);
        }
        if (this.isSnippet && !this.isGroup) {
          return SnippetToolTip();
        }
        if (!this.isProcessCommand || this.isGroup) {
          return this.Target;
        }
        return this.CommandLineText;
      }
    }
    // The name, the snippet text (cut after 10 lines), then how to paste.
    private string SnippetToolTip() {
      string value = Preview(this.snippetText);
      string nl = Environment.NewLine;
      return (this.Name ?? "") + nl + (value == null ? "" : value + nl) + nl + this.PasteHint;
    }
    /// <summary>
    /// A run command's command line as its tooltip shows it and "Copy command to memory" copies it: the program (quoted
    /// when it contains a space), then the resolved arguments. Null when it is not a run command or the line is empty.
    /// </summary>
    public string CommandLineText {
      get {
        if (!this.isProcessCommand || this.isGroup) {
          return null;
        }
        string commandLine = RunCommandLine.CommandLine(this.ProcessFileName, this.ResolvedProcessArguments);
        return commandLine.Length == 0 ? null : commandLine;
      }
    }
    // The values above depend on the parent group's name, the parent itself and the command fields.
    private void RaiseCommandLineChanged() {
      OnPropertyChanged(nameof(this.ResolvedProcessArguments));
      OnPropertyChanged(nameof(this.ToolTipText));
      OnPropertyChanged(nameof(this.RunCommandError));
      OnPropertyChanged(nameof(this.HasRunCommandError));
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
    /// <summary>True for a note button.</summary>
    public bool IsNote { get { return this.isNote; } }
    /// <summary>The colours of the note's window (all defaults for other buttons).</summary>
    public NoteColors NoteColors { get; }
    /// <summary>Saves the colours of the note's window (a default colour is saved as empty).</summary>
    public void SaveNoteColors() {
      var stored = this.NoteColors.Stored;
      DataStore.Shortcuts.UpdateNoteColors(this.ID, stored.Border, stored.TitleBackground, stored.TitleText, stored.Background, stored.Text);
    }
    /// <summary>The shortcuts inside a group (empty for a normal shortcut).</summary>
    public ObservableCollection<ShortcutViewModel> Children { get { return shortcuts; } set { shortcuts = value; } }
    // Shown or hidden (the views turn these into Visibility).
    /// <summary>True for a group.</summary>
    public bool ShowGroup => this.isGroup;
    /// <summary>True for a file, folder or web link shortcut.</summary>
    public bool ShowSetNameTheSameAsTarget => this.ShowShortcut;
    /// <summary>True for a file, folder or web link shortcut.</summary>
    public bool ShowCopyTargetToMemory => this.ShowShortcut;
    /// <summary>True for a run command.</summary>
    public bool ShowCopyCommandToMemory => this.ShowCommand;
    /// <summary>True for a file, folder or web link shortcut (not for groups and commands).</summary>
    public bool ShowShortcut => !this.isGroup && !this.isProcessCommand && !this.isNote && !this.isSnippet;
    /// <summary>True for a note button.</summary>
    public bool ShowNote => !this.isGroup && this.isNote;
    /// <summary>True when the shortcut is marked.</summary>
    public bool ShowMark => this.isMarked;
    /// <summary>True for a command shortcut.</summary>
    public bool ShowCommand => !this.isGroup && this.isProcessCommand;
    /// <summary>True for a group (only groups can get new items).</summary>
    public bool ShowNewShortcut => this.isGroup;
    /// <summary>True for a snippet.</summary>
    public bool IsSnippet { get { return this.isSnippet; } }
    /// <summary>True for a snippet.</summary>
    public bool ShowSnippet => !this.isGroup && this.isSnippet;
    /// <summary>True for a snippet.</summary>
    public bool ShowCopySnippetToMemory => this.ShowSnippet;
    /// <summary>
    /// Pastes a snippet's text into the window the user came from (the app's <see cref="ClipboardPaster"/>).
    /// Set by the app at startup; tests replace it. Until it is set it throws.
    /// </summary>
    public static Func<string, System.Threading.Tasks.Task<PasteOutcome>> PasteValue { get; set; } = value =>
      throw new InvalidOperationException("ShortcutViewModel.PasteValue is not set.");
    /// <summary>Opens the Edit snippet window for a snippet (set by the app; tests replace it).</summary>
    public static Action<ShortcutViewModel> OpenSnippetEditor { get; set; }
    /// <summary>The error when a paste failed.</summary>
    public const string CouldNotPasteText = "MDSHO could not paste the snippet.";
    /// <summary>The line below <see cref="CouldNotPasteText"/>.</summary>
    public const string CopySnippetInsteadText = "Try again, or right-click the button, choose Copy snippet to memory, then press Ctrl+V.";
    /// <summary>The tooltip's last line: how to paste in this button's panel.</summary>
    public string PasteHint => this.SingleClickToOpen ? "Click to paste" : "Double-click to paste";
    // The Single click to open setting of the panel this button is in.
    private bool SingleClickToOpen => this.OwningWindow?.Appearance?.SingleClickToOpen ?? false;
    /// <summary>Tells the tooltips of the snippets in this button or group that the click setting changed.</summary>
    public void RaisePasteHintChanged() {
      if (this.isSnippet) {
        OnPropertyChanged(nameof(this.PasteHint));
        OnPropertyChanged(nameof(this.ToolTipText));
      }
      foreach (ShortcutViewModel child in this.shortcuts) {
        child.RaisePasteHintChanged();
      }
    }
    // Commands: generated from the [RelayCommand] methods below (NewGroupShortcutCommand, NewProcessCommandShortcutCommand,
    // EditRunCommandCommand, RenameCommand, SetNameTheSameAsTargetCommand, CopyTargetToMemoryCommand, CutShortcutCommand,
    // CopyShortcutCommand, PasteShortcutCommand, MarkShortcutCommand, DeleteShortcutCommand, CommitEditCommand, CancelEditCommand).
    #endregion PROPERTIES

    /// <summary>Creates the view model of a shortcut from its stored values.</summary>
    public ShortcutViewModel(AppViewModel appViewModel, ShortcutViewModel parentShortcut, long id, long? parentWindowID, string name, string target, string ext, string guid, bool isGroup, bool isGroupExpanded, string comment, bool isMarked, string markColor, long position, bool isProcessCommand, string processFileName, string processArguments, bool processRunAsAdmin = false, string processWorkingFolder = "", bool isNote = false, string noteText = "", NoteColors noteColors = null, bool isSnippet = false, string snippetText = "") {
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
      this.isNote = isNote;
      this.noteText = noteText ?? "";
      this.isSnippet = isSnippet;
      this.snippetText = snippetText ?? "";
      this.NoteColors = noteColors ?? new NoteColors();
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
    // True while SaveNoteText sets the text it already wrote to the database.
    private bool savingNoteText;

    /// <summary>
    /// Saves the note text: the database first, then memory. A failed write throws and leaves the old text in both, so
    /// the new text still counts as unsaved and the next save tries again.
    /// </summary>
    public void SaveNoteText(string value) {
      value ??= "";
      DataStore.Shortcuts.UpdateNoteText(this.ID, value);
      this.savingNoteText = true;
      try {
        this.NoteText = value;
      } finally {
        this.savingNoteText = false;
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
      } else if (e.PropertyName == nameof(this.SnippetText)) {
        if (!this.savingSnippetText) {
          DataStore.Shortcuts.UpdateSnippetText(this.ID, this.SnippetText ?? "");
        }
        OnPropertyChanged(nameof(this.HasToolTip));
      } else if (e.PropertyName == nameof(this.NoteText)) {
        if (!this.savingNoteText) {
          DataStore.Shortcuts.UpdateNoteText(this.ID, this.NoteText ?? "");
        }
      } else if (e.PropertyName == nameof(this.ToolTipText) || e.PropertyName == nameof(this.HasRunCommandError)) {
        OnPropertyChanged(nameof(this.HasToolTip));
        OnPropertyChanged(nameof(this.ShowCommentDivider));
      }
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
    /// <summary>Adds a new note inside this group and opens the Note window; on Cancel the new note stays.</summary>
    [RelayCommand(CanExecute = nameof(CanExecuteNewProcessCommandShortcut))]
    private void NewNoteShortcut() {
      ShortcutViewModel note = ViewModelFactory.NewNoteShortcut(this.AppViewModel, this, null);
      this.Children.Add(note);
      OpenNoteEditor?.Invoke(note);
    }
    /// <summary>Adds a new snippet inside this group and starts its rename.</summary>
    [RelayCommand(CanExecute = nameof(CanExecuteNewProcessCommandShortcut))]
    private void NewSnippetShortcut() {
      ShortcutViewModel clip = ViewModelFactory.NewSnippetShortcut(this.AppViewModel, this, null);
      this.IsGroupExpanded = true;
      this.Children.Add(clip);
      clip.BeginEdit(EditField.Name);
    }
    /// <summary>Opens the Edit snippet window for this snippet.</summary>
    [RelayCommand]
    private void EditSnippet() {
      if (this.isSnippet) {
        OpenSnippetEditor?.Invoke(this);
      }
    }
    /// <summary>Copies the value to the clipboard and leaves it there (a normal copy). Group values are not filled in.</summary>
    [RelayCommand]
    private void CopySnippetToMemory() {
      if (this.isSnippet && !string.IsNullOrEmpty(this.snippetText)) {
        CopyText(this.snippetText);
      }
    }
    // True while SaveSnippetText sets the text it already wrote to the database.
    private bool savingSnippetText;
    /// <summary>Saves the value: the database first, then memory (a failed write throws and keeps the old value).</summary>
    public void SaveSnippetText(string value) {
      value ??= "";
      DataStore.Shortcuts.UpdateSnippetText(this.ID, value);
      this.savingSnippetText = true;
      try {
        this.SnippetText = value;
      } finally {
        this.savingSnippetText = false;
      }
    }
    /// <summary>
    /// Pastes the text into the window the user came from and says why when it can't. An empty snippet opens Edit snippet
    /// instead. The value is used as it is (no group values) and is never logged.
    /// </summary>
    public async System.Threading.Tasks.Task PasteForUserAsync() {
      if (string.IsNullOrEmpty(this.snippetText)) {
        OpenSnippetEditor?.Invoke(this);
        return;
      }
      bool singleClick = this.SingleClickToOpen;
      // Panels brought to front go back first.
      Opened?.Invoke();
      PasteOutcome outcome;
      try {
        outcome = await PasteValue(this.snippetText);
      } catch (Exception ex) {
        // The type only: a message could hold the value.
        Common.AppLog.Warning(null, "A snippet could not paste: {ErrorType}", ex.GetType().FullName);
        Dialogs.ShowError(this.Name, CouldNotPasteText, CopySnippetInsteadText);
        return;
      }
      ClipboardPasteMessage message = ClipboardPasteMessage.For(outcome, this.Name, singleClick);
      if (message != null) {
        Dialogs.ShowInfo(ClipboardPasteMessage.Caption, message.Heading, message.Text);
      }
    }
    /// <summary>Opens the Note window for this note.</summary>
    [RelayCommand]
    private void EditNote() {
      if (this.isNote) {
        OpenNoteEditor?.Invoke(this);
      }
    }
    /// <summary>Opens the Comment window for this button or group (not for notes).</summary>
    [RelayCommand(CanExecute = nameof(ShowComment))]
    private void EditComment() {
      if (!this.ShowComment) {
        return;
      }
      OpenCommentEditor?.Invoke(this);
    }
    /// <summary>Opens the Run Command window for this run command.</summary>
    [RelayCommand]
    private void EditRunCommand() {
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
    /// <summary>Copies a run command's command line, as its tooltip shows it.</summary>
    [RelayCommand]
    private void CopyCommandToMemory() {
      string commandLine = this.CommandLineText;
      if (!string.IsNullOrEmpty(commandLine)) {
        CopyText(commandLine);
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
      NotesDeleted?.Invoke(this);
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
      if (this.isSnippet) {
        // A snippet pastes its text into the window the user came from.
        _ = PasteForUserAsync();
        return;
      }
      if (this.isNote) {
        // A note runs nothing: triggering it shows the note, or hides it when it is open.
        (ToggleNoteEditor ?? OpenNoteEditor)?.Invoke(this);
        Opened?.Invoke();
        return;
      }
      // The target may have been deleted or restored since the icon was drawn.
      RefreshTargetKind();
      if (this.StartProcessAction != null) {
        this.StartProcessAction(this, runAsAdministrator);
        Opened?.Invoke();
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
            string error = this.RunCommandError;
            if (error != null) {
              Dialogs.ShowError(this.Name, error, RunCommandNotRunText);
              return;
            }
            // Resolved now, so a renamed group takes effect at the next run.
            StartRunCommand(CreateRunCommandStartInfo(this.ProcessFileName, this.ProcessArguments, this.ProcessWorkingFolder,
              runAsAdministrator || this.ProcessRunAsAdmin, this.ParentGroupValues));
            Opened?.Invoke();
          } else {
            string shortcutsPath = Common.Paths.GetShortcutFilePath(this.Guid, this.Ext);
            ProcessStarter(new ProcessStartInfo { UseShellExecute = true, FileName = shortcutsPath });
            Opened?.Invoke();
          }
        }
      } catch (Exception ex) {
        // A short plain message; the details go to the log. A run command was already logged by StartRunCommand
        // (without its arguments, which can hold passwords).
        if (this.IsProcessCommand) {
          Dialogs.ShowError(this.Name, CouldNotStartText + RunFailureReason(ex), CheckProgramText);
        } else {
          Common.AppLog.Warning(ex, "Shortcut file {File} could not be opened", this.Guid + this.Ext);
          Dialogs.ShowError(this.Name, CouldNotOpenText + RunFailureReason(ex), CheckTargetText);
        }
      }
    }
    /// <summary>The start of the message when Windows can't open a shortcut's file.</summary>
    public const string CouldNotOpenText = "Windows could not open this file: ";
    /// <summary>The start of the message when Windows can't start a run command's program.</summary>
    public const string CouldNotStartText = "Windows could not start the program: ";
    /// <summary>The line below a shortcut that can't be opened.</summary>
    public const string CheckTargetText = "Check that the file or folder it points to still exists.";
    /// <summary>The line below a run command that can't start.</summary>
    public const string CheckProgramText = "Check that the program and its folder are correct.";
    /// <summary>
    /// A short reason for a failed start: the Windows text for the error code (not ex.Message, which can hold the working
    /// folder), or the message of any other exception. Never the HRESULT or a stack trace.
    /// </summary>
    public static string RunFailureReason(Exception ex) {
      if (ex is System.ComponentModel.Win32Exception win32) {
        return new System.ComponentModel.Win32Exception(win32.NativeErrorCode).Message;
      }
      return ex.Message;
    }
    #endregion METHODS
  }

}