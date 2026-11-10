using System.IO;
using System.IO.Compression;
using Xunit;
using MDSHO.Data;

namespace MDSHO.Tests {

  /// <summary>Pins the names in an exported data.json (export format v1) so a C# rename can never change the file format.</summary>
  public class ExportFormatPinTests {
    [Fact]
    public void Exported_json_keeps_the_v1_property_names() {
      using (TempDb db = new TempDb()) {
        var window = DataStore.Windows.LoadAll()[0];
        DataStore.Clones.Create(window.ID, "D1", 1, 2);
        DataStore.Shortcuts.CreateGroup(null, window.ID);
        string zipPath = Path.Combine(db.Folder, "pin.zip");
        DataExporter.Export(zipPath);
        string data;
        using (ZipArchive zip = ZipFile.OpenRead(zipPath))
        using (StreamReader reader = new StreamReader(zip.GetEntry("data.json").Open())) {
          data = reader.ReadToEnd();
        }
        Assert.Contains("\"windows\"", data);
        Assert.Contains("\"clones\"", data);
        Assert.Contains("\"shortcuts\"", data);
        Assert.Contains("\"parentMDSHOWindowID\"", data);
        Assert.Contains("\"parentShortcutID\"", data);
        Assert.Contains("\"screenDeviceName\"", data);
        Assert.DoesNotContain("parentWindowID", data);
      }
    }
  }
}
