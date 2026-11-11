using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json.Nodes;
using MDSHO.Common;
using MDSHO.Core;
using MDSHO.Data;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>Export files no longer carry RoundedCorners; older files that have it still import, with the field ignored.</summary>
  public class ExportRoundedCornersTests : IDisposable {
    private readonly TempDb db = new TempDb();
    public void Dispose() { db.Dispose(); }

    private static string ReadEntry(string zip, string entry) {
      using (ZipArchive archive = ZipFile.OpenRead(zip))
      using (StreamReader reader = new StreamReader(archive.GetEntry(entry).Open())) {
        return reader.ReadToEnd();
      }
    }

    private static void WriteEntry(string zip, string entry, string text) {
      using (ZipArchive archive = ZipFile.Open(zip, ZipArchiveMode.Update)) {
        archive.GetEntry(entry).Delete();
        using (StreamWriter writer = new StreamWriter(archive.CreateEntry(entry).Open())) {
          writer.Write(text);
        }
      }
    }

    private string ExportWithAClone() {
      DesktopWindow window = DataStore.Windows.LoadAll()[0];
      DataStore.Windows.UpdateTitle(window.ID, "Old file");
      DataStore.Clones.Create(window.ID, "D1", 7, 8);
      string zip = Path.Combine(db.Folder, "export.zip");
      DataExporter.Export(zip);
      return zip;
    }

    [Fact]
    public void New_export_has_no_RoundedCorners_and_the_current_schema_version() {
      string zip = ExportWithAClone();
      Assert.DoesNotContain("roundedCorners", ReadEntry(zip, "data.json"), StringComparison.OrdinalIgnoreCase);
      JsonNode manifest = JsonNode.Parse(ReadEntry(zip, "manifest.json"));
      Assert.Equal(MDSHO.Common.AppConstants.DatabaseVersion, (int)manifest["schemaVersion"]);
      Assert.Equal(ExportManifest.CurrentFormatVersion, (int)manifest["formatVersion"]);
    }

    [Fact]
    public void Older_export_with_RoundedCorners_still_imports() {
      string zip = ExportWithAClone();
      // Turn it into a file as version 4 wrote it: RoundedCorners on every window and clone.
      JsonNode data = JsonNode.Parse(ReadEntry(zip, "data.json"));
      foreach (JsonNode w in data["windows"].AsArray()) { w["roundedCorners"] = false; }
      foreach (JsonNode c in data["clones"].AsArray()) { c["roundedCorners"] = true; }
      WriteEntry(zip, "data.json", data.ToJsonString());
      JsonNode manifest = JsonNode.Parse(ReadEntry(zip, "manifest.json"));
      manifest["schemaVersion"] = 4;
      WriteEntry(zip, "manifest.json", manifest.ToJsonString());
      Assert.Contains("\"roundedCorners\"", ReadEntry(zip, "data.json"));

      DataImporter.Inspect(zip);
      DataImporter.Import(zip, ImportMode.Replace);

      DesktopWindow imported = Assert.Single(DataStore.Windows.GetAll());
      Assert.Equal("Old file", imported.Title);
      DesktopWindowClone clone = Assert.Single(DataStore.Clones.GetAll());
      Assert.Equal((7L, 8L, "D1"), (clone.Left, clone.Top, clone.ScreenDeviceName));
      Assert.Equal(0, db.Scalar("SELECT count(*) FROM pragma_table_info('MDSHOWindows') WHERE name='RoundedCorners'"));
    }
  }
}
