using MDSHO.Core;

namespace MDSHO.Data {

  /// <summary>The AppSettings table.</summary>
  internal sealed class AppSettingsRepository : RepositoryBase, IAppSettingsRepository {

    public string Get(string key) {
      return One<string>("SELECT Value FROM AppSettings WHERE Key = @key", new { key });
    }

    public void Set(string key, string value) {
      Run("INSERT INTO AppSettings (Key, Value) VALUES (@key, @value) ON CONFLICT(Key) DO UPDATE SET Value = excluded.Value",
        new { key, value = value ?? "" });
    }

    public void Delete(string key) {
      Run("DELETE FROM AppSettings WHERE Key = @key", new { key });
    }
  }
}
