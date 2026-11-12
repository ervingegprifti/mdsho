using System.Collections.Generic;

namespace MDSHO.Core {

  /// <summary>Persistence of <see cref="DesktopWindow"/> (MDSHOWindows table).</summary>
  public interface IDesktopWindowRepository {
    /// <summary>
    /// Loads every window with its whole shortcut tree (ordered by Position) and its clones, in a fixed number of queries.
    /// The clones share their window's <see cref="DesktopWindow.RootShortcuts"/> list.
    /// When there is no window yet (first run) the first one is created.
    /// </summary>
    List<DesktopWindow> LoadAll();
    /// <summary>The window row (without shortcuts and clones) or null.</summary>
    DesktopWindow Get(long id);
    /// <summary>All window rows (without shortcuts and clones).</summary>
    List<DesktopWindow> GetAll();
    /// <summary>Creates a window with the default title, size and colors and returns the stored row.</summary>
    DesktopWindow Create(string screenDeviceName = "", long left = 0, long top = 0);
    /// <summary>Inserts the window row (with its ID when not 0, otherwise a new one is assigned), sets <see cref="DesktopWindow.ID"/> and returns it.</summary>
    long Add(DesktopWindow window);
    /// <summary>Writes every column of the row.</summary>
    void Update(DesktopWindow window);
    /// <summary>Deletes the window; its clones and shortcuts are deleted by the database (cascade).</summary>
    void Delete(long id);

    /// <summary>Saves the title.</summary>
    void UpdateTitle(long id, string title);
    /// <summary>Saves the border colour.</summary>
    void UpdateBorderColor(long id, string borderColor);
    /// <summary>Saves the background colour.</summary>
    void UpdateBackgroundColor(long id, string backgroundColor);
    /// <summary>Saves the text colour.</summary>
    void UpdateTextColor(long id, string textColor);
    /// <summary>Saves the header background colour.</summary>
    void UpdateTitleBackgroundColor(long id, string titleBackgroundColor);
    /// <summary>Saves the header text colour.</summary>
    void UpdateTitleTextColor(long id, string titleTextColor);
    /// <summary>Saves the solid icons setting.</summary>
    void UpdateSolidIcons(long id, bool solidIcons);
    /// <summary>Saves the single click setting.</summary>
    void UpdateSingleClickToOpen(long id, bool singleClickToOpen);
    /// <summary>Saves whether the window's Comment windows wrap their text.</summary>
    void UpdateCommentWrapText(long id, bool commentWrapText);
    /// <summary>Saves position and height.</summary>
    void UpdateLeftTopHeight(long id, long left, long top, long height);
    /// <summary>Saves position and size.</summary>
    void UpdateLeftTopWidthHeight(long id, long left, long top, long width, long height);
    /// <summary>Saves position and the screen it is on.</summary>
    void UpdateLeftTopScreenDeviceName(long id, long left, long top, string screenDeviceName);
    /// <summary>Saves size and the screen it is on.</summary>
    void UpdateWidthHeightScreenDeviceName(long id, long width, long height, string screenDeviceName);
  }

}
