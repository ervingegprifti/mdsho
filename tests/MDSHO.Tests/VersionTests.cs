using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>The app version is 1.5.8 everywhere: Directory.Build.props, the MSIX manifest and the built assemblies.</summary>
  public class VersionTests {
    private const string Expected = "1.5.8";

    private static string RepoRoot() {
      DirectoryInfo dir = new DirectoryInfo(AppContext.BaseDirectory);
      while (dir != null && !File.Exists(Path.Combine(dir.FullName, "MDSHO.slnx"))) {
        dir = dir.Parent;
      }
      Assert.True(dir != null, "Repository root (MDSHO.slnx) not found above " + AppContext.BaseDirectory);
      return dir.FullName;
    }

    [Fact]
    public void Directory_build_props_has_the_version() {
      XDocument props = XDocument.Load(Path.Combine(RepoRoot(), "Directory.Build.props"));
      string version = props.Descendants("Version").Single().Value;
      Assert.Equal(Expected, version);
    }

    [Fact]
    public void Package_manifest_identity_has_the_version_with_a_zero_fourth_part() {
      XDocument manifest = XDocument.Load(Path.Combine(RepoRoot(), "src", "MDSHO.Package", "Package.appxmanifest"));
      XElement identity = manifest.Descendants().Single(e => e.Name.LocalName == "Identity");
      Assert.Equal(Expected + ".0", identity.Attribute("Version").Value);
    }

    [Fact]
    public void Built_app_assembly_has_the_version() {
      Assert.Equal(new Version(Expected + ".0"), typeof(App).Assembly.GetName().Version);
    }
  }
}
