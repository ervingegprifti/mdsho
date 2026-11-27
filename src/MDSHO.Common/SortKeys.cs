using System;
using System.Collections.Generic;
using System.Text;

namespace MDSHO.Common {

  /// <summary>
  /// Text sort keys that order siblings (fractional indexing). A new key fits between any two keys, so an insert or a
  /// move changes only its own key, and two PCs that insert while apart never need to renumber. Keys compare
  /// ordinally (byte order, as SQLite sorts text); equal keys are ordered by SyncId.
  /// <para>
  /// A key is an integer part and a fraction, in the digits 0-9, A-Z, a-z. The integer part's first character gives
  /// its length: 'a' to 'z' = 2 to 27 characters, 'A' to 'Z' = 27 to 2 (below zero). The fraction never ends in '0', so
  /// there is always room below it. This is David Greenspan's scheme, as used by the fractional-indexing package.
  /// </para>
  /// </summary>
  public static class SortKeys {
    private const string Digits = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
    private const char Zero = '0';
    // The smallest integer part: it can't go lower, so keys below it use the fraction.
    private static readonly string SmallestInteger = "A" + new string(Zero, 26);

    /// <summary>A key between two keys; null = no key on that side. Throws ArgumentException for keys out of order or invalid.</summary>
    public static string Between(string before, string after) {
      if (before != null) { Validate(before); }
      if (after != null) { Validate(after); }
      if (before != null && after != null && string.CompareOrdinal(before, after) >= 0) {
        throw new ArgumentException("The keys are not in order: " + before + " >= " + after + ".");
      }
      if (before == null) {
        if (after == null) {
          return "a" + Zero;
        }
        string ib = IntegerPart(after);
        string fb = after.Substring(ib.Length);
        if (ib == SmallestInteger) {
          return ib + Midpoint("", fb);
        }
        if (string.CompareOrdinal(ib, after) < 0) {
          return ib;
        }
        return Decrement(ib) ?? throw new ArgumentException("There is no key below " + after + ".");
      }
      if (after == null) {
        string ia = IntegerPart(before);
        string fa = before.Substring(ia.Length);
        return Increment(ia) ?? ia + Midpoint(fa, null);
      }
      string ia2 = IntegerPart(before);
      string fa2 = before.Substring(ia2.Length);
      string ib2 = IntegerPart(after);
      string fb2 = after.Substring(ib2.Length);
      if (ia2 == ib2) {
        return ia2 + Midpoint(fa2, fb2);
      }
      string i = Increment(ia2) ?? throw new ArgumentException("There is no key above " + before + ".");
      return string.CompareOrdinal(i, after) < 0 ? i : ia2 + Midpoint(fa2, null);
    }

    /// <summary>Count keys in order between two keys (null = open on that side).</summary>
    public static List<string> Between(string before, string after, int count) {
      List<string> keys = new List<string>(Math.Max(count, 0));
      AddBetween(keys, before, after, count);
      return keys;
    }

    /// <summary>Count keys in order from the first key (a0, a1, a2, ...).</summary>
    public static List<string> Sequence(int count) {
      return Between(null, null, count);
    }

    /// <summary>True when the text is a well-formed key.</summary>
    public static bool IsValid(string key) {
      try {
        Validate(key);
        return true;
      } catch (ArgumentException) {
        return false;
      }
    }

    /// <summary>
    /// The keys for a list in its new order, changing as few as possible: the longest run of keys that are already
    /// valid and strictly increasing is kept, and every other item (moved, new = null, a duplicate or an invalid key)
    /// gets a key between its kept neighbours.
    /// </summary>
    public static string[] Reorder(IReadOnlyList<string> keys) {
      int n = keys.Count;
      string[] result = new string[n];
      bool[] keep = LongestIncreasing(keys);
      int i = 0;
      string previous = null;
      while (i < n) {
        if (keep[i]) {
          result[i] = keys[i];
          previous = keys[i];
          i++;
          continue;
        }
        int start = i;
        while (i < n && !keep[i]) { i++; }
        string next = i < n ? keys[i] : null;
        List<string> fresh = Between(previous, next, i - start);
        for (int k = 0; k < fresh.Count; k++) {
          result[start + k] = fresh[k];
        }
      }
      return result;
    }

    // Marks the longest subsequence of valid, strictly increasing keys (patience sorting, O(n log n)).
    private static bool[] LongestIncreasing(IReadOnlyList<string> keys) {
      int n = keys.Count;
      int[] tails = new int[n];   // index of the smallest tail of each length
      int[] parent = new int[n];
      int length = 0;
      for (int i = 0; i < n; i++) {
        parent[i] = -1;
        if (!IsValid(keys[i])) {
          continue;
        }
        int lo = 0, hi = length;
        while (lo < hi) {
          int mid = (lo + hi) / 2;
          if (string.CompareOrdinal(keys[tails[mid]], keys[i]) < 0) { lo = mid + 1; } else { hi = mid; }
        }
        parent[i] = lo > 0 ? tails[lo - 1] : -1;
        tails[lo] = i;
        if (lo == length) { length++; }
      }
      bool[] keep = new bool[n];
      for (int at = length > 0 ? tails[length - 1] : -1; at >= 0; at = parent[at]) {
        keep[at] = true;
      }
      return keep;
    }

