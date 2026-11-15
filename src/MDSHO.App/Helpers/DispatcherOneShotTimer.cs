using System;
using System.Windows.Threading;

namespace MDSHO.Helpers {

  /// <summary>A one-shot timer on the UI thread's dispatcher, so the call back runs on the UI thread.</summary>
  public sealed class DispatcherOneShotTimer : Common.IOneShotTimer {
    private DispatcherTimer timer;

    /// <summary>Calls <paramref name="elapsed"/> once after <paramref name="delay"/>, instead of any call still waiting.</summary>
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

    /// <summary>Drops the waiting call, if any.</summary>
    public void Stop() {
      this.timer?.Stop();
      this.timer = null;
    }
  }
}
