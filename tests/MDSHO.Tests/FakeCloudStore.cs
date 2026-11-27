using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using MDSHO.Data;

namespace MDSHO.Tests {

  /// <summary>
  /// An in-memory sync location like Google Drive or OneDrive, for tests: it replaces the index in one call (no rename)
  /// and can be slow, fail, or keep a file half uploaded.
  /// </summary>
  public sealed class FakeCloudStore : ISyncStore {
    private readonly object gate = new object();
    private readonly Dictionary<string, (byte[] Bytes, DateTime Modified)> files = new Dictionary<string, (byte[], DateTime)>(StringComparer.Ordinal);

    public FakeCloudStore(string kind = SyncKinds.GoogleDrive) { Kind = kind; }

    public string Kind { get; }
    /// <summary>Every call throws as if the network were down.</summary>
    public bool Offline { get; set; }
    /// <summary>Each call waits this long first.</summary>
    public TimeSpan Delay { get; set; }
    /// <summary>The next item or blob file written keeps only its first half (an upload that broke off).</summary>
    public bool HalfUploadNext { get; set; }
    /// <summary>What was called, in order ("write dev/items/x.json", "index dev", ...).</summary>
    public List<string> Calls { get; } = new List<string>();
    /// <summary>Now for the modified times.</summary>
    public DateTime UtcNow { get; set; } = DateTime.UtcNow;

    private void Enter(string call) {
      if (Delay > TimeSpan.Zero) {
        Thread.Sleep(Delay);
      }
      if (Offline) {
        throw new SyncStoreException("The fake cloud is offline");
      }
      lock (this.gate) {
        Calls.Add(call);
      }
    }

    private static string Key(string device, string path) => device + "/" + path;

    public bool IsReachable() {
      Enter("reach");
      return true;
    }

    public IReadOnlyList<string> Devices() {
      Enter("devices");
      lock (this.gate) {
        return this.files.Keys.Where(k => k.Contains('/')).Select(k => k.Substring(0, k.IndexOf('/'))).Distinct().OrderBy(d => d, StringComparer.Ordinal).ToList();
      }
    }

    public byte[] ReadRoot(string name) {
      Enter("read-root " + name);
      lock (this.gate) {
        return this.files.TryGetValue(name, out var f) ? f.Bytes : null;
      }
    }

    public bool CreateRoot(string name, byte[] content) {
      Enter("create-root " + name);
      lock (this.gate) {
        if (this.files.ContainsKey(name)) {
          return false;
        }
        this.files[name] = (content, UtcNow);
        return true;
      }
    }

    public byte[] Read(string device, string path) {
      Enter("read " + Key(device, path));
      lock (this.gate) {
        return this.files.TryGetValue(Key(device, path), out var f) ? f.Bytes : null;
      }
    }

    public void Write(string device, string path, byte[] content) {
      Enter("write " + Key(device, path));
      if (HalfUploadNext) {
        HalfUploadNext = false;
        content = content.Take(content.Length / 2).ToArray();
      }
      lock (this.gate) {
        this.files[Key(device, path)] = (content, UtcNow);
      }
    }

    public void ReplaceIndex(string device, byte[] content) {
      Enter("index " + device);
      lock (this.gate) {
        if (this.files.TryGetValue(Key(device, SyncFolder.IndexName), out var old)) {
          this.files[Key(device, SyncFolder.PreviousName)] = old;
        }
        this.files[Key(device, SyncFolder.IndexName)] = (content, UtcNow);
      }
    }

    public IReadOnlyList<SyncStoreFile> List(string device, string folder) {
      Enter("list " + Key(device, folder));
      string prefix = Key(device, folder) + "/";
      lock (this.gate) {
        return this.files.Where(f => f.Key.StartsWith(prefix, StringComparison.Ordinal) && f.Key.IndexOf('/', prefix.Length) < 0)
          .Select(f => new SyncStoreFile(f.Key.Substring(prefix.Length), f.Value.Modified)).ToList();
      }
    }

    public void Delete(string device, string path) {
      Enter("delete " + Key(device, path));
      lock (this.gate) {
        this.files.Remove(Key(device, path));
      }
    }

    /// <summary>The bytes of a file, or null (no call recorded).</summary>
    public byte[] Peek(string key) {
      lock (this.gate) {
        return this.files.TryGetValue(key, out var f) ? f.Bytes : null;
      }
    }

    /// <summary>Every file name.</summary>
    public IReadOnlyList<string> Names() {
      lock (this.gate) {
        return this.files.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();
      }
    }

    /// <summary>Sets a file straight away (no call recorded).</summary>
    public void Put(string key, byte[] bytes) {
      lock (this.gate) {
        this.files[key] = (bytes, UtcNow);
      }
    }
  }
}
