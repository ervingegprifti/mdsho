using System;
using System.IO;
using System.Linq;
using System.Windows.Media;
using MDSHO.Common;
using MDSHO.Converters;
using MDSHO.ViewModels;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>ToColor validation, failed copy leaves no orphan files.</summary>
  public class ToColorTests {
    [Fact]
    public void Valid_AARRGGBB_parses() {
      Assert.Equal(Color.FromArgb(0x80, 0x12, 0xAB, 0xCD), "#8012ABCD".ToColor());
      Assert.Equal(Color.FromArgb(0xFF, 0, 0, 0), "#ff000000".ToColor());
    }
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("#")]
    [InlineData("#FF")]
    [InlineData("#AABBCC")]        // #RRGGBB is not a supported form
    [InlineData("#AABBCCDDEE")]    // too long
    [InlineData("FF112233")]       // no '#', wrong length
    [InlineData("xAABBCCDD")]      // right length, no '#'
    [InlineData("#GGBBCCDD")]      // bad hex
    [InlineData("#AABBCCZZ")]
    [InlineData("Red")]            // names are not supported
    public void Invalid_input_throws_ArgumentException(string input) {
      Assert.Throws<ArgumentException>(() => input.ToColor());
    }
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("garbage")]
    [InlineData("xAABBCCDD")]
    [InlineData("#GGBBCCDD")]
    public void ToSolidColorBrush_falls_back_to_white_on_invalid_stored_color(string input) {
      SolidColorBrush brush = input.ToSolidColorBrush();
      Assert.Equal(Colors.White, brush.Color);
    }
    [Fact]
    public void ToSolidColorBrush_valid_color_is_kept() {
      Assert.Equal(Color.FromArgb(0xFF, 1, 2, 3), "#FF010203".ToSolidColorBrush().Color);
    }
  }

  public class CopyOrphanFileTests : IDisposable {
    private readonly TempDb db = new TempDb();
    private readonly AppViewModel app = new AppViewModel();
    private readonly DesktopWindowViewModel win;
    public CopyOrphanFileTests() { win = app.AddWindow("TEST", 0, 0); }
    public void Dispose() { db.Dispose(); }

    private string Dir { get { return Paths.GetShortcutsPath(); } }
    private string[] Files() { return Directory.Exists(Dir) ? Directory.GetFiles(Dir).OrderBy(f => f).ToArray() : new string[0]; }
    private ShortcutViewModel Group(ShortcutViewModel parent, DesktopWindowViewModel w) {
      ShortcutViewModel g = ViewModelFactory.NewGroupShortcut(app, parent, w);
      if (parent != null) parent.Children.Add(g); else w.RootShortcuts.Add(g);
      return g;
    }
    private ShortcutViewModel Url(ShortcutViewModel parent, string guid) {
      Directory.CreateDirectory(Dir);
      ShortcutViewModel s = ViewModelFactory.NewShortcut(app, parent, null, "n" + guid, "http://x/" + guid, ".url", guid);
      parent.Children.Add(s);
      return s;
    }
    private (ShortcutViewModel src, ShortcutViewModel dest) Arrange() {
      ShortcutViewModel src = Group(null, win), dest = Group(null, win);
      Url(src, "g1"); Url(src, "g2"); Url(src, "g3");
      Assert.Equal(3, Files().Length);
      return (src, dest);
    }

    [Fact]
    public void Copy_happy_path_creates_the_files() {
      var (src, dest) = Arrange();
      long before = db.Scalar("SELECT COUNT(*) FROM Shortcuts");
      app.CopyShortcut(src);
      app.PasteShortcut(dest, null);
      Assert.Equal(before + 4, db.Scalar("SELECT COUNT(*) FROM Shortcuts"));
      Assert.Equal(6, Files().Length);
    }

    [Fact]
    public void Failed_copy_leaves_no_new_files_and_db_unchanged() {
      var (src, dest) = Arrange();
      string[] filesBefore = Files();
      long before = db.Scalar("SELECT COUNT(*) FROM Shortcuts");
      // root of the copy + 2 children insert (and their files are copied), the 4th insert fails.
      db.Exec("CREATE TRIGGER fail_ins BEFORE INSERT ON Shortcuts WHEN (SELECT COUNT(*) FROM Shortcuts) >= " + (before + 3) + " BEGIN SELECT RAISE(ABORT,'boom'); END;");
      app.CopyShortcut(src);
      Assert.ThrowsAny<Exception>(() => app.PasteShortcut(dest, null));
      Assert.Equal(before, db.Scalar("SELECT COUNT(*) FROM Shortcuts"));
      Assert.Equal(filesBefore, Files());
      Assert.Empty(dest.Children);
    }
  }
}
