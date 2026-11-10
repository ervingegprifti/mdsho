using System;

namespace MDSHO.Common {

  /// <summary>Why Start with Windows is on or off.</summary>
  public enum AutoStartState {
    /// <summary>Switched on.</summary>
    Enabled,
    /// <summary>Allowed but switched off.</summary>
    Disabled,
    /// <summary>Switched off in Windows Settings; only the user can turn it on again there.</summary>
    DisabledByUser,
    /// <summary>Switched off by an administrator policy; the user cannot turn it on.</summary>
    DisabledByPolicy,
    /// <summary>Not available because MDSHO is not running from the installed package.</summary>
    NotPackaged
  }

  /// <summary>Seam over the MSIX StartupTask API.</summary>
  public interface IAutoStartService {
    /// <summary>True when Start with Windows can be used at all.</summary>
    bool IsSupported { get; }
    /// <summary>Reads the current state.</summary>
    AutoStartState GetState();
    /// <summary>Switches Start with Windows on and returns the resulting state.</summary>
    AutoStartState Enable();
    /// <summary>Switches Start with Windows off.</summary>
    void Disable();
  }

  /// <summary>Toggle logic of the "Start with Windows" tray item.</summary>
  public class AutoStartController {
    /// <summary>Title of the message boxes shown by the controller.</summary>
    public const string MessageTitle = "Start with Windows";
    private readonly IAutoStartService service;
    private readonly Action<string> showMessage;

    /// <summary>Creates the controller over a service and a function that shows messages to the user.</summary>
    public AutoStartController(IAutoStartService service, Action<string> showMessage) {
      this.service = service;
      this.showMessage = showMessage;
    }

    /// <summary>Whether the tray item is checked (never throws).</summary>
    public bool IsChecked() {
      try {
        return service.GetState() == AutoStartState.Enabled;
      } catch (Exception) {
        return false;
      }
    }

    /// <summary>Handles a click on the tray item (never throws).</summary>
    public void Toggle() {
      try {
        AutoStartState state = service.GetState();
        switch (state) {
          case AutoStartState.Enabled:
            service.Disable();
            break;
          case AutoStartState.Disabled:
            AutoStartState result = service.Enable();
            if (result == AutoStartState.DisabledByUser) {
              showMessage(DisabledByUserMessage);
            } else if (result == AutoStartState.DisabledByPolicy) {
              showMessage(DisabledByPolicyMessage);
            }
            break;
          case AutoStartState.DisabledByUser:
            showMessage(DisabledByUserMessage);
            break;
          case AutoStartState.DisabledByPolicy:
            showMessage(DisabledByPolicyMessage);
            break;
          default:
            showMessage("Start with Windows only works in the installed (Microsoft Store / MSIX) version of MDSHO.");
            break;
        }
      } catch (Exception ex) {
        try {
          showMessage("Could not change Start with Windows: " + ex.Message);
        } catch (Exception) {
        }
      }
    }

    private const string DisabledByUserMessage = "Start with Windows was turned off in Windows. To enable it, open Task Manager -> Startup apps, select MDSHO and click Enable.";
    private const string DisabledByPolicyMessage = "Start with Windows is blocked by a policy on this computer.";
  }
}
