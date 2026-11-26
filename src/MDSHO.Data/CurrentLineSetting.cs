using System;
using MDSHO.Common;

namespace MDSHO.Data {

  /// <summary>
  /// "Highlight current line" in the More options menu of notes, comments and snippets, app-wide: on unless set Off. It is
  /// a row of the AppSettings table (no new database version), and exports carry it (see <see cref="PortableSettings"/>).
  /// </summary>
  public static class CurrentLineSetting {
    /// <summary>The AppSettings key: "On" or "Off"; no row or any other value means on.</summary>
    public const string SettingKey = "HighlightCurrentLine";

    /// <summary>Raised after <see cref="SetOn"/>, so every open window follows.</summary>
    public static event Action Changed;

    /// <summary>True unless the saved value is Off. Never throws.</summary>
    public static bool IsOn {
      get {
        try {
          return DataStore.AppSettings.Get(SettingKey) != "Off";
        } catch (Exception ex) {
          AppLog.Warning(ex, "The current line setting could not be read; it is on");
          return true;
        }
      }
    }

    /// <summary>Saves the choice and tells the open windows.</summary>
    public static void SetOn(bool on) {
      DataStore.AppSettings.Set(SettingKey, on ? "On" : "Off");
      Changed?.Invoke();
    }
  }
}
