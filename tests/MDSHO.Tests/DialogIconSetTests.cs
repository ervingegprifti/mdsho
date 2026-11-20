using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Controls;
using MDSHO.Common;
using MDSHO.Controls;
using MDSHO.Helpers;
using MDSHO.ViewModels;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// A dialog raised by a panel or one of its buttons uses that panel's Solid icons setting (clones share it) for the title
  /// bar logo, the Info or Warning icon and the close glyph; app-wide dialogs (tray exit, export, import, startup, the
  /// unexpected-error window) stay solid, like About and Settings.
  /// </summary>
  public class DialogIconSetTests {

    private static void AssertIcons(MessageWindow window, bool solid, bool warning) {
      IconGlyphs icons = new IconGlyphs(solid);
      DialogTitleBar bar = (DialogTitleBar)window.FindName("TitleBar");
      Assert.Equal(icons.MDSHO, bar.Glyph);
      Assert.Equal(icons.Times, bar.CloseGlyph);
      Assert.Equal(warning ? icons.Warning : icons.Info, ((TextBlock)window.FindName("TextIcon")).Text);
    }

    private static MessageWindow Captured(Action show) {
      Func<MessageWindow, MessageResult> old = MdshoMessage.ShowWindow;
      List<MessageWindow> shown = new List<MessageWindow>();
      MdshoMessage.ShowWindow = w => { shown.Add(w); return MessageResult.No; };
      try {
        show();
      } finally {
        MdshoMessage.ShowWindow = old;
      }
      MessageWindow window = Assert.Single(shown);
      window.Close();
      return window;
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void The_dialog_uses_the_icon_set_it_is_given(bool solid) {
      WpfAppHost.Run(() => {
        MessageWindow window = new MessageWindow("T", "H.", "", MessageIcon.Warning, MessageButtons.OK, MessageResult.None, solid);
        AssertIcons(window, solid, warning: true);
        window.Close();
        MessageWindow info = Captured(() => MdshoMessage.Show(null, "T", "H.", MessageIcon.Info, MessageButtons.OK, MessageResult.None, solid));
        AssertIcons(info, solid, warning: false);
      });
    }

    [Fact]
    public void Without_an_icon_set_a_dialog_is_solid() {
      WpfAppHost.Run(() => {
        MessageWindow window = new MessageWindow("T", "H.", "", MessageIcon.Info, MessageButtons.OK, MessageResult.None);
        AssertIcons(window, true, warning: false);
        window.Close();
      });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Delete_window_uses_the_panels_icon_set(bool solid) {
      WpfAppHost.Run(() => {
        MessageWindow window = Captured(() => AppMessages.ConfirmDeleteWindow(null, solid));
        AssertIcons(window, solid, warning: true);
      });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void The_drop_message_uses_the_panels_icon_set(bool solid) {
      WpfAppHost.Run(() => {
        MessageWindow window = Captured(() => AppMessages.VirtualFilesDropped(solid));
        AssertIcons(window, solid, warning: false);
      });
    }

    [Fact]
    public void App_wide_dialogs_stay_solid() {
      WpfAppHost.Run(() => {
        AssertIcons(Captured(() => AppMessages.ConfirmExit()), true, warning: true);
        AssertIcons(Captured(() => AppMessages.ExportDone("Done.")), true, warning: false);
        AssertIcons(Captured(() => AppMessages.AlreadyRunning()), true, warning: false);
        ErrorWindow error = new ErrorWindow(new InvalidOperationException("x"));
        AssertIcons(error, true, warning: true);
        error.Close();
      });
    }

    [Fact]
    public void The_icon_set_scope_is_solid_by_default_and_restored_after_use() {
      Assert.True(Dialogs.SolidIcons);
      using (Dialogs.UseIcons(false)) {
        Assert.False(Dialogs.SolidIcons);
        using (Dialogs.UseIcons(true)) {
          Assert.True(Dialogs.SolidIcons);
        }
        Assert.False(Dialogs.SolidIcons);
      }
      Assert.True(Dialogs.SolidIcons);
    }

    // ---- Messages raised by a button go through WpfBridge with the panel's icon set ----

    private sealed class PanelFixture : IDisposable {
      public readonly TempDb Db = new TempDb();
      public readonly AppViewModel App = new AppViewModel();
      public readonly DesktopWindowViewModel Win;
      private readonly Func<string, string, bool> oldConfirm = Dialogs.Confirm;
      private readonly Action<string, string> oldMessage = Dialogs.ShowMessage;
      private readonly Action<string, string, string> oldError = Dialogs.ShowError;
      private readonly Action<string, string, string> oldInfo = Dialogs.ShowInfo;
      private readonly Action<CommandErrorWindow> oldErrorShow = CommandErrorWindow.ShowWindow;
      private readonly Action<PasteMessageWindow> oldPasteShow = PasteMessageWindow.ShowWindow;
      private readonly Action<ProcessStartInfo> oldStarter = ShortcutViewModel.ProcessStarter;
      public readonly List<MessageWindow> Shown = new List<MessageWindow>();

      public PanelFixture(bool solid) {
        Win = App.AddWindow("TEST", 0, 0);
        Win.Appearance.SolidIcons = solid;
        WpfBridge.Connect();
        CommandErrorWindow.ShowWindow = w => Shown.Add(w);
        PasteMessageWindow.ShowWindow = w => Shown.Add(w);
      }

      public void Dispose() {
        Shown.ForEach(w => w.Close());
        Dialogs.Confirm = oldConfirm;
        Dialogs.ShowMessage = oldMessage;
        Dialogs.ShowError = oldError;
        Dialogs.ShowInfo = oldInfo;
        CommandErrorWindow.ShowWindow = oldErrorShow;
        PasteMessageWindow.ShowWindow = oldPasteShow;
        ShortcutViewModel.ProcessStarter = oldStarter;
        Db.Dispose();
      }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_broken_shortcut_uses_its_panels_icon_set(bool solid) {
      WpfAppHost.Run(() => {
        using (PanelFixture f = new PanelFixture(solid)) {
          string guid = Identifiers.NewGuid();
          ShortcutViewModel s = ViewModelFactory.NewShortcut(f.App, null, f.Win, "Tool", @"C:\Target\Tool.exe", ".lnk", guid);
          f.Win.RootShortcuts.Add(s);
          File.WriteAllText(Paths.GetShortcutFilePath(guid, ".lnk"), "x");
          ShortcutViewModel.ProcessStarter = psi => throw new Win32Exception(2);
          s.RunShortcut(false);
          AssertIcons(Assert.Single(f.Shown), solid, warning: true);
          Assert.True(Dialogs.SolidIcons);
        }
      });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_run_command_error_uses_its_panels_icon_set(bool solid) {
      WpfAppHost.Run(() => {
        using (PanelFixture f = new PanelFixture(solid)) {
          ShortcutViewModel s = ViewModelFactory.NewProcessCommandShortcut(f.App, null, f.Win);
          f.Win.RootShortcuts.Add(s);
          s.ProcessFileName = "missing.exe";
          ShortcutViewModel.ProcessStarter = psi => throw new Win32Exception(2);
          s.RunShortcut(false);
          new RunCommandViewModel(s, false).TestRun();
          Assert.Equal(2, f.Shown.Count);
          f.Shown.ForEach(w => AssertIcons(w, solid, warning: true));
        }
      });
    }

    [Fact]
    public void A_button_in_a_clone_uses_the_parents_setting() {
      WpfAppHost.Run(() => {
        using (PanelFixture f = new PanelFixture(false)) {
          f.Win.CloneWindow("TEST", 10, 10);
          DesktopWindowViewModel clone = System.Linq.Enumerable.Last(f.Win.Clones);
          Assert.Same(f.Win.Appearance, clone.Appearance);
          Assert.False(clone.Appearance.SolidIcons);
          ShortcutViewModel s = ViewModelFactory.NewProcessCommandShortcut(f.App, null, f.Win);
          f.Win.RootShortcuts.Add(s);
          s.ProcessFileName = "missing.exe";
          ShortcutViewModel.ProcessStarter = psi => throw new Win32Exception(2);
          s.RunShortcut(false);
          AssertIcons(Assert.Single(f.Shown), false, warning: true);
        }
      });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Deleting_a_button_asks_with_its_panels_icon_set(bool solid) {
      WpfAppHost.Run(() => {
        using (PanelFixture f = new PanelFixture(solid)) {
          ShortcutViewModel s = ViewModelFactory.NewProcessCommandShortcut(f.App, null, f.Win);
          f.Win.RootShortcuts.Add(s);
          MessageWindow window = Captured(() => s.DeleteShortcutCommand.Execute(null));
          AssertIcons(window, solid, warning: true);
          Assert.Contains(s, f.Win.RootShortcuts); // the hook answered No
        }
      });
    }
  }
}
