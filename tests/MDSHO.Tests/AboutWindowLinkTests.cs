using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using MDSHO.Common;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// Links of the real About window: the project, privacy and terms links point to the GitHub project and the MDSHO docs
  /// site, nothing points to the old personal website, and every link uses https.
  /// </summary>
  public class AboutWindowLinkTests {

    private const string ProjectUrl = "https://github.com/ervingegprifti/mdsho";
    private const string PrivacyUrl = "https://ervingegprifti.github.io/mdsho/privacy/";
    private const string TermsUrl = "https://ervingegprifti.github.io/mdsho/terms/";

    /// <summary>Shows the About window off screen, runs <paramref name="check"/> on it, then closes it.</summary>
    private static void WithAboutWindow(Action<AboutWindow> check) {
      WpfAppHost.Run(() => {
        AboutWindow window = new AboutWindow {
          WindowStartupLocation = WindowStartupLocation.Manual,
          Left = -10000,
          Top = -10000,
          ShowActivated = false,
          ShowInTaskbar = false
        };
        window.Show();
        try {
          WpfAppHost.Pump();
          check(window);
        } finally {
          window.Close();
        }
      });
    }

    /// <summary>Every hyperlink in the window, also the ones made by the third-party list template.</summary>
    private static List<Hyperlink> AllHyperlinks(DependencyObject root) {
      HashSet<DependencyObject> seen = new HashSet<DependencyObject>();
      List<Hyperlink> links = new List<Hyperlink>();
      Stack<DependencyObject> todo = new Stack<DependencyObject>();
      todo.Push(root);
      while (todo.Count > 0) {
        DependencyObject current = todo.Pop();
        if (!seen.Add(current)) {
          continue;
        }
        if (current is Hyperlink link) {
          links.Add(link);
        }
        foreach (object child in LogicalTreeHelper.GetChildren(current)) {
          if (child is DependencyObject d) {
            todo.Push(d);
          }
        }
        if (current is Visual || current is System.Windows.Media.Media3D.Visual3D) {
          for (int i = 0; i < VisualTreeHelper.GetChildrenCount(current); i++) {
            todo.Push(VisualTreeHelper.GetChild(current, i));
          }
        }
      }
      return links;
    }

    [Fact]
    public void Url_constants_have_the_published_addresses() {
      Assert.Equal(ProjectUrl, AppConstants.ProjectUrl);
      Assert.Equal(PrivacyUrl, AppConstants.PrivacyPolicyUrl);
      Assert.Equal(TermsUrl, AppConstants.TermsUrl);
    }

    [Fact]
    public void No_link_points_to_the_old_website() {
      WithAboutWindow(window => {
        List<Hyperlink> links = AllHyperlinks(window).Where(l => l.NavigateUri != null).ToList();
        Assert.NotEmpty(links);
        foreach (Hyperlink link in links) {
          Assert.DoesNotContain("gegprifti.com", link.NavigateUri.OriginalString, StringComparison.OrdinalIgnoreCase);
        }
      });
    }

    [Fact]
    public void Every_link_uses_https() {
      WithAboutWindow(window => {
        List<Hyperlink> links = AllHyperlinks(window).Where(l => l.NavigateUri != null).ToList();
        // The window's own rows plus two links per third-party entry.
        Assert.True(links.Count >= 3 + 2 * ThirdPartyNotices.All.Count, "Only " + links.Count + " links found.");
        foreach (Hyperlink link in links) {
          Assert.True(link.NavigateUri.Scheme == Uri.UriSchemeHttps, "Not https: " + link.NavigateUri.OriginalString);
        }
      });
    }

    [Theory]
    [InlineData("HyperlinkProject", ProjectUrl)]
    [InlineData("HyperlinkPrivacyPolicy", PrivacyUrl)]
    [InlineData("HyperlinkTermsAndConditions", TermsUrl)]
    public void Project_privacy_and_terms_links_have_the_exact_addresses(string name, string url) {
      WithAboutWindow(window => {
        Hyperlink link = Assert.IsType<Hyperlink>(window.FindName(name));
        Assert.Equal(url, link.NavigateUri.OriginalString);
        FrameworkElement host = Assert.IsAssignableFrom<FrameworkElement>(link.Parent);
        Assert.True(host.IsVisible, name + " is not visible.");
      });
    }

    [Fact]
    public void The_old_home_row_is_gone() {
      WithAboutWindow(window => {
        Assert.Null(window.FindName("HyperlinkHome"));
      });
    }
  }
}
