using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Media;
using System.Xml.Linq;
using MDSHO.Core;
using MDSHO.Helpers;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// The five note fonts that come with MDSHO are embedded (Regular only), load as their own family, show in About with
  /// their versions, and ship with their licence texts.
  /// </summary>
  public class NoteFontFilesTests {

    private static readonly Dictionary<string, string> Versions = new Dictionary<string, string> {
      ["JetBrains Mono"] = "2.304",
      ["Source Code Pro"] = "2.042",
      ["IBM Plex Mono"] = "2.5.0",
      ["Latin Modern Roman"] = "2.004",
      ["Latin Modern Mono"] = "2.004",
    };

    private static readonly string[] Licences = {
      "OFL-JetBrainsMono.txt", "OFL-SourceCodePro.txt", "OFL-IBMPlexMono.txt", "GUST-FONT-LICENSE.txt", "LPPL-1.3c.txt" };

    [Fact]
    public void Each_shipped_font_file_is_embedded() {
      foreach (string name in NoteFonts.ComesWithMdsho) {
        Assert.True(NoteFontFamilies.EmbeddedFontFileExists(NoteFontFamilies.FileOf(name)), name + " is not embedded");
      }
    }

    /// <summary>One test for all fonts on purpose: WPF's font cache is not safe to use from parallel test cases.</summary>
    [Fact]
    public void Each_shipped_font_loads_its_own_file_and_does_not_fall_back() {
      WpfAppHost.Run(() => {
        foreach (string name in NoteFonts.ComesWithMdsho) {
          FontFamily family = NoteFontFamilies.Resolve(name);
          Assert.NotEqual(NoteFonts.Default, family.Source);
          List<GlyphTypeface> faces = new List<GlyphTypeface>();
          foreach (Typeface typeface in family.GetTypefaces()) {
            if (typeface.TryGetGlyphTypeface(out GlyphTypeface glyphs)) {
              faces.Add(glyphs);
            }
          }
          Assert.True(faces.Count > 0, name + ": no typeface loads from " + family.Source);
          Assert.All(faces, g => Assert.EndsWith("/" + NoteFontFamilies.FileOf(name).ToLowerInvariant(), g.FontUri.ToString().ToLowerInvariant()));
          Assert.Contains(faces, g => g.CharacterToGlyphMap.ContainsKey('A') && g.CharacterToGlyphMap.ContainsKey('a'));
        }
      });
    }

    [Fact]
    public void The_real_family_names_inside_the_files_are_used() {
      Assert.Equal("JetBrains Mono", NoteFontFamilies.FamilyInFileOf("JetBrains Mono"));
      Assert.Equal("Source Code Pro", NoteFontFamilies.FamilyInFileOf("Source Code Pro"));
      Assert.Equal("IBM Plex Mono", NoteFontFamilies.FamilyInFileOf("IBM Plex Mono"));
      // WPF names Latin Modern by typographic family and design size, not "LM Roman 10" / "LM Mono 10" (name ID 1).
      Assert.Equal("Latin Modern Roman 10", NoteFontFamilies.FamilyInFileOf("Latin Modern Roman"));
      Assert.Equal("Latin Modern Mono 10", NoteFontFamilies.FamilyInFileOf("Latin Modern Mono"));
    }

    [Fact]
    public void About_shows_all_five_fonts_with_their_versions() {
      List<ThirdPartyNotice> shown = ThirdPartyNotices.Shown().ToList();
      foreach (string name in NoteFonts.ComesWithMdsho) {
        ThirdPartyNotice notice = Assert.Single(shown, n => n.Name == name);
        Assert.Equal(Versions[name], notice.Version);
      }
      Assert.DoesNotContain(ThirdPartyNotices.Fonts, n => n.Version == "TBD");
    }

    [Fact]
    public void Each_licence_text_is_in_the_assets() {
      string folder = Path.Combine(ThirdPartyNoticesTests.RepoRoot(), "assets", "Font", "licenses");
      foreach (string file in Licences) {
        Assert.True(File.Exists(Path.Combine(folder, file)), file + " is missing");
      }
      foreach (string ofl in Licences.Where(f => f.StartsWith("OFL-", StringComparison.Ordinal))) {
        Assert.Contains("SIL Open Font License, Version 1.1", File.ReadAllText(Path.Combine(folder, ofl)));
      }
      Assert.Contains("GUST Font License", File.ReadAllText(Path.Combine(folder, "GUST-FONT-LICENSE.txt")));
      Assert.Contains("LPPL Version 1.3c", File.ReadAllText(Path.Combine(folder, "LPPL-1.3c.txt")));
    }

    [Fact]
    public void The_licence_texts_ship_with_the_app() {
      XDocument project = XDocument.Load(Path.Combine(ThirdPartyNoticesTests.RepoRoot(), "src", "MDSHO.App", "MDSHO.App.csproj"));
      XElement content = project.Descendants("Content").Single(e => ((string)e.Attribute("Include") ?? "").Contains(@"assets\Font\licenses"));
      Assert.Equal(@"Assets\Fonts\licenses\%(Filename)%(Extension)", (string)content.Attribute("Link"));
      Assert.Equal("PreserveNewest", (string)content.Element("CopyToOutputDirectory"));
      foreach (string file in Licences) {
        Assert.True(File.Exists(Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts", "licenses", file)), file + " is not in the build output");
      }
    }

    [Fact]
    public void Only_the_regular_files_and_the_icon_font_are_in_the_fonts_folder() {
      string folder = Path.Combine(ThirdPartyNoticesTests.RepoRoot(), "src", "MDSHO.App", "Assets", "Fonts");
      string[] files = Directory.GetFiles(folder).Select(Path.GetFileName).OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToArray();
      string[] expected = NoteFonts.ComesWithMdsho.Select(NoteFontFamilies.FileOf).Append("MDSHOFont.ttf").OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToArray();
      Assert.Equal(expected, files);
    }
  }
}
