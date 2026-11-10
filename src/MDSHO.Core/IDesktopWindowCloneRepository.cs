using System.Collections.Generic;

namespace MDSHO.Core {

  /// <summary>Persistence of <see cref="DesktopWindowClone"/> (MDSHOWindowClones table).</summary>
  public interface IDesktopWindowCloneRepository {
    /// <summary>The clone row or null.</summary>
    DesktopWindowClone Get(long id);
    /// <summary>All clone rows.</summary>
    List<DesktopWindowClone> GetAll();
    /// <summary>The clones of one window.</summary>
    List<DesktopWindowClone> GetForWindow(long parentWindowID);
    /// <summary>Creates a clone with the default size and returns the stored row.</summary>
    DesktopWindowClone Create(long parentWindowID, string screenDeviceName, long left, long top);
    /// <summary>Inserts the clone row (with its ID when not 0, otherwise a new one is assigned), sets <see cref="DesktopWindowClone.ID"/> and returns it.</summary>
    long Add(DesktopWindowClone clone);
    /// <summary>Writes every column of the row.</summary>
    void Update(DesktopWindowClone clone);
    /// <summary>Deletes the clone.</summary>
    void Delete(long id);

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
