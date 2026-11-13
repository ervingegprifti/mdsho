using System.Collections.Generic;
using System.Linq;
using MDSHO.Common;
using MDSHO.Core;

namespace MDSHO.Data {

  internal sealed class DesktopWindowRepository : RepositoryBase, IDesktopWindowRepository {
    private const string InsertSql = @"
      INSERT INTO MDSHOWindows (
        ID, Title, ScreenDeviceName, Left, Top, Width, Height, BorderColor, BackgroundColor, TextColor,
        TitleBackgroundColor, TitleTextColor, Notes, SolidIcons, SingleClickToOpen, CommentWrapText, ReserveDockedSpace)
      VALUES (
        NULLIF(@ID, 0), @Title, @ScreenDeviceName, @Left, @Top, @Width, @Height, @BorderColor, @BackgroundColor, @TextColor,
        @TitleBackgroundColor, @TitleTextColor, @Notes, @SolidIcons, @SingleClickToOpen, @CommentWrapText, @ReserveDockedSpace)";

    private const string UpdateSql = @"
      UPDATE MDSHOWindows SET
        Title = @Title, ScreenDeviceName = @ScreenDeviceName, Left = @Left, Top = @Top, Width = @Width, Height = @Height,
        BorderColor = @BorderColor, BackgroundColor = @BackgroundColor, TextColor = @TextColor,
        TitleBackgroundColor = @TitleBackgroundColor, TitleTextColor = @TitleTextColor, Notes = @Notes,
        SolidIcons = @SolidIcons, SingleClickToOpen = @SingleClickToOpen, CommentWrapText = @CommentWrapText,
        ReserveDockedSpace = @ReserveDockedSpace
      WHERE ID = @ID";

    private readonly IDesktopWindowCloneRepository clones;

    public DesktopWindowRepository(IDesktopWindowCloneRepository clones) {
      this.clones = clones;
    }

    #region LOAD
    public List<DesktopWindow> LoadAll() {
      // One scope = one connection and one consistent snapshot for all the queries below.
      return InTransaction(() => {
        List<DesktopWindow> windows = GetAll();
        if (windows.Count == 0) {
          // This is the first time the application is run, so we just create the first DesktopWindow to start with.
          windows.Add(Create());
        }
        List<Shortcut> allShortcuts = Many<Shortcut>("SELECT " + ShortcutRepository.SelectColumns + " FROM Shortcuts ORDER BY Position ASC, ID ASC");
        List<DesktopWindowClone> allClones = clones.GetAll();

        // Build the tree in memory. The lists keep the Position order of the query.
        Dictionary<long, Shortcut> byId = allShortcuts.ToDictionary(s => s.ID);
        Dictionary<long, DesktopWindow> windowById = windows.ToDictionary(w => w.ID);
        foreach (Shortcut shortcut in allShortcuts) {
          if (shortcut.ParentShortcutID != null) {
            Shortcut parent;
            if (byId.TryGetValue(shortcut.ParentShortcutID.Value, out parent)) {
              parent.Shortcuts.Add(shortcut);
            }
          } else if (shortcut.ParentWindowID != null) {
            DesktopWindow window;
            if (windowById.TryGetValue(shortcut.ParentWindowID.Value, out window)) {
              window.RootShortcuts.Add(shortcut);
            }
          }
        }
        foreach (DesktopWindowClone clone in allClones) {
          DesktopWindow window;
          if (windowById.TryGetValue(clone.ParentWindowID, out window)) {
            clone.Shortcuts = window.RootShortcuts; // clones show the window's shortcuts
            window.Clones.Add(clone);
          }
        }
        return windows;
      });
    }
    #endregion LOAD

    #region CRUD
    public DesktopWindow Get(long id) {
      return One<DesktopWindow>("SELECT * FROM MDSHOWindows WHERE ID = @id", new { id });
    }

    public List<DesktopWindow> GetAll() {
      return Many<DesktopWindow>("SELECT * FROM MDSHOWindows ORDER BY ID ASC");
    }

