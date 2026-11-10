using System.Collections.Generic;
using MDSHO.Common;
using MDSHO.Core;

namespace MDSHO.Data {

  internal sealed class DesktopWindowCloneRepository : RepositoryBase, IDesktopWindowCloneRepository {
    /// <summary>Column list for SELECTs: the database column ParentMDSHOWindowID is exposed as the property ParentWindowID.</summary>
    private const string SelectColumns = "ID, ParentMDSHOWindowID AS ParentWindowID, ScreenDeviceName, Left, Top, Width, Height";

    private const string InsertSql = @"
      INSERT INTO MDSHOWindowClones (ID, ParentMDSHOWindowID, ScreenDeviceName, Left, Top, Width, Height)
      VALUES (NULLIF(@ID, 0), @ParentWindowID, @ScreenDeviceName, @Left, @Top, @Width, @Height)";

    private const string UpdateSql = @"
      UPDATE MDSHOWindowClones SET
        ParentMDSHOWindowID = @ParentWindowID, ScreenDeviceName = @ScreenDeviceName, Left = @Left, Top = @Top,
        Width = @Width, Height = @Height
      WHERE ID = @ID";

    public DesktopWindowClone Get(long id) {
      return One<DesktopWindowClone>("SELECT " + SelectColumns + " FROM MDSHOWindowClones WHERE ID = @id", new { id });
    }

    public List<DesktopWindowClone> GetAll() {
      return Many<DesktopWindowClone>("SELECT " + SelectColumns + " FROM MDSHOWindowClones ORDER BY ID ASC");
    }

    public List<DesktopWindowClone> GetForWindow(long parentWindowID) {
      return Many<DesktopWindowClone>("SELECT " + SelectColumns + " FROM MDSHOWindowClones WHERE ParentMDSHOWindowID = @parentWindowID ORDER BY ID ASC", new { parentWindowID });
    }

    public DesktopWindowClone Create(long parentWindowID, string screenDeviceName, long left, long top) {
      return InTransaction(() => {
        DesktopWindowClone clone = new DesktopWindowClone(parentWindowID, screenDeviceName, left, top, AppConstants.InitialWindowWidth, AppConstants.InitialWindowHeight);
        return Get(Add(clone));
      });
    }

    public long Add(DesktopWindowClone clone) {
      clone.ID = Insert(InsertSql, clone);
      return clone.ID;
    }

    public void Update(DesktopWindowClone clone) {
      Run(UpdateSql, clone);
    }

    public void Delete(long id) {
      Run("DELETE FROM MDSHOWindowClones WHERE ID = @id", new { id });
    }

    public void UpdateLeftTopHeight(long id, long left, long top, long height) {
      Run("UPDATE MDSHOWindowClones SET Left = @left, Top = @top, Height = @height WHERE ID = @id;", new { id, left, top, height });
    }
    public void UpdateLeftTopWidthHeight(long id, long left, long top, long width, long height) {
      Run("UPDATE MDSHOWindowClones SET Left = @left, Top = @top, Width = @width, Height = @height WHERE ID = @id;", new { id, left, top, width, height });
    }
    public void UpdateLeftTopScreenDeviceName(long id, long left, long top, string screenDeviceName) {
      Run("UPDATE MDSHOWindowClones SET Left = @left, Top = @top, ScreenDeviceName = @screenDeviceName WHERE ID = @id;", new { id, left, top, screenDeviceName });
    }
    public void UpdateWidthHeightScreenDeviceName(long id, long width, long height, string screenDeviceName) {
      Run("UPDATE MDSHOWindowClones SET Width = @width, Height = @height, ScreenDeviceName = @screenDeviceName WHERE ID = @id;", new { id, width, height, screenDeviceName });
    }
  }

}
