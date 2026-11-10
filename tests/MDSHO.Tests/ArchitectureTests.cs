using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// The view models (and the projects under them) are plain .NET: no WPF assemblies and no Windows-only target, so
  /// they can be reused outside the WPF app (for example by an MCP server).
  /// </summary>
  public class ArchitectureTests {
    private static readonly string[] Wpf = { "PresentationFramework", "PresentationCore", "WindowsBase" };

    [Theory]
    [InlineData(typeof(MDSHO.ViewModels.AppViewModel))]
    [InlineData(typeof(MDSHO.Common.AppConstants))]
    [InlineData(typeof(MDSHO.Data.DataStore))]
    [InlineData(typeof(MDSHO.Core.IDataStore))]
    public void Project_has_no_WPF_reference_and_a_plain_net10_target(Type typeInProject) {
      Assembly assembly = typeInProject.Assembly;
      string[] references = assembly.GetReferencedAssemblies().Select(a => a.Name).ToArray();
      foreach (string wpf in Wpf) {
        Assert.DoesNotContain(wpf, references);
      }
      Assert.Equal(".NETCoreApp,Version=v10.0", assembly.GetCustomAttribute<TargetFrameworkAttribute>().FrameworkName);
      Assert.Null(assembly.GetCustomAttribute<SupportedOSPlatformAttribute>());
    }
  }
}