    public DesktopWindow Create(string screenDeviceName = "", long left = 0, long top = 0) {
      return InTransaction(() => {
        DesktopWindow window = new DesktopWindow(
          title: "MDSHO", // (Multi Desktop Shortcut Organizer)
          screenDeviceName: screenDeviceName,
          left: left,
          top: top,
          width: AppConstants.InitialWindowWidth,
          height: AppConstants.InitialWindowHeight,
          solidIcons: true,
          borderColor: AppConstants.DefaultWindowBorderColor,
          backgroundColor: AppConstants.DefaultWindowBackgroundColor,
          textColor: AppConstants.DefaultWindowTextColor,
          titleBackgroundColor: AppConstants.DefaultWindowTitleBackgroundColor,
          titleTextColor: AppConstants.DefaultWindowTitleTextColor,
          notes: "Notes for this window.",
          singleClickToOpen: false);
        return Get(Add(window));
      });
    }

    public long Add(DesktopWindow window) {
      window.ID = Insert(InsertSql, window);
      return window.ID;
    }

    public void Update(DesktopWindow window) {
      Run(UpdateSql, window);
    }

    public void Delete(long id) {
      Run("DELETE FROM MDSHOWindows WHERE ID = @id", new { id });
    }
    #endregion CRUD

    #region PARTIAL UPDATES
    public void UpdateTitle(long id, string title) {
      Run("UPDATE MDSHOWindows SET Title = @title WHERE ID = @id;", new { id, title });
    }
    public void UpdateBorderColor(long id, string borderColor) {
      Run("UPDATE MDSHOWindows SET BorderColor = @borderColor WHERE ID = @id;", new { id, borderColor });
    }
    public void UpdateBackgroundColor(long id, string backgroundColor) {
      Run("UPDATE MDSHOWindows SET BackgroundColor = @backgroundColor WHERE ID = @id;", new { id, backgroundColor });
    }
    public void UpdateTextColor(long id, string textColor) {
      Run("UPDATE MDSHOWindows SET TextColor = @textColor WHERE ID = @id;", new { id, textColor });
    }
    public void UpdateTitleBackgroundColor(long id, string titleBackgroundColor) {
      Run("UPDATE MDSHOWindows SET TitleBackgroundColor = @titleBackgroundColor WHERE ID = @id;", new { id, titleBackgroundColor });
    }
    public void UpdateTitleTextColor(long id, string titleTextColor) {
      Run("UPDATE MDSHOWindows SET TitleTextColor = @titleTextColor WHERE ID = @id;", new { id, titleTextColor });
    }
    public void UpdateSolidIcons(long id, bool solidIcons) {
      Run("UPDATE MDSHOWindows SET SolidIcons = @solidIcons WHERE ID = @id;", new { id, solidIcons });
    }
    public void UpdateSingleClickToOpen(long id, bool singleClickToOpen) {
      Run("UPDATE MDSHOWindows SET SingleClickToOpen = @singleClickToOpen WHERE ID = @id;", new { id, singleClickToOpen });
    }
    public void UpdateReserveDockedSpace(long id, bool reserveDockedSpace) {
      Run("UPDATE MDSHOWindows SET ReserveDockedSpace = @reserveDockedSpace WHERE ID = @id;", new { id, reserveDockedSpace });
    }
    public void UpdateCommentWrapText(long id, bool commentWrapText) {
      Run("UPDATE MDSHOWindows SET CommentWrapText = @commentWrapText WHERE ID = @id;", new { id, commentWrapText });
    }
    public void UpdateLeftTopHeight(long id, long left, long top, long height) {
      Run("UPDATE MDSHOWindows SET Left = @left, Top = @top, Height = @height WHERE ID = @id;", new { id, left, top, height });
    }
    public void UpdateLeftTopWidthHeight(long id, long left, long top, long width, long height) {
      Run("UPDATE MDSHOWindows SET Left = @left, Top = @top, Width = @width, Height = @height WHERE ID = @id;", new { id, left, top, width, height });
    }
    public void UpdateLeftTopScreenDeviceName(long id, long left, long top, string screenDeviceName) {
      Run("UPDATE MDSHOWindows SET Left = @left, Top = @top, ScreenDeviceName = @screenDeviceName WHERE ID = @id;", new { id, left, top, screenDeviceName });
    }
    public void UpdateWidthHeightScreenDeviceName(long id, long width, long height, string screenDeviceName) {
      Run("UPDATE MDSHOWindows SET Width = @width, Height = @height, ScreenDeviceName = @screenDeviceName WHERE ID = @id;", new { id, width, height, screenDeviceName });
    }
    #endregion PARTIAL UPDATES
  }

}
