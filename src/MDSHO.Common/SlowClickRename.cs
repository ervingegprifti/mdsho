using System;

namespace MDSHO.Common {

  /// <summary>
  /// Explorer style "click, pause, click" rename, without any UI. The caller reports left mouse presses, moves while the
  /// button is down, and releases. A press on the same item as the last press, coming after the double-click time,
  /// starts a wait of one more double-click time; if no other press comes, the item is renamed (after the release, if
  /// the button is still down). A fast double click, a move past the drag distance, a press on another item or
  /// <see cref="Cancel"/> forgets the clicks.
  /// </summary>
  public sealed class SlowClickRename {
    private readonly Func<long> nowMs;
    private readonly IOneShotTimer timer;
    private readonly int doubleClickTimeMs;
    private readonly double dragWidth;
    private readonly double dragHeight;
    private readonly Action<object> rename;

    private object last;
    private long lastTime;
    private double lastX, lastY;
    private bool pressed;
    private bool due;

    /// <param name="nowMs">The time in milliseconds (e.g. Environment.TickCount64).</param>
    /// <param name="timer">Waits the double-click time before renaming.</param>
    /// <param name="doubleClickTimeMs">The system double-click time.</param>
    /// <param name="dragWidth">The system drag distance across.</param>
    /// <param name="dragHeight">The system drag distance down.</param>
    /// <param name="rename">Renames the item that was clicked slowly.</param>
    public SlowClickRename(Func<long> nowMs, IOneShotTimer timer, int doubleClickTimeMs, double dragWidth, double dragHeight, Action<object> rename) {
      this.nowMs = nowMs ?? throw new ArgumentNullException(nameof(nowMs));
      this.timer = timer ?? throw new ArgumentNullException(nameof(timer));
      if (doubleClickTimeMs <= 0) { throw new ArgumentOutOfRangeException(nameof(doubleClickTimeMs)); }
      this.doubleClickTimeMs = doubleClickTimeMs;
      this.dragWidth = dragWidth;
      this.dragHeight = dragHeight;
      this.rename = rename ?? throw new ArgumentNullException(nameof(rename));
    }

    /// <summary>True from a slow second click until the rename starts or is cancelled.</summary>
    public bool IsWaiting { get; private set; }

    /// <summary>
    /// A left mouse press on <paramref name="target"/> (null for anything that can't be renamed) at a point.
    /// With <paramref name="canRename"/> false (e.g. the item was not selected yet) it only counts as a first click.
    /// </summary>
    public void Press(object target, double x, double y, bool canRename) {
      if (target == null) {
        Cancel();
        return;
      }
      this.timer.Stop();
      this.IsWaiting = false;
      this.due = false;
      long now = this.nowMs();
      bool slow = this.last != null && Equals(this.last, target) && now - this.lastTime > this.doubleClickTimeMs;
      this.last = target;
      this.lastTime = now;
      this.lastX = x;
      this.lastY = y;
      this.pressed = true;
      if (slow && canRename) {
        this.IsWaiting = true;
        this.timer.Start(TimeSpan.FromMilliseconds(this.doubleClickTimeMs), Elapsed);
      }
    }

    /// <summary>A mouse move while the button is down; past the drag distance it forgets the clicks.</summary>
    public void Move(double x, double y) {
      if (!this.pressed || this.last == null) { return; }
      if (Math.Abs(x - this.lastX) >= this.dragWidth || Math.Abs(y - this.lastY) >= this.dragHeight) {
        Cancel();
      }
    }

    /// <summary>The left button was let go.</summary>
    public void Release() {
      this.pressed = false;
      if (this.IsWaiting && this.due) {
        Fire();
      }
    }

    /// <summary>Forgets the clicks and drops a waiting rename.</summary>
    public void Cancel() {
      this.timer.Stop();
      this.last = null;
      this.IsWaiting = false;
      this.due = false;
      this.pressed = false;
    }

    private void Elapsed() {
      if (!this.IsWaiting) { return; }
      if (this.pressed) {
        // Still held: rename on the release, unless it turns into a drag.
        this.due = true;
        return;
      }
      Fire();
    }

    private void Fire() {
      object target = this.last;
      Cancel();
      this.rename(target);
    }
  }
}
