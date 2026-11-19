using System;
using System.Collections.Generic;
using MDSHO.Common;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// Explorer style "click, pause, click" rename: a slow second click on the same item starts renaming once a further
  /// double-click time passes with no other click; a fast double click, a drag or a click elsewhere does not.
  /// </summary>
  public class SlowClickRenameTests {

    private const int DoubleClick = 500;
    private const double DragWidth = 4, DragHeight = 4;

    private sealed class FakeTimer : IOneShotTimer {
      public TimeSpan? Delay;
      private Action elapsed;
      public bool Running => elapsed != null;
      public void Start(TimeSpan delay, Action elapsed) { Delay = delay; this.elapsed = elapsed; }
      public void Stop() { elapsed = null; }
      public void Fire() {
        Action a = elapsed;
        elapsed = null;
        a?.Invoke();
      }
    }

    private long now = 10_000;
    private readonly FakeTimer timer = new FakeTimer();
    private readonly List<object> renamed = new List<object>();
    private readonly SlowClickRename slow;
    private readonly object itemA = "A", itemB = "B";

    public SlowClickRenameTests() {
      slow = new SlowClickRename(() => now, timer, DoubleClick, DragWidth, DragHeight, renamed.Add);
    }

    private void Click(object target, double x = 10, double y = 10, bool canRename = true) {
      slow.Press(target, x, y, canRename);
      slow.Release();
    }

    /// <summary>Lets time pass and fires the timer if it is due by then.</summary>
    private void Wait(int ms) {
      now += ms;
      if (timer.Running && timer.Delay.HasValue && ms >= timer.Delay.Value.TotalMilliseconds) {
        timer.Fire();
      }
    }

    [Fact]
    public void A_slow_second_click_renames_after_a_further_double_click_time() {
      Click(itemA);
      Wait(800);
      Click(itemA);
      Assert.True(slow.IsWaiting);
      Assert.Empty(renamed);
      Assert.Equal(TimeSpan.FromMilliseconds(DoubleClick), timer.Delay);
      Wait(DoubleClick);
      Assert.Equal(new[] { itemA }, renamed);
      Assert.False(slow.IsWaiting);
    }

    [Fact]
    public void One_click_alone_never_renames() {
      Click(itemA);
      Assert.False(slow.IsWaiting);
      Assert.False(timer.Running);
      Wait(5000);
      Assert.Empty(renamed);
    }

    [Fact]
    public void A_fast_double_click_does_not_rename() {
      Click(itemA);
      Wait(200);
      Click(itemA);
      Assert.False(slow.IsWaiting);
      Wait(5000);
      Assert.Empty(renamed);
    }

    [Fact]
    public void A_click_exactly_at_the_double_click_time_still_counts_as_a_double_click() {
      Click(itemA);
      Wait(DoubleClick);
      Click(itemA);
      Assert.False(slow.IsWaiting);
    }

    [Fact]
    public void A_double_click_after_the_slow_click_cancels_the_rename() {
      // Slow click arms, then a quick extra click turns it into a double click (which opens the item).
      Click(itemA);
      Wait(800);
      Click(itemA);
      Wait(150);
      Click(itemA);
      Assert.False(slow.IsWaiting);
      Wait(5000);
      Assert.Empty(renamed);
    }

    [Fact]
    public void After_a_double_click_a_later_slow_click_renames() {
      Click(itemA);
      Wait(150);
      Click(itemA);
      Wait(1000);
      Click(itemA);
      Wait(DoubleClick);
      Assert.Equal(new[] { itemA }, renamed);
    }

    [Fact]
    public void A_click_on_another_item_in_between_starts_over() {
      Click(itemA);
      Wait(800);
      Click(itemB);
      Wait(800);
      Assert.False(slow.IsWaiting);
      Click(itemA);
      Assert.False(slow.IsWaiting);
      Wait(5000);
      Assert.Empty(renamed);
    }

    [Fact]
    public void A_click_elsewhere_while_waiting_cancels_the_rename() {
      Click(itemA);
      Wait(800);
      Click(itemA);
      Wait(100);
      Click(itemB);
      Wait(5000);
      Assert.Empty(renamed);
    }

    [Fact]
    public void Cancel_forgets_the_first_click() {
      Click(itemA);
      Wait(800);
      slow.Cancel();
      Click(itemA);
      Assert.False(slow.IsWaiting);
    }

    [Fact]
    public void Cancel_while_waiting_stops_the_rename() {
      Click(itemA);
      Wait(800);
      Click(itemA);
      slow.Cancel();
      Assert.False(slow.IsWaiting);
      Assert.False(timer.Running);
      Wait(5000);
      Assert.Empty(renamed);
    }

    [Fact]
    public void A_click_that_cannot_rename_counts_as_a_first_click() {
      // E.g. the button was not selected yet, or it is being edited.
      Click(itemA);
      Wait(800);
      Click(itemA, canRename: false);
      Assert.False(slow.IsWaiting);
      Wait(800);
      Click(itemA);
      Assert.True(slow.IsWaiting);
    }

    [Fact]
    public void Moving_past_the_drag_distance_with_the_button_down_cancels() {
      Click(itemA);
      Wait(800);
      slow.Press(itemA, 10, 10, true);
      slow.Move(10 + DragWidth, 10);
      slow.Release();
      Assert.False(slow.IsWaiting);
      Wait(5000);
      Assert.Empty(renamed);
    }

    [Fact]
    public void Moving_down_past_the_drag_distance_also_cancels() {
      Click(itemA);
      Wait(800);
      slow.Press(itemA, 10, 10, true);
      slow.Move(10, 10 - DragHeight);
      Assert.False(slow.IsWaiting);
    }

    [Fact]
    public void Dragging_on_the_first_click_forgets_it() {
      slow.Press(itemA, 10, 10, true);
      slow.Move(30, 30);
      slow.Release();
      Wait(800);
      Click(itemA);
      Assert.False(slow.IsWaiting);
    }

    [Fact]
    public void A_small_wobble_within_the_drag_distance_still_renames() {
      Click(itemA);
      Wait(800);
      slow.Press(itemA, 10, 10, true);
      slow.Move(12, 8);
      slow.Release();
      Wait(DoubleClick);
      Assert.Equal(new[] { itemA }, renamed);
    }

    [Fact]
    public void While_the_button_is_still_held_the_rename_waits_for_the_release() {
      Click(itemA);
      Wait(800);
      slow.Press(itemA, 10, 10, true);
      Wait(DoubleClick);
      Assert.Empty(renamed);
      Assert.True(slow.IsWaiting);
      slow.Release();
      Assert.Equal(new[] { itemA }, renamed);
    }

    [Fact]
    public void A_release_before_the_time_is_up_does_not_rename_early() {
      Click(itemA);
      Wait(800);
      slow.Press(itemA, 10, 10, true);
      now += 100;
      slow.Release();
      Assert.Empty(renamed);
      Assert.True(timer.Running);
    }

    [Fact]
    public void It_renames_only_once() {
      Click(itemA);
      Wait(800);
      Click(itemA);
      Wait(DoubleClick);
      slow.Release();
      timer.Fire();
      Assert.Single(renamed);
    }

    [Fact]
    public void A_third_slow_click_after_a_rename_needs_two_new_clicks() {
      Click(itemA);
      Wait(800);
      Click(itemA);
      Wait(DoubleClick);
      Assert.Single(renamed);
      // The rename forgets the clicks, so the next click is a first click again.
      Wait(800);
      Click(itemA);
      Assert.False(slow.IsWaiting);
    }

    [Fact]
    public void A_null_target_cancels() {
      Click(itemA);
      Wait(800);
      Click(null);
      Assert.False(slow.IsWaiting);
      Wait(800);
      Click(itemA);
      Assert.False(slow.IsWaiting);
    }

    [Fact]
    public void Arguments_are_checked() {
      Assert.Throws<ArgumentNullException>(() => new SlowClickRename(null, timer, DoubleClick, DragWidth, DragHeight, _ => { }));
      Assert.Throws<ArgumentNullException>(() => new SlowClickRename(() => 0, null, DoubleClick, DragWidth, DragHeight, _ => { }));
      Assert.Throws<ArgumentNullException>(() => new SlowClickRename(() => 0, timer, DoubleClick, DragWidth, DragHeight, null));
      Assert.Throws<ArgumentOutOfRangeException>(() => new SlowClickRename(() => 0, timer, 0, DragWidth, DragHeight, _ => { }));
    }
  }
}
