using System;
using System.Collections.Generic;
using System.Linq;
using MDSHO.Common;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>1.30.0: text sort keys (fractional indexing) that order siblings without renumbering.</summary>
  public class SortKeysTests {

    private static bool Less(string a, string b) { return string.CompareOrdinal(a, b) < 0; }

    [Fact]
    public void The_first_key_and_keys_after_it_go_up() {
      string a = SortKeys.Between(null, null);
      Assert.Equal("a0", a);
      string b = SortKeys.Between(a, null);
      string c = SortKeys.Between(b, null);
      Assert.True(Less(a, b) && Less(b, c));
    }

    [Fact]
    public void A_key_before_the_first_is_smaller() {
      string a = SortKeys.Between(null, null);
      string before = SortKeys.Between(null, a);
      Assert.True(Less(before, a));
      Assert.True(Less(SortKeys.Between(null, before), before));
    }

    [Fact]
    public void A_key_between_two_keys_lies_between_them() {
      string a = SortKeys.Between(null, null);
      string b = SortKeys.Between(a, null);
      string m = SortKeys.Between(a, b);
      Assert.True(Less(a, m) && Less(m, b));
      // Keep splitting the same gap: always strictly between, never equal.
      string lo = a, hi = b;
      for (int i = 0; i < 200; i++) {
        string mid = SortKeys.Between(lo, hi);
        Assert.True(Less(lo, mid) && Less(mid, hi), lo + " < " + mid + " < " + hi);
        if (i % 2 == 0) { lo = mid; } else { hi = mid; }
      }
    }

    [Fact]
    public void Appending_many_keeps_keys_short() {
      string key = null;
      for (int i = 0; i < 5000; i++) {
        key = SortKeys.Between(key, null);
      }
      Assert.True(key.Length <= 4, key);
    }

    [Fact]
    public void Wrong_input_is_rejected() {
      Assert.Throws<ArgumentException>(() => SortKeys.Between("a1", "a0"));
      Assert.Throws<ArgumentException>(() => SortKeys.Between("a1", "a1"));
      Assert.Throws<ArgumentException>(() => SortKeys.Between("a10", null)); // a trailing zero digit
      Assert.Throws<ArgumentException>(() => SortKeys.Between("", null));
      Assert.Throws<ArgumentException>(() => SortKeys.Between("!", null));
      Assert.False(SortKeys.IsValid("a10"));
      Assert.True(SortKeys.IsValid("a1V"));
    }

    [Fact]
    public void Several_keys_in_a_row_are_in_order() {
      List<string> keys = SortKeys.Sequence(100);
      Assert.Equal(100, keys.Count);
      Assert.Equal("a0", keys[0]);
      for (int i = 1; i < keys.Count; i++) {
        Assert.True(Less(keys[i - 1], keys[i]));
      }
      List<string> between = SortKeys.Between("a0", "a1", 50);
      Assert.Equal(50, between.Count);
      Assert.True(Less("a0", between[0]) && Less(between[49], "a1"));
      for (int i = 1; i < between.Count; i++) {
        Assert.True(Less(between[i - 1], between[i]));
      }
    }

    [Fact]
    public void Reorder_keeps_the_keys_that_are_still_in_order() {
      // a0 a1 a2 a3, the last moved to the front: only that one gets a new key.
      string[] result = SortKeys.Reorder(new[] { "a3", "a0", "a1", "a2" });
      Assert.Equal("a0", result[1]);
      Assert.Equal("a1", result[2]);
      Assert.Equal("a2", result[3]);
      Assert.True(Less(result[0], "a0"));
    }

    [Fact]
    public void Reorder_gives_a_key_between_the_neighbours_when_inserting() {
      // A new item (no key) inserted between a0 and a1.
      string[] result = SortKeys.Reorder(new[] { "a0", null, "a1" });
      Assert.Equal("a0", result[0]);
      Assert.Equal("a1", result[2]);
      Assert.True(Less("a0", result[1]) && Less(result[1], "a1"));
    }

    [Fact]
    public void Reorder_of_an_ordered_list_changes_nothing() {
      string[] keys = { "Zz", "a0", "a0V", "a1", "b00" };
      Assert.Equal(keys, SortKeys.Reorder(keys));
    }

    [Fact]
    public void Reorder_fixes_equal_and_invalid_keys() {
      string[] result = SortKeys.Reorder(new[] { "a1", "a1", "", "bad!", "a10", "a2" });
      for (int i = 1; i < result.Length; i++) {
        Assert.True(Less(result[i - 1], result[i]), string.Join(",", result));
      }
      Assert.All(result, k => Assert.True(SortKeys.IsValid(k)));
      Assert.Equal("a2", result[5]);
    }

    [Fact]
    public void Reorder_of_random_moves_always_gives_strictly_increasing_keys() {
      Random random = new Random(7);
      List<string> keys = SortKeys.Sequence(20);
      for (int round = 0; round < 500; round++) {
        int from = random.Next(keys.Count), to = random.Next(keys.Count);
        string moved = keys[from];
        keys.RemoveAt(from);
        keys.Insert(to, moved);
        if (round % 7 == 0) { keys.Insert(random.Next(keys.Count + 1), null); }
        string[] result = SortKeys.Reorder(keys);
        int changed = 0;
        for (int i = 0; i < result.Length; i++) {
          if (i > 0) { Assert.True(Less(result[i - 1], result[i])); }
          if (result[i] != keys[i]) { changed++; }
        }
        Assert.True(changed <= 2, "Only the moved item and a new one change, not " + changed);
        keys = result.ToList();
      }
    }
  }
}
