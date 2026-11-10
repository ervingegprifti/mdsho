using MDSHO.Common;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>How MDSHO starts itself again after an import (packaged builds must keep their package identity).</summary>
  public class RestartCommandTests {
    private const string Exe = @"C:\Program Files\WindowsApps\MDSHO_1.1.0.0_x64__abc\MDSHO.exe";

    [Fact]
    public void Packaged_starts_explorer_with_the_AppsFolder_moniker() {
      RestartCommand c = RestartCommand.Build(true, "MDSHO_abc123", Exe);
      Assert.Equal("explorer.exe", c.FileName);
      Assert.Equal(@"shell:AppsFolder\MDSHO_abc123!App", c.Arguments);
    }

    [Fact]
    public void Unpackaged_starts_the_exe() {
      RestartCommand c = RestartCommand.Build(false, null, @"C:\x\MDSHO.exe");
      Assert.Equal(@"C:\x\MDSHO.exe", c.FileName);
      Assert.Equal("", c.Arguments);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Packaged_without_a_family_name_falls_back_to_the_exe(string family) {
      RestartCommand c = RestartCommand.Build(true, family, Exe);
      Assert.Equal(Exe, c.FileName);
      Assert.Equal("", c.Arguments);
    }

    [Fact]
    public void Nothing_to_start_returns_null() {
      Assert.Null(RestartCommand.Build(false, null, ""));
      Assert.Null(RestartCommand.Build(false, null, null));
    }
  }
}
