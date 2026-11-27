using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MDSHO.Common;

namespace MDSHO.Data {

  /// <summary>
  /// The HTTP calls of a cloud store: the bearer token on each request, one refresh after a 401, and tries again with a
  /// growing wait (or as long as Retry-After says) after a network error, 429 or a 5xx. After the last try the cloud
  /// counts as unreachable (<see cref="SyncStoreException"/>). Sync rounds run in the background, so the calls block.
  /// </summary>
  public sealed class CloudHttp {
    /// <summary>Tries per call.</summary>
    public const int MaxAttempts = 5;
    /// <summary>The longest wait between tries.</summary>
    public static readonly TimeSpan MaxWait = TimeSpan.FromSeconds(30);

    private readonly string kind;
    private readonly HttpClient http;
    private readonly Func<CancellationToken, Task<string>> accessToken;
    private readonly Action invalidate;

    /// <summary>The calls of a cloud kind with its token source; <paramref name="handler"/> null uses the network.</summary>
    public CloudHttp(string kind, HttpMessageHandler handler, Func<CancellationToken, Task<string>> accessToken, Action invalidate) {
      this.kind = kind;
      this.http = handler == null ? new HttpClient() : new HttpClient(handler, disposeHandler: false);
      this.http.Timeout = TimeSpan.FromSeconds(100);
      this.accessToken = accessToken ?? throw new ArgumentNullException(nameof(accessToken));
      this.invalidate = invalidate ?? (() => { });
    }

    /// <summary>The calls of a cloud kind signed in through its token source.</summary>
    public CloudHttp(string kind, CloudTokenSource tokens, HttpMessageHandler handler = null)
      : this(kind, handler, tokens.AccessTokenAsync, tokens.Invalidate) { }

    /// <summary>Waits between tries (tests record it instead).</summary>
    public Func<TimeSpan, CancellationToken, Task> Wait { get; set; } = Task.Delay;

    /// <summary>The name shown to the user.</summary>
    public string DisplayName => SyncKinds.DisplayName(this.kind);

    /// <summary>Sends a request made by <paramref name="make"/> (made again for each try) and returns the answer.</summary>
    public HttpResponseMessage Send(Func<HttpRequestMessage> make) => SendAsync(make, CancellationToken.None).GetAwaiter().GetResult();

    /// <summary>Sends a request; see the class.</summary>
    public async Task<HttpResponseMessage> SendAsync(Func<HttpRequestMessage> make, CancellationToken cancel) {
      bool refreshed = false;
      int attempt = 0;
      while (true) {
        attempt++;
        string token = await this.accessToken(cancel).ConfigureAwait(false);
        HttpResponseMessage response;
        try {
          using (HttpRequestMessage request = make()) {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            response = await this.http.SendAsync(request, cancel).ConfigureAwait(false);
          }
        } catch (Exception ex) when ((ex is HttpRequestException || ex is TaskCanceledException) && !cancel.IsCancellationRequested) {
          if (attempt >= MaxAttempts) {
            throw new SyncStoreException("Can't reach " + DisplayName, ex);
          }
          await this.Wait(Backoff(attempt), cancel).ConfigureAwait(false);
          continue;
        }
        if (response.StatusCode == HttpStatusCode.Unauthorized) {
          response.Dispose();
          if (refreshed) {
            throw new CloudSignedOutException(DisplayName + " did not take this PC's sign-in");
          }
          refreshed = true;
          attempt--;
          this.invalidate();
          continue;
        }
        if (IsRetryable(response.StatusCode)) {
          TimeSpan wait = RetryAfter(response) ?? Backoff(attempt);
          int code = (int)response.StatusCode;
          response.Dispose();
          if (attempt >= MaxAttempts) {
            throw new SyncStoreException(DisplayName + " is busy or down (" + code + ")");
          }
          AppLog.Information("{Kind} answered {Status}; trying again in {Seconds} s", DisplayName, code, wait.TotalSeconds);
          await this.Wait(wait > MaxWait ? MaxWait : wait, cancel).ConfigureAwait(false);
          continue;
        }
        return response;
      }
    }

