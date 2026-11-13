using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shell;
using MDSHO.Controls;
using MDSHO.Data;
using MDSHO.ViewModels;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// The Note, Comment, Run command, Examples, Settings (and Note colours), About and Error windows round their corners
  /// the way the panels do: a see-through window that draws its own 5 px rounded frame with the border on top, the same
  /// on Windows 10 and 11, square while maximised. Settings, About and Error have MDSHO's own title bar for it.
  /// </summary>
  public class RoundedWindowsTests {

    private sealed class Fixture {
      public AppViewModel App;
      public DesktopWindowViewModel Win;
      public ShortcutViewModel Group;
      public ShortcutViewModel Note;
      public ShortcutViewModel Command;
    }

    private static void WithFixture(Action<Fixture> body) {
      WpfAppHost.Run(() => {
        Action<ShortcutViewModel> oldNoteEditor = ShortcutViewModel.OpenNoteEditor;
        Action<ShortcutViewModel, bool> oldRunEditor = ShortcutViewModel.OpenRunCommandEditor;
        ShortcutViewModel.OpenNoteEditor = s => { };
        ShortcutViewModel.OpenRunCommandEditor = (s, isNew) => { };
        try {
          using (TempDb db = new TempDb()) {
            Fixture f = new Fixture();
            f.App = new AppViewModel();
            f.Win = f.App.AddWindow("TEST", 0, 0);
            f.Group = ViewModelFactory.NewGroupShortcut(f.App, null, f.Win);
            f.Win.RootShortcuts.Add(f.Group);
            f.Group.NewNoteShortcutCommand.Execute(null);
            f.Note = f.Group.Children.Last();
            f.Command = ViewModelFactory.NewProcessCommandShortcut(f.App, f.Group, null);
            f.Group.Children.Add(f.Command);
            body(f);
          }
        } finally {
          ShortcutViewModel.OpenNoteEditor = oldNoteEditor;
          ShortcutViewModel.OpenRunCommandEditor = oldRunEditor;
        }
      });
    }

    public static IEnumerable<object[]> AllWindows() {
      foreach (string name in new[] { "Note", "Comment", "RunCommand", "Examples", "Settings", "Colours", "NoteColours", "About", "Error" }) {
        yield return new object[] { name };
      }
    }

    private static Window Create(string name, Fixture f) {
      switch (name) {
        case "Note": return TextEditWindow.ForNote(f.Note);
        case "Comment": return TextEditWindow.ForComment(f.Group);
        case "RunCommand": return new RunCommandWindow(new RunCommandViewModel(f.Command, false));
        case "Examples": return new RunCommandExamplesWindow(new RunCommandViewModel(f.Command, false));
        case "Settings": return new SettingsWindow(f.Win);
        case "Colours": return new ColorsWindow(f.Win);
        case "NoteColours": return new ColorsWindow(f.Note);
        case "About": return new AboutWindow();
        case "Error": return new ErrorWindow(new InvalidOperationException("Test error"));
        default: throw new ArgumentException(name);
      }
    }

    private static void Shown(Window window, Action check) {
      window.WindowStartupLocation = WindowStartupLocation.Manual;
      window.Left = -10000;
      window.Top = -10000;
      window.ShowActivated = false;
      window.Show();
      try {
        WpfAppHost.Pump();
        check();
      } finally {
        window.Close();
        WpfAppHost.Pump();
      }
    }

    private static ClippingBorder Frame(Window window) {
      return Assert.IsType<ClippingBorder>(VisualTreeHelper.GetChild(window, 0));
    }

    [Theory]
    [MemberData(nameof(AllWindows))]
    public void The_window_is_see_through_with_its_own_5_px_rounded_frame(string name) {
      WithFixture(f => {
        Window window = Create(name, f);
        Assert.True(window.AllowsTransparency);
        Assert.Equal(WindowStyle.None, window.WindowStyle);
        Shown(window, () => {
          ClippingBorder frame = Frame(window);
          Assert.Equal(new CornerRadius(5), frame.CornerRadius);
          Assert.Equal(new Thickness(1), frame.BorderThickness);
          // The border is drawn on top of everything inside the frame, in the window's border colour.
          Assert.Same(window.BorderBrush, frame.BorderBrush);
          Assert.NotNull(frame.Background);
          Assert.NotEqual(Colors.Transparent, ((SolidColorBrush)frame.Background).Color);
          // What is inside is clipped to the inner rounded corner, so a title bar can't poke out of the corners.
          RectangleGeometry clip = Assert.IsType<RectangleGeometry>(((UIElement)frame.Child).Clip);
          Assert.Equal(4, clip.RadiusX);
          Assert.Equal(4, clip.RadiusY);
          // A title bar to drag the window by.
          Assert.Equal(32, WindowChrome.GetWindowChrome(window).CaptionHeight);
        });
      });
    }

    [Fact]
    public void The_frame_goes_square_while_maximised() {
      Assert.Equal(new CornerRadius(5), ClippingBorder.CornersFor(WindowState.Normal));
      Assert.Equal(new CornerRadius(5), ClippingBorder.CornersFor(WindowState.Minimized));
      Assert.Equal(new CornerRadius(0), ClippingBorder.CornersFor(WindowState.Maximized));
      WithFixture(f => {
        Window window = Create("Note", f);
        Shown(window, () => {
          BindingExpression binding = BindingOperations.GetBindingExpression(Frame(window), Border.CornerRadiusProperty);
          Assert.NotNull(binding);
          Assert.Equal("WindowState", binding.ParentBinding.Path.Path);
        });
      });
    }

    [Fact]
    public void A_square_frame_clips_with_square_corners() {
      WpfAppHost.Run(() => {
        ClippingBorder frame = new ClippingBorder { CornerRadius = new CornerRadius(0), BorderThickness = new Thickness(1), Child = new Grid() };
        frame.Measure(new Size(100, 50));
        frame.Arrange(new Rect(0, 0, 100, 50));
        RectangleGeometry clip = Assert.IsType<RectangleGeometry>(frame.Child.Clip);
        Assert.Equal(0, clip.RadiusX);
        Assert.Equal(new Rect(0, 0, 98, 48), clip.Rect);
      });
    }

    [Fact]
    public void The_note_window_border_follows_the_note_colour() {
      WithFixture(f => {
        Window window = Create("Note", f);
        Shown(window, () => {
          f.Note.NoteColors.BorderColor = "#FFFF0000";
          WpfAppHost.Pump();
          Assert.Equal(Color.FromRgb(0xFF, 0, 0), ((SolidColorBrush)Frame(window).BorderBrush).Color);
        });
      });
    }

    // ---- MDSHO's own title bar on Settings, About and Error ----

    public static IEnumerable<object[]> DialogWindows() {
      foreach (string name in new[] { "Settings", "Colours", "NoteColours", "About", "Error" }) {
        yield return new object[] { name };
      }
    }

    [Theory]
    [MemberData(nameof(DialogWindows))]
    public void The_dialog_has_a_title_bar_with_its_icon_title_and_close(string name) {
      WithFixture(f => {
        Window window = Create(name, f);
        bool closed = false;
        window.Closed += (s, e) => closed = true;
        Shown(window, () => {
          DialogTitleBar bar = Assert.IsType<DialogTitleBar>(window.FindName("TitleBar"));
          Assert.Equal(window.Title, bar.Text);
          Assert.False(string.IsNullOrEmpty(bar.Glyph));
          Button close = Assert.IsType<Button>(bar.FindName("ButtonClose"));
          Assert.Equal("Close", System.Windows.Automation.AutomationProperties.GetName(close));
          Assert.True(WindowChrome.GetIsHitTestVisibleInChrome(close));
          // The title bar sits at the top of the window.
          FrameworkElement content = (FrameworkElement)window.Content;
          Assert.Equal(0, bar.TransformToAncestor(content).Transform(new Point(0, 0)).Y, 1);
          Assert.Equal(32, bar.ActualHeight, 1);
          close.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, close));
          WpfAppHost.Pump();
          Assert.True(closed);
        });
      });
    }

    [Fact]
    public void The_settings_title_bar_shows_the_gear_and_both_colours_windows_the_brush_following_the_icon_set() {
      WithFixture(f => {
        IconGlyphs icons = f.Win.Appearance.Icons;
        bool solid = icons.SolidIcons;
        try {
          SettingsWindow settings = new SettingsWindow(f.Win);
          Shown(settings, () => {
            DialogTitleBar bar = (DialogTitleBar)settings.FindName("TitleBar");
            Assert.Equal(icons.Settings, bar.Glyph);
            Assert.Equal(icons.Times, bar.CloseGlyph);
            icons.SolidIcons = !solid;
            WpfAppHost.Pump();
            Assert.Equal(icons.Settings, bar.Glyph);
            Assert.Equal(icons.Times, bar.CloseGlyph);
          });
          ColorsWindow colours = new ColorsWindow(f.Note);
          Shown(colours, () => Assert.Equal(icons.Brush, ((DialogTitleBar)colours.FindName("TitleBar")).Glyph));
          ColorsWindow panelColours = new ColorsWindow(f.Win);
          Shown(panelColours, () => Assert.Equal(icons.Brush, ((DialogTitleBar)panelColours.FindName("TitleBar")).Glyph));
        } finally {
          icons.SolidIcons = solid;
        }
      });
    }

    [Fact]
    public void The_about_title_bar_shows_info_and_the_error_title_bar_the_warning() {
      WithFixture(f => {
        AboutWindow about = new AboutWindow();
        Shown(about, () => Assert.Equal(new IconGlyphs(true).Info, ((DialogTitleBar)about.FindName("TitleBar")).Glyph));
        ErrorWindow error = new ErrorWindow(new InvalidOperationException("x"));
        Shown(error, () => Assert.Equal(new IconGlyphs(true).Warning, ((DialogTitleBar)error.FindName("TitleBar")).Glyph));
      });
    }
  }
}
