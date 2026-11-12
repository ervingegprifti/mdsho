using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using MDSHO.Common;
using MDSHO.Helpers;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// The Comment window opens beside its panel, 20 px away (right when there is room, else left), with its title bar
  /// level with the button the comment belongs to, kept inside the work area. With no room on either side it is centred
  /// on the panel's screen.
  /// </summary>
  public class CommentPlacementTests {

    // A 1920x1080 screen at 100 % with a 40 px taskbar at the bottom.
    private static readonly ScreenRect Work = new ScreenRect(0, 0, 1920, 1040);

    // ---- Geometry (physical pixels) ----

    [Fact]
    public void With_room_on_the_right_it_opens_on_the_right_with_its_title_level_with_the_button() {
      // Panel 300..600 wide; button middle at y = 400; the window's title middle is 17 px below its top.
      ScreenPoint? p = ScreenGeometry.BesideButtonPhysical(new ScreenRect(300, 100, 300, 800), 400, 300, 150, 17, Work, 20);
      Assert.Equal(new ScreenPoint(300 + 300 + 20, 400 - 17), p);
    }

    [Fact]
    public void Without_room_on_the_right_it_opens_on_the_left() {
      ScreenPoint? p = ScreenGeometry.BesideButtonPhysical(new ScreenRect(1600, 0, 320, 1040), 400, 300, 150, 17, Work, 20);
      Assert.Equal(new ScreenPoint(1600 - 20 - 300, 400 - 17), p);
    }

    [Fact]
    public void Without_room_on_either_side_there_is_no_place_beside_the_panel() {
      Assert.Null(ScreenGeometry.BesideButtonPhysical(new ScreenRect(100, 0, 1720, 1040), 400, 300, 150, 17, Work, 20));
    }

    [Fact]
    public void A_button_near_the_top_or_bottom_keeps_the_window_inside_the_work_area() {
      ScreenPoint? low = ScreenGeometry.BesideButtonPhysical(new ScreenRect(300, 0, 300, 1040), 1030, 300, 150, 17, Work, 20);
      Assert.Equal(1040 - 150, low.Value.Y);
      ScreenPoint? high = ScreenGeometry.BesideButtonPhysical(new ScreenRect(300, 0, 300, 1040), 5, 300, 150, 17, Work, 20);
      Assert.Equal(0, high.Value.Y);
    }

    [Fact]
    public void It_works_on_a_screen_left_of_the_primary() {
      ScreenRect work = new ScreenRect(-1920, 0, 1920, 1040);
      ScreenPoint? p = ScreenGeometry.BesideButtonPhysical(new ScreenRect(-1920, 0, 320, 1040), 300, 300, 150, 17, work, 20);
      Assert.Equal(new ScreenPoint(-1920 + 320 + 20, 300 - 17), p);
    }

    // ---- Dialog placement through the window handle ----

    private sealed class FakeHandles {
      public readonly Dictionary<object, Rect> Rects = new Dictionary<object, Rect>();
      public Rect BoundsOf(Window w) {
        return Rects.TryGetValue(w, out Rect r) ? r : new Rect(w.Left, w.Top, w.Width, w.Height);
      }
      public Rect BoundsOfElement(FrameworkElement e) {
        return Rects.TryGetValue(e, out Rect r) ? r : Rect.Empty;
      }
      public void Move(Window w, Point p) {
        Rects[w] = new Rect(p, BoundsOf(w).Size);
      }
    }

    internal static void WithFakes(ScreenInfo screen, Action<FakeHandlesView> body) {
      FakeHandles fake = new FakeHandles();
      Func<Window, ScreenInfo> oldScreen = DialogPlacement.ScreenOf;
      Func<Window, Rect> oldBounds = DialogPlacement.PhysicalBoundsOf;
      Func<FrameworkElement, Rect> oldElement = DialogPlacement.PhysicalBoundsOfElement;
      Action<Window, Point> oldMove = DialogPlacement.MoveToPhysical;
      DialogPlacement.ScreenOf = w => screen;
      DialogPlacement.PhysicalBoundsOf = fake.BoundsOf;
      DialogPlacement.PhysicalBoundsOfElement = fake.BoundsOfElement;
      DialogPlacement.MoveToPhysical = fake.Move;
      try {
        body(new FakeHandlesView(fake.Rects));
      } finally {
        DialogPlacement.ScreenOf = oldScreen;
        DialogPlacement.PhysicalBoundsOf = oldBounds;
        DialogPlacement.PhysicalBoundsOfElement = oldElement;
        DialogPlacement.MoveToPhysical = oldMove;
      }
    }

    /// <summary>The fake physical rects of windows and elements, for other test classes.</summary>
    internal sealed class FakeHandlesView {
      public readonly Dictionary<object, Rect> Rects;
      public FakeHandlesView(Dictionary<object, Rect> rects) { this.Rects = rects; }
    }

    [Fact]
    public void The_dialog_is_moved_beside_its_owner_level_with_the_button_scaled_for_the_screen() {
      WpfAppHost.Run(() => {
        // A 150 % screen: the 20 DIP gap is 30 physical pixels and a 17 DIP title middle is 25.5.
        ScreenInfo screen = new ScreenInfo("D1", new ScreenRect(0, 0, 3840, 2160), new ScreenRect(0, 0, 3840, 2100), 1.5, true);
        WithFakes(screen, fake => {
          Window owner = new Window { WindowStartupLocation = WindowStartupLocation.Manual, Left = -10000, Top = -10000, Width = 300, Height = 300, ShowActivated = false };
          Button button = new Button();
          Window dialog = new Window { Width = 300, Height = 150, ShowActivated = false };
          owner.Show();
          try {
            fake.Rects[owner] = new Rect(600, 0, 450, 2100);
            fake.Rects[button] = new Rect(610, 700, 430, 36);
            fake.Rects[dialog] = new Rect(0, 0, 450, 225);
            DialogPlacement.BesideButton(dialog, owner, button, 20, 17);
            Assert.Same(owner, dialog.Owner);
            dialog.Show();
            WpfAppHost.Pump();
            Assert.Equal(new Point(600 + 450 + 30, Math.Floor(718 - 25.5)), ((Rect)fake.Rects[dialog]).TopLeft);
          } finally {
            dialog.Close();
            owner.Close();
          }
        });
      });
    }

    [Fact]
    public void With_no_room_beside_the_owner_the_dialog_is_centred_on_its_screen() {
      WpfAppHost.Run(() => {
        ScreenInfo screen = new ScreenInfo("D1", new ScreenRect(0, 0, 1920, 1080), Work, 1.0, true);
        WithFakes(screen, fake => {
          Window owner = new Window { WindowStartupLocation = WindowStartupLocation.Manual, Left = -10000, Top = -10000, Width = 300, Height = 300, ShowActivated = false };
          Button button = new Button();
          Window dialog = new Window { Width = 300, Height = 150, ShowActivated = false };
          owner.Show();
          try {
            fake.Rects[owner] = new Rect(100, 0, 1720, 1040);
            fake.Rects[button] = new Rect(110, 400, 400, 24);
            fake.Rects[dialog] = new Rect(0, 0, 300, 150);
            DialogPlacement.BesideButton(dialog, owner, button, 20, 17);
            dialog.Show();
            WpfAppHost.Pump();
            Assert.Equal(new Point((1920 - 300) / 2, (1040 - 150) / 2), ((Rect)fake.Rects[dialog]).TopLeft);
          } finally {
            dialog.Close();
            owner.Close();
          }
        });
      });
    }
  }
}
