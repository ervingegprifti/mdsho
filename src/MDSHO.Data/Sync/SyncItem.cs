using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace MDSHO.Data {

  /// <summary>
  /// One panel or button as an item file: its SyncId, change record, where it sits (the SyncId of its group and of its
  /// panel), its synced columns and the hash of its shortcut file. The base is the version it was changed from, so a
  /// reader can tell a change made on top of its own version from a clash.
  /// </summary>
  internal sealed class SyncItem {
    public const string PanelKind = "panel";
    public const string ButtonKind = "button";
    /// <summary>The prefix of the note window's synced columns in <see cref="Fields"/>.</summary>
    public const string NoteWindowPrefix = "NoteWindows.";

    public string Kind { get; set; } = ButtonKind;
    public string SyncId { get; set; } = "";
    public long ChangedAt { get; set; }
    public string ChangedBy { get; set; } = "";
    public long BaseAt { get; set; }
    public string BaseBy { get; set; } = "";
    /// <summary>The group it is in, or null at the top of its panel (always null for a panel).</summary>
    public string Parent { get; set; }
    /// <summary>The panel it is in (null for a panel).</summary>
    public string Panel { get; set; }
    /// <summary>The synced columns: column name to a long, a string or null.</summary>
    public SortedDictionary<string, object> Fields { get; set; } = new SortedDictionary<string, object>(StringComparer.Ordinal);
    /// <summary>The blob with the shortcut file's bytes, or null.</summary>
    public string FileHash { get; set; }

    public bool IsPanel => Kind == PanelKind;

    /// <summary>The value of a field as text ("" when missing).</summary>
    public string Text(string field) {
      return Fields.TryGetValue(field, out object v) && v != null ? Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture) : "";
    }

    /// <summary>The same item always gives the same bytes: keys are sorted.</summary>
    public byte[] ToBytes() {
      using (MemoryStream stream = new MemoryStream())
      using (Utf8JsonWriter w = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true })) {
        w.WriteStartObject();
        w.WriteNumber("baseAt", BaseAt);
        w.WriteString("baseBy", BaseBy ?? "");
        w.WriteNumber("changedAt", ChangedAt);
        w.WriteString("changedBy", ChangedBy ?? "");
        w.WriteStartObject("fields");
        foreach (KeyValuePair<string, object> f in Fields) {
          switch (f.Value) {
            case null: w.WriteNull(f.Key); break;
            case long l: w.WriteNumber(f.Key, l); break;
            case int i: w.WriteNumber(f.Key, i); break;
            case double d: w.WriteNumber(f.Key, d); break;
            default: w.WriteString(f.Key, Convert.ToString(f.Value, System.Globalization.CultureInfo.InvariantCulture)); break;
          }
        }
        w.WriteEndObject();
        if (FileHash == null) { w.WriteNull("fileHash"); } else { w.WriteString("fileHash", FileHash); }
        w.WriteString("kind", Kind);
        if (Panel == null) { w.WriteNull("panel"); } else { w.WriteString("panel", Panel); }
        if (Parent == null) { w.WriteNull("parent"); } else { w.WriteString("parent", Parent); }
        w.WriteString("syncId", SyncId);
        w.WriteEndObject();
        w.Flush();
        return stream.ToArray();
      }
    }

    /// <summary>The item in the bytes, or null when they are not an item.</summary>
    public static SyncItem Parse(byte[] bytes) {
      try {
        using (JsonDocument doc = JsonDocument.Parse(bytes)) {
          JsonElement r = doc.RootElement;
          SyncItem item = new SyncItem {
            Kind = Str(r, "kind"),
            SyncId = Str(r, "syncId"),
            ChangedAt = Num(r, "changedAt"),
            ChangedBy = Str(r, "changedBy") ?? "",
            BaseAt = Num(r, "baseAt"),
            BaseBy = Str(r, "baseBy") ?? "",
            Parent = Str(r, "parent"),
            Panel = Str(r, "panel"),
            FileHash = Str(r, "fileHash")
          };
          if (string.IsNullOrEmpty(item.SyncId) || (item.Kind != PanelKind && item.Kind != ButtonKind)) {
            return null;
          }
          if (r.TryGetProperty("fields", out JsonElement fields) && fields.ValueKind == JsonValueKind.Object) {
            foreach (JsonProperty p in fields.EnumerateObject()) {
              switch (p.Value.ValueKind) {
                case JsonValueKind.Number:
                  if (p.Value.TryGetInt64(out long l)) { item.Fields[p.Name] = l; }
                  break;
                case JsonValueKind.String: item.Fields[p.Name] = p.Value.GetString(); break;
                case JsonValueKind.Null: item.Fields[p.Name] = null; break;
              }
            }
          }
          return item;
        }
      } catch (JsonException) {
        return null;
      }
    }

    private static string Str(JsonElement r, string name) {
      return r.TryGetProperty(name, out JsonElement v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    }

    private static long Num(JsonElement r, string name) {
      return r.TryGetProperty(name, out JsonElement v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out long l) ? l : 0;
    }

    public override string ToString() {
      return Encoding.UTF8.GetString(ToBytes());
    }
  }
}