    private static bool IsRetryable(HttpStatusCode code) {
      int c = (int)code;
      return c == 429 || c == 500 || c == 502 || c == 503 || c == 504;
    }

    // 1, 2, 4, 8 ... seconds, at most MaxWait.
    private static TimeSpan Backoff(int attempt) {
      double seconds = Math.Pow(2, attempt - 1);
      return seconds >= MaxWait.TotalSeconds ? MaxWait : TimeSpan.FromSeconds(seconds);
    }

    private static TimeSpan? RetryAfter(HttpResponseMessage response) {
      RetryConditionHeaderValue retry = response.Headers.RetryAfter;
      if (retry?.Delta != null) {
        return retry.Delta.Value;
      }
      if (retry?.Date != null) {
        TimeSpan d = retry.Date.Value - DateTimeOffset.UtcNow;
        return d > TimeSpan.Zero ? d : TimeSpan.Zero;
      }
      return null;
    }

    /// <summary>The body of a successful answer as bytes; a failure throws <see cref="SyncStoreException"/>.</summary>
    internal byte[] Bytes(HttpResponseMessage r) {
      Ensure(r);
      return r.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
    }

    /// <summary>The body of a successful answer as JSON.</summary>
    internal JsonDocument Json(HttpResponseMessage r) {
      return JsonDocument.Parse(Bytes(r));
    }

    /// <summary>Throws for anything but success.</summary>
    internal void Ensure(HttpResponseMessage r) {
      if (!r.IsSuccessStatusCode) {
        throw new SyncStoreException(DisplayName + " answered " + (int)r.StatusCode + " " + r.ReasonPhrase);
      }
    }

    internal static DateTime Time(JsonElement e, string name) {
      return e.TryGetProperty(name, out JsonElement t) && DateTime.TryParse(t.GetString(), CultureInfo.InvariantCulture,
        DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime d) ? d : DateTime.UtcNow;
    }
  }

  /// <summary>
  /// Google Drive through the Drive API v3, with scope drive.file (MDSHO sees only the files it made). A folder
  /// "MDSHO Sync" in My Drive holds sync.json and one subfolder per PC, as in a sync folder. Item files are made, then their
  /// content uploaded; the index is replaced with one content update.
  /// </summary>
  public sealed class GoogleDriveStore : ISyncStore {
    /// <summary>The folder MDSHO makes in My Drive.</summary>
    public const string RootFolderName = "MDSHO Sync";
    private const string Api = "https://www.googleapis.com/drive/v3/files";
    private const string UploadApi = "https://www.googleapis.com/upload/drive/v3/files";
    private const string FolderMime = "application/vnd.google-apps.folder";
    private const string Fields = "nextPageToken,files(id,name,mimeType,modifiedTime)";

    private readonly CloudHttp http;
    // Folder IDs by path ("" is the root folder, then "device", "device/items").
    private readonly Dictionary<string, string> folders = new Dictionary<string, string>(StringComparer.Ordinal);
    private readonly object gate = new object();

    /// <summary>The store over the Drive calls.</summary>
    public GoogleDriveStore(CloudHttp http) {
      this.http = http ?? throw new ArgumentNullException(nameof(http));
    }

    /// <inheritdoc/>
    public string Kind => SyncKinds.GoogleDrive;

    private static string Quote(string s) => s.Replace("\\", "\\\\").Replace("'", "\\'");

    private sealed record Entry(string Id, string Name, bool IsFolder, DateTime Modified);

