using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MDSHO.Tests {

  /// <summary>
  /// A small Google Drive API v3 for tests: the calls MDSHO makes (list with a query, create metadata, upload media,
  /// download, delete, about). Files are ID-based with one parent, as in Drive.
  /// </summary>
  public sealed class FakeGoogleDrive {
    private const string Folder = "application/vnd.google-apps.folder";
    private readonly object gate = new object();
    private int next = 1;

    public sealed class File {
      public string Id, Name, Parent, MimeType;
      public byte[] Bytes = new byte[0];
      public DateTime Modified = DateTime.UtcNow;
    }

    public List<File> Files { get; } = new List<File>();
    public string Token { get; set; } = "Bearer at";
    public Func<FakeRequest, HttpResponseMessage> Before { get; set; }

    public FakeHttp Http() => new FakeHttp(Handle);

    private static HttpResponseMessage Json(object o, HttpStatusCode code = HttpStatusCode.OK) => FakeHttp.Json(JsonSerializer.Serialize(o), code);

    private static object Meta(File f) => new Dictionary<string, object> {
      ["id"] = f.Id, ["name"] = f.Name, ["mimeType"] = f.MimeType, ["modifiedTime"] = f.Modified.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture)
    };

    public HttpResponseMessage Handle(FakeRequest r) {
      HttpResponseMessage early = Before?.Invoke(r);
      if (early != null) {
        return early;
      }
      if (r.Authorization != Token) {
        return FakeHttp.Status(HttpStatusCode.Unauthorized);
      }
      lock (this.gate) {
        Uri u = new Uri(r.Url);
        var q = System.Web.HttpUtility.ParseQueryString(u.Query);
        string path = u.AbsolutePath;
        if (r.Method == "GET" && path == "/drive/v3/about") {
          return Json(new { user = new { displayName = "Ann", emailAddress = "ann@example.com" } });
        }
        if (r.Method == "GET" && path == "/drive/v3/files") {
          IEnumerable<File> found = Files;
          string query = q["q"] ?? "";
          Match parent = Regex.Match(query, @"'([^']+)' in parents");
          if (parent.Success) {
            string p = parent.Groups[1].Value == "root" ? "root" : parent.Groups[1].Value;
            found = found.Where(f => f.Parent == p);
          }
          Match name = Regex.Match(query, @"name = '((?:[^'\\]|\\.)*)'");
          if (name.Success) {
            string n = name.Groups[1].Value.Replace("\\'", "'").Replace("\\\\", "\\");
            found = found.Where(f => f.Name == n);
          }
          if (query.Contains("mimeType = '" + Folder + "'")) {
            found = found.Where(f => f.MimeType == Folder);
          }
          List<File> all = found.OrderBy(f => f.Id, StringComparer.Ordinal).ToList();
          // Pages of 2, to check that MDSHO reads every page.
          int start = int.TryParse(q["pageToken"], out int s) ? s : 0;
          List<File> page = all.Skip(start).Take(2).ToList();
          Dictionary<string, object> result = new Dictionary<string, object> { ["files"] = page.Select(Meta).ToList() };
          if (start + 2 < all.Count) {
            result["nextPageToken"] = (start + 2).ToString(CultureInfo.InvariantCulture);
          }
          return Json(result);
        }
        if (r.Method == "POST" && path == "/drive/v3/files") {
          using (JsonDocument doc = JsonDocument.Parse(r.Body)) {
            JsonElement m = doc.RootElement;
            File f = new File {
              Id = "id" + (this.next++), Name = m.GetProperty("name").GetString(),
              Parent = m.TryGetProperty("parents", out JsonElement ps) ? ps[0].GetString() : "root",
              MimeType = m.TryGetProperty("mimeType", out JsonElement mt) ? mt.GetString() : "application/octet-stream"
            };
            Files.Add(f);
            return Json(Meta(f));
          }
        }
        Match media = Regex.Match(path, "^/upload/drive/v3/files/([^/]+)$");
        if (r.Method == "PATCH" && media.Success && q["uploadType"] == "media") {
          File f = Files.FirstOrDefault(x => x.Id == media.Groups[1].Value);
          if (f == null) {
            return FakeHttp.Status(HttpStatusCode.NotFound);
          }
          f.Bytes = r.BodyBytes;
          f.Modified = DateTime.UtcNow;
          return Json(Meta(f));
        }
        Match one = Regex.Match(path, "^/drive/v3/files/([^/]+)$");
        if (one.Success) {
          File f = Files.FirstOrDefault(x => x.Id == one.Groups[1].Value);
          if (f == null) {
            return FakeHttp.Status(HttpStatusCode.NotFound);
          }
          if (r.Method == "GET" && q["alt"] == "media") {
            return FakeHttp.Bytes(f.Bytes);
          }
          if (r.Method == "DELETE") {
            Files.Remove(f);
            return FakeHttp.Status(HttpStatusCode.NoContent);
          }
        }
        return FakeHttp.Status(HttpStatusCode.BadRequest);
      }
    }
  }

  /// <summary>
  /// A small Microsoft Graph OneDrive app folder for tests: path-based content download and upload (with
  /// conflictBehavior=fail), children listing with paging, delete and the app folder item.
  /// </summary>
  public sealed class FakeOneDrive {
    private const string Root = "/v1.0/me/drive/special/approot";
    private readonly object gate = new object();

    public Dictionary<string, (byte[] Bytes, DateTime Modified)> Files { get; } = new Dictionary<string, (byte[], DateTime)>(StringComparer.OrdinalIgnoreCase);
    public string Token { get; set; } = "Bearer at";
    public Func<FakeRequest, HttpResponseMessage> Before { get; set; }

    public FakeHttp Http() => new FakeHttp(Handle);

    private static HttpResponseMessage Json(object o, HttpStatusCode code = HttpStatusCode.OK) => FakeHttp.Json(JsonSerializer.Serialize(o), code);

    private IEnumerable<string> Folders(string under) {
      string prefix = under.Length == 0 ? "" : under + "/";
      return Files.Keys.Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && k.IndexOf('/', prefix.Length) > 0)
        .Select(k => k.Substring(prefix.Length, k.IndexOf('/', prefix.Length) - prefix.Length)).Distinct(StringComparer.OrdinalIgnoreCase);
    }

    public HttpResponseMessage Handle(FakeRequest r) {
      HttpResponseMessage early = Before?.Invoke(r);
      if (early != null) {
        return early;
      }
      if (r.Authorization != Token) {
        return FakeHttp.Status(HttpStatusCode.Unauthorized);
      }
      lock (this.gate) {
        Uri u = new Uri(r.Url);
        string path = Uri.UnescapeDataString(u.AbsolutePath);
        var q = System.Web.HttpUtility.ParseQueryString(u.Query);
        if (!path.StartsWith(Root, StringComparison.Ordinal)) {
          return FakeHttp.Status(HttpStatusCode.BadRequest);
        }
        string rest = path.Substring(Root.Length);
        if (rest == "" && r.Method == "GET") {
          return Json(new { id = "approot", createdBy = new { user = new { displayName = "Ann" } } });
        }
        string item;
        string action;
        if (rest == "/children") {
          item = "";
          action = "children";
        } else {
          Match m = Regex.Match(rest, "^:/(.+?)(:/(content|children))?:?$");
          if (!m.Success) {
            return FakeHttp.Status(HttpStatusCode.BadRequest);
          }
          item = m.Groups[1].Value;
          action = m.Groups[3].Success ? m.Groups[3].Value : "";
        }
        if (action == "children" && r.Method == "GET") {
          List<object> children = Folders(item).Select(n => (object)new Dictionary<string, object> { ["name"] = n, ["folder"] = new { childCount = 1 } }).ToList();
          string prefix = item.Length == 0 ? "" : item + "/";
          children.AddRange(Files.Where(f => f.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && f.Key.IndexOf('/', prefix.Length) < 0)
            .Select(f => (object)new Dictionary<string, object> {
              ["name"] = f.Key.Substring(prefix.Length), ["file"] = new { },
              ["lastModifiedDateTime"] = f.Value.Modified.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)
            }));
          if (item.Length > 0 && children.Count == 0) {
            return FakeHttp.Json("{\"error\":{\"code\":\"itemNotFound\"}}", HttpStatusCode.NotFound);
          }
          int skip = int.TryParse(q["skip"], out int s) ? s : 0;
          Dictionary<string, object> result = new Dictionary<string, object> { ["value"] = children.Skip(skip).Take(2).ToList() };
          if (skip + 2 < children.Count) {
            result["@odata.nextLink"] = "https://graph.microsoft.com" + Root + (item.Length == 0 ? "/children" : ":/" + item + ":/children") + "?skip=" + (skip + 2);
          }
          return Json(result);
        }
        if (action == "content" && r.Method == "GET") {
          return Files.TryGetValue(item, out var f) ? FakeHttp.Bytes(f.Bytes) : FakeHttp.Json("{\"error\":{\"code\":\"itemNotFound\"}}", HttpStatusCode.NotFound);
        }
        if (action == "content" && r.Method == "PUT") {
          if (q["@microsoft.graph.conflictBehavior"] == "fail" && Files.ContainsKey(item)) {
            return FakeHttp.Json("{\"error\":{\"code\":\"nameAlreadyExists\"}}", HttpStatusCode.Conflict);
          }
          Files[item] = (r.BodyBytes, DateTime.UtcNow);
          return Json(new { name = item });
        }
        if (action == "" && r.Method == "DELETE") {
          return Files.Remove(item) ? FakeHttp.Status(HttpStatusCode.NoContent) : FakeHttp.Json("{\"error\":{\"code\":\"itemNotFound\"}}", HttpStatusCode.NotFound);
        }
        return FakeHttp.Status(HttpStatusCode.BadRequest);
      }
    }
  }
}
