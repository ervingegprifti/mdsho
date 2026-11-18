using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using MDSHO.Common;
using MDSHO.Data;
using MDSHO.Infrastructure;
using MDSHO.ViewModels;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>Windows' app mode for tests: light or dark, changed by hand.</summary>
  public sealed class FakeWindowsAppMode : IWindowsAppMode {
    public bool Light { get; set; } = true;
    public bool Started { get; private set; }
    public bool AppsUseLightTheme() { return Light; }
    public event Action Changed;
    public void Start() { Started = true; }
    /// <summary>Windows switches to the other mode.</summary>
    public void Switch(bool light) {
      Light = light;
      Changed?.Invoke();
    }
    public void Dispose() { }
  }

  /// <summary>
  /// The "Window theme" setting: Same as Windows (the default), Light or Dark. App-wide, stored in AppSettings, not
  /// exported and kept by an import. "Same as Windows" follows Windows' app mode, read from the registry.
  /// </summary>
  public class WindowThemeSettingTests : IDisposable {
    private readonly TempDb db = new TempDb();
    private readonly FakeWindowsAppMode windows = new FakeWindowsAppMode();

    public void Dispose() { db.Dispose(); }

    private WindowThemeSetting NewSetting() { return new WindowThemeSetting(windows); }

    private static string Stored() { return DataStore.AppSettings.Get(WindowThemeSetting.SettingKey); }

    // ---- The choice and the stored value ----

    [Fact]
    public void The_choices_are_Same_as_Windows_Light_and_Dark_in_that_order() {
      Assert.Equal(new[] { WindowThemeChoice.SameAsWindows, WindowThemeChoice.Light, WindowThemeChoice.Dark },
        Enum.GetValues<WindowThemeChoice>());
    }

    [Fact]
    public void Same_as_Windows_is_the_default_and_nothing_is_saved_until_it_is_set() {
      Assert.Equal(WindowThemeChoice.SameAsWindows, NewSetting().Choice);
      Assert.Null(Stored());
    }

    [Theory]
    [InlineData(WindowThemeChoice.SameAsWindows, "SameAsWindows")]
    [InlineData(WindowThemeChoice.Light, "Light")]
    [InlineData(WindowThemeChoice.Dark, "Dark")]
    public void The_choice_is_saved_in_AppSettings_and_read_back(WindowThemeChoice choice, string text) {
      Assert.Equal("WindowTheme", WindowThemeSetting.SettingKey);
      NewSetting().SetChoice(choice);
      Assert.Equal(text, Stored());
      Assert.Equal(choice, NewSetting().Choice);
    }

    [Theory]
    [InlineData(null, WindowThemeChoice.SameAsWindows)]
    [InlineData("", WindowThemeChoice.SameAsWindows)]
    [InlineData("Purple", WindowThemeChoice.SameAsWindows)]
    [InlineData("dark", WindowThemeChoice.Dark)]
    [InlineData(" Light ", WindowThemeChoice.Light)]
    public void A_stored_value_is_read_leniently_and_an_unknown_one_means_Same_as_Windows(string text, WindowThemeChoice expected) {
      Assert.Equal(expected, WindowThemeSetting.Parse(text));
    }

    [Fact]
    public void A_setting_that_cannot_be_read_means_Same_as_Windows() {
      db.Exec("DROP TABLE AppSettings");
      WindowThemeSetting setting = NewSetting();
      Assert.Equal(WindowThemeChoice.SameAsWindows, setting.Choice);
      Assert.Equal(WindowThemeKind.Light, setting.Current);
    }

    // ---- The theme in use ----

    [Theory]
    [InlineData(WindowThemeChoice.SameAsWindows, true, WindowThemeKind.Light)]
    [InlineData(WindowThemeChoice.SameAsWindows, false, WindowThemeKind.Dark)]
    [InlineData(WindowThemeChoice.Light, false, WindowThemeKind.Light)]
    [InlineData(WindowThemeChoice.Light, true, WindowThemeKind.Light)]
    [InlineData(WindowThemeChoice.Dark, true, WindowThemeKind.Dark)]
    [InlineData(WindowThemeChoice.Dark, false, WindowThemeKind.Dark)]
    public void The_theme_in_use_comes_from_the_choice_and_for_Same_as_Windows_from_Windows(WindowThemeChoice choice, bool windowsLight, WindowThemeKind expected) {
      Assert.Equal(expected, WindowThemeSetting.Resolve(choice, windowsLight));
      windows.Light = windowsLight;
      WindowThemeSetting setting = NewSetting();
      setting.SetChoice(choice);
      Assert.Equal(expected, setting.Current);
    }

    [Fact]
    public void Changing_the_choice_raises_Changed() {
      WindowThemeSetting setting = NewSetting();
      int changed = 0;
      setting.Changed += () => changed++;
      setting.SetChoice(WindowThemeChoice.Dark);
      Assert.Equal(1, changed);
      Assert.Equal(WindowThemeKind.Dark, setting.Current);
    }

    [Fact]
    public void A_preview_shows_a_choice_at_once_without_saving_it_and_ending_it_goes_back() {
      WindowThemeSetting setting = NewSetting();
      int changed = 0;
      setting.Changed += () => changed++;
      setting.Preview(WindowThemeChoice.Dark);
      Assert.Equal(1, changed);
      Assert.Equal(WindowThemeKind.Dark, setting.Current);
      Assert.Equal(WindowThemeChoice.SameAsWindows, setting.Choice);
      Assert.Null(Stored());
      setting.Preview(null);
      Assert.Equal(2, changed);
      Assert.Equal(WindowThemeKind.Light, setting.Current);
    }

    [Fact]
    public void Saving_a_choice_ends_the_preview() {
      WindowThemeSetting setting = NewSetting();
      setting.Preview(WindowThemeChoice.Dark);
      setting.SetChoice(WindowThemeChoice.Light);
      Assert.Equal(WindowThemeKind.Light, setting.Current);
      setting.Preview(null);
      Assert.Equal(WindowThemeKind.Light, setting.Current);
    }

    [Fact]
    public void A_change_in_Windows_follows_the_previewed_choice() {
      WindowThemeSetting setting = NewSetting();
      setting.Start();
      int changed = 0;
      setting.Changed += () => changed++;
      setting.Preview(WindowThemeChoice.Light);
      windows.Switch(light: false);
      Assert.Equal(1, changed); // only the preview
      setting.SetChoice(WindowThemeChoice.Dark);
      setting.Preview(WindowThemeChoice.SameAsWindows);
      changed = 0;
      windows.Switch(light: true);
      Assert.Equal(1, changed);
      Assert.Equal(WindowThemeKind.Light, setting.Current);
    }

    [Fact]
    public void While_Same_as_Windows_a_change_in_Windows_switches_the_theme_live() {
      WindowThemeSetting setting = NewSetting();
      setting.Start();
      Assert.True(windows.Started);
      int changed = 0;
      setting.Changed += () => changed++;
      windows.Switch(light: false);
      Assert.Equal(1, changed);
      Assert.Equal(WindowThemeKind.Dark, setting.Current);
      windows.Switch(light: true);
      Assert.Equal(2, changed);
      Assert.Equal(WindowThemeKind.Light, setting.Current);
    }

    [Fact]
    public void With_Light_or_Dark_chosen_a_change_in_Windows_changes_nothing() {
      WindowThemeSetting setting = NewSetting();
      setting.Start();
      setting.SetChoice(WindowThemeChoice.Light);
      int changed = 0;
      setting.Changed += () => changed++;
      windows.Switch(light: false);
      Assert.Equal(0, changed);
      Assert.Equal(WindowThemeKind.Light, setting.Current);
    }

    // ---- Export and import ----

    [Fact]
    public void The_theme_is_exported_and_only_an_import_that_replaces_the_data_sets_it() {
      NewSetting().SetChoice(WindowThemeChoice.Dark);
      new AppViewModel().AddWindow("TEST", 0, 0);
      string zip = Path.Combine(db.Folder, "export.zip");
      DataExporter.Export(zip);
      using (ZipArchive archive = ZipFile.OpenRead(zip))
      using (StreamReader reader = new StreamReader(archive.GetEntry("data.json").Open())) {
        Assert.Equal("Dark", (string)System.Text.Json.Nodes.JsonNode.Parse(reader.ReadToEnd())["settings"][WindowThemeSetting.SettingKey]);
      }
      DataStore.AppSettings.Set(WindowThemeSetting.SettingKey, "Light");
      DataImporter.Import(zip, ImportMode.AddAsNew);
      Assert.Equal("Light", Stored());
      DataImporter.Import(zip, ImportMode.Replace);
      Assert.Equal("Dark", Stored());
    }

    // ---- Windows' app mode, from the registry ----

    [Fact]
    public void The_registry_value_is_AppsUseLightTheme_under_Personalize() {
      Assert.Equal(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", WindowsAppMode.RegistryKey);
      Assert.Equal("AppsUseLightTheme", WindowsAppMode.RegistryValue);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(null, true)]
    [InlineData("0", true)]
    public void Zero_means_dark_and_anything_else_or_no_value_means_light(object value, bool light) {
      Assert.Equal(light, WindowsAppMode.IsLight(value));
      Assert.Equal(light, new WindowsAppMode(() => value).AppsUseLightTheme());
    }

    [Fact]
    public void A_registry_read_that_fails_means_light() {
      Assert.True(new WindowsAppMode(() => throw new UnauthorizedAccessException()).AppsUseLightTheme());
    }

    [Fact]
    public void Changed_is_raised_only_when_the_mode_really_changes() {
      object value = 1;
      using (WindowsAppMode mode = new WindowsAppMode(() => value)) {
        int changed = 0;
        mode.Changed += () => changed++;
        mode.CheckForChange();
        Assert.Equal(0, changed);
        value = 0;
        mode.CheckForChange();
        Assert.Equal(1, changed);
        mode.CheckForChange();
        Assert.Equal(1, changed);
      }
    }
  }
}
