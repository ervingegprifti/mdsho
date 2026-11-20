using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MDSHO.Core;
using MDSHO.Helpers;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// The fonts that come with MDSHO have notices ready (ThirdPartyNotices.Fonts and a table in THIRD-PARTY-NOTICES.md).
  /// The About window lists a font only once its file is embedded.
  /// </summary>
  public class NoteFontNoticesTests {

    [Fact]
    public void Each_font_that_comes_with_mdsho_has_a_notice_with_its_licence() {
      Assert.Equal(NoteFonts.ComesWithMdsho, ThirdPartyNotices.Fonts.Select(n => n.Name));
      Dictionary<string, string> licences = ThirdPartyNotices.Fonts.ToDictionary(n => n.Name, n => n.License);
      Assert.Equal("OFL-1.1", licences["JetBrains Mono"]);
      Assert.Equal("OFL-1.1", licences["Source Code Pro"]);
      Assert.Equal("OFL-1.1", licences["IBM Plex Mono"]);
      Assert.Equal("GUST Font License", licences["Latin Modern Roman"]);
      Assert.Equal("GUST Font License", licences["Latin Modern Mono"]);
      foreach (ThirdPartyNotice n in ThirdPartyNotices.Fonts) {
        Assert.False(string.IsNullOrWhiteSpace(n.Version), n.Name);
        Assert.Equal("https", n.LicenseUrl.Scheme);
        Assert.Equal("https", n.ProjectUrl.Scheme);
      }
    }

    [Fact]
    public void About_lists_a_font_only_once_its_file_is_embedded() {
      Func<string, bool> old = NoteFontFamilies.ResourceExists;
      try {
        NoteFontFamilies.ResourceExists = file => false;
        Assert.Equal(ThirdPartyNotices.All, ThirdPartyNotices.Shown());
        NoteFontFamilies.ResourceExists = file => file == NoteFontFamilies.FileOf("IBM Plex Mono");
        Assert.Equal(ThirdPartyNotices.All.Concat(ThirdPartyNotices.Fonts.Where(n => n.Name == "IBM Plex Mono")), ThirdPartyNotices.Shown());
        NoteFontFamilies.ResourceExists = file => true;
        Assert.Equal(ThirdPartyNotices.All.Concat(ThirdPartyNotices.Fonts), ThirdPartyNotices.Shown());
      } finally {
        NoteFontFamilies.ResourceExists = old;
      }
    }

    [Fact]
    public void The_about_window_shows_the_shown_notices() {
      string xaml = File.ReadAllText(Path.Combine(ThirdPartyNoticesTests.RepoRoot(), "src", "MDSHO.App", "AboutWindow.xaml"));
      Assert.Contains("ItemsSource=\"{x:Static local:ThirdPartyNotices.ShownNow}\"", xaml);
    }

    [Fact]
    public void The_markdown_has_a_fonts_table_that_matches_the_font_notices() {
      string[] lines = File.ReadAllLines(Path.Combine(ThirdPartyNoticesTests.RepoRoot(), "THIRD-PARTY-NOTICES.md"));
      int start = Array.FindIndex(lines, l => l.Trim() == "## Fonts that come with MDSHO");
      Assert.True(start >= 0, "THIRD-PARTY-NOTICES.md has no '## Fonts that come with MDSHO' section.");
      List<string[]> rows = new List<string[]>();
      for (int i = start + 1; i < lines.Length && !lines[i].StartsWith("## ", StringComparison.Ordinal); i++) {
        string line = lines[i].Trim();
        if (!line.StartsWith("|", StringComparison.Ordinal)) {
          continue;
        }
        string[] cells = line.Trim('|').Split('|').Select(c => c.Trim()).ToArray();
        if (cells[0] == "Font" || cells[0].StartsWith(":", StringComparison.Ordinal) || cells[0].StartsWith("-", StringComparison.Ordinal)) {
          continue;
        }
        rows.Add(cells);
      }
      Assert.Equal(ThirdPartyNotices.Fonts.Select(n => n.Name), rows.Select(r => r[0]));
      foreach (ThirdPartyNotice n in ThirdPartyNotices.Fonts) {
        string[] row = rows.Single(r => r[0] == n.Name);
        Assert.Equal((n.Version, n.License), (row[1], row[2]));
      }
    }
  }
}
