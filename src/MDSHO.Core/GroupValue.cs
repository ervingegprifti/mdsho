using System;

namespace MDSHO.Core {

  /// <summary>
  /// The "group value": a group whose name holds one value in braces, e.g. <c>Test {SRV01}</c>, acts as a small profile.
  /// Run commands directly inside that group get the value in place of every <c>{}</c> in their arguments, so renaming
  /// the group retargets all of its commands.
  /// </summary>
  /// <remarks>
  /// Rules:
  /// <list type="bullet">
  /// <item>The value is the text between the first <c>{</c> of the group name and the first <c>}</c> after it, kept
  /// verbatim (spaces included, nothing trimmed). So <c>A {first} B {second}</c> gives <c>first</c>, and a nested name
  /// such as <c>{a{b}}</c> gives <c>a{b</c>.</item>
  /// <item>No value when the name has no <c>{</c>, the first <c>{</c> has no <c>}</c> after it (<c>Test {SRV</c>), or the
  /// first pair is empty (<c>Test {}</c>); a later pair is not looked at.</item>
  /// <item>In the arguments, only the exact token <c>{}</c> is replaced, every occurrence, with the value as it is (no
  /// quotes added). Without a value the arguments are used unchanged, <c>{}</c> included.</item>
  /// </list>
  /// </remarks>
  public static class GroupValue {

    /// <summary>The token in run-command arguments that is replaced with the group value.</summary>
    public const string Token = "{}";

    /// <summary>Reads the value of a group from its name.</summary>
    /// <param name="groupName">The group name as typed, e.g. <c>Test {SRV01}</c>.</param>
    /// <param name="value">The text inside the first brace pair, or null when there is no value.</param>
    /// <returns>True when the name holds a (non-empty) value.</returns>
    public static bool TryParse(string groupName, out string value) {
      value = null;
      if (string.IsNullOrEmpty(groupName)) {
        return false;
      }
      int open = groupName.IndexOf('{');
      if (open < 0) {
        return false;
      }
      int close = groupName.IndexOf('}', open + 1);
      if (close < 0 || close == open + 1) {
        return false;
      }
      value = groupName.Substring(open + 1, close - open - 1);
      return true;
    }

    /// <summary>Puts the group value in place of every <c>{}</c> in run-command arguments.</summary>
    /// <param name="arguments">The arguments as stored (may be null).</param>
    /// <param name="value">The group value, or null when there is none.</param>
    /// <returns>The arguments to run; unchanged when <paramref name="value"/> or <paramref name="arguments"/> is null.</returns>
    public static string ResolveArguments(string arguments, string value) {
      if (arguments == null || value == null) {
        return arguments;
      }
      return arguments.Replace(Token, value, StringComparison.Ordinal);
    }
  }
}
