using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MDSHO.Common;
using MDSHO.Data;
using MDSHO.ViewModels;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>A brand-new database starts with a "Getting started" group of samples; nothing else ever gets it.</summary>
  public class FirstStartSamplesTests {

    private sealed class ThrowingLnkWriter : IShellLinkWriter {
      private readonly IShellLinkWriter inner = new Infrastructure.ShellLinkWriter();
      public void CreateLnk(string lnkFilePath, string targetPath, string workingFolder) { throw new IOException("No .lnk for you."); }
      public void CreateUrl(string urlFilePath, string url) { inner.CreateUrl(urlFilePath, url); }
    }

    // A brand-new database: created by the normal upgrade step, with nothing in it yet.
    private static TempDb NewDatabase() {
      TempDb db = new TempDb(false);
      DataStore.Upgrade();
      return db;
    }

    private static string DocumentsFolder(TempDb db) {
      string folder = Path.Combine(db.Folder, "Documents");
      Directory.CreateDirectory(folder);
      return folder;
    }

    private static ShortcutViewModel SampleGroup(AppViewModel app) {
      return app.Windows.Single().RootShortcuts.Single();
    }

    [Fact]
    public void BrandNewDatabase_GetsTheGettingStartedGroupWithFourSamples() {
      using (TempDb db = NewDatabase()) {
        string documents = DocumentsFolder(db);
        AppViewModel app = ViewModelFactory.CreateAppViewModel(documents);

        ShortcutViewModel group = SampleGroup(app);
        Assert.True(group.IsGroup);
        Assert.Equal("Getting started", group.Name);
        Assert.True(group.IsGroupExpanded);
        Assert.Equal(new[] { "Welcome to MDSHO", "Documents", "Show my IP address", "MDSHO guide" }, group.Children.Select(c => c.Name));

        ShortcutViewModel note = group.Children[0];
        Assert.True(note.IsNote);
        Assert.Equal(FirstStartSamples.NoteText, note.NoteText);
        Assert.Equal("Opens this note. Notes save themselves as you type.", note.Comment);

        ShortcutViewModel folder = group.Children[1];
        Assert.False(folder.IsNote || folder.IsProcessCommand || folder.IsGroup);
        Assert.Equal(AppConstants.LnkExtension, folder.Ext);
        Assert.Equal(documents, folder.Target);
        Assert.True(File.Exists(Paths.GetShortcutFilePath(folder.Guid, folder.Ext)));
        Assert.Equal("A shortcut to your Documents folder. Drag your own files and folders here to add more.", folder.Comment);

        ShortcutViewModel command = group.Children[2];
        Assert.True(command.IsProcessCommand);
        Assert.Equal("cmd.exe", command.ProcessFileName);
        Assert.Equal("/K ipconfig", command.ProcessArguments);
        Assert.False(command.ProcessRunAsAdmin);
        Assert.Equal("A run command. Right-click it and choose Edit run command... to see how it works.", command.Comment);

        ShortcutViewModel guide = group.Children[3];
        Assert.Equal(AppConstants.UrlExtension, guide.Ext);
        Assert.Equal("https://ervingegprifti.github.io/mdsho/", guide.Target);
        Assert.Equal(AppConstants.DocsUrl, guide.Target);
        Assert.True(File.Exists(Paths.GetShortcutFilePath(guide.Guid, guide.Ext)));
        Assert.Equal("A web link to the MDSHO guide. Drag links from your browser to add more.", guide.Comment);

        Assert.Same(note, app.FirstStartNote);
      }
    }

    [Fact]
    public void WelcomeNoteText_IsTheApprovedText() {
      string expected = string.Join("\r\n",
        "Welcome to MDSHO.",
        "",
        "Drag files, folders or web links onto a panel to make buttons.",
        "Right-click anything for more: new groups, run commands, notes, colours.",
        "Press Ctrl+Shift+Alt+M to bring your panels to the front.",
        "",
        "These samples are yours to try. Delete the group when you are done.",
        "",
        "MDSHO is developed in spare time. If it saves you time, you can support its development: right-click a panel's title and choose Support MDSHO.");
      Assert.Equal(expected, FirstStartSamples.NoteText);
    }

    /// <summary>The support line closes the welcome note, after a blank line, and names the panel menu item.</summary>
    [Fact]
    public void WelcomeNote_ends_with_the_support_line() {
      string[] lines = FirstStartSamples.NoteText.Split("\r\n");
      Assert.Equal("", lines[lines.Length - 2]);
      Assert.Equal("MDSHO is developed in spare time. If it saves you time, you can support its development: right-click a panel's title and choose Support MDSHO.", lines[lines.Length - 1]);
      Assert.EndsWith("choose Support MDSHO.", lines[lines.Length - 1]);
    }

    [Fact]
    public void SamplesAreSavedInTheDatabase() {
      using (TempDb db = NewDatabase()) {
        ViewModelFactory.CreateAppViewModel(DocumentsFolder(db));
        Assert.Equal(1, db.Scalar("SELECT COUNT(*) FROM MDSHOWindows"));
        Assert.Equal(5, db.Scalar("SELECT COUNT(*) FROM Shortcuts"));
        Assert.Equal(4, db.Scalar("SELECT COUNT(*) FROM Shortcuts WHERE ParentShortcutID = (SELECT ID FROM Shortcuts WHERE IsGroup = 1)"));
      }
    }

    [Fact]
    public void ExistingDatabase_GetsNoSamples() {
      using (TempDb db = new TempDb()) {
        AppViewModel app = ViewModelFactory.CreateAppViewModel(DocumentsFolder(db));
        Assert.Empty(app.Windows.Single().RootShortcuts);
        Assert.Null(app.FirstStartNote);
      }
    }

    [Fact]
    public void SecondStart_DoesNotAddTheSamplesAgain() {
      using (TempDb db = NewDatabase()) {
        string documents = DocumentsFolder(db);
        ViewModelFactory.CreateAppViewModel(documents);
        AppViewModel second = ViewModelFactory.CreateAppViewModel(documents);
        Assert.Single(second.Windows.Single().RootShortcuts);
        Assert.Null(second.FirstStartNote);
      }
    }

    [Fact]
    public void DeletedSamples_DoNotComeBack() {
      using (TempDb db = NewDatabase()) {
        string documents = DocumentsFolder(db);
        AppViewModel first = ViewModelFactory.CreateAppViewModel(documents);
        DataStore.Shortcuts.Delete(SampleGroup(first).ID);
        AppViewModel second = ViewModelFactory.CreateAppViewModel(documents);
        Assert.Empty(second.Windows.Single().RootShortcuts);
      }
    }

    [Fact]
    public void DatabaseWithoutPanelsButUsedBefore_GetsNoSamples() {
      // For example after an import that replaced the data with a file without panels: the screen setups stay.
      using (TempDb db = NewDatabase()) {
        DataStore.ScreenSetups.Ensure("1920x1080*@c0r0", "1 screen: 1920×1080");
        AppViewModel app = ViewModelFactory.CreateAppViewModel(DocumentsFolder(db));
        Assert.Empty(app.Windows.Single().RootShortcuts);
        Assert.Null(app.FirstStartNote);
      }
    }

    [Fact]
    public void LoadAll_SaysBrandNewOnlyTheFirstTime() {
      using (TempDb db = NewDatabase()) {
        DataStore.Windows.LoadAll(out bool first);
        DataStore.Windows.LoadAll(out bool second);
        Assert.True(first);
        Assert.False(second);
      }
    }

    [Fact]
    public void FailingDocumentsShortcut_IsLoggedAndSkipped() {
      IShellLinkWriter oldWriter = ShortcutFileService.LinkWriter;
      ShortcutFileService.LinkWriter = new ThrowingLnkWriter();
      try {
        using (LogCapture log = new LogCapture())
        using (TempDb db = NewDatabase()) {
          AppViewModel app = ViewModelFactory.CreateAppViewModel(DocumentsFolder(db));
          Assert.Equal(new[] { "Welcome to MDSHO", "Show my IP address", "MDSHO guide" }, SampleGroup(app).Children.Select(c => c.Name));
          Assert.Contains(log.Messages, m => m.Contains("Documents") && m.Contains("not added"));
          Assert.Empty(Directory.GetFiles(Paths.GetShortcutsPath(), "*.lnk"));
        }
      } finally {
        ShortcutFileService.LinkWriter = oldWriter;
      }
    }

    [Fact]
    public void MissingDocumentsFolder_IsLoggedAndSkipped() {
      using (LogCapture log = new LogCapture())
      using (TempDb db = NewDatabase()) {
        AppViewModel app = ViewModelFactory.CreateAppViewModel(Path.Combine(db.Folder, "NoSuchFolder"));
        Assert.DoesNotContain(SampleGroup(app).Children, c => c.Name == "Documents");
        Assert.Equal(3, SampleGroup(app).Children.Count);
        Assert.Contains(log.Messages, m => m.Contains("Documents") && m.Contains("not added"));
      }
    }

    [Fact]
    public void OpenFirstStartNote_OpensTheWelcomeNoteOnce() {
      Action<ShortcutViewModel> oldEditor = ShortcutViewModel.OpenNoteEditor;
      List<ShortcutViewModel> opened = new List<ShortcutViewModel>();
      ShortcutViewModel.OpenNoteEditor = s => opened.Add(s);
      try {
        using (TempDb db = NewDatabase()) {
          AppViewModel app = ViewModelFactory.CreateAppViewModel(DocumentsFolder(db));
          app.OpenFirstStartNote();
          app.OpenFirstStartNote();
          Assert.Equal("Welcome to MDSHO", Assert.Single(opened).Name);
        }
      } finally {
        ShortcutViewModel.OpenNoteEditor = oldEditor;
      }
    }

    [Fact]
    public void OpenFirstStartNote_DoesNothingOnAnExistingDatabase() {
      Action<ShortcutViewModel> oldEditor = ShortcutViewModel.OpenNoteEditor;
      List<ShortcutViewModel> opened = new List<ShortcutViewModel>();
      ShortcutViewModel.OpenNoteEditor = s => opened.Add(s);
      try {
        using (TempDb db = new TempDb()) {
          ViewModelFactory.CreateAppViewModel(DocumentsFolder(db)).OpenFirstStartNote();
          Assert.Empty(opened);
        }
      } finally {
        ShortcutViewModel.OpenNoteEditor = oldEditor;
      }
    }

    [Fact]
    public void App_OpensTheWelcomeNoteAtStartup() {
      string source = File.ReadAllText(Path.Combine(ThirdPartyNoticesTests.RepoRoot(), "src", "MDSHO.App", "App.xaml.cs"));
      int reopen = source.IndexOf("TextEditWindow.ReopenOpenNotes(", StringComparison.Ordinal);
      int welcome = source.IndexOf("this.DataContext.OpenFirstStartNote();", StringComparison.Ordinal);
      Assert.True(reopen > 0 && welcome > reopen, "App.OnStartup should open the welcome note after reopening the open notes.");
    }
  }
}
