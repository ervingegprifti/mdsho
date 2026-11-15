using System;
using System.Collections.Generic;
using System.Linq;
using MDSHO.Common;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// Bring panels to front and send them back: what is remembered when the panels come up, and how the panels, the
  /// window that was in front before and the keyboard focus are put back. The windows are fakes (handles are numbers).
  /// </summary>
  public class PanelsToFrontTests {

    /// <summary>A desktop of numbered windows: <see cref="ZOrder"/> is top first.</summary>
    private sealed class FakeWindows : IPanelWindows {
      public List<long> ZOrder = new List<long>();
      public HashSet<long> PanelHandles = new HashSet<long>();
      public HashSet<long> OwnHandles = new HashSet<long>();
      public HashSet<long> Topmost = new HashSet<long>();
      public HashSet<long> Closed = new HashSet<long>();
      public long ForegroundHandle;
      public readonly List<string> Calls = new List<string>();

      public IntPtr Foreground() { return (IntPtr)ForegroundHandle; }
      public bool IsAppWindow(IntPtr window) { return PanelHandles.Contains((long)window) || OwnHandles.Contains((long)window); }
      public IList<IntPtr> Panels() { return ZOrder.Where(PanelHandles.Contains).Select(h => (IntPtr)h).ToList(); }
      public IntPtr WindowAbove(IntPtr window) {
        int i = ZOrder.IndexOf((long)window);
        return i > 0 ? (IntPtr)ZOrder[i - 1] : IntPtr.Zero;
      }
      // Every window of the fake desktop is shown on screen, so all of them count.
      public IList<IntPtr> WindowsAbove(IntPtr window) {
        int i = ZOrder.IndexOf((long)window);
        return ZOrder.Take(Math.Max(i, 0)).Reverse().Select(h => (IntPtr)h).ToList();
      }
      public IList<IntPtr> WindowsBelow(IntPtr window) {
        int i = ZOrder.IndexOf((long)window);
        return i < 0 ? new List<IntPtr>() : ZOrder.Skip(i + 1).Select(h => (IntPtr)h).ToList();
      }
      public bool IsAlive(IntPtr window) { return window != IntPtr.Zero && !Closed.Contains((long)window); }
      public bool IsTopmost(IntPtr window) { return Topmost.Contains((long)window); }
      public void Raise(IList<IntPtr> panels) {
        Calls.Add("raise " + string.Join(",", panels.Select(p => (long)p)));
        // The panels come to the top, in the order given, and the first one gets the focus.
        foreach (IntPtr p in panels.Reverse()) {
          ZOrder.Remove((long)p);
          ZOrder.Insert(0, (long)p);
        }
        ForegroundHandle = (long)panels[0];
      }
      public void PlaceBehind(IntPtr panel, IntPtr insertAfter) {
        Calls.Add("place " + (long)panel + " behind " + (long)insertAfter);
        ZOrder.Remove((long)panel);
        ZOrder.Insert(insertAfter == IntPtr.Zero ? 0 : ZOrder.IndexOf((long)insertAfter) + 1, (long)panel);
      }
      public void Activate(IntPtr window) {
        Calls.Add("activate " + (long)window);
        ZOrder.Remove((long)window);
        // A normal window comes up to the top of the normal windows, below the always-on-top ones.
        int top = ZOrder.FindIndex(h => !Topmost.Contains(h));
        ZOrder.Insert(top < 0 ? ZOrder.Count : top, (long)window);
        ForegroundHandle = (long)window;
      }
    }

    // An editor (100) in front, a browser (200) behind it, then panel 1, a mail app (300) and panel 2.
    private static FakeWindows Desktop() {
      FakeWindows w = new FakeWindows { ZOrder = new List<long> { 100, 200, 1, 300, 2 }, ForegroundHandle = 100 };
      w.PanelHandles.UnionWith(new long[] { 1, 2 });
      return w;
    }

    [Fact]
    public void The_first_press_brings_every_panel_to_the_front_top_panel_first() {
      FakeWindows w = Desktop();
      PanelsToFront p = new PanelsToFront(w);
      Assert.False(p.IsInFront);
      p.Toggle();
      Assert.True(p.IsInFront);
      Assert.Equal(new[] { "raise 1,2" }, w.Calls);
      Assert.Equal(new long[] { 1, 2, 100, 200, 300 }, w.ZOrder);
    }

    [Fact]
    public void The_second_press_puts_the_panels_back_where_they_were_and_the_previous_window_gets_the_focus() {
      FakeWindows w = Desktop();
      PanelsToFront p = new PanelsToFront(w);
      p.Toggle();
      w.Calls.Clear();
      p.Toggle();
      Assert.False(p.IsInFront);
      // Top panel first, so the lower one can go back behind it.
      Assert.Equal(new[] { "place 1 behind 200", "place 2 behind 300", "activate 100" }, w.Calls);
      Assert.Equal(new long[] { 100, 200, 1, 300, 2 }, w.ZOrder);
      Assert.Equal(100, w.ForegroundHandle);
    }

    [Fact]
    public void A_panel_right_below_another_panel_goes_back_below_it() {
      FakeWindows w = new FakeWindows { ZOrder = new List<long> { 100, 1, 2, 300 }, ForegroundHandle = 100 };
      w.PanelHandles.UnionWith(new long[] { 1, 2 });
      PanelsToFront p = new PanelsToFront(w);
      p.Toggle();
      p.Toggle();
      Assert.Equal(new long[] { 100, 1, 2, 300 }, w.ZOrder);
    }

    [Fact]
    public void A_panel_that_was_on_top_or_below_a_closed_or_always_on_top_window_goes_back_to_the_top_of_the_normal_windows() {
      FakeWindows w = new FakeWindows { ZOrder = new List<long> { 900, 1, 100, 200, 2 }, ForegroundHandle = 100 };
      w.PanelHandles.UnionWith(new long[] { 1, 2 });
      w.Topmost.Add(900);
      PanelsToFront p = new PanelsToFront(w);
      p.Toggle();
      w.Calls.Clear();
      w.Closed.Add(200);
      p.Toggle();
      Assert.Equal(new[] { "place 1 behind 0", "place 2 behind 0", "activate 100" }, w.Calls);
    }

    [Fact]
    public void Esc_sends_the_panels_back_only_while_they_are_in_front_and_nothing_is_being_typed() {
      FakeWindows w = Desktop();
      PanelsToFront p = new PanelsToFront(w);
      Assert.False(p.HandleEscape(isEditing: false));
      Assert.Empty(w.Calls);
      p.Toggle();
      Assert.False(p.HandleEscape(isEditing: true));
      Assert.True(p.IsInFront);
      Assert.True(p.HandleEscape(isEditing: false));
      Assert.False(p.IsInFront);
      Assert.Equal("activate 100", w.Calls.Last());
      Assert.False(p.HandleEscape(isEditing: false));
    }

    [Fact]
    public void Opening_a_button_sends_the_panels_back_and_leaves_the_focus_to_what_opened() {
      FakeWindows w = Desktop();
      PanelsToFront p = new PanelsToFront(w);
      p.ButtonOpened();
      Assert.Empty(w.Calls);
      p.Toggle();
      w.Calls.Clear();
      p.ButtonOpened();
      Assert.False(p.IsInFront);
      Assert.Equal(new[] { "place 1 behind 200", "place 2 behind 300" }, w.Calls);
    }

    [Fact]
    public void After_the_user_switches_to_another_app_the_next_press_brings_the_panels_up_again() {
      FakeWindows w = Desktop();
      PanelsToFront p = new PanelsToFront(w);
      p.Toggle();
      // The user clicks the mail app: it comes up over the panels.
      w.Activate((IntPtr)300);
      p.AppDeactivated();
      Assert.False(p.IsInFront);
      w.Calls.Clear();
      p.Toggle();
      Assert.True(p.IsInFront);
      Assert.Equal(new[] { "raise 1,2" }, w.Calls);
    }

    [Fact]
    public void When_an_MDSHO_window_was_in_front_no_window_is_activated_on_the_way_back() {
      FakeWindows w = Desktop();
      w.OwnHandles.Add(100); // for example a note window
      PanelsToFront p = new PanelsToFront(w);
      p.Toggle();
      w.Calls.Clear();
      p.Toggle();
      Assert.DoesNotContain(w.Calls, c => c.StartsWith("activate"));
    }

    [Fact]
    public void A_previous_window_that_was_closed_meanwhile_is_not_activated() {
      FakeWindows w = Desktop();
      PanelsToFront p = new PanelsToFront(w);
      p.Toggle();
      w.Closed.Add(100);
      w.Calls.Clear();
      p.Toggle();
      Assert.DoesNotContain(w.Calls, c => c.StartsWith("activate"));
    }

    // ---- Panels that are already in front without the shortcut ----

    [Fact]
    public void When_the_user_clicked_a_panel_the_first_press_sends_the_panels_back_behind_the_app_windows_below_them() {
      // Panel 1 was clicked (in front, focused); panel 2 sits between the browser and the mail app.
      FakeWindows w = new FakeWindows { ZOrder = new List<long> { 1, 100, 200, 2, 300 }, ForegroundHandle = 1 };
      w.PanelHandles.UnionWith(new long[] { 1, 2 });
      PanelsToFront p = new PanelsToFront(w);
      p.Toggle();
      Assert.Equal(new[] { "place 1 behind 100", "place 2 behind 300", "activate 100" }, w.Calls);
      Assert.Equal(new long[] { 100, 1, 200, 300, 2 }, w.ZOrder);
      Assert.Equal(100, w.ForegroundHandle);
      Assert.False(p.IsInFront);
      // The next press brings them up again.
      w.Calls.Clear();
      p.Toggle();
      Assert.StartsWith("raise", w.Calls.Single());
      Assert.True(p.IsInFront);
    }

    [Fact]
    public void Panels_on_top_of_every_app_window_go_back_together_keeping_their_order() {
      // Nothing is focused (the desktop); only an always-on-top window and MDSHO windows are above the panels.
      FakeWindows w = new FakeWindows { ZOrder = new List<long> { 900, 1, 50, 2, 100, 200 }, ForegroundHandle = 0 };
      w.PanelHandles.UnionWith(new long[] { 1, 2 });
      w.OwnHandles.Add(50); // a note window
      w.Topmost.Add(900);
      PanelsToFront p = new PanelsToFront(w);
      p.Toggle();
      Assert.Equal(new[] { "place 1 behind 100", "place 2 behind 1", "activate 100" }, w.Calls);
      Assert.Equal(new long[] { 900, 100, 50, 1, 2, 200 }, w.ZOrder);
      Assert.False(p.IsInFront);
    }

    [Fact]
    public void Sending_back_skips_MDSHO_windows_and_panels_below_a_panel() {
      FakeWindows w = new FakeWindows { ZOrder = new List<long> { 1, 50, 2, 100 }, ForegroundHandle = 1 };
      w.PanelHandles.UnionWith(new long[] { 1, 2 });
      w.OwnHandles.Add(50);
      PanelsToFront p = new PanelsToFront(w);
      p.Toggle();
      Assert.Equal(new[] { "place 1 behind 100", "place 2 behind 1", "activate 100" }, w.Calls);
    }

    [Fact]
    public void A_panel_with_no_app_window_below_stays_where_it_is() {
      // Panel 2 is already the lowest window: only panel 1 moves.
      FakeWindows w = new FakeWindows { ZOrder = new List<long> { 1, 100, 2 }, ForegroundHandle = 1 };
      w.PanelHandles.UnionWith(new long[] { 1, 2 });
      PanelsToFront p = new PanelsToFront(w);
      p.Toggle();
      Assert.Equal(new[] { "place 1 behind 100", "activate 100" }, w.Calls);
    }

    [Fact]
    public void Panels_in_front_with_no_app_window_anywhere_below_are_left_alone() {
      FakeWindows w = new FakeWindows { ZOrder = new List<long> { 1, 2 }, ForegroundHandle = 1 };
      w.PanelHandles.UnionWith(new long[] { 1, 2 });
      PanelsToFront p = new PanelsToFront(w);
      p.Toggle();
      Assert.Empty(w.Calls);
      Assert.False(p.IsInFront);
    }

    [Fact]
    public void A_focused_note_with_panels_covered_by_other_apps_brings_the_panels_up() {
      // An MDSHO note is in front, but the browser covers the panels: they come up.
      FakeWindows w = new FakeWindows { ZOrder = new List<long> { 50, 100, 1, 200 }, ForegroundHandle = 50 };
      w.PanelHandles.Add(1);
      w.OwnHandles.Add(50);
      PanelsToFront p = new PanelsToFront(w);
      p.Toggle();
      Assert.Equal(new[] { "raise 1" }, w.Calls);
      Assert.True(p.IsInFront);
    }

    [Fact]
    public void A_focused_note_right_above_the_panels_counts_as_in_front() {
      FakeWindows w = new FakeWindows { ZOrder = new List<long> { 50, 1, 100 }, ForegroundHandle = 50 };
      w.PanelHandles.Add(1);
      w.OwnHandles.Add(50);
      PanelsToFront p = new PanelsToFront(w);
      p.Toggle();
      Assert.Equal(new[] { "place 1 behind 100", "activate 100" }, w.Calls);
    }

    [Fact]
    public void One_covered_panel_is_enough_to_bring_them_all_up() {
      // Panel 1 is on top, but the mail app covers panel 2.
      FakeWindows w = new FakeWindows { ZOrder = new List<long> { 1, 100, 2 }, ForegroundHandle = 100 };
      w.PanelHandles.UnionWith(new long[] { 1, 2 });
      PanelsToFront p = new PanelsToFront(w);
      p.Toggle();
      Assert.Equal(new[] { "raise 1,2" }, w.Calls);
    }

    [Fact]
    public void Esc_does_nothing_in_panels_that_are_in_front_without_the_shortcut() {
      FakeWindows w = new FakeWindows { ZOrder = new List<long> { 1, 100 }, ForegroundHandle = 1 };
      w.PanelHandles.Add(1);
      PanelsToFront p = new PanelsToFront(w);
      Assert.False(p.HandleEscape(isEditing: false));
      p.ButtonOpened();
      Assert.Empty(w.Calls);
    }

    [Fact]
    public void Without_visible_panels_a_press_does_nothing() {
      FakeWindows w = new FakeWindows { ZOrder = new List<long> { 100 }, ForegroundHandle = 100 };
      PanelsToFront p = new PanelsToFront(w);
      p.Toggle();
      Assert.False(p.IsInFront);
      Assert.Empty(w.Calls);
    }
  }
}
