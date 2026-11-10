using System;
using System.Collections.Generic;
using MDSHO.Common;
using Xunit;

namespace MDSHO.Tests {

  public class AutoStartControllerTests {
    private class FakeService : IAutoStartService {
      public AutoStartState State = AutoStartState.Disabled;
      public AutoStartState EnableResult = AutoStartState.Enabled;
      public Exception Throw;
      public int EnableCalls, DisableCalls;
      public bool IsSupported => State != AutoStartState.NotPackaged;
      public AutoStartState GetState() {
        if (Throw != null) { throw Throw; }
        return State;
      }
      public AutoStartState Enable() {
        if (Throw != null) { throw Throw; }
        EnableCalls++;
        State = EnableResult;
        return State;
      }
      public void Disable() {
        if (Throw != null) { throw Throw; }
        DisableCalls++;
        State = AutoStartState.Disabled;
      }
    }

    private static (AutoStartController, FakeService, List<string>) Make(AutoStartState state) {
      FakeService service = new FakeService { State = state };
      List<string> messages = new List<string>();
      return (new AutoStartController(service, messages.Add), service, messages);
    }

    [Fact]
    public void IsChecked_OnlyWhenEnabled() {
      Assert.True(Make(AutoStartState.Enabled).Item1.IsChecked());
      foreach (AutoStartState state in new[] { AutoStartState.Disabled, AutoStartState.DisabledByUser, AutoStartState.DisabledByPolicy, AutoStartState.NotPackaged }) {
        Assert.False(Make(state).Item1.IsChecked());
      }
    }

    [Fact]
    public void Toggle_FromDisabled_EnablesAndChecks() {
      var (controller, service, messages) = Make(AutoStartState.Disabled);
      controller.Toggle();
      Assert.Equal(1, service.EnableCalls);
      Assert.True(controller.IsChecked());
      Assert.Empty(messages);
    }

    [Fact]
    public void Toggle_FromEnabled_Disables() {
      var (controller, service, messages) = Make(AutoStartState.Enabled);
      controller.Toggle();
      Assert.Equal(1, service.DisableCalls);
      Assert.Equal(0, service.EnableCalls);
      Assert.False(controller.IsChecked());
      Assert.Empty(messages);
    }

    [Fact]
    public void Toggle_EnableDeniedByUser_ShowsTaskManagerMessage() {
      var (controller, service, messages) = Make(AutoStartState.Disabled);
      service.EnableResult = AutoStartState.DisabledByUser;
      controller.Toggle();
      Assert.Single(messages);
      Assert.Contains("Task Manager", messages[0]);
      Assert.False(controller.IsChecked());
    }

    [Fact]
    public void Toggle_DisabledByUser_NoEnableCall_ShowsTaskManagerMessage() {
      var (controller, service, messages) = Make(AutoStartState.DisabledByUser);
      controller.Toggle();
      Assert.Equal(0, service.EnableCalls);
      Assert.Equal(0, service.DisableCalls);
      Assert.Single(messages);
      Assert.Contains("Task Manager", messages[0]);
      Assert.Contains("Startup", messages[0]);
    }

    [Fact]
    public void Toggle_NotPackaged_ShowsInstalledVersionMessage() {
      var (controller, service, messages) = Make(AutoStartState.NotPackaged);
      controller.Toggle();
      Assert.Equal(0, service.EnableCalls);
      Assert.Equal(0, service.DisableCalls);
      Assert.Single(messages);
      Assert.Contains("installed", messages[0]);
      Assert.Contains("Store", messages[0]);
    }

    [Fact]
    public void Toggle_DisabledByPolicy_ShowsPolicyMessage() {
      var (controller, service, messages) = Make(AutoStartState.DisabledByPolicy);
      controller.Toggle();
      Assert.Equal(0, service.EnableCalls);
      Assert.Equal(0, service.DisableCalls);
      Assert.Single(messages);
      Assert.Contains("policy", messages[0]);
    }

    [Fact]
    public void Toggle_ServiceThrows_MessageShown_NoCrash() {
      var (controller, service, messages) = Make(AutoStartState.Disabled);
      service.Throw = new InvalidOperationException("boom");
      controller.Toggle();
      Assert.Single(messages);
      Assert.Contains("boom", messages[0]);
    }

    [Fact]
    public void IsChecked_ServiceThrows_ReturnsFalse() {
      var (controller, service, messages) = Make(AutoStartState.Enabled);
      service.Throw = new InvalidOperationException("boom");
      Assert.False(controller.IsChecked());
    }
  }
}