    // Every child of a folder (all pages), optionally only those with a name or only folders.
    private List<Entry> Children(string parentId, string name = null, bool foldersOnly = false) {
      string q = "'" + Quote(parentId) + "' in parents and trashed = false";
      if (name != null) {
        q = "name = '" + Quote(name) + "' and " + q;
      }
      if (foldersOnly) {
        q += " and mimeType = '" + FolderMime + "'";
      }
      List<Entry> all = new List<Entry>();
      string page = null;
      do {
        string url = Api + "?q=" + Uri.EscapeDataString(q) + "&fields=" + Uri.EscapeDataString(Fields) + "&pageSize=1000&spaces=drive&orderBy=createdTime"
          + (page == null ? "" : "&pageToken=" + Uri.EscapeDataString(page));
        using (HttpResponseMessage r = this.http.Send(() => new HttpRequestMessage(HttpMethod.Get, url)))
        using (JsonDocument doc = this.http.Json(r)) {
          foreach (JsonElement f in doc.RootElement.GetProperty("files").EnumerateArray()) {
            all.Add(new Entry(f.GetProperty("id").GetString(), f.GetProperty("name").GetString(),
              f.TryGetProperty("mimeType", out JsonElement m) && m.GetString() == FolderMime, CloudHttp.Time(f, "modifiedTime")));
          }
          page = doc.RootElement.TryGetProperty("nextPageToken", out JsonElement p) ? p.GetString() : null;
        }
      } while (!string.IsNullOrEmpty(page));
      return all;
    }

    private string Create(string parentId, string name, bool folder) {
      Dictionary<string, object> meta = new Dictionary<string, object> { ["name"] = name, ["parents"] = new[] { parentId } };
      if (folder) {
        meta["mimeType"] = FolderMime;
      }
      byte[] body = JsonSerializer.SerializeToUtf8Bytes(meta);
      using (HttpResponseMessage r = this.http.Send(() => new HttpRequestMessage(HttpMethod.Post, Api + "?fields=id") {
        Content = new ByteArrayContent(body) { Headers = { ContentType = new MediaTypeHeaderValue("application/json") } }
      }))
      using (JsonDocument doc = this.http.Json(r)) {
        return doc.RootElement.GetProperty("id").GetString();
      }
    }

    // The ID of a folder path, made when asked; null when it is not there.
    private string Folder(string path, bool create) {
      lock (this.gate) {
        if (this.folders.TryGetValue(path, out string known)) {
          return known;
        }
      }
      string id;
      if (path.Length == 0) {
        // The oldest "MDSHO Sync" folder, so two PCs that made one at the same moment both use the same one.
        id = Children("root", RootFolderName, foldersOnly: true).FirstOrDefault()?.Id;
        if (id == null && create) {
          id = Create("root", RootFolderName, folder: true);
        }
      } else {
        int slash = path.LastIndexOf('/');
        string parent = Folder(slash < 0 ? "" : path.Substring(0, slash), create);
        if (parent == null) {
          return null;
        }
        string name = slash < 0 ? path : path.Substring(slash + 1);
        id = Children(parent, name, foldersOnly: true).FirstOrDefault()?.Id;
        if (id == null && create) {
          id = Create(parent, name, folder: true);
        }
      }
      if (id != null) {
        lock (this.gate) {
          this.folders[path] = id;
        }
      }
      return id;
    }

    private (string Folder, string Name) Split(string device, string path) {
      string full = device == null ? path : device + "/" + path;
      int slash = full.LastIndexOf('/');
      return (slash < 0 ? "" : full.Substring(0, slash), slash < 0 ? full : full.Substring(slash + 1));
    }

    private Entry File(string device, string path) {
      (string folder, string name) = Split(device, path);
      string parent = Folder(folder, create: false);
      return parent == null ? null : Children(parent, name).FirstOrDefault(e => !e.IsFolder);
    }

    private byte[] Download(Entry e) {
      if (e == null) {
        return null;
      }
      using (HttpResponseMessage r = this.http.Send(() => new HttpRequestMessage(HttpMethod.Get, Api + "/" + Uri.EscapeDataString(e.Id) + "?alt=media"))) {
        return r.StatusCode == HttpStatusCode.NotFound ? null : this.http.Bytes(r);
      }
    }

    private void Upload(string id, byte[] content) {
      using (HttpResponseMessage r = this.http.Send(() => new HttpRequestMessage(HttpMethod.Patch, UploadApi + "/" + Uri.EscapeDataString(id) + "?uploadType=media") {
        Content = new ByteArrayContent(content) { Headers = { ContentType = new MediaTypeHeaderValue("application/octet-stream") } }
      })) {
        this.http.Ensure(r);
      }
    }

