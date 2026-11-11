using CommunityToolkit.Mvvm.ComponentModel;
using MDSHO.Common;

namespace MDSHO.ViewModels {

  /// <summary>The characters of the icon font; the solid or the outline set, depending on the Solid icons setting.</summary>
  public partial class IconGlyphs : ObservableObject {
    /// <summary>True for the solid set, false for the outline set. Changing it raises PropertyChanged for every glyph.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MDSHO), nameof(Times), nameof(Rename), nameof(Settings), nameof(Clones), nameof(DockLeft),
      nameof(DockRight), nameof(Shortcuts), nameof(Shortcut), nameof(Plus), nameof(Delete), nameof(Cut), nameof(Copy),
      nameof(Paste), nameof(Send), nameof(Database), nameof(Command), nameof(Bookmark), nameof(Share), nameof(Star),
      nameof(Folder), nameof(Globe), nameof(Warning), nameof(Run), nameof(Wizard), nameof(Info), nameof(Desktop))]
    private bool solidIcons;

    /// <summary>Icon font character of the MDSHO logo.</summary>
    public string MDSHO { get { return this.SolidIcons ? UnicodeText.FromHex("e900") : UnicodeText.FromHex("e901"); } }
    /// <summary>Icon font character for Times.</summary>
    public string Times { get { return this.SolidIcons ? UnicodeText.FromHex("e902") : UnicodeText.FromHex("e903"); } }
    /// <summary>Icon font character for Rename.</summary>
    public string Rename { get { return this.SolidIcons ? UnicodeText.FromHex("e904") : UnicodeText.FromHex("e905"); } }
    /// <summary>Icon font character for Settings.</summary>
    public string Settings { get { return this.SolidIcons ? UnicodeText.FromHex("e906") : UnicodeText.FromHex("e907"); } }
    /// <summary>Icon font character for Clones.</summary>
    public string Clones { get { return this.SolidIcons ? UnicodeText.FromHex("e90a") : UnicodeText.FromHex("e90b"); } }
    /// <summary>Icon font character for DockLeft.</summary>
    public string DockLeft { get { return this.SolidIcons ? UnicodeText.FromHex("e90c") : UnicodeText.FromHex("e90d"); } }
    /// <summary>Icon font character for DockRight.</summary>
    public string DockRight { get { return this.SolidIcons ? UnicodeText.FromHex("e910") : UnicodeText.FromHex("e911"); } }
    /// <summary>Icon font character for Shortcuts.</summary>
    public string Shortcuts { get { return this.SolidIcons ? UnicodeText.FromHex("e914") : UnicodeText.FromHex("e915"); } }
    /// <summary>Icon font character for Shortcut.</summary>
    public string Shortcut { get { return this.SolidIcons ? UnicodeText.FromHex("e916") : UnicodeText.FromHex("e917"); } }
    /// <summary>Icon font character for Plus.</summary>
    public string Plus { get { return this.SolidIcons ? UnicodeText.FromHex("e91a") : UnicodeText.FromHex("e91b"); } }
    /// <summary>Icon font character for Delete.</summary>
    public string Delete { get { return this.SolidIcons ? UnicodeText.FromHex("e91c") : UnicodeText.FromHex("e91d"); } }
    /// <summary>Icon font character for Cut.</summary>
    public string Cut { get { return this.SolidIcons ? UnicodeText.FromHex("e91e") : UnicodeText.FromHex("e91f"); } }
    /// <summary>Icon font character for Copy.</summary>
    public string Copy { get { return this.SolidIcons ? UnicodeText.FromHex("e920") : UnicodeText.FromHex("e921"); } }
    /// <summary>Icon font character for Paste.</summary>
    public string Paste { get { return this.SolidIcons ? UnicodeText.FromHex("e922") : UnicodeText.FromHex("e923"); } }
    /// <summary>Icon font character for Send.</summary>
    public string Send { get { return this.SolidIcons ? UnicodeText.FromHex("e934") : UnicodeText.FromHex("e935"); } }
    /// <summary>Icon font character for Database.</summary>
    public string Database { get { return this.SolidIcons ? UnicodeText.FromHex("e938") : UnicodeText.FromHex("e939"); } }
    /// <summary>Icon font character for Command.</summary>
    public string Command { get { return this.SolidIcons ? UnicodeText.FromHex("e93a") : UnicodeText.FromHex("e93b"); } }
    /// <summary>Icon font character for Bookmark.</summary>
    public string Bookmark { get { return this.SolidIcons ? UnicodeText.FromHex("e93c") : UnicodeText.FromHex("e93d"); } }
    /// <summary>Icon font character for Share.</summary>
    public string Share { get { return this.SolidIcons ? UnicodeText.FromHex("e93e") : UnicodeText.FromHex("e93f"); } }
    /// <summary>Icon font character for Star.</summary>
    public string Star { get { return this.SolidIcons ? UnicodeText.FromHex("e940") : UnicodeText.FromHex("e941"); } }
    /// <summary>Icon font character for Folder (added in font v1.13).</summary>
    public string Folder { get { return this.SolidIcons ? UnicodeText.FromHex("e944") : UnicodeText.FromHex("e945"); } }
    /// <summary>Icon font character for Globe, used for web links (added in font v1.13).</summary>
    public string Globe { get { return this.SolidIcons ? UnicodeText.FromHex("e946") : UnicodeText.FromHex("e947"); } }
    /// <summary>Icon font character for Warning, used for a shortcut whose target file or folder is missing.</summary>
    public string Warning { get { return this.SolidIcons ? UnicodeText.FromHex("e92c") : UnicodeText.FromHex("e92d"); } }
    /// <summary>Icon font character for Run, used for run command buttons (added in font v1.14 at the codes of the removed unknown icon).</summary>
    public string Run { get { return this.SolidIcons ? UnicodeText.FromHex("e948") : UnicodeText.FromHex("e949"); } }
    /// <summary>Icon font character for Wizard, used for the Examples button of the Run Command window (added in font v1.14).</summary>
    public string Wizard { get { return this.SolidIcons ? UnicodeText.FromHex("e94a") : UnicodeText.FromHex("e94b"); } }

    /// <summary>The desktop icon (Find MDSHO Lost Windows and Find Other Lost Windows in the tray menu).</summary>
    public string Desktop { get { return this.SolidIcons ? UnicodeText.FromHex("e92e") : UnicodeText.FromHex("e92f"); } }

    /// <summary>Icon font character for Info, used for the About window and the About menu items.</summary>
    public string Info { get { return this.SolidIcons ? UnicodeText.FromHex("e92a") : UnicodeText.FromHex("e92b"); } }

    /// <summary>The first icon of a shortcut of this kind, in the current (solid or outline) set.</summary>
    public string ForKind(ShortcutTargetKind kind) {
      switch (kind) {
        case ShortcutTargetKind.Folder: return this.Folder;
        case ShortcutTargetKind.WebLink: return this.Globe;
        case ShortcutTargetKind.Missing: return this.Warning;
        case ShortcutTargetKind.Command: return this.Run;
        case ShortcutTargetKind.Group: return this.Shortcuts;
        default: return this.Shortcut;
      }
    }


    /// <summary>Creates the set (solid or outline).</summary>
    public IconGlyphs(bool solidIcons) {
      this.solidIcons = solidIcons;
    }

  }

}