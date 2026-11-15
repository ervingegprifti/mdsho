using System.Collections.Generic;
using System.Windows;
using MDSHO.Common;
using MDSHO.ViewModels;

namespace MDSHO.Helpers {

  /// <summary>An open note window as an <see cref="IPlacedItem"/>: its normal place and size, keyed by the note button's ID.</summary>
  public sealed class NoteWindowPlacedItem : IPlacedItem {
    private readonly TextEditWindow window;

    /// <summary>The item of this note window.</summary>
    public NoteWindowPlacedItem(TextEditWindow window) {
      this.window = window;
    }

    /// <summary>Always a note.</summary>
    public PlacedKind Kind => PlacedKind.Note;
    /// <summary>The note button's ID.</summary>
    public long ID => this.window.Shortcut.ID;
    /// <summary>The window's normal place and size (DIPs).</summary>
    public ScreenRect Rect {
      get {
        Rect r = this.window.NormalBounds;
        return r.IsEmpty ? new ScreenRect(this.window.Left, this.window.Top, 0, 0) : GeometryConversions.ToScreenRect(r);
      }
    }
    /// <summary>Note windows don't store a screen name.</summary>
    public string ScreenDeviceName => "";

    /// <summary>Moves the window; its own place (NoteWindows) is saved shortly after, as for any move.</summary>
    public void MoveTo(ItemPlace target, IList<ScreenInfo> screens) {
      this.window.MoveNormalBoundsTo(GeometryConversions.ToRect(target.Rect));
    }
  }
}
