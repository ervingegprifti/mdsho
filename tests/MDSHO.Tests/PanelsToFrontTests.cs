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
        ZOrder.Insert(0, (long)window);
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
