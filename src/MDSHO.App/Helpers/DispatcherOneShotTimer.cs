using System;
using System.Windows.Threading;

namespace MDSHO.Helpers {

  /// <summary>A one-shot timer that calls back on the UI thread.</summary>
  public sealed class DispatcherOneShotTimer : Common.IOneShotTimer {
    private DispatcherTimer timer;

    public void Start(TimeSpan delay, Action elapsed) {
      Stop();
      DispatcherTimer started = new DispatcherTimer(DispatcherPriority.Normal) { Interval = delay };
      started.Tick += (s, e) => {
        started.Stop();
        if (ReferenceEquals(this.timer, started)) {
          this.timer = null;
        }
        elapsed();
      };
      this.timer = started;
      started.Start();
    }

    public void Stop() {
      this.timer?.Stop();
      this.timer = null;
    }
  }
}