    private static void AddBetween(List<string> keys, string before, string after, int count) {
      if (count <= 0) {
        return;
      }
      if (count == 1) {
        keys.Add(Between(before, after));
        return;
      }
      if (after == null) {
        string c = before;
        for (int i = 0; i < count; i++) { c = Between(c, null); keys.Add(c); }
        return;
      }
      if (before == null) {
        string[] below = new string[count];
        string c = after;
        for (int i = count - 1; i >= 0; i--) { c = Between(null, c); below[i] = c; }
        keys.AddRange(below);
        return;
      }
      int half = count / 2;
      string middle = Between(before, after);
      AddBetween(keys, before, middle, half);
      keys.Add(middle);
      AddBetween(keys, middle, after, count - half - 1);
    }

    // A fraction between two fractions (after null = 1). Neither ends in Zero.
    private static string Midpoint(string a, string b) {
      if (b != null) {
        int n = 0;
        while (n < b.Length && (n < a.Length ? a[n] : Zero) == b[n]) { n++; }
        if (n > 0) {
          return b.Substring(0, n) + Midpoint(n < a.Length ? a.Substring(n) : "", b.Substring(n));
        }
      }
      int digitA = a.Length > 0 ? Digits.IndexOf(a[0]) : 0;
      int digitB = b != null ? Digits.IndexOf(b[0]) : Digits.Length;
      if (digitB - digitA > 1) {
        return Digits[(digitA + digitB + 1) / 2].ToString();
      }
      if (b != null && b.Length > 1) {
        return b.Substring(0, 1);
      }
      return Digits[digitA] + Midpoint(a.Length > 0 ? a.Substring(1) : "", null);
    }

    private static int IntegerLength(char head) {
      if (head >= 'a' && head <= 'z') { return head - 'a' + 2; }
      if (head >= 'A' && head <= 'Z') { return 'Z' - head + 2; }
      throw new ArgumentException("Not a sort key head: " + head + ".");
    }

    private static string IntegerPart(string key) {
      int length = IntegerLength(key[0]);
      if (length > key.Length) {
        throw new ArgumentException("The sort key is too short: " + key + ".");
      }
      return key.Substring(0, length);
    }

    private static void Validate(string key) {
      if (string.IsNullOrEmpty(key)) {
        throw new ArgumentException("A sort key can't be empty.");
      }
      foreach (char c in key) {
        if (Digits.IndexOf(c) < 0) {
          throw new ArgumentException("A sort key has only 0-9, A-Z and a-z: " + key + ".");
        }
      }
      if (key == SmallestInteger) {
        throw new ArgumentException("The sort key is too small: " + key + ".");
      }
      string integer = IntegerPart(key);
      if (key.Length > integer.Length && key[key.Length - 1] == Zero) {
        throw new ArgumentException("A sort key can't end in 0 after its integer part: " + key + ".");
      }
    }

    private static string Increment(string x) {
      char head = x[0];
      StringBuilder digits = new StringBuilder(x.Substring(1));
      bool carry = true;
      for (int i = digits.Length - 1; carry && i >= 0; i--) {
        int d = Digits.IndexOf(digits[i]) + 1;
        if (d == Digits.Length) {
          digits[i] = Zero;
        } else {
          digits[i] = Digits[d];
          carry = false;
        }
      }
      if (!carry) {
        return head + digits.ToString();
      }
      if (head == 'Z') { return "a" + Zero; }
      if (head == 'z') { return null; }
      char h = (char)(head + 1);
      if (h > 'a') {
        digits.Append(Zero);
      } else {
        digits.Length--;
      }
      return h + digits.ToString();
    }

    private static string Decrement(string x) {
      char head = x[0];
      StringBuilder digits = new StringBuilder(x.Substring(1));
      bool borrow = true;
      for (int i = digits.Length - 1; borrow && i >= 0; i--) {
        int d = Digits.IndexOf(digits[i]) - 1;
        if (d == -1) {
          digits[i] = Digits[Digits.Length - 1];
        } else {
          digits[i] = Digits[d];
          borrow = false;
        }
      }
      if (!borrow) {
        return head + digits.ToString();
      }
      if (head == 'a') { return "Z" + Digits[Digits.Length - 1]; }
      if (head == 'A') { return null; }
      char h = (char)(head - 1);
      if (h < 'Z') {
        digits.Append(Digits[Digits.Length - 1]);
      } else {
        digits.Length--;
      }
      return h + digits.ToString();
    }
  }
}