    private void Put(string device, string path, byte[] content) {
      (string folder, string name) = Split(device, path);
      string parent = Folder(folder, create: true);
      Entry existing = Children(parent, name).FirstOrDefault(e => !e.IsFolder);
      // A new file is made first, then its content goes in one upload; a reader checks the hash of item files.
      Upload(existing?.Id ?? Create(parent, name, folder: false), content);
    }

    /// <summary>The signed-in account (its e-mail address), or "".</summary>
    public string Account() {
      using (HttpResponseMessage r = this.http.Send(() => new HttpRequestMessage(HttpMethod.Get, "https://www.googleapis.com/drive/v3/about?fields=user(displayName,emailAddress)")))
      using (JsonDocument doc = this.http.Json(r)) {
        JsonElement user = doc.RootElement.GetProperty("user");
        return user.TryGetProperty("emailAddress", out JsonElement e) ? e.GetString() : user.TryGetProperty("displayName", out JsonElement n) ? n.GetString() : "";
      }
    }

    /// <inheritdoc/>
    public bool IsReachable() => Folder("", create: true) != null;

    /// <inheritdoc/>
    public IReadOnlyList<string> Devices() {
      string root = Folder("", create: true);
      return Children(root, foldersOnly: true).Select(e => e.Name).Where(n => !n.StartsWith(".", StringComparison.Ordinal))
        .Distinct(StringComparer.Ordinal).OrderBy(n => n, StringComparer.Ordinal).ToList();
    }

    /// <inheritdoc/>
    public byte[] ReadRoot(string name) => Download(File(null, name));

    /// <inheritdoc/>
    public bool CreateRoot(string name, byte[] content) {
      if (File(null, name) != null) {
        return false;
      }
      Put(null, name, content);
      return true;
    }

    /// <inheritdoc/>
    public byte[] Read(string device, string path) => Download(File(device, path));

    /// <inheritdoc/>
    public void Write(string device, string path, byte[] content) => Put(device, path, content);

    /// <inheritdoc/>
    public void ReplaceIndex(string device, byte[] content) => Put(device, SyncFolder.IndexName, content);

    /// <inheritdoc/>
    public IReadOnlyList<SyncStoreFile> List(string device, string folder) {
      string id = Folder(device + "/" + folder, create: false);
      if (id == null) {
        return Array.Empty<SyncStoreFile>();
      }
      return Children(id).Where(e => !e.IsFolder).Select(e => new SyncStoreFile(e.Name, e.Modified)).ToList();
    }

    /// <inheritdoc/>
    public void Delete(string device, string path) {
      Entry e = File(device, path);
      if (e == null) {
        return;
      }
      using (HttpResponseMessage r = this.http.Send(() => new HttpRequestMessage(HttpMethod.Delete, Api + "/" + Uri.EscapeDataString(e.Id)))) {
        if (r.StatusCode != HttpStatusCode.NotFound) {
          this.http.Ensure(r);
        }
      }
    }
  }

  /// <summary>
  /// OneDrive through Microsoft Graph, in MDSHO's app folder (Apps/MDSHO, scope Files.ReadWrite.AppFolder): sync.json and
  /// one subfolder per PC, addressed by path. A PUT replaces a file's content in one call, so the index needs no rename.
  /// </summary>
  public sealed class OneDriveStore : ISyncStore {
    private const string Base = "https://graph.microsoft.com/v1.0/me/drive/special/approot";
    private readonly CloudHttp http;

    /// <summary>The store over the Graph calls.</summary>
    public OneDriveStore(CloudHttp http) {
      this.http = http ?? throw new ArgumentNullException(nameof(http));
    }

    /// <inheritdoc/>
    public string Kind => SyncKinds.OneDrive;

    private static string Item(string device, string path) {
      string full = device == null ? path : device + "/" + path;
      return Base + ":/" + string.Join("/", full.Split('/').Select(Uri.EscapeDataString));
    }

    private byte[] Get(string url) {
      using (HttpResponseMessage r = this.http.Send(() => new HttpRequestMessage(HttpMethod.Get, url))) {
        return r.StatusCode == HttpStatusCode.NotFound ? null : this.http.Bytes(r);
      }
    }

