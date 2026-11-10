using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using MDSHO.Common;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>Rate / Share MDSHO: one implementation used by the About window and the window title menu, no longer by every desktop window footer.</summary>
  public class StoreActionsTests {

    /// <summary>Records every call in order; can be told to fail a step.</summary>
    private class FakeHost : IStoreActionsHost {
      public readonly List<string> Calls = new List<string>();
      public readonly List<Exception> Errors = new List<Exception>();
      public bool FailClipboard;
      public bool FailLaunch;
      public void CopyToClipboard(string text) {
        if (FailClipboard) { throw new InvalidOperationException("clipboard busy"); }
        Calls.Add("clipboard:" + text);
      }
      public void ShowInformation(string message, string caption) { Calls.Add("message:" + caption + "|" + message); }
      public void OpenInBrowser(string url) { Calls.Add("browser:" + url); }
      public Task<bool> LaunchUriAsync(Uri uri) {
        if (FailLaunch) { return Task.FromException<bool>(new InvalidOperationException("no store")); }
        Calls.Add("launch:" + uri.OriginalString);
        return Task.FromResult(true);
      }
      public void ShowError(Exception exception) { Errors.Add(exception); }
    }

    private const string StoreWebUrl = "https://www.microsoft.com/store/productId/9P8DNCD0PPG8";
    private const string ReviewUri = "ms-windows-store://review/?ProductId=9P8DNCD0PPG8";
    private const string ProductPageUri = "ms-windows-store://pdp/?ProductId=9P8DNCD0PPG8";

    [Fact]
    public void Uris_are_the_same_as_the_old_window_buttons() {
      Assert.Equal(StoreWebUrl, StoreActions.StoreWebUrl);
      Assert.Equal(ReviewUri, StoreActions.ReviewUri.OriginalString);
      Assert.Equal(ProductPageUri, StoreActions.ProductPageUri.OriginalString);
    }

    [Fact]
    public async Task Rate_opens_the_store_review_page_only() {
      FakeHost host = new FakeHost();
      await new StoreActions(host).RateAsync();
      Assert.Equal(new[] { "launch:" + ReviewUri }, host.Calls);
      Assert.Empty(host.Errors);
    }

    [Fact]
    public async Task Share_copies_link_tells_user_opens_browser_then_store_page() {
      FakeHost host = new FakeHost();
      await new StoreActions(host).ShareAsync();
      string message = "The following link has been copied into your clipboard. You can paste and share it with friends."
        + Environment.NewLine + Environment.NewLine + StoreWebUrl;
      Assert.Equal(new[] {
        "clipboard:" + StoreWebUrl,
        "message:Share MDSHO|" + message,
        "browser:" + StoreWebUrl,
        "launch:" + ProductPageUri
      }, host.Calls);
      Assert.Empty(host.Errors);
    }

    [Fact]
    public async Task Share_still_opens_store_page_when_clipboard_fails() {
      FakeHost host = new FakeHost { FailClipboard = true };
      await new StoreActions(host).ShareAsync();
      Assert.Single(host.Errors);
      Assert.Equal(new[] { "launch:" + ProductPageUri }, host.Calls);
    }

    [Fact]
    public async Task Launch_failure_is_reported_not_thrown() {
      FakeHost host = new FakeHost { FailLaunch = true };
      StoreActions actions = new StoreActions(host);
      await actions.RateAsync();
      await actions.ShareAsync();
      Assert.Equal(2, host.Errors.Count);
    }

    private static FieldInfo NamedElement(Type view, string name) {
      return view.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
    }

    [Fact]
    public void Desktop_window_footer_keeps_only_the_settings_gear() {
      Assert.NotNull(NamedElement(typeof(DesktopWindowView), "ButtonFooterSettings"));
      Assert.Null(NamedElement(typeof(DesktopWindowView), "ButtonRateAndReview"));
      Assert.Null(NamedElement(typeof(DesktopWindowView), "ButtonShare"));
    }

    [Fact]
    public void About_window_offers_rate_and_share() {
      Assert.NotNull(NamedElement(typeof(AboutWindow), "HyperlinkRate"));
      Assert.NotNull(NamedElement(typeof(AboutWindow), "HyperlinkShare"));
    }

    [Fact]
    public void Title_menu_offers_rate_and_share() {
      Assert.NotNull(NamedElement(typeof(DesktopWindowView), "MenuItemHeaderRate"));
      Assert.NotNull(NamedElement(typeof(DesktopWindowView), "MenuItemHeaderShare"));
    }
  }
}
