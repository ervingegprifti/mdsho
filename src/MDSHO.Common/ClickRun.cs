using System;

namespace MDSHO.Common {

  /// <summary>
  /// Counts a run of quick clicks. WPF measures its double-click time from the first click of a run, so at a normal pace
  /// the 4th click comes too late and WPF counts it as 1. Here each click only has to come within the double-click time
  /// and spot of the click before; a WPF count above 1 always wins, and a WPF 1 continues a run of 3 or more.
  /// </summary>
  public sealed class ClickRun {
    private int count;
    private int lastTime;
    private double lastX, lastY;

    /// <summary>The number of this click in its run (1 for a single click).</summary>
    public int Next(int wpfCount, int time, double x, double y, int doubleClickTime, double maxDx, double maxDy) {
      bool continues = this.count > 0 && unchecked(time - this.lastTime) >= 0 && unchecked(time - this.lastTime) <= doubleClickTime
        && Math.Abs(x - this.lastX) <= maxDx && Math.Abs(y - this.lastY) <= maxDy;
      if (wpfCount > 1) {
        this.count = continues ? Math.Max(wpfCount, this.count + 1) : wpfCount;
      } else {
        this.count = continues && this.count >= 3 ? this.count + 1 : 1;
      }
      this.lastTime = time;
      this.lastX = x;
      this.lastY = y;
      return this.count;
    }
  }
}
