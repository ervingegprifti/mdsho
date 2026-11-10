using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Xml.Linq;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// Keeps the About window's third-party notices (ThirdPartyNotices.All) and THIRD-PARTY-NOTICES.md in sync with what
  /// the app really ships: the central package versions, the packages restored for MDSHO.App (incl. transitive ones)
  /// and the runtime packs of the self-contained MSIX.
  /// </summary>
  public class ThirdPartyNoticesTests {

    /// <summary>Packages referenced only by the test project; never shipped.</summary>
    private static readonly string[] TestOnlyPackages = { "xunit", "xunit.runner.visualstudio", "Microsoft.NET.Test.Sdk" };

    /// <summary>Restore downloads this runtime pack for every win-x64 project, but a WPF app does not ship it.</summary>
    private static readonly string[] NotShippedDownloads = { "Microsoft.AspNetCore.App.Runtime.win-x64" };

    internal static string RepoRoot() {
      DirectoryInfo dir = new DirectoryInfo(AppContext.BaseDirectory);
      while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Directory.Packages.props"))) {
        dir = dir.Parent;
      }
      Assert.True(dir != null, "Repository root (Directory.Packages.props) not found above " + AppContext.BaseDirectory);
      return dir.FullName;
    }

    /// <summary>Id -> version from Directory.Packages.props, without test-only packages.</summary>
    private static Dictionary<string, string> CentralShippedPackages() {
      XDocument props = XDocument.Load(Path.Combine(RepoRoot(), "Directory.Packages.props"));
      return props.Descendants("PackageVersion")
        .Where(e => !TestOnlyPackages.Contains((string)e.Attribute("Include"), StringComparer.OrdinalIgnoreCase))
        .ToDictionary(e => (string)e.Attribute("Include"), e => (string)e.Attribute("Version"), StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Id -> version of every package MDSHO.App restores (direct, transitive and the runtime packs it ships).</summary>
    private static Dictionary<string, string> AppRestoredPackages() {
      string assets = Path.Combine(RepoRoot(), "src", "MDSHO.App", "obj", "project.assets.json");
      Assert.True(File.Exists(assets), "Build MDSHO.App first: " + assets);
      Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
      using (JsonDocument doc = JsonDocument.Parse(File.ReadAllText(assets))) {
        foreach (JsonProperty library in doc.RootElement.GetProperty("libraries").EnumerateObject()) {
          if (library.Value.GetProperty("type").GetString() == "package") {
            string[] parts = library.Name.Split('/');
            result[parts[0]] = parts[1];
          }
        }
        foreach (JsonProperty framework in doc.RootElement.GetProperty("project").GetProperty("frameworks").EnumerateObject()) {
          if (!framework.Value.TryGetProperty("downloadDependencies", out JsonElement downloads)) {
            continue;
          }
          foreach (JsonElement download in downloads.EnumerateArray()) {
            string name = download.GetProperty("name").GetString();
            if (NotShippedDownloads.Contains(name, StringComparer.OrdinalIgnoreCase)) {
              continue;
            }
            result[name] = download.GetProperty("version").GetString().Trim('[', ']').Split(',')[0].Trim();
          }
        }
      }
      return result;
    }

    private static ThirdPartyNotice NoticeFor(string packageId) {
      return ThirdPartyNotices.All.FirstOrDefault(n => n.Packages.Contains(packageId, StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>Rows (first cell, second cell) of the "Shipped with the application" table in THIRD-PARTY-NOTICES.md.</summary>
    private static List<string[]> MarkdownShippedRows() {
      string[] lines = File.ReadAllLines(Path.Combine(RepoRoot(), "THIRD-PARTY-NOTICES.md"));
      int start = Array.FindIndex(lines, l => l.Trim() == "## Shipped with the application");
      Assert.True(start >= 0, "THIRD-PARTY-NOTICES.md has no '## Shipped with the application' section.");
      List<string[]> rows = new List<string[]>();
      for (int i = start + 1; i < lines.Length && !lines[i].StartsWith("## ", StringComparison.Ordinal); i++) {
        string line = lines[i].Trim();
        if (!line.StartsWith("|", StringComparison.Ordinal)) {
          continue;
        }
        string[] cells = line.Trim('|').Split('|').Select(c => c.Trim()).ToArray();
        if (cells[0] == "Package" || cells[0].StartsWith(":", StringComparison.Ordinal) || cells[0].StartsWith("-", StringComparison.Ordinal)) {
          continue;
        }
        rows.Add(cells);
      }
      return rows;
    }

    [Fact]
    public void Notices_CoverEveryCentralPackageWithItsVersion() {
      List<string> problems = new List<string>();
      foreach (KeyValuePair<string, string> package in CentralShippedPackages()) {
        ThirdPartyNotice notice = NoticeFor(package.Key);
        if (notice == null) {
          problems.Add(package.Key + " " + package.Value + " is missing from the About notices");
        } else if (notice.Version != package.Value) {
          problems.Add(package.Key + ": notice says '" + notice.Version + "', Directory.Packages.props says " + package.Value);
        }
      }
      Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    [Fact]
    public void Notices_CoverEveryShippedPackage_IncludingTransitiveAndRuntime() {
      List<string> problems = AppRestoredPackages().Keys
        .Where(id => NoticeFor(id) == null)
        .Select(id => id + " is shipped but missing from the About notices")
        .ToList();
      Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    [Fact]
    public void Notices_ListNoPackageThatIsNotShipped() {
      Dictionary<string, string> restored = AppRestoredPackages();
      List<string> stale = ThirdPartyNotices.All
        .SelectMany(n => n.Packages.Select(p => n.Name + " -> " + p + (restored.ContainsKey(p) ? null : " (not shipped)")))
        .Where(s => s.EndsWith("(not shipped)", StringComparison.Ordinal))
        .ToList();
      Assert.True(stale.Count == 0, string.Join(Environment.NewLine, stale));
    }

    [Fact]
    public void Notices_VersionsMatchRestoredPackages() {
      Dictionary<string, string> restored = AppRestoredPackages();
      List<string> problems = new List<string>();
      foreach (ThirdPartyNotice notice in ThirdPartyNotices.All) {
        if (notice.Packages.Count == 0) {
          continue;
        }
        string version;
        if (restored.TryGetValue(notice.Packages[0], out version) && version != notice.Version) {
          problems.Add(notice.Name + ": notice says '" + notice.Version + "', restored " + notice.Packages[0] + " is " + version);
        }
      }
      Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    [Fact]
    public void Notices_HaveLicenseAndHttpsLinks() {
      Assert.NotEmpty(ThirdPartyNotices.All);
      foreach (ThirdPartyNotice notice in ThirdPartyNotices.All) {
        Assert.False(string.IsNullOrWhiteSpace(notice.Name));
        Assert.False(string.IsNullOrWhiteSpace(notice.Version), notice.Name + " has no version");
        Assert.False(string.IsNullOrWhiteSpace(notice.License), notice.Name + " has no license name");
        Assert.Equal("https", notice.LicenseUrl.Scheme);
        Assert.Equal("https", notice.ProjectUrl.Scheme);
      }
    }

    [Fact]
    public void Markdown_ShippedTableMatchesNotices() {
      List<string[]> rows = MarkdownShippedRows();
      List<string> problems = new List<string>();
      foreach (ThirdPartyNotice notice in ThirdPartyNotices.All) {
        string[] row = rows.FirstOrDefault(r => r[0] == notice.Name);
        if (row == null) {
          problems.Add("THIRD-PARTY-NOTICES.md is missing '" + notice.Name + "'");
        } else if (row.Length < 3 || row[1] != notice.Version || row[2] != notice.License) {
          problems.Add("THIRD-PARTY-NOTICES.md row '" + notice.Name + "' should have version " + notice.Version + " and license " + notice.License);
        }
      }
      foreach (string[] row in rows.Where(r => ThirdPartyNotices.All.All(n => n.Name != r[0]))) {
        problems.Add("THIRD-PARTY-NOTICES.md lists '" + row[0] + "', which the About notices do not");
      }
      Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    [Fact]
    public void Notices_ListTheMvvmToolkitAsMit() {
      ThirdPartyNotice notice = NoticeFor("CommunityToolkit.Mvvm");
      Assert.NotNull(notice);
      Assert.Equal("CommunityToolkit.Mvvm", notice.Name);
      Assert.Equal("MIT", notice.License);
      Assert.Equal("https://github.com/CommunityToolkit/dotnet", notice.ProjectUrl.ToString().TrimEnd('/'));
    }

    [Fact]
    public void Copyright_RunsFrom2022ToTheCurrentYear() {
      Assert.Equal("© 2022–2026 Ervin Gegprifti. Licensed under GPLv3 or a commercial licence.", ThirdPartyNotices.CopyrightFor(2026));
      Assert.Equal("© 2022–2031 Ervin Gegprifti. Licensed under GPLv3 or a commercial licence.", ThirdPartyNotices.CopyrightFor(2031));
      Assert.Equal("© 2022 Ervin Gegprifti. Licensed under GPLv3 or a commercial licence.", ThirdPartyNotices.CopyrightFor(2022));
    }
  }
}
