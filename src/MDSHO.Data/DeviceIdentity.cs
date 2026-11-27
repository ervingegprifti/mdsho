using System;

namespace MDSHO.Data {

  /// <summary>
  /// This PC as sync sees it: a DeviceId (a GUID made once, the ChangedBy of every change made here) and a device name
  /// (the Windows computer name unless set). Both are local app settings: never exported, imported or synced.
  /// </summary>
  public static class DeviceIdentity {
    /// <summary>The AppSettings key of the device ID.</summary>
    public const string IdKey = "DeviceId";
    /// <summary>The AppSettings key of the device name.</summary>
    public const string NameKey = "DeviceName";

    /// <summary>This PC's device ID; made (and saved) the first time it is needed.</summary>
    public static string Id {
      get {
        string id = DataStore.AppSettings.Get(IdKey);
        if (string.IsNullOrEmpty(id)) {
          // The database makes it at creation and upgrade; this only covers a deleted row.
          id = Guid.NewGuid().ToString();
          DataStore.AppSettings.Set(IdKey, id);
        }
        return id;
      }
    }

    /// <summary>
    /// This PC's name: the saved name, else the Windows computer name (a sync lab profile: its name). An empty name goes
    /// back to that.
    /// </summary>
    public static string Name {
      get {
        string name = DataStore.AppSettings.Get(NameKey);
        return string.IsNullOrWhiteSpace(name) ? Common.LabOptions.Current.Profile ?? Environment.MachineName : name;
      }
      set {
        if (string.IsNullOrWhiteSpace(value)) {
          DataStore.AppSettings.Delete(NameKey);
        } else {
          DataStore.AppSettings.Set(NameKey, value.Trim());
        }
      }
    }
  }
}
