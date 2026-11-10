using MDSHO.Common;

namespace MDSHO.Infrastructure {

  /// <summary>The Windows clipboard. Call it on an STA thread (the UI thread).</summary>
  public sealed class ClipboardService : IClipboardService {
    /// <summary>Puts text on the clipboard (kept there after MDSHO exits).</summary>
    public void SetText(string text) {
      System.Windows.Forms.Clipboard.SetText(text);
    }
  }
}