    private HttpResponseMessage PutContent(string url, byte[] content) {
      return this.http.Send(() => new HttpRequestMessage(HttpMethod.Put, url) {
        Content = new ByteArrayContent(content) { Headers = { ContentType = new MediaTypeHeaderValue("application/octet-stream") } }
      });
    }

    // Every child of a folder (all pages); empty when the folder is not there.
    private List<JsonElement> Children(string url) {
      List<JsonElement> all = new List<JsonElement>();
      string next = url;
      while (next != null) {
        string current = next;
        using (HttpResponseMessage r = this.http.Send(() => new HttpRequestMessage(HttpMethod.Get, current))) {
          if (r.StatusCode == HttpStatusCode.NotFound) {
            return all;
          }
          using (JsonDocument doc = this.http.Json(r)) {
            all.AddRange(doc.RootElement.GetProperty("value").EnumerateArray().Select(e => e.Clone()));
            next = doc.RootElement.TryGetProperty("@odata.nextLink", out JsonElement n) ? n.GetString() : null;
          }
        }
      }
      return all;
    }

    /// <summary>The signed-in account's name, or "".</summary>
    public string Account() {
      using (HttpResponseMessage r = this.http.Send(() => new HttpRequestMessage(HttpMethod.Get, Base + "?$select=id,createdBy")))
      using (JsonDocument doc = this.http.Json(r)) {
        return doc.RootElement.TryGetProperty("createdBy", out JsonElement c) && c.TryGetProperty("user", out JsonElement u)
          && u.TryGetProperty("displayName", out JsonElement n) ? n.GetString() ?? "" : "";
      }
    }

    /// <inheritdoc/>
    public bool IsReachable() {
      // The first call makes the app folder.
      using (HttpResponseMessage r = this.http.Send(() => new HttpRequestMessage(HttpMethod.Get, Base + "?$select=id"))) {
        this.http.Ensure(r);
        return true;
      }
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> Devices() {
      return Children(Base + "/children?$select=name,folder&$top=200")
        .Where(e => e.TryGetProperty("folder", out _)).Select(e => e.GetProperty("name").GetString())
        .Where(n => !n.StartsWith(".", StringComparison.Ordinal)).OrderBy(n => n, StringComparer.Ordinal).ToList();
    }

    /// <inheritdoc/>
    public byte[] ReadRoot(string name) => Get(Item(null, name) + ":/content");

    /// <inheritdoc/>
    public bool CreateRoot(string name, byte[] content) {
      using (HttpResponseMessage r = PutContent(Item(null, name) + ":/content?@microsoft.graph.conflictBehavior=fail", content)) {
        if (r.StatusCode == HttpStatusCode.Conflict) {
          return false;
        }
        this.http.Ensure(r);
        return true;
      }
    }

    /// <inheritdoc/>
    public byte[] Read(string device, string path) => Get(Item(device, path) + ":/content");

    /// <inheritdoc/>
    public void Write(string device, string path, byte[] content) {
      using (HttpResponseMessage r = PutContent(Item(device, path) + ":/content", content)) {
        this.http.Ensure(r);
      }
    }

    /// <inheritdoc/>
    public void ReplaceIndex(string device, byte[] content) => Write(device, SyncFolder.IndexName, content);

    /// <inheritdoc/>
    public IReadOnlyList<SyncStoreFile> List(string device, string folder) {
      return Children(Item(device, folder) + ":/children?$select=name,file,lastModifiedDateTime&$top=200")
        .Where(e => e.TryGetProperty("file", out _))
        .Select(e => new SyncStoreFile(e.GetProperty("name").GetString(), CloudHttp.Time(e, "lastModifiedDateTime"))).ToList();
    }

    /// <inheritdoc/>
    public void Delete(string device, string path) {
      using (HttpResponseMessage r = this.http.Send(() => new HttpRequestMessage(HttpMethod.Delete, Item(device, path)))) {
        if (r.StatusCode != HttpStatusCode.NotFound) {
          this.http.Ensure(r);
        }
      }
    }
  }
}
