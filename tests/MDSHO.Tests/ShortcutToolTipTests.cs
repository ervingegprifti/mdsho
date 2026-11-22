using System;
using System.Collections.Generic;
using System.Linq;
using MDSHO.Data;
using MDSHO.ViewModels;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// The button tooltip (1.23.0): grey labels, the value in the normal colour, never the button name. A link, file or
  /// folder shows "Opens: target"; a run command "Runs: command line" with its error under it; a note its lines; a snippet
  /// a grey "Double-click to paste:" then its lines. Note, snippet and comment lines stop at 10, the last then a grey
  /// "… N more lines". The comment comes last: "Comment: text", below a separator when anything comes before it.
  /// </summary>
  public class ShortcutToolTipTests : IDisposable {
    private readonly TempDb db = new TempDb();
    private readonly AppViewModel app = new AppViewModel();
    private readonly DesktopWindowViewModel win;

    public ShortcutToolTipTests() {
      win = app.AddWindow("TEST", 0, 0);
    }

    public void Dispose() => db.Dispose();

    private ShortcutViewModel Link(string target = "https://example.com/reports") {
      ShortcutViewModel s = ViewModelFactory.NewShortcut(app, null, win, "Reports", target, "", Guid.NewGuid().ToString());
      win.RootShortcuts.Add(s);
      return s;
    }

    private ShortcutViewModel Command(string program = "cmd.exe", string arguments = "/K ping SRV01") {
      ShortcutViewModel s = ViewModelFactory.NewProcessCommandShortcut(app, null, win);
      win.RootShortcuts.Add(s);
      s.Name = "Ping";
      s.ProcessFileName = program;
      s.ProcessArguments = arguments;
      return s;
    }

    private ShortcutViewModel Note(string text) {
      ShortcutViewModel s = ViewModelFactory.NewNoteShortcut(app, null, win);
      win.RootShortcuts.Add(s);
      s.SaveNoteText(text);
      return s;
    }

    private ShortcutViewModel Snippet(string text) {
      ShortcutViewModel s = ViewModelFactory.NewSnippetShortcut(app, null, win);
      win.RootShortcuts.Add(s);
      s.Name = "Home address";
      s.SaveSnippetText(text);
      return s;
    }

    private ShortcutViewModel Group() {
      ShortcutViewModel s = ViewModelFactory.NewGroupShortcut(app, null, win);
      win.RootShortcuts.Add(s);
      return s;
    }

    // The lines as "text" or "grey:text".
    private static string[] Lines(ShortcutViewModel s) => s.ToolTipLines.Select(l => (l.IsGrey ? "grey:" : "") + l.Text).ToArray();

    private static string Many(int count) => string.Join("\r\n", Enumerable.Range(1, count).Select(i => "line " + i));

    // ---- What comes before the comment ----

    [Fact]
    public void A_link_file_or_folder_shows_opens_and_its_target() {
      ShortcutViewModel link = Link();
      Assert.Equal("Opens:", link.ToolTipLabel);
      Assert.Equal("https://example.com/reports", link.ToolTipValue);
      Assert.True(link.ShowToolTipTarget);
      Assert.Null(link.ToolTipHint);
      Assert.Empty(link.ToolTipLines);
      Assert.Equal("Opens:", Link(@"C:\Work\Reports\weekly.xlsx").ToolTipLabel);
      Assert.True(link.HasToolTip);
    }

    [Fact]
    public void A_link_without_a_target_shows_no_opens_line() {
      ShortcutViewModel link = Link("");
      Assert.False(link.ShowToolTipTarget);
      Assert.False(link.HasToolTip);
    }

    [Fact]
    public void A_run_command_shows_runs_and_its_command_line() {
      ShortcutViewModel cmd = Command();
      Assert.Equal("Runs:", cmd.ToolTipLabel);
      Assert.Equal("cmd.exe /K ping SRV01", cmd.ToolTipValue);
      Assert.Equal(cmd.CommandLineText, cmd.ToolTipValue);
      Assert.True(cmd.ShowToolTipTarget);
      Assert.Null(cmd.ToolTipHint);
      Assert.Empty(cmd.ToolTipLines);
    }

    [Fact]
    public void A_note_shows_its_lines_with_no_label() {
      ShortcutViewModel note = Note("\r\nShopping list for the weekend\r\nmilk\r\n\r\nbread and butter\r\n");
      Assert.False(note.ShowToolTipTarget);
      Assert.Null(note.ToolTipLabel);
      Assert.Null(note.ToolTipHint);
      Assert.Equal(new[] { "Shopping list for the weekend", "milk", "", "bread and butter" }, Lines(note));
      Assert.True(note.HasToolTip);
    }

    [Fact]
    public void An_empty_note_has_no_tooltip() {
      ShortcutViewModel note = Note(" \r\n ");
      Assert.Empty(note.ToolTipLines);
      Assert.False(note.HasToolTip);
    }

    [Fact]
    public void A_snippet_shows_the_paste_hint_then_its_lines_and_never_its_name() {
      ShortcutViewModel clip = Snippet("Kind regards,\r\nAlex");
      Assert.Equal("Double-click to paste:", clip.ToolTipHint);
      Assert.Equal(new[] { "Kind regards,", "Alex" }, Lines(clip));
      Assert.False(clip.ShowToolTipTarget);
      Assert.DoesNotContain("Home address", clip.ToolTipText);
      win.Appearance.SingleClickToOpen = true;
      Assert.Equal("Click to paste:", clip.ToolTipHint);
    }

    [Fact]
    public void An_empty_snippet_shows_a_grey_empty() {
      ShortcutViewModel clip = Snippet("  ");
      Assert.Equal(new[] { "grey:(empty)" }, Lines(clip));
      Assert.True(clip.HasToolTip);
    }

    [Theory]
    [InlineData(11, "… 1 more line")]
    [InlineData(14, "… 4 more lines")]
    public void Past_10_lines_the_last_is_a_grey_count_of_the_rest(int count, string more) {
      foreach (ShortcutViewModel s in new[] { Note(Many(count)), Snippet(Many(count)) }) {
        string[] lines = Lines(s);
        Assert.Equal(11, lines.Length);
        Assert.Equal("line 10", lines[9]);
        Assert.Equal("grey:" + more, lines[10]);
      }
      Assert.Equal(10, Lines(Note(Many(10))).Length);
      Assert.DoesNotContain(Lines(Note(Many(10))), l => l.StartsWith("grey:"));
    }

    [Fact]
    public void A_group_shows_nothing_before_the_comment() {
      ShortcutViewModel group = Group();
      Assert.False(group.ShowToolTipTarget);
      Assert.Null(group.ToolTipHint);
      Assert.Empty(group.ToolTipLines);
      Assert.False(group.HasToolTip);
    }

    // ---- The comment ----

    [Fact]
    public void The_comment_keeps_10_lines_then_a_grey_count_of_the_rest() {
      ShortcutViewModel cmd = Command();
      cmd.Comment = "one\r\ntwo";
      Assert.Equal("one" + Environment.NewLine + "two", cmd.CommentPreview);
      Assert.Null(cmd.CommentMoreLines);
      cmd.Comment = Many(12);
      Assert.Equal(string.Join(Environment.NewLine, Enumerable.Range(1, 10).Select(i => "line " + i)), cmd.CommentPreview);
      Assert.Equal("… 2 more lines", cmd.CommentMoreLines);
      cmd.Comment = Many(11);
      Assert.Equal("… 1 more line", cmd.CommentMoreLines);
    }

    [Fact]
    public void The_separator_shows_only_with_a_comment_and_something_before_it() {
      ShortcutViewModel link = Link();
      ShortcutViewModel cmd = Command();
      ShortcutViewModel note = Note("milk");
      ShortcutViewModel clip = Snippet("");
      ShortcutViewModel group = Group();
      foreach (ShortcutViewModel s in new[] { link, cmd, clip, group }) {
        Assert.False(s.ShowCommentDivider);
        s.Comment = "Ask Sam first.";
        Assert.True(s.HasComment);
        Assert.True(s.HasToolTip);
      }
      Assert.True(link.ShowCommentDivider);
      Assert.True(cmd.ShowCommentDivider);
      Assert.True(clip.ShowCommentDivider);
      Assert.False(group.ShowCommentDivider);
      Assert.False(note.HasComment);
      Assert.False(note.ShowCommentDivider);
    }

    [Fact]
    public void A_run_command_error_alone_still_comes_before_the_comment() {
      ShortcutViewModel group = Group();
      group.Name = "Servers";
      ShortcutViewModel cmd = ViewModelFactory.NewProcessCommandShortcut(app, group, null);
      group.Children.Add(cmd);
      cmd.ProcessFileName = "cmd.exe";
      cmd.ProcessArguments = "/K ping {=1}";
      Assert.True(cmd.HasRunCommandError);
      cmd.Comment = "x";
      Assert.True(cmd.ShowCommentDivider);
    }

    // ---- Change notifications the tooltip binds to ----

    [Fact]
    public void Changes_raise_the_tooltip_parts() {
      ShortcutViewModel clip = Snippet("a");
      List<string> raised = new List<string>();
      clip.PropertyChanged += (s, e) => raised.Add(e.PropertyName);
      clip.SaveSnippetText("b");
      Assert.Contains(nameof(ShortcutViewModel.ToolTipLines), raised);
      raised.Clear();
      win.Appearance.SingleClickToOpen = true;
      Assert.Contains(nameof(ShortcutViewModel.ToolTipHint), raised);
      ShortcutViewModel cmd = Command();
      cmd.PropertyChanged += (s, e) => raised.Add(e.PropertyName);
      raised.Clear();
      cmd.ProcessArguments = "/K ping SRV02";
      Assert.Contains(nameof(ShortcutViewModel.ToolTipValue), raised);
      Assert.Contains(nameof(ShortcutViewModel.ShowToolTipTarget), raised);
      raised.Clear();
      cmd.Comment = Many(12);
      Assert.Contains(nameof(ShortcutViewModel.CommentMoreLines), raised);
    }

    [Fact]
    public void Copy_command_to_memory_text_is_unchanged() {
      ShortcutViewModel cmd = Command(@"C:\Program Files\App\app.exe", "-x");
      Assert.Equal("\"C:\\Program Files\\App\\app.exe\" -x", cmd.CommandLineText);
    }
  }
}
