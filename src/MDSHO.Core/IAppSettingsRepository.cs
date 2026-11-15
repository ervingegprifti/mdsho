namespace MDSHO.Core {

  /// <summary>
  /// Settings of the whole app on this PC (the AppSettings table): one text value per key. They are not part of exports,
  /// and an import that replaces the data keeps them.
  /// </summary>
  public interface IAppSettingsRepository {
    /// <summary>The saved value, or null when the setting was never saved (then its default applies).</summary>
    string Get(string key);
    /// <summary>Saves the value (adds the row the first time, replaces it after that).</summary>
    void Set(string key, string value);
  }
}
