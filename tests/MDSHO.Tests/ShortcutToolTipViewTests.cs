using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MDSHO.Data;
using MDSHO.Helpers;
using MDSHO.ViewModels;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// The button tooltip as the panel shows it (1.23.0): the parts in order, grey labels (Theme.ToolTipSecondaryText) and
  /// the value in the normal colour, note and snippet lines cut to one row, the comment wrapping under "Comment:". Also in
  /// clones and in both window themes.
  /// </summary>
  public class ShortcutToolTipViewTests {
    private static readonly Color Grey = Color.FromRgb(0x66, 0x66, 0x66);

    private sealed class Fixture {
      public AppViewModel App;
      public DesktopWindowViewModel Win;
    }

    private static void WithPanel(Action<Fixture> body) {
      WpfAppHost.Run(() => {
        using (TempDb db = new TempDb()) {
          Fixture f = new Fixture { App = new AppViewModel() };
          f.Win = f.App.AddWindow("TEST", 0, 0);
          body(f);
        }
      });
    }

    private static ShortcutViewModel Link(Fixture f, string target) {
      ShortcutViewModel s = ViewModelFactory.NewShortcut(f.App, null, f.Win, "Reports", target, "", Guid.NewGuid().ToString());
      f.Win.RootShortcuts.Add(s);
      return s;
    }

    private static ShortcutViewModel Command(Fixture f, ShortcutViewModel group = null) {
      ShortcutViewModel s = ViewModelFactory.NewProcessCommandShortcut(f.App, group, group == null ? f.Win : null);
      if (group == null) { f.Win.RootShortcuts.Add(s); } else { group.Children.Add(s); }
      s.Name = "Ping";
      s.ProcessFileName = "cmd.exe";
      s.ProcessArguments = "/K ping SRV01";
      return s;
    }

    private static ShortcutViewModel Note(Fixture f, string text) {
      ShortcutViewModel s = ViewModelFactory.NewNoteShortcut(f.App, null, f.Win);
      f.Win.RootShortcuts.Add(s);
      s.SaveNoteText(text);
      return s;
    }

    private static ShortcutViewModel Snippet(Fixture f, string text) {
      ShortcutViewModel s = ViewModelFactory.NewSnippetShortcut(f.App, null, f.Win);
      f.Win.RootShortcuts.Add(s);
      s.Name = "Signature";
      s.SaveSnippetText(text);
      return s;
    }

    private static ShortcutViewModel Group(Fixture f) {
      ShortcutViewModel s = ViewModelFactory.NewGroupShortcut(f.App, null, f.Win);
      f.Win.RootShortcuts.Add(s);
      s.Name = "Servers";
      return s;
    }

    // Opens the panel (or a clone), opens the tooltip of each button and hands over its content.
    private static void WithToolTips(DesktopWindowViewModel panel, ShortcutViewModel[] shortcuts, Action<ShortcutViewModel, Button, ToolTip> check) {
      DesktopWindowView view = new DesktopWindowView(panel) { ShowActivated = false, Left = -10000, Top = -10000, Width = 300, Height = 900 };
      try {
        view.Show();
        WpfAppHost.Pump();
        foreach (ShortcutViewModel s in shortcuts) {
          if (s.ParentShortcut != null) {
            s.ParentShortcut.IsGroupExpanded = true;
            WpfAppHost.Pump();
          }
          TreeView tree = (TreeView)view.FindName("TreeViewShortcuts");
          Button button = WpfAppHost.VisualDescendants<Button>(tree).First(b => b.DataContext == s);
          ToolTip tip = Assert.IsType<ToolTip>(button.ToolTip);
          tip.PlacementTarget = button;
          tip.IsOpen = true;
          WpfAppHost.Pump();
          try {
            check(s, button, tip);
          } finally {
            tip.IsOpen = false;
            WpfAppHost.Pump();
          }
        }
      } finally {
        view.Close();
        WpfAppHost.Pump();
      }
    }

    private static bool IsGrey(Brush brush) => brush is SolidColorBrush b && b.Color == Grey;

    // A text with its grey parts in {braces}; spaces between parts stay as they are.
    private static string Describe(TextBlock text) {
      if (text.Inlines.Count == 0 || text.Inlines.All(i => !(i is Run))) {
        return IsGrey(text.Foreground) ? "{" + text.Text + "}" : text.Text;
      }
      return string.Concat(text.Inlines.OfType<Run>().Select(r => string.IsNullOrWhiteSpace(r.Text) || !IsGrey(r.Foreground) ? r.Text : "{" + r.Text + "}"));
    }

    // The visible parts of the tooltip in order: "---" for the separator, one entry per text or line.
    private static List<string> Parts(ToolTip tip) {
      StackPanel panel = Assert.IsType<StackPanel>(tip.Content);
      List<string> parts = new List<string>();
      foreach (UIElement child in panel.Children) {
        if (child.Visibility != Visibility.Visible) { continue; }
        if (child is Separator) {
          parts.Add("---");
        } else if (child is TextBlock text) {
          parts.Add(Describe(text));
        } else if (child is ItemsControl items) {
          parts.AddRange(Lines(items).Select(Describe));
        }
      }
      return parts;
    }

    private static List<TextBlock> Lines(ItemsControl items) {
      return items.Items.Cast<object>()
        .Select(item => (FrameworkElement)items.ItemContainerGenerator.ContainerFromItem(item))
        .Select(c => WpfAppHost.VisualDescendants<TextBlock>(c).First()).ToList();
    }

    private static TextBlock CommentText(ToolTip tip) {
      StackPanel panel = (StackPanel)tip.Content;
      return panel.Children.OfType<TextBlock>().Single(t => t.Inlines.OfType<Run>().Any(r => r.Text == "Comment:"));
    }

    private static string Many(int count) => string.Join("\r\n", Enumerable.Range(1, count).Select(i => "line " + i));

    [Fact]
    public void Each_kind_shows_its_parts_in_order_with_grey_labels() {
      WithPanel(f => {
        ShortcutViewModel link = Link(f, "https://example.com/reports");
        link.Comment = "Used for the weekly report.";
        ShortcutViewModel file = Link(f, @"C:\Work\Reports\weekly.xlsx");
        ShortcutViewModel cmd = Command(f);
        cmd.Comment = "Keeps pinging until closed.";
        ShortcutViewModel note = Note(f, "Shopping list\r\nmilk\r\nbread and butter");
        ShortcutViewModel clip = Snippet(f, "Kind regards,\r\nAlex");
        clip.Comment = "For mail";
        ShortcutViewModel empty = Snippet(f, "");
        ShortcutViewModel group = Group(f);
        group.Comment = "Servers of team A";
        Dictionary<ShortcutViewModel, string[]> expected = new Dictionary<ShortcutViewModel, string[]> {
          [link] = new[] { "{Opens:} https://example.com/reports", "---", "{Comment:} Used for the weekly report." },
          [file] = new[] { @"{Opens:} C:\Work\Reports\weekly.xlsx" },
          [cmd] = new[] { "{Runs:} cmd.exe /K ping SRV01", "---", "{Comment:} Keeps pinging until closed." },
          [note] = new[] { "Shopping list", "milk", "bread and butter" },
          [clip] = new[] { "{Double-click to paste:}", "Kind regards,", "Alex", "---", "{Comment:} For mail" },
          [empty] = new[] { "{Double-click to paste:}", "{(empty)}" },
          [group] = new[] { "{Comment:} Servers of team A" },
        };
        WithToolTips(f.Win, expected.Keys.ToArray(), (s, button, tip) => {
          Assert.Equal(expected[s], Parts(tip));
          Assert.True(ToolTipService.GetIsEnabled(button));
          Assert.Equal(420, ((StackPanel)tip.Content).MaxWidth);
        });
      });
    }

    [Fact]
    public void The_command_line_is_monospace_and_wraps_and_the_error_sits_under_it() {
      WithPanel(f => {
        ShortcutViewModel group = Group(f);
        ShortcutViewModel cmd = Command(f, group);
        cmd.ProcessArguments = "/K ping {=1}";
        cmd.Comment = "Asks first.";
        Assert.True(cmd.HasRunCommandError);
        WithToolTips(f.Win, new[] { cmd }, (s, button, tip) => {
          List<string> parts = Parts(tip);
          Assert.Equal(4, parts.Count);
          Assert.Equal("{Runs:} cmd.exe /K ping {=1}", parts[0]);
          Assert.Equal(cmd.RunCommandError, parts[1]);
          Assert.Equal("---", parts[2]);
          Assert.Equal("{Comment:} Asks first.", parts[3]);
          TextBlock runs = ((StackPanel)tip.Content).Children.OfType<TextBlock>().First(t => t.Inlines.OfType<Run>().Any(r => r.Text == "Runs:"));
          Assert.Equal(TextWrapping.Wrap, runs.TextWrapping);
          Assert.Equal("Consolas", runs.Inlines.OfType<Run>().Last().FontFamily.Source);
        });
      });
    }

    [Fact]
    public void Note_and_snippet_lines_are_cut_to_one_row_and_the_comment_wraps() {
      WithPanel(f => {
        string longLine = string.Join(" ", Enumerable.Repeat("word", 60));
        ShortcutViewModel note = Note(f, longLine + "\r\nmilk");
        ShortcutViewModel clip = Snippet(f, longLine);
        clip.Comment = longLine;
        WithToolTips(f.Win, new[] { note, clip }, (s, button, tip) => {
          ItemsControl items = ((StackPanel)tip.Content).Children.OfType<ItemsControl>().Single();
          TextBlock first = Lines(items).First();
          Assert.Equal(TextTrimming.CharacterEllipsis, first.TextTrimming);
          Assert.Equal(TextWrapping.NoWrap, first.TextWrapping);
          Assert.True(first.ActualHeight < 2 * first.FontSize * 1.5);
          if (s == clip) {
            TextBlock comment = CommentText(tip);
            Assert.Equal(TextWrapping.Wrap, comment.TextWrapping);
            Assert.True(comment.ActualHeight > 2 * comment.FontSize);
          }
        });
      });
    }

    [Fact]
    public void Past_10_lines_a_grey_count_of_the_rest_ends_the_note_snippet_and_comment() {
      WithPanel(f => {
        ShortcutViewModel note = Note(f, Many(12));
        ShortcutViewModel clip = Snippet(f, Many(11));
        ShortcutViewModel cmd = Command(f);
        cmd.Comment = Many(14);
        WithToolTips(f.Win, new[] { note, clip, cmd }, (s, button, tip) => {
          List<string> parts = Parts(tip);
          if (s == note) {
            Assert.Equal(Enumerable.Range(1, 10).Select(i => "line " + i).Append("{… 2 more lines}"), parts);
          } else if (s == clip) {
            Assert.Equal("{… 1 more line}", parts.Last());
            Assert.Equal(12, parts.Count);
          } else {
            Assert.Equal("{Comment:} " + string.Join(Environment.NewLine, Enumerable.Range(1, 10).Select(i => "line " + i)), parts[^2]);
            Assert.Equal("{… 4 more lines}", parts[^1]);
          }
        });
      });
    }

    [Fact]
    public void Clones_show_the_same_tooltip() {
      WithPanel(f => {
        ShortcutViewModel link = Link(f, "https://example.com/reports");
        link.Comment = "Weekly";
        f.Win.CloneWindow("TEST2", 0, 0);
        DesktopWindowViewModel clone = f.Win.Clones.Last();
        WithToolTips(clone, new[] { link }, (s, button, tip) =>
          Assert.Equal(new[] { "{Opens:} https://example.com/reports", "---", "{Comment:} Weekly" }, Parts(tip)));
      });
    }

    [Theory]
    [InlineData(WindowThemeKind.Light)]
    [InlineData(WindowThemeKind.Dark)]
    public void Both_window_themes_show_grey_labels_and_normal_values(WindowThemeKind theme) {
      WithPanel(f => {
        ShortcutViewModel cmd = Command(f);
        cmd.Comment = "Ask Sam.";
        try {
          WindowThemes.Apply(theme);
          WithToolTips(f.Win, new[] { cmd }, (s, button, tip) => {
            Assert.Equal(new[] { "{Runs:} cmd.exe /K ping SRV01", "---", "{Comment:} Ask Sam." }, Parts(tip));
            Run value = CommentText(tip).Inlines.OfType<Run>().Last();
            Assert.False(IsGrey(value.Foreground));
          });
        } finally {
          WindowThemes.Apply(WindowThemeKind.Light);
        }
      });
    }

    // Values use Theme.ToolTipText (#1E1E1E), dark enough that the grey labels (Theme.ToolTipSecondaryText, #666) read as grey.
    [Theory]
    [InlineData(WindowThemeKind.Light)]
    [InlineData(WindowThemeKind.Dark)]
    public void Values_use_the_tooltip_text_brush_and_labels_the_secondary_brush(WindowThemeKind theme) {
      WithPanel(f => {
        ShortcutViewModel link = Link(f, "https://example.com/reports");
        link.Comment = "Weekly";
        ShortcutViewModel cmd = Command(f);
        ShortcutViewModel note = Note(f, Many(12));
        ShortcutViewModel clip = Snippet(f, "");
        try {
          WindowThemes.Apply(theme);
          Color text = ((SolidColorBrush)Application.Current.FindResource("Theme.ToolTipText")).Color;
          Color secondary = ((SolidColorBrush)Application.Current.FindResource("Theme.ToolTipSecondaryText")).Color;
          Assert.Equal(Color.FromRgb(0x1E, 0x1E, 0x1E), text);
          Assert.Equal(Grey, secondary);
          Color Of(Brush b) => Assert.IsType<SolidColorBrush>(b).Color;
          WithToolTips(f.Win, new[] { link, cmd, note, clip }, (s, button, tip) => {
            StackPanel panel = (StackPanel)tip.Content;
            if (s == link || s == cmd) {
              Run[] runs = panel.Children.OfType<TextBlock>().First().Inlines.OfType<Run>().ToArray();
              Assert.Equal(secondary, Of(runs[0].Foreground));
              Assert.Equal(text, Of(runs[^1].Foreground));
            }
            if (s == link) {
              Run[] comment = CommentText(tip).Inlines.OfType<Run>().ToArray();
              Assert.Equal(secondary, Of(comment[0].Foreground));
              Assert.Equal(text, Of(comment[^1].Foreground));
            }
            if (s == note) {
              List<TextBlock> lines = Lines(panel.Children.OfType<ItemsControl>().Single());
              Assert.Equal(text, Of(lines[0].Foreground));
              Assert.Equal(secondary, Of(lines[^1].Foreground));
            }
            if (s == clip) {
              Assert.Equal(secondary, Of(panel.Children.OfType<TextBlock>().Single(t => t.Text == "Double-click to paste:").Foreground));
              Assert.Equal(secondary, Of(Lines(panel.Children.OfType<ItemsControl>().Single()).Single().Foreground));
            }
          });
        } finally {
          WindowThemes.Apply(WindowThemeKind.Light);
        }
      });
    }

    // Throwaway-free picture of every kind, with and without a comment, when MDSHO_MOCKUP_DIR is set.
    [Fact]
    public void Render_each_kind_when_asked() {
      string folder = Environment.GetEnvironmentVariable("MDSHO_MOCKUP_DIR");
      if (string.IsNullOrEmpty(folder)) { return; }
      RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.SoftwareOnly;
      WithPanel(f => {
        ShortcutViewModel group = Group(f);
        group.Comment = "Servers of team A";
        List<(string Title, ShortcutViewModel S)> kinds = new List<(string, ShortcutViewModel)>();
        void Pair(string title, Func<ShortcutViewModel> make, string comment) {
          kinds.Add((title + ", with a comment", make()));
          kinds.Last().S.Comment = comment;
          kinds.Add((title + ", without a comment", make()));
        }
        Pair("Link", () => Link(f, "https://example.com/reports"), "Used for the weekly report. Ask Sam before changing it.");
        Pair("File", () => Link(f, @"C:\Work\Reports\weekly.xlsx"), "The weekly numbers.");
        Pair("Run command", () => {
          ShortcutViewModel c = Command(f);
          c.ProcessFileName = "powershell";
          c.ProcessArguments = "-NoProfile -File \"C:\\Scripts\\backup.ps1\" -Target D:\\Backup -Verbose -LogFile D:\\Logs\\backup.log";
          return c;
        }, "Keeps pinging until closed.");
        Pair("Note", () => Note(f, "Shopping list for the weekend at the market and the bakery\r\nmilk\r\nbread and butter"), "Not used: a note has no comment.");
        Pair("Snippet", () => Snippet(f, "Kind regards,\r\nAlex"), "For mail");
        kinds.Add(("Empty snippet", Snippet(f, "")));
        kinds.Add(("Note of 14 lines", Note(f, Many(14))));
        kinds.Add(("Group with a comment", group));
        ShortcutViewModel err = Command(f, Group(f));
        err.ProcessArguments = "/K ping {=1}";
        err.Comment = "Asks first.";
        kinds.Add(("Run command with an error", err));
        foreach (WindowThemeKind theme in new[] { WindowThemeKind.Light, WindowThemeKind.Dark }) {
          List<(string Title, RenderTargetBitmap Shot, double W, double H)> shots = new List<(string, RenderTargetBitmap, double, double)>();
          try {
            WindowThemes.Apply(theme);
            WithToolTips(f.Win, kinds.Select(k => k.S).ToArray(), (s, button, tip) => {
              for (int i = 0; i < 3; i++) { System.Threading.Thread.Sleep(80); WpfAppHost.Pump(); }
              FrameworkElement root = (FrameworkElement)tip;
              RenderTargetBitmap shot = new RenderTargetBitmap((int)Math.Ceiling(root.ActualWidth * 2), (int)Math.Ceiling(root.ActualHeight * 2), 192, 192, PixelFormats.Pbgra32);
              shot.Render(root);
              shots.Add((kinds.First(k => k.S == s).Title, shot, root.ActualWidth, root.ActualHeight));
            });
          } finally {
            WindowThemes.Apply(WindowThemeKind.Light);
          }
          double width = 2 * 460 + 48;
          double y = 16;
          DrawingVisual visual = new DrawingVisual();
          Typeface face = new Typeface("Segoe UI");
          using (DrawingContext dc = visual.RenderOpen()) {
            int rows = (shots.Count + 1) / 2;
            double[] heights = Enumerable.Range(0, rows).Select(r => shots.Skip(r * 2).Take(2).Max(s => s.H) + 30).ToArray();
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0xE8, 0xE4, 0xD4)), null, new Rect(0, 0, width, heights.Sum() + 32));
            for (int r = 0; r < rows; r++) {
              for (int c = 0; c < 2 && r * 2 + c < shots.Count; c++) {
                var s = shots[r * 2 + c];
                double x = 16 + c * (460 + 16);
                dc.DrawText(new FormattedText(s.Title + " (" + theme + ")", System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, face, 12, Brushes.Black, 1.0), new Point(x, y));
                dc.DrawImage(s.Shot, new Rect(x, y + 20, s.W, s.H));
              }
              y += heights[r];
            }
          }
          RenderTargetBitmap all = new RenderTargetBitmap((int)(width * 2), (int)((y + 16) * 2), 192, 192, PixelFormats.Pbgra32);
          all.Render(visual);
          PngBitmapEncoder png = new PngBitmapEncoder();
          png.Frames.Add(BitmapFrame.Create(all));
          using (FileStream file = File.Create(Path.Combine(folder, "tooltip-" + theme.ToString().ToLowerInvariant() + ".png"))) { png.Save(file); }
        }
      });
    }
  }
}
