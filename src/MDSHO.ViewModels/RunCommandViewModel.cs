using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.Generic;
using MDSHO.Core;
using MDSHO.Data;

namespace MDSHO.ViewModels {

  /// <summary>
  /// The Run Command window: edits the program, working folder, arguments (one command per line), "keep window open"
  /// and "run as administrator" of one run command. Nothing is written to the command until <see cref="Save"/>.
  /// </summary>
  public partial class RunCommandViewModel : ObservableObject {
    private readonly ShortcutViewModel shortcut;
    private string program;
    private string workingFolder;
    private string arguments;
    /// <summary>Keep the window open after it runs (cmd <c>/K</c>, PowerShell <c>-NoExit</c>).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Preview))]
    private bool keepWindowOpen;
    /// <summary>Run the program as administrator.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Preview))]
    private bool runAsAdministrator;

    /// <summary>Loads the command into the window. A new command starts as <c>cmd.exe</c> with "keep window open" ticked.</summary>
    /// <param name="shortcut">The run command to edit.</param>
    /// <param name="isNewCommand">True when the command was just created.</param>
    public RunCommandViewModel(ShortcutViewModel shortcut, bool isNewCommand) {
      this.shortcut = shortcut ?? throw new ArgumentNullException(nameof(shortcut));
      this.program = shortcut.ProcessFileName ?? "";
      this.arguments = RunCommandLine.SplitKeepOpen(this.program, shortcut.ProcessArguments, out this.keepWindowOpen);
      if (isNewCommand) {
        if (string.IsNullOrWhiteSpace(this.program)) {
          this.program = RunCommandLine.DefaultProgram;
        }
        this.keepWindowOpen = true;
      }
      this.workingFolder = shortcut.ProcessWorkingFolder ?? "";
      this.runAsAdministrator = shortcut.ProcessRunAsAdmin;
    }

    // Program, WorkingFolder and Arguments stay hand-written: they turn null into "" before storing, which a generated
    // property cannot do.
    /// <summary>The program to run; anything can be typed.</summary>
    public string Program {
      get { return this.program; }
      set {
        if (SetProperty(ref this.program, value ?? "")) {
          OnPropertyChanged(nameof(this.IsKeepOpenEnabled));
          OnPropertyChanged(nameof(this.Preview));
        }
      }
    }
    /// <summary>The folder the program starts in; empty = none.</summary>
    public string WorkingFolder {
      get { return this.workingFolder; }
      set { if (SetProperty(ref this.workingFolder, value ?? "")) { OnPropertyChanged(nameof(this.Preview)); } }
    }
    /// <summary>The arguments as typed, one command per line, without the keep-open switch.</summary>
    public string Arguments {
      get { return this.arguments; }
      set { if (SetProperty(ref this.arguments, value ?? "")) { OnPropertyChanged(nameof(this.Preview)); } }
    }
    /// <summary>True when the program has a keep-open switch (cmd or PowerShell); otherwise the checkbox is disabled.</summary>
    public bool IsKeepOpenEnabled {
      get { return RunCommandLine.SupportsKeepOpen(this.program); }
    }
    /// <summary>The arguments as they will be saved: the keep-open switch put back in front.</summary>
    public string StoredArguments {
      get { return RunCommandLine.ApplyKeepOpen(this.program, this.arguments, this.keepWindowOpen); }
    }
    /// <summary>The "Will run as" text: the final command line with the group value, plus the folder and administrator lines.</summary>
    public string Preview {
      get { return RunCommandLine.Describe(this.program, this.StoredArguments, this.shortcut.ParentGroupValue, this.workingFolder, this.runAsAdministrator); }
    }
    /// <summary>The programs offered in the Program list.</summary>
    public IReadOnlyList<RunCommandProgram> Programs { get { return RunCommandLine.KnownPrograms; } }
    /// <summary>The examples offered by the star button.</summary>
    public IReadOnlyList<RunCommandExample> Examples { get { return RunCommandExamples.All; } }
    /// <summary>The icon glyphs of the command's window (they follow its Solid icons setting).</summary>
    public IconGlyphs Icons {
      get { return this.shortcut.OwningWindow?.Appearance?.Icons ?? fallbackIcons; }
    }
    private static readonly IconGlyphs fallbackIcons = new IconGlyphs(true);
    /// <summary>True when the user has typed arguments or a working folder.</summary>
    public bool HasTypedSomething {
      get { return !string.IsNullOrWhiteSpace(this.arguments) || !string.IsNullOrWhiteSpace(this.workingFolder); }
    }

    /// <summary>Writes the program, arguments (switch included), working folder and administrator choice to the command, in one transaction.</summary>
    public void Save() {
      string storedArguments = this.StoredArguments;
      using (ITransactionScope tx = DataStore.BeginTransaction()) {
        if (this.shortcut.ProcessFileName != this.program) {
          this.shortcut.ProcessFileName = this.program;
        }
        if (this.shortcut.ProcessArguments != storedArguments) {
          this.shortcut.ProcessArguments = storedArguments;
        }
        this.shortcut.ProcessWorkingFolder = this.workingFolder;
        this.shortcut.ProcessRunAsAdmin = this.runAsAdministrator;
        tx.Commit();
      }
    }

    /// <summary>
    /// Starts what is on screen (not saved), with the group value, through <see cref="ShortcutViewModel.ProcessStarter"/>;
    /// a declined administrator prompt is ignored. TestRunCommand runs it.
    /// </summary>
    [RelayCommand]
    public void TestRun() {
      if (string.IsNullOrWhiteSpace(this.program)) {
        // No program chosen yet: the same plain hint as running the command, not an error.
        Dialogs.ShowMessage(ShortcutViewModel.NoProgramMessage, ShortcutViewModel.NoProgramCaption);
        return;
      }
      try {
        ShortcutViewModel.StartRunCommand(ShortcutViewModel.CreateRunCommandStartInfo(
          this.program, this.StoredArguments, this.workingFolder, this.runAsAdministrator, this.shortcut.ParentGroupValue));
      } catch (System.ComponentModel.Win32Exception ex) {
        Dialogs.ShowMessage($"Error code: {ex.ErrorCode} \n{ex.Message}. \nPlease make sure the program is correct!", "Test run");
      } catch (InvalidOperationException ex) {
        Dialogs.ShowMessage(ex.Message, "Test run");
      }
    }

    /// <summary>
    /// Fills every field from an example. When the user has typed something, asks "Replace what you've typed?" first.
    /// </summary>
    /// <returns>True when the example was used, false when the user said No.</returns>
    public bool UseExample(RunCommandExample example) {
      if (example == null) {
        return false;
      }
      if (this.HasTypedSomething && !Dialogs.Confirm("Replace what you've typed?", "Examples")) {
        return false;
      }
      this.Program = example.ProgramFileName;
      this.WorkingFolder = example.WorkingFolder;
      this.Arguments = example.Arguments;
      this.KeepWindowOpen = example.KeepWindowOpen;
      this.RunAsAdministrator = example.RunAsAdministrator;
      return true;
    }
  }
}
