namespace MDSHO.Core {

  /// <summary>
  /// App-wide settings on this PC, one text value per key. Exports carry only the keys in PortableSettings; an import
  /// that replaces the data sets those and keeps the rest.
  /// </summary>
  public interface IAppSettingsRepository {
    /// <summary>The saved value, or null if never saved.</summary>
    string Get(string key);
    /// <summary>Saves the value.</summary>
    void Set(string key, string value);
    /// <summary>Removes the value, so the setting's default applies.</summary>
    void Delete(string key);
  }
}
