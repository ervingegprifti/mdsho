using System.Collections.Generic;
using MDSHO.Core;

namespace MDSHO.Data {

  /// <summary>The NoteWindows table: one row per note window (see <see cref="INoteWindowRepository"/>).</summary>
  internal sealed class NoteWindowRepository : RepositoryBase, INoteWindowRepository {
    private const string SelectColumns = "ID, ShortcutID, Left, Top, Width, Height, State, IsOpen, IsTopmost, IsWrapText";

    public NoteWindowPlace Get(long shortcutID) {
      return One<NoteWindowPlace>("SELECT " + SelectColumns + " FROM NoteWindows WHERE ShortcutID = @shortcutID", new { shortcutID });
    }

    public List<NoteWindowPlace> GetOpen() {
      return Many<NoteWindowPlace>("SELECT " + SelectColumns + " FROM NoteWindows WHERE IsOpen = 1 ORDER BY ShortcutID ASC");
    }

    public void Save(NoteWindowPlace place) {
      Run(@"
        INSERT INTO NoteWindows (ShortcutID, Left, Top, Width, Height, State, IsOpen, IsTopmost, IsWrapText)
        VALUES (@ShortcutID, @Left, @Top, @Width, @Height, @State, @IsOpen, @IsTopmost, @IsWrapText)
        ON CONFLICT(ShortcutID) DO UPDATE SET
          Left = excluded.Left, Top = excluded.Top, Width = excluded.Width, Height = excluded.Height,
          State = excluded.State, IsOpen = excluded.IsOpen, IsTopmost = excluded.IsTopmost,
          IsWrapText = excluded.IsWrapText", place);
    }

    public void SetOpen(long shortcutID, bool isOpen) {
      Run("UPDATE NoteWindows SET IsOpen = @isOpen WHERE ShortcutID = @shortcutID", new { shortcutID, isOpen });
    }
  }
}
