using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// Keeps the Markdown docs honest: relative links in docs/*.md and the root *.md files point to files that exist, and
  /// docs/database.md lists exactly the tables, columns and indexes of a database freshly created by the app.
  /// </summary>
  public class DocsTests {

    private static readonly Regex InlineLink = new Regex(@"\]\(\s*<?([^)\s>]+)>?(?:\s+""[^""]*"")?\s*\)");
    private static readonly Regex ReferenceLink = new Regex(@"^\s{0,3}\[[^\]]+\]:\s*<?(\S+?)>?(?:\s+.*)?$");
    private static readonly Regex InlineCode = new Regex(@"`[^`]*`");

    private static string RepoRoot() {
      DirectoryInfo dir = new DirectoryInfo(AppContext.BaseDirectory);
      while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Directory.Packages.props"))) {
        dir = dir.Parent;
      }
      Assert.True(dir != null, "Repository root (Directory.Packages.props) not found above " + AppContext.BaseDirectory);
      return dir.FullName;
    }

    /// <summary>The Markdown files the test checks: docs/*.md and the root *.md files.</summary>
    private static IEnumerable<string> MarkdownFiles() {
      string root = RepoRoot();
      return Directory.GetFiles(root, "*.md").Concat(Directory.GetFiles(Path.Combine(root, "docs"), "*.md"));
    }

    /// <summary>Link targets of one Markdown file, outside code blocks and inline code.</summary>
    internal static List<string> LinkTargets(string[] lines) {
      List<string> targets = new List<string>();
      bool inFence = false;
      foreach (string raw in lines) {
        if (raw.TrimStart().StartsWith("```", StringComparison.Ordinal)) {
          inFence = !inFence;
          continue;
        }
        if (inFence) {
          continue;
        }
        string line = InlineCode.Replace(raw, "");
        foreach (Match m in InlineLink.Matches(line)) {
          targets.Add(m.Groups[1].Value);
        }
        Match reference = ReferenceLink.Match(line);
        if (reference.Success) {
          targets.Add(reference.Groups[1].Value);
        }
      }
      return targets;
    }

    /// <summary>True for links that leave the repository (a scheme such as https: or mailto:) or stay on the page.</summary>
    private static bool IsExternalOrAnchor(string target) {
      return target.StartsWith("#", StringComparison.Ordinal) || Regex.IsMatch(target, @"^[a-zA-Z][a-zA-Z0-9+.-]*:");
    }

    [Fact]
    public void Link_parser_finds_inline_and_reference_links_but_skips_code() {
      string[] lines = {
        "See [a](a.md) and [b](sub/b.md#part \"title\").",
        "`[not](code.md)`",
        "```",
        "[not](fenced.md)",
        "```",
        "[ref]: other.md"
      };
      Assert.Equal(new[] { "a.md", "sub/b.md#part", "other.md" }, LinkTargets(lines));
    }

    [Fact]
    public void Relative_markdown_links_point_to_existing_files() {
      List<string> broken = new List<string>();
      int checkedLinks = 0;
      foreach (string file in MarkdownFiles()) {
        string folder = Path.GetDirectoryName(file);
        foreach (string target in LinkTargets(File.ReadAllLines(file))) {
          if (IsExternalOrAnchor(target)) {
            continue;
          }
          string path = Uri.UnescapeDataString(target.Split('#')[0].Split('?')[0]);
          if (path.Length == 0) {
            continue;
          }
          checkedLinks++;
          string full = Path.GetFullPath(Path.Combine(folder, path.Replace('/', Path.DirectorySeparatorChar)));
          if (!File.Exists(full) && !Directory.Exists(full)) {
            broken.Add(Path.GetFileName(file) + " -> " + target);
          }
        }
      }
      Assert.True(checkedLinks > 0, "No relative links found; the parser is probably broken.");
      Assert.True(broken.Count == 0, "Broken relative links:" + Environment.NewLine + string.Join(Environment.NewLine, broken));
    }

    /// <summary>Rows "| `name` | cell | cell | ..." right below the heading "### <paramref name="heading"/>" of database.md.</summary>
    private static List<string[]> DocRows(string[] lines, string heading) {
      int start = Array.FindIndex(lines, l => l.Trim() == "### " + heading);
      Assert.True(start >= 0, "docs/database.md has no '### " + heading + "' heading.");
      List<string[]> rows = new List<string[]>();
      for (int i = start + 1; i < lines.Length && !lines[i].StartsWith("#", StringComparison.Ordinal); i++) {
        string line = lines[i].Trim();
        if (!line.StartsWith("| `", StringComparison.Ordinal)) {
          continue;
        }
        rows.Add(line.Trim('|').Split('|').Select(c => c.Trim()).ToArray());
      }
      return rows;
    }

    private static string Unquote(string cell) {
      return cell.Trim('`');
    }

    [Fact]
    public void Database_doc_matches_a_fresh_database() {
      string[] doc = File.ReadAllLines(Path.Combine(RepoRoot(), "docs", "database.md"));
      List<string> problems = new List<string>();
      using (TempDb db = new TempDb())
      using (SqliteConnection conn = db.Open()) {
        List<string> tables = new List<string>();
        using (SqliteCommand cmd = new SqliteCommand("SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%' ORDER BY name", conn))
        using (SqliteDataReader r = cmd.ExecuteReader()) {
          while (r.Read()) {
            tables.Add(r.GetString(0));
          }
        }
        Assert.NotEmpty(tables);
        foreach (string table in tables) {
          // Real columns as "name | TYPE | NOT NULL/NULL | default".
          List<string> real = new List<string>();
          using (SqliteCommand cmd = new SqliteCommand("SELECT name, type, \"notnull\", dflt_value FROM pragma_table_info(@t) ORDER BY cid", conn)) {
            cmd.Parameters.AddWithValue("@t", table);
            using (SqliteDataReader r = cmd.ExecuteReader()) {
              while (r.Read()) {
                string dflt = r.IsDBNull(3) ? "" : r.GetString(3);
                real.Add(r.GetString(0) + " | " + r.GetString(1) + " | " + (r.GetInt64(2) == 1 ? "NOT NULL" : "NULL") + " | " + dflt);
              }
            }
          }
          List<string> documented = DocRows(doc, table)
            .Select(c => Unquote(c[0]) + " | " + c[1] + " | " + c[2] + " | " + Unquote(c[3]))
            .ToList();
          if (!real.SequenceEqual(documented)) {
            problems.Add(table + ": expected" + Environment.NewLine + "  " + string.Join(Environment.NewLine + "  ", real)
              + Environment.NewLine + "documented" + Environment.NewLine + "  " + string.Join(Environment.NewLine + "  ", documented));
          }
        }
        // Indexes as "name | table(column)".
        List<string> realIndexes = new List<string>();
        using (SqliteCommand cmd = new SqliteCommand(
          "SELECT m.name, m.tbl_name, i.name FROM sqlite_master m, pragma_index_info(m.name) i WHERE m.type = 'index' AND m.name NOT LIKE 'sqlite_%' ORDER BY m.name", conn))
        using (SqliteDataReader r = cmd.ExecuteReader()) {
          while (r.Read()) {
            realIndexes.Add(r.GetString(0) + " | " + r.GetString(1) + "(" + r.GetString(2) + ")");
          }
        }
        List<string> documentedIndexes = DocRows(doc, "Indexes")
          .Select(c => Unquote(c[0]) + " | " + Unquote(c[1]))
          .OrderBy(s => s, StringComparer.Ordinal)
          .ToList();
        if (!realIndexes.SequenceEqual(documentedIndexes)) {
          problems.Add("Indexes: expected " + string.Join(", ", realIndexes) + "; documented " + string.Join(", ", documentedIndexes));
        }
        using (SqliteCommand cmd = new SqliteCommand("PRAGMA user_version;", conn)) {
          long version = Convert.ToInt64(cmd.ExecuteScalar());
          if (!doc.Any(l => l.Contains("version " + version + "**", StringComparison.Ordinal))) {
            problems.Add("docs/database.md does not say the current version as '**... version " + version + "**'.");
          }
        }
      }
      Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }
  }
}
