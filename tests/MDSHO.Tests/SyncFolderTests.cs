using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MDSHO.Data;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// 2.0.0: the files of the sync folder. Each PC writes only in its own subfolder (named by its device id): index.json,
  /// index.prev.json, items/&lt;sha256&gt;.json and blobs/&lt;sha256&gt;.bin.
  /// </summary>
  public class SyncFolderTests : IDisposable {
    private readonly string root = Path.Combine(Path.GetTempPath(), "MDSHO.Tests", "sync-" + Guid.NewGuid().ToString("N"));

    public SyncFolderTests() { Directory.CreateDirectory(root); }

    public void Dispose() {
      try { Directory.Delete(root, true); } catch (IOException) { }
    }

    private static SyncIndex Sample(string device = "dev-a", long seq = 3) {
      SyncIndex index = new SyncIndex { Device = device, Name = "Home PC", AppVersion = "2.0.0", Seq = seq, WrittenAt = 1000 };
      index.Items["item-1"] = new SyncIndexItem { Hash = "abc", ChangedAt = 10, ChangedBy = device, Seq = seq };
      index.Deleted["item-2"] = new SyncIndexDelete { DeletedAt = 20, DeletedBy = device, Seq = 2 };
      index.Seen["dev-b"] = 7;
      return index;
    }

    [Fact]
    public void The_device_folder_is_named_by_the_device_id() {
      Assert.Equal(Path.Combine(root, "dev-a"), SyncFolder.DeviceFolder(root, "dev-a"));
    }

    [Fact]
    public void An_index_reads_back_as_written() {
      string folder = SyncFolder.DeviceFolder(root, "dev-a");
      SyncFolder.WriteIndex(folder, Sample());
      SyncIndexRead read = SyncFolder.ReadIndex(folder);
      Assert.Equal(SyncIndexState.Ok, read.State);
      Assert.Equal(SyncIndex.CurrentFormat, read.Index.Format);
      Assert.Equal("dev-a", read.Index.Device);
      Assert.Equal("Home PC", read.Index.Name);
      Assert.Equal("2.0.0", read.Index.AppVersion);
      Assert.Equal(3, read.Index.Seq);
      Assert.Equal("abc", read.Index.Items["item-1"].Hash);
      Assert.Equal(20, read.Index.Deleted["item-2"].DeletedAt);
      Assert.Equal(7, read.Index.Seen["dev-b"]);
      Assert.False(string.IsNullOrEmpty(read.Index.Check));
    }

    [Fact]
    public void Writing_keeps_the_last_index_as_index_prev_and_leaves_no_tmp() {
      string folder = SyncFolder.DeviceFolder(root, "dev-a");
      SyncFolder.WriteIndex(folder, Sample(seq: 1));
      SyncFolder.WriteIndex(folder, Sample(seq: 2));
      Assert.True(File.Exists(Path.Combine(folder, "index.json")));
      Assert.True(File.Exists(Path.Combine(folder, "index.prev.json")));
      Assert.False(File.Exists(Path.Combine(folder, "index.tmp")));
      Assert.Contains("\"seq\": 1", File.ReadAllText(Path.Combine(folder, "index.prev.json")));
    }

    [Fact]
    public void A_half_written_index_reads_index_prev() {
      string folder = SyncFolder.DeviceFolder(root, "dev-a");
      SyncFolder.WriteIndex(folder, Sample(seq: 1));
      SyncFolder.WriteIndex(folder, Sample(seq: 2));
      string path = Path.Combine(folder, "index.json");
      string text = File.ReadAllText(path);
      File.WriteAllText(path, text.Substring(0, text.Length / 2));
      SyncIndexRead read = SyncFolder.ReadIndex(folder);
      Assert.Equal(SyncIndexState.FromPrevious, read.State);
      Assert.Equal(1, read.Index.Seq);
    }

    [Fact]
    public void An_index_with_a_wrong_check_reads_index_prev() {
      string folder = SyncFolder.DeviceFolder(root, "dev-a");
      SyncFolder.WriteIndex(folder, Sample(seq: 1));
      SyncFolder.WriteIndex(folder, Sample(seq: 2));
      string path = Path.Combine(folder, "index.json");
      File.WriteAllText(path, File.ReadAllText(path).Replace("\"abc\"", "\"abd\""));
      SyncIndexRead read = SyncFolder.ReadIndex(folder);
      Assert.Equal(SyncIndexState.FromPrevious, read.State);
      Assert.Equal(1, read.Index.Seq);
    }

    [Fact]
    public void When_both_indexes_are_bad_the_reader_waits() {
      string folder = SyncFolder.DeviceFolder(root, "dev-a");
      Directory.CreateDirectory(folder);
      File.WriteAllText(Path.Combine(folder, "index.json"), "{\"format\": 1, \"seq\": ");
      SyncIndexRead read = SyncFolder.ReadIndex(folder);
      Assert.Equal(SyncIndexState.Unreadable, read.State);
      Assert.Null(read.Index);
    }

    [Fact]
    public void No_index_yet_is_missing() {
      Assert.Equal(SyncIndexState.Missing, SyncFolder.ReadIndex(SyncFolder.DeviceFolder(root, "nobody")).State);
    }

    [Fact]
    public void A_newer_format_is_not_read() {
      string folder = SyncFolder.DeviceFolder(root, "dev-a");
      Directory.CreateDirectory(folder);
      File.WriteAllText(Path.Combine(folder, "index.json"), "{\"format\": " + (SyncIndex.CurrentFormat + 1) + ", \"name\": \"Work PC\", \"appVersion\": \"3.0.0\", \"whatever\": [1, 2]}");
      SyncIndexRead read = SyncFolder.ReadIndex(folder);
      Assert.Equal(SyncIndexState.NewerFormat, read.State);
      Assert.Equal("Work PC", read.Name);
      Assert.Equal("3.0.0", read.AppVersion);
      Assert.Null(read.Index);
    }

    [Fact]
    public void Item_files_are_named_by_the_sha256_of_their_content() {
      string folder = SyncFolder.DeviceFolder(root, "dev-a");
      byte[] content = Encoding.UTF8.GetBytes("{\"x\":1}");
      string hash = SyncFolder.WriteItem(folder, content);
      Assert.Equal(SyncFolder.Hash(content), hash);
      Assert.Equal(64, hash.Length);
      Assert.True(File.Exists(Path.Combine(folder, "items", hash + ".json")));
      Assert.Equal(content, SyncFolder.ReadItem(folder, hash));
    }

    [Fact]
    public void An_item_file_whose_hash_does_not_match_is_skipped() {
      string folder = SyncFolder.DeviceFolder(root, "dev-a");
      string hash = SyncFolder.WriteItem(folder, Encoding.UTF8.GetBytes("{\"x\":1}"));
      // Still uploading: only part of it is there.
      File.WriteAllText(Path.Combine(folder, "items", hash + ".json"), "{\"x\"");
      Assert.Null(SyncFolder.ReadItem(folder, hash));
      Assert.Null(SyncFolder.ReadItem(folder, new string('0', 64)));
    }

    [Fact]
    public void Blobs_are_named_by_their_hash_too() {
      string folder = SyncFolder.DeviceFolder(root, "dev-a");
      byte[] bytes = { 1, 2, 3, 4 };
      string hash = SyncFolder.WriteBlob(folder, bytes);
      Assert.True(File.Exists(Path.Combine(folder, "blobs", hash + ".bin")));
      Assert.Equal(bytes, SyncFolder.ReadBlob(folder, hash));
    }

    [Fact]
    public void Files_no_index_points_to_are_deleted_after_7_days() {
      string folder = SyncFolder.DeviceFolder(root, "dev-a");
      string used = SyncFolder.WriteItem(folder, Encoding.UTF8.GetBytes("used"));
      string usedByPrev = SyncFolder.WriteItem(folder, Encoding.UTF8.GetBytes("used by prev"));
      string oldUnused = SyncFolder.WriteItem(folder, Encoding.UTF8.GetBytes("old unused"));
      string newUnused = SyncFolder.WriteItem(folder, Encoding.UTF8.GetBytes("new unused"));
      string blob = SyncFolder.WriteBlob(folder, new byte[] { 9 });
      string oldBlob = SyncFolder.WriteBlob(folder, new byte[] { 8 });
      SyncIndex prev = Sample(seq: 1);
      prev.Items.Clear();
      prev.Items["p"] = new SyncIndexItem { Hash = usedByPrev, Seq = 1 };
      SyncFolder.WriteIndex(folder, prev);
      SyncIndex index = Sample(seq: 2);
      index.Items.Clear();
      index.Items["u"] = new SyncIndexItem { Hash = used, Seq = 2 };
      index.Blobs.Add(blob);
      SyncFolder.WriteIndex(folder, index);
      DateTime now = DateTime.UtcNow;
      foreach (string f in Directory.GetFiles(folder, "*", SearchOption.AllDirectories)) {
        File.SetLastWriteTimeUtc(f, now.AddDays(-8));
      }
      File.SetLastWriteTimeUtc(Path.Combine(folder, "items", newUnused + ".json"), now.AddDays(-6));
      SyncFolder.DeleteUnused(folder, now);
      Assert.True(File.Exists(Path.Combine(folder, "items", used + ".json")));
      Assert.True(File.Exists(Path.Combine(folder, "items", usedByPrev + ".json")));
      Assert.False(File.Exists(Path.Combine(folder, "items", oldUnused + ".json")));
      Assert.True(File.Exists(Path.Combine(folder, "items", newUnused + ".json")));
      Assert.True(File.Exists(Path.Combine(folder, "blobs", blob + ".bin")));
      Assert.False(File.Exists(Path.Combine(folder, "blobs", oldBlob + ".bin")));
    }

    [Fact]
    public void The_other_devices_are_the_other_subfolders() {
      Directory.CreateDirectory(Path.Combine(root, "dev-a"));
      Directory.CreateDirectory(Path.Combine(root, "dev-b"));
      Directory.CreateDirectory(Path.Combine(root, "dev-c"));
      Assert.Equal(new[] { "dev-b", "dev-c" }, SyncFolder.OtherDevices(root, "dev-a").OrderBy(d => d).ToArray());
    }
  }
}
