using System.Text;

namespace MDSHO.Common {

  /// <summary>Parts of the file names MDSHO suggests or writes (exports and backups).</summary>
  public static class FileNames {
    /// <summary>The PC name with only letters, digits, - and _ (others become _); "PC" when empty.</summary>
    public static string SafePcName(string name) {
      if (string.IsNullOrEmpty(name)) {
        return "PC";
      }
      StringBuilder safe = new StringBuilder(name.Length);
      foreach (char c in name) {
        safe.Append(char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '_');
      }
      return safe.ToString();
    }
  }
}
