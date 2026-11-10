using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Windows.Media;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// Checks the MDSHOFont icon font embedded in the app: it must contain every glyph the app uses,
  /// and it must be the font from assets/Font/MDSHOFont-v1.14 (which added run and wizard).
  /// </summary>
  public class IconFontTests {

    private const string FontResourceKey = "assets/fonts/mdshofont.ttf";

    /// <summary>Copies the embedded font out of MDSHO.g.resources into a temp file and opens it.</summary>
    private static GlyphTypeface LoadEmbeddedFont() {
      Assembly app = typeof(MDSHO.App).Assembly;
      using Stream resources = app.GetManifestResourceStream("MDSHO.g.resources");
      Assert.NotNull(resources);
      using ResourceReader reader = new ResourceReader(resources);
      DictionaryEntry entry = reader.Cast<DictionaryEntry>().FirstOrDefault(e => (string)e.Key == FontResourceKey);
      Assert.True(entry.Value is Stream, "Embedded resource " + FontResourceKey + " not found.");
      string path = Path.Combine(Path.GetTempPath(), "MDSHOFont-test-" + Guid.NewGuid().ToString("N") + ".ttf");
      using (FileStream file = File.Create(path)) {
        ((Stream)entry.Value).CopyTo(file);
      }
      return new GlyphTypeface(new Uri(path));
    }

    /// <summary>
    /// One test for all codes on purpose: WPF's font cache is not safe to use from parallel test cases.
    /// The range runs from mdsho_s (U+E900) to wizard_o (U+E94B). In v1.14 U+E948/U+E949 are run (they were unknown in
    /// v1.13) and U+E94A/U+E94B are the new wizard pair; warning stays at U+E92C/U+E92D.
    /// </summary>
    [Fact]
    public void Embedded_font_contains_every_glyph_up_to_v1_14() {
      GlyphTypeface font = LoadEmbeddedFont();
      int[] missing = Enumerable.Range(0xE900, 0xE94B - 0xE900 + 1)
        .Where(code => !font.CharacterToGlyphMap.ContainsKey(code))
        .ToArray();
      Assert.True(missing.Length == 0, "MDSHOFont has no glyph for " + string.Join(", ", missing.Select(c => $"U+{c:X4}")));
    }

    /// <summary>The embedded font is byte for byte the IcoMoon v1.14 build kept in assets.</summary>
    [Fact]
    public void Embedded_font_is_the_v1_14_build_from_assets() {
      string source = Path.Combine(ThirdPartyNoticesTests.RepoRoot(), "assets", "Font", "MDSHOFont-v1.14", "fonts", "MDSHOFont.ttf");
      Assert.True(File.Exists(source), source + " not found.");
      Assert.Equal(File.ReadAllBytes(source), EmbeddedFontBytes());
    }

    /// <summary>The old v1.13 IcoMoon project is deleted, so the README and selection.json point at one version only.</summary>
    [Fact]
    public void Old_v1_13_font_folder_is_gone() {
      Assert.False(Directory.Exists(Path.Combine(ThirdPartyNoticesTests.RepoRoot(), "assets", "Font", "MDSHOFont-v1.13")));
    }

    private static byte[] EmbeddedFontBytes() {
      Assembly app = typeof(MDSHO.App).Assembly;
      using Stream resources = app.GetManifestResourceStream("MDSHO.g.resources");
      using ResourceReader reader = new ResourceReader(resources);
      DictionaryEntry entry = reader.Cast<DictionaryEntry>().First(e => (string)e.Key == FontResourceKey);
      using MemoryStream copy = new MemoryStream();
      ((Stream)entry.Value).CopyTo(copy);
      return copy.ToArray();
    }
  }
}
