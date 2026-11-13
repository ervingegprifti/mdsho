using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using MDSHO.Common;
using MDSHO.Helpers;
using MDSHO.ViewModels;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// The Note colours window opens beside its note, 20 px away: on the right when there is room, otherwise on the left,
  /// otherwise on the side with more room, always inside the work area. Its top lines up with the note's top.
  /// </summary>
  public class NoteColoursPlacementTests {

    // A 1920x1080 screen at 100 % with a 40 px taskbar at the bottom.
    private static readonly ScreenRect Work = new ScreenRect(0, 0, 1920, 1040);

    // ---- Geometry (physical pixels) ----

    [Fact]
    public void With_room_on_the_right_it_opens_on_the_right_with_the_gap_and_the_tops_lined_up() {
      ScreenPoint p = ScreenGeometry.BesideWindowPhysical(new ScreenRect(300, 200, 500, 400), 600, 500, Work, 20);
      Assert.Equal(300 + 500 + 20, p.X);
      Assert.Equal(200, p.Y);
    }

    [Fact]
    public void Without_room_on_the_right_it_opens_on_the_left() {
      ScreenPoint p = ScreenGeometry.BesideWindowPhysical(new ScreenRect(1200, 200, 500, 400), 600, 500, Work, 20);
      Assert.Equal(1200 - 20 - 600, p.X);
      Assert.Equal(200, p.Y);
    }

    [Fact]
    public void Without_room_on_either_side_it_takes_the_side_with_more_room_and_stays_in_the_work_area() {
      // 700 px free on the left, 520 on the right: the left side, pushed in to the work area's left edge.
      ScreenPoint p = ScreenGeometry.BesideWindowPhysical(new ScreenRect(700, 200, 700, 400), 800, 500, Work, 20);
      Assert.Equal(0, p.X);
      // 520 px free on the right, 300 on the left: the right side, pushed in to the work area's right edge.
      ScreenPoint q = ScreenGeometry.BesideWindowPhysical(new ScreenRect(300, 200, 1100, 400), 800, 500, Work, 20);
      Assert.Equal(1920 - 800, q.X);
    }

    [Fact]
    public void A_note_low_on_the_screen_moves_the_window_up_so_it_stays_above_the_taskbar() {
      ScreenPoint p = ScreenGeometry.BesideWindowPhysical(new ScreenRect(300, 800, 500, 200), 600, 500, Work, 20);
      Assert.Equal(1040 - 500, p.Y);
      ScreenPoint high = ScreenGeometry.BesideWindowPhysical(new ScreenRect(300, -50, 500, 200), 600, 500, Work, 20);
      Assert.Equal(0, high.Y);
    }

    [Fact]
    public void It_works_on_a_screen_left_of_the_primary() {
      ScreenRect work = new ScreenRect(-1920, 0, 1920, 1040);
      ScreenPoint p = ScreenGeometry.BesideWindowPhysical(new ScreenRect(-1000, 100, 500, 400), 600, 500, work, 20);
      Assert.Equal(-1000 - 20 - 600, p.X);
    }

    // ---- Dialog placement through the window handle ----

    private sealed class FakeHandles {
      public readonly Dictionary<Window, Rect> Rects = new Dictionary<Window, Rect>();
      public readonly List<Point> Moves = new List<Point>();
      public Rect BoundsOf(Window w) {
        return Rects.TryGetValue(w, out Rect r) ? r : new Rect(w.Left, w.Top, w.Width, w.Height);
      }
      public void Move(Window w, Point p) {
        Moves.Add(p);
        Rects[w] = new Rect(p, BoundsOf(w).Size);
      }
    }

    private static void WithFakes(ScreenInfo screen, Action<FakeHandles> body) {
      FakeHandles fake = new FakeHandles();
      Func<Window, ScreenInfo> oldScreen = DialogPlacement.ScreenOf;
      Func<Window, Rect> oldBounds = DialogPlacement.PhysicalBoundsOf;
      Action<Window, Point> oldMove = DialogPlacement.MoveToPhysical;
      DialogPlacement.ScreenOf = w => screen;
      DialogPlacement.PhysicalBoundsOf = fake.BoundsOf;
      DialogPlacement.MoveToPhysical = fake.Move;
      try {
        body(fake);
      } finally {
        DialogPlacement.ScreenOf = oldScreen;
        DialogPlacement.PhysicalBoundsOf = oldBounds;
        DialogPlacement.MoveToPhysical = oldMove;
      }
    }

    [Fact]
    public void The_dialog_is_moved_beside_its_owner_with_the_gap_scaled_for_the_screen() {
      WpfAppHost.Run(() => {
        // A 150 % screen: 20 DIPs are 30 physical pixels.
        ScreenInfo screen = new ScreenInfo("D1", new ScreenRect(0, 0, 3840, 2160), new ScreenRect(0, 0, 3840, 2100), 1.5, true);
        WithFakes(screen, fake => {
          Window owner = new Window { WindowStartupLocation = WindowStartupLocation.Manual, Left = -10000, Top = -10000, Width = 300, Height = 300, ShowActivated = false };
          Window dialog = new Window { Width = 400, Height = 350, ShowActivated = false };
          owner.Show();
          try {
            fake.Rects[owner] = new Rect(600, 300, 750, 600);
            fake.Rects[dialog] = new Rect(0, 0, 600, 525);
            DialogPlacement.BesideWindow(dialog, owner, 20);
            Assert.Same(owner, dialog.Owner);
            dialog.Show();
            WpfAppHost.Pump();
            Assert.Equal(new Point(600 + 750 + 30, 300), fake.Rects[dialog].TopLeft);
          } finally {
            dialog.Close();
            owner.Close();
          }
        });
      });
    }

    [Fact]
    public void The_colours_button_opens_Note_colours_beside_the_note_20_pixels_away() {
      WpfAppHost.Run(() => {
        Action<ShortcutViewModel> oldNoteEditor = ShortcutViewModel.OpenNoteEditor;
        Func<Window, bool?> oldShow = TextEditWindow.ShowNoteColoursDialog;
        ShortcutViewModel.OpenNoteEditor = s => { };
        try {
          using (TempDb db = new TempDb()) {
            AppViewModel app = new AppViewModel();
            DesktopWindowViewModel win = app.AddWindow("TEST", 0, 0);
            win.NewNoteShortcutCommand.Execute(null);
            ShortcutViewModel note = win.RootShortcuts.Last();
            ScreenInfo screen = new ScreenInfo("D1", new ScreenRect(0, 0, 1920, 1080), Work, 1.0, true);
            WithFakes(screen, fake => {
              TextEditWindow window = TextEditWindow.ForNote(note);
              window.WindowStartupLocation = WindowStartupLocation.Manual;
              window.Left = -10000;
              window.Top = -10000;
              window.ShowActivated = false;
              window.Show();
              Point? placed = null;
              Window shown = null;
              try {
                fake.Rects[window] = new Rect(1300, 150, 500, 400);
                TextEditWindow.ShowNoteColoursDialog = w => {
                  shown = w;
                  fake.Rects[w] = new Rect(0, 0, 560, 537);
                  w.ShowActivated = false;
                  w.Show();
                  WpfAppHost.Pump();
                  placed = fake.Rects[w].TopLeft;
                  w.Close();
                  return false;
                };
                System.Windows.Controls.Button colours = (System.Windows.Controls.Button)window.FindName("ButtonColors");
                colours.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent, colours));
                WpfAppHost.Pump();
              } finally {
                window.Close();
                WpfAppHost.Pump();
              }
              Assert.IsType<ColorsWindow>(shown);
              Assert.Same(window, shown.Owner);
              // No room on the right of a note at x 1300..1800: on its left, 20 px away.
              Assert.Equal(new Point(1300 - 20 - 560, 150), placed);
            });
          }
        } finally {
          ShortcutViewModel.OpenNoteEditor = oldNoteEditor;
          TextEditWindow.ShowNoteColoursDialog = oldShow;
        }
      });
    }
  }
}
