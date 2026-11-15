using System;
using System.Threading;
using MDSHO.Common;
using MDSHO.Infrastructure;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// The real RegisterHotKey wrapper, with a combination nobody uses (Ctrl+Shift+Alt+Win+F23) that is released at the end.
  /// No key is ever pressed.
  /// </summary>
  public class GlobalHotkeyServiceTests {
    private static readonly Hotkey Unused = new Hotkey(HotkeyModifiers.Control | HotkeyModifiers.Shift | HotkeyModifiers.Alt | HotkeyModifiers.Win, 0x86);

    private static void OnStaThread(Action body) {
      Exception failure = null;
      Thread thread = new Thread(() => {
        try { body(); } catch (Exception ex) { failure = ex; }
      });
      thread.SetApartmentState(ApartmentState.STA);
      thread.Start();
      thread.Join();
      if (failure != null) {
        throw new Exception("STA body failed: " + failure, failure);
      }
    }

    [Fact]
    public void Keys_held_by_one_holder_are_refused_to_another_until_released() {
      OnStaThread(() => {
        using (GlobalHotkeyService first = new GlobalHotkeyService())
        using (GlobalHotkeyService second = new GlobalHotkeyService()) {
          Assert.True(first.Register(Unused));
          Assert.False(second.Register(Unused));
          first.Unregister();
          Assert.True(second.Register(Unused));
          second.Unregister();
        }
      });
    }

    [Fact]
    public void Register_replaces_the_keys_and_Dispose_releases_them() {
      OnStaThread(() => {
        Hotkey other = new Hotkey(Unused.Modifiers, 0x85); // F22
        using (GlobalHotkeyService probe = new GlobalHotkeyService()) {
          GlobalHotkeyService service = new GlobalHotkeyService();
          Assert.True(service.Register(Unused));
          Assert.True(service.Register(other));
          Assert.True(probe.Register(Unused)); // the first keys were released
          probe.Unregister();
          service.Dispose();
          Assert.True(probe.Register(other));
          probe.Unregister();
        }
      });
    }

    [Fact]
    public void Creating_the_service_registers_nothing() {
      OnStaThread(() => {
        using (GlobalHotkeyService service = new GlobalHotkeyService()) {
          service.Unregister();
        }
      });
    }

    [Fact]
    public void The_app_services_use_the_real_hotkey_service_by_default() {
      Assert.IsType<GlobalHotkeyService>(MDSHO.Helpers.AppServices.Defaults().HotkeyService);
    }
  }
}
