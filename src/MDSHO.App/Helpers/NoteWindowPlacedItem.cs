using System.Collections.Generic;
using System.Windows;
using MDSHO.Common;
using MDSHO.ViewModels;

namespace MDSHO.Helpers {

  /// <summary>An open note window as a placed item.</summary>
  public sealed class NoteWindowPlacedItem : IPlacedItem {
    private readonly TextEditWindow window;

    public NoteWindowPlacedItem(TextEditWindow window) {
      this.window = window;
    }

    public PlacedKind Kind => PlacedKind.Note;
    public long ID => this.window.Shortcut.ID;
    /// <summary>The normal (not maximized) place, in DIPs.</summary>
    public ScreenRect Rect {
      get {
        Rect r = this.window.NormalBounds;
        return r.IsEmpty ? new ScreenRect(this.window.Left, this.window.Top, 0, 0) : GeometryConversions.ToScreenRect(r);
      }
    }
    public string ScreenDeviceName => "";

    /// <summary>Moves the window. Its own place is saved shortly after, as for any move.</summary>
    public void MoveTo(ItemPlace target, IList<ScreenInfo> screens) {
      this.window.MoveNormalBoundsTo(GeometryConversions.ToRect(target.Rect));
    }
  }
}
