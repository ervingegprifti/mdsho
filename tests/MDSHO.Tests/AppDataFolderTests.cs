using System;
using System.Collections.Generic;
using System.IO;
using MDSHO.Common;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// "Browse app data": which folder really holds the database and the shortcuts. When MDSHO runs as a package, Windows may
  /// redirect %LOCALAPPDATA%\MDSHO to the package's LocalCache\Local\MDSHO; Explorer runs outside the package and would show
  /// the (empty) real folder, so the redirected one must be opened instead.
  /// </summary>
  public class AppDataFolderTests {

    private const string Root = @"X:\Local\MDSHO";
    private const string Db = @"X:\Local\MDSHO\mdsho.sqlite3";
    private const string Shortcuts = @"X:\Local\MDSHO\Shortcuts";
    private const string Cache = @"X:\Packages\Family\LocalCache";
    private static readonly string Redirected = Path.Combine(Cache, "Local", AppConstants.ApplicationFolderName);

    [Fact]
    public void Unpackaged_opens_the_normal_folder_and_never_asks_for_the_package_cache() {
      string folder = AppDataFolder.Locate(false, () => throw new InvalidOperationException("no package identity"), path => true, Db, Shortcuts);
      Assert.Equal(Root, folder);
    }

    [Fact]
    public void Packaged_with_redirected_folder_opens_the_redirected_folder() {
      List<string> asked = new List<string>();
      string folder = AppDataFolder.Locate(true, () => Cache, path => { asked.Add(path); return path == Redirected; }, Db, Shortcuts);
      Assert.Equal(Redirected, folder);
      Assert.Contains(Redirected, asked);
    }

    [Fact]
    public void Packaged_without_redirected_folder_opens_the_normal_folder() {
      string folder = AppDataFolder.Locate(true, () => Cache, path => false, Db, Shortcuts);
      Assert.Equal(Root, folder);
    }

    [Fact]
    public void Packaged_but_cache_unavailable_opens_the_normal_folder() {
      Assert.Equal(Root, AppDataFolder.Locate(true, () => null, path => true, Db, Shortcuts));
      Assert.Equal(Root, AppDataFolder.Locate(true, () => throw new InvalidOperationException("cache"), path => true, Db, Shortcuts));
    }

    [Fact]
    public void Shortcuts_elsewhere_opens_the_folder_that_contains_both() {
      Assert.Equal(@"X:\Data", AppDataFolder.Locate(false, () => null, path => true, @"X:\Data\Db\mdsho.sqlite3", @"X:\Data\Links"));
      Assert.Equal(@"X:\Data", AppDataFolder.CommonFolder(@"X:\Data", @"X:\Data\Shortcuts"));
      Assert.Equal(@"X:\Data", AppDataFolder.CommonFolder(@"X:\Data\Shortcuts\", @"X:\Data"));
      // Case-insensitive, and never a partial name ("Data" is not the parent of "Database").
      Assert.Equal(@"X:\", AppDataFolder.CommonFolder(@"X:\Data", @"x:\Database"));
      Assert.Equal(@"X:\Data", AppDataFolder.CommonFolder(@"X:\Data\A", @"x:\data\B"));
    }
  }
}
