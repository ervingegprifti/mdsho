using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace MDSHO.Core {

  /// <summary>One value of a group name, e.g. <c>{SRV01}</c> or <c>{user=admin}</c>.</summary>
  public sealed class GroupValueItem {
    /// <summary>Creates a value.</summary>
    public GroupValueItem(int number, string name, string value) {
      this.Number = number;
      this.Name = name;
      this.Value = value;
    }
    /// <summary>Its place in the name, from 1 (used as <c>{=1}</c>).</summary>
    public int Number { get; }
    /// <summary>Its name (used as <c>{=name}</c>), or null when it has none.</summary>
    public string Name { get; }
    /// <summary>The value, as typed.</summary>
    public string Value { get; }
    /// <summary>The token a click inserts: <c>{=name}</c> for a named value, else <c>{=1}</c>.</summary>
    public string Token => this.Name != null ? "{=" + this.Name + "}" : NumberToken;
    /// <summary>What the values line shows, e.g. <c>{=1} SRV01</c> or <c>{=2} {=user} admin</c>.</summary>
    public string Label => this.Name != null ? NumberToken + " {=" + this.Name + "} " + this.Value : NumberToken + " " + this.Value;
    private string NumberToken => "{=" + this.Number + "}";
  }

  /// <summary>
  /// Group values: a group name holds values in braces, e.g. <c>Prod {SRV01} {user=admin}</c>. Run commands directly
  /// inside the group use them in their arguments and working folder as <c>{=1}</c>…<c>{=9}</c> or <c>{=name}</c>.
  /// </summary>
  /// <remarks>
  /// <list type="bullet">
  /// <item>Each non-empty <c>{…}</c> of the name is one value, numbered from the left, up to 9. <c>{}</c> is not a value.</item>
  /// <item><c>{name=value}</c> names a value when the text before the first <c>=</c> is a valid name; names ignore case
  /// and the first of two equal names counts.</item>
  /// <item>Tokens are replaced by the value as is. <c>{}</c> is never replaced; it is an error when the group has values.</item>
  /// </list>
  /// </remarks>
  public static class GroupValue {
    /// <summary>The most values a group name can hold.</summary>
    public const int MaxValues = 9;
    /// <summary>The error for <c>{}</c> in a command whose group has values.</summary>
    public const string OldTokenMessage = "{} is no longer supported. Use {=1}.";
    /// <summary>The error for a token when the group has no values or the command is not in a group.</summary>
    public const string NoValuesMessage = "This command uses group values, but its group has none.";

    private const string OldToken = "{}";
    private const string TokenStart = "{=";
    private static readonly Regex NamePattern = new Regex("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.CultureInvariant);

    /// <summary>The error for a number higher than the values count.</summary>
    public static string NoNumberMessage(int number) {
      return $"The group has no value {number}.";
    }

    /// <summary>The error for an unknown name.</summary>
    public static string NoNameMessage(string name) {
      return $"The group has no value named \"{name}\".";
    }

    /// <summary>The error for a malformed token, which is quoted.</summary>
    public static string InvalidTokenMessage(string token) {
      return $"\"{token}\" is not valid. Use {{=1}} or {{=name}}.";
    }

    /// <summary>Reads the values of a group name; empty when it has none.</summary>
    public static IReadOnlyList<GroupValueItem> Parse(string groupName) {
      List<GroupValueItem> values = new List<GroupValueItem>();
      if (string.IsNullOrEmpty(groupName)) {
        return values;
      }
      HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      int index = 0;
      while (values.Count < MaxValues) {
        int open = groupName.IndexOf('{', index);
        if (open < 0) {
          break;
        }
        int close = groupName.IndexOf('}', open + 1);
        if (close < 0) {
          break;
        }
        index = close + 1;
        string content = groupName.Substring(open + 1, close - open - 1);
        if (content.Length == 0) {
          continue;
        }
        string name = null;
        string value = content;
        int equals = content.IndexOf('=');
        if (equals > 0 && NamePattern.IsMatch(content.Substring(0, equals))) {
          name = content.Substring(0, equals);
          value = content.Substring(equals + 1);
          if (!names.Add(name)) {
            name = null;
          }
        }
        values.Add(new GroupValueItem(values.Count + 1, name, value));
      }
      return values;
    }

    /// <summary>
    /// The first syntax error in the text, from the left, or null when it is fine. <paramref name="values"/> is null or
    /// empty when the command is not in a group or its group has no values.
    /// </summary>
    public static string FindError(string text, IReadOnlyList<GroupValueItem> values) {
      string error = null;
      Scan(text, values, (token, _) => {
        error = token.Error;
        return error == null;
      });
      return error;
    }

    /// <summary>The first syntax error in the arguments, else in the working folder, or null when both are fine.</summary>
    public static string FindError(string arguments, string workingFolder, IReadOnlyList<GroupValueItem> values) {
      return FindError(arguments, values) ?? FindError(workingFolder, values);
    }

    /// <summary>Replaces every valid token by its value; anything else, <c>{}</c> included, is left as it is. Null stays null.</summary>
    public static string Fill(string text, IReadOnlyList<GroupValueItem> values) {
      if (text == null || values == null || values.Count == 0) {
        return text;
      }
      System.Text.StringBuilder result = new System.Text.StringBuilder();
      int copied = 0;
      Scan(text, values, (token, start) => {
        if (token.Value != null) {
          result.Append(text, copied, start - copied).Append(token.Value);
          copied = start + token.Length;
        }
        return true;
      });
      return result.Append(text, copied, text.Length - copied).ToString();
    }

    /// <summary>True when the text has a <c>{=</c> token, valid or not.</summary>
    public static bool UsesValues(string text) {
      return text != null && text.Contains(TokenStart, StringComparison.Ordinal);
    }

    private sealed class Token {
      public int Length;
      public string Value;
      public string Error;
    }

    // Calls found(token, start) for each {=…} and {} from the left, until it returns false.
    private static void Scan(string text, IReadOnlyList<GroupValueItem> values, Func<Token, int, bool> found) {
      if (string.IsNullOrEmpty(text)) {
        return;
      }
      bool hasValues = values != null && values.Count > 0;
      int i = 0;
      while (i < text.Length) {
        Token token = null;
        if (string.CompareOrdinal(text, i, TokenStart, 0, TokenStart.Length) == 0) {
          token = ReadToken(text, i, values, hasValues);
        } else if (string.CompareOrdinal(text, i, OldToken, 0, OldToken.Length) == 0) {
          token = new Token { Length = OldToken.Length, Error = hasValues ? OldTokenMessage : null };
        }
        if (token == null) {
          i++;
          continue;
        }
        if ((token.Error != null || token.Value != null) && !found(token, i)) {
          return;
        }
        i += token.Length;
      }
    }

    private static Token ReadToken(string text, int start, IReadOnlyList<GroupValueItem> values, bool hasValues) {
      int lineEnd = text.IndexOfAny(new[] { '\r', '\n' }, start);
      if (lineEnd < 0) {
        lineEnd = text.Length;
      }
      int close = text.IndexOf('}', start, lineEnd - start);
      if (close < 0) {
        // Unclosed: quote it up to the first space or the end of the line.
        int end = start + TokenStart.Length;
        while (end < lineEnd && !char.IsWhiteSpace(text[end])) {
          end++;
        }
        return new Token { Length = end - start, Error = InvalidTokenMessage(text.Substring(start, end - start)) };
      }
      int length = close - start + 1;
      string content = text.Substring(start + TokenStart.Length, close - start - TokenStart.Length);
      bool isNumber = content.Length == 1 && content[0] >= '1' && content[0] <= '9';
      if (!isNumber && !NamePattern.IsMatch(content)) {
        return new Token { Length = length, Error = InvalidTokenMessage(text.Substring(start, length)) };
      }
      if (!hasValues) {
        return new Token { Length = length, Error = NoValuesMessage };
      }
      if (isNumber) {
        int number = content[0] - '0';
        return number <= values.Count
          ? new Token { Length = length, Value = values[number - 1].Value }
          : new Token { Length = length, Error = NoNumberMessage(number) };
      }
      foreach (GroupValueItem item in values) {
        if (string.Equals(item.Name, content, StringComparison.OrdinalIgnoreCase)) {
          return new Token { Length = length, Value = item.Value };
        }
      }
      return new Token { Length = length, Error = NoNameMessage(content) };
    }
  }
}
