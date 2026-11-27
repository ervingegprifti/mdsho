using System.Collections.Generic;
using MDSHO.Common;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>1.30.0: paths that work on another PC (%USERPROFILE%, Desktop, Program Files, ...) and back.</summary>
  public class PortablePathsTests {

    // Home PC: Desktop and Documents moved into OneDrive.
    private static readonly KnownFolders Home = new KnownFolders(new Dictionary<string, string> {
      [KnownFolders.UserProfile] = @"C:\Users\Ann",
      [KnownFolders.Desktop] = @"C:\Users\Ann\OneDrive\Desktop",
      [KnownFolders.Documents] = @"C:\Users\Ann\OneDrive\Documents",
      [KnownFolders.Downloads] = @"C:\Users\Ann\Downloads",
      [KnownFolders.Pictures] = @"C:\Users\Ann\Pictures",
      [KnownFolders.Music] = @"C:\Users\Ann\Music",
      [KnownFolders.Videos] = @"C:\Users\Ann\Videos",
      [KnownFolders.OneDrive] = @"C:\Users\Ann\OneDrive",
      [KnownFolders.ProgramFiles] = @"C:\Program Files",
      [KnownFolders.ProgramFilesX86] = @"C:\Program Files (x86)",
      [KnownFolders.Windows] = @"C:\Windows",
    });

    // Work PC: another user name and drive, no OneDrive.
    private static readonly KnownFolders Work = new KnownFolders(new Dictionary<string, string> {
      [KnownFolders.UserProfile] = @"D:\Users\ann.smith",
      [KnownFolders.Desktop] = @"D:\Users\ann.smith\Desktop",
      [KnownFolders.Documents] = @"D:\Users\ann.smith\Documents",
      [KnownFolders.Downloads] = @"D:\Users\ann.smith\Downloads",
      [KnownFolders.ProgramFiles] = @"D:\Program Files",
      [KnownFolders.ProgramFilesX86] = @"D:\Program Files (x86)",
      [KnownFolders.Windows] = @"D:\Windows",
    });

    [Theory]
    [InlineData(@"C:\Users\Ann\notes.txt", @"%USERPROFILE%\notes.txt")]
    [InlineData(@"C:\Users\Ann\OneDrive\Desktop\a.lnk", @"%DESKTOP%\a.lnk")]
    [InlineData(@"C:\Users\Ann\OneDrive\Documents\Work\plan.docx", @"%DOCUMENTS%\Work\plan.docx")]
    [InlineData(@"C:\Users\Ann\OneDrive\Shared\x.xlsx", @"%ONEDRIVE%\Shared\x.xlsx")]
    [InlineData(@"C:\Users\Ann\Downloads\setup.exe", @"%DOWNLOADS%\setup.exe")]
    [InlineData(@"C:\Users\Ann\Pictures\cat.png", @"%PICTURES%\cat.png")]
    [InlineData(@"C:\Users\Ann\Music\song.mp3", @"%MUSIC%\song.mp3")]
    [InlineData(@"C:\Users\Ann\Videos\clip.mp4", @"%VIDEOS%\clip.mp4")]
    [InlineData(@"C:\Program Files\App\app.exe", @"%PROGRAMFILES%\App\app.exe")]
    [InlineData(@"C:\Program Files (x86)\Old\old.exe", @"%PROGRAMFILES(X86)%\Old\old.exe")]
    [InlineData(@"C:\Windows\System32\notepad.exe", @"%WINDIR%\System32\notepad.exe")]
    [InlineData(@"c:\users\ann\desktop2\x", @"%USERPROFILE%\desktop2\x")]
    [InlineData(@"C:\Users\Ann", @"%USERPROFILE%")]
    [InlineData(@"C:\Users\Ann\", @"%USERPROFILE%\")]
    public void An_absolute_path_becomes_portable(string path, string portable) {
      Assert.Equal(portable, PortablePaths.ToPortable(path, Home));
    }

    [Theory]
    [InlineData(@"C:\Users\Annabel\x.txt")]      // not on a folder boundary
    [InlineData(@"E:\Data\x.txt")]
    [InlineData(@"https://example.com/C:\Users\Ann")]
    [InlineData(@"\\server\share\x")]
    [InlineData(@"notepad.exe")]
    [InlineData(@"%USERPROFILE%\already.txt")]
    [InlineData("")]
    [InlineData(null)]
    public void Other_paths_stay_as_they_are(string path) {
      Assert.Equal(path, PortablePaths.ToPortable(path, Home));
    }

    [Theory]
    [InlineData(@"%USERPROFILE%\notes.txt", @"D:\Users\ann.smith\notes.txt")]
    [InlineData(@"%DESKTOP%\a.lnk", @"D:\Users\ann.smith\Desktop\a.lnk")]
    [InlineData(@"%documents%\Work\plan.docx", @"D:\Users\ann.smith\Documents\Work\plan.docx")]
    [InlineData(@"%PROGRAMFILES(X86)%\Old\old.exe", @"D:\Program Files (x86)\Old\old.exe")]
    [InlineData(@"%WINDIR%\System32\notepad.exe", @"D:\Windows\System32\notepad.exe")]
    [InlineData(@"%DESKTOP%", @"D:\Users\ann.smith\Desktop")]
    public void A_portable_path_becomes_absolute_on_another_PC(string portable, string path) {
      Assert.Equal(path, PortablePaths.FromPortable(portable, Work));
    }

    [Theory]
    [InlineData(@"%ONEDRIVE%\Shared\x.xlsx")]   // no OneDrive on that PC
    [InlineData(@"%TEMP%\x")]                  // not one of ours
    [InlineData(@"C:\plain\path")]
    [InlineData(@"%DESKTOP")]
    [InlineData("")]
    [InlineData(null)]
    public void A_portable_path_without_a_folder_here_stays_as_it_is(string portable) {
      Assert.Equal(portable, PortablePaths.FromPortable(portable, Work));
    }

    [Fact]
    public void A_path_goes_there_and_back() {
      string path = @"C:\Users\Ann\OneDrive\Desktop\Project\run.bat";
      string portable = PortablePaths.ToPortable(path, Home);
      Assert.Equal(path, PortablePaths.FromPortable(portable, Home));
      Assert.Equal(@"D:\Users\ann.smith\Desktop\Project\run.bat", PortablePaths.FromPortable(portable, Work));
    }

    [Fact]
    public void This_PC_has_its_folders() {
      KnownFolders here = KnownFolders.Current();
      Assert.False(string.IsNullOrEmpty(here.PathOf(KnownFolders.UserProfile)));
      Assert.False(string.IsNullOrEmpty(here.PathOf(KnownFolders.Windows)));
      Assert.Null(here.PathOf("%NOPE%"));
    }
  }
}
