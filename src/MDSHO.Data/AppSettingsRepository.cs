using MDSHO.Core;

namespace MDSHO.Data {

  /// <summary>The AppSettings table: one row per setting, keyed by its name (see <see cref="IAppSettingsRepository"/>).</summary>
  internal sealed class AppSettingsRepository : RepositoryBase, IAppSettingsRepository {

    public string Get(string key) {
      return One<string>("SELECT Value FROM AppSettings WHERE Key = @key", new { key });
    }

    public void Set(string key, string value) {
      Run("INSERT INTO AppSettings (Key, Value) VALUES (@key, @value) ON CONFLICT(Key) DO UPDATE SET Value = excluded.Value",
        new { key, value = value ?? "" });
    }
  }
}
