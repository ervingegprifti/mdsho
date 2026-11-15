namespace MDSHO.Core {

  /// <summary>App-wide settings on this PC, one text value per key. Not exported; kept by an import.</summary>
  public interface IAppSettingsRepository {
    /// <summary>The saved value, or null if never saved.</summary>
    string Get(string key);
    /// <summary>Saves the value.</summary>
    void Set(string key, string value);
  }
}
