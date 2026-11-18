using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using MDSHO.Common;
using MDSHO.Core;
using MDSHO.Helpers;
using MDSHO.ViewModels;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// A link dropped from a browser is named after the page or link title found in the drop data (Chrome/Edge
  /// FileGroupDescriptorW, Firefox text/x-moz-url), never by fetching the page. Without a usable title the URL stays the name.
  /// </summary>
  public class DroppedLinkTitleTests {

    private const string Url = "https://example.org/a";

    // ---- Fake drop data with the browsers' byte layouts ----

    /// <summary>
    /// FILEGROUPDESCRIPTORW: UINT cItems, then cItems FILEDESCRIPTORW of 592 bytes each. In a FILEDESCRIPTORW, cFileName
    /// (WCHAR[260]) starts at byte 72, after dwFlags, clsid, sizel, pointl, dwFileAttributes, three FILETIMEs and the two size parts.
    /// </summary>
    private static byte[] FileGroupDescriptor(params string[] names) {
      byte[] data = new byte[4 + 592 * names.Length];
      BitConverter.GetBytes((uint)names.Length).CopyTo(data, 0);
      for (int i = 0; i < names.Length; i++) {
        int item = 4 + 592 * i;
        BitConverter.GetBytes(0x00008000u | 0x00000040u).CopyTo(data, item); // FD_LINKUI | FD_PROGRESSUI, like Chrome
        byte[] name = Encoding.Unicode.GetBytes(names[i]);
        Array.Copy(name, 0, data, item + 72, Math.Min(name.Length, 518)); // the rest stays zero: the terminator
      }
      return data;
    }

    /// <summary>text/x-moz-url: UTF-16 "url\ntitle", ended with a null character.</summary>
    private static byte[] MozUrl(string text) {
      return Encoding.Unicode.GetBytes(text + "\0");
    }

    // ---- Reading the formats ----

    [Fact]
    public void Chrome_and_Edge_file_name_gives_the_title_without_url() {
      Assert.Equal("Example Domain", DroppedLinkTitle.Find(Url, FileGroupDescriptor("Example Domain.url"), null));
    }

    [Fact]
    public void The_url_ending_is_removed_in_any_case() {
      Assert.Equal("Example Domain", DroppedLinkTitle.Find(Url, FileGroupDescriptor("Example Domain.URL"), null));
    }

    [Fact]
    public void A_file_group_with_other_files_or_more_than_one_file_is_ignored() {
      Assert.Null(DroppedLinkTitle.Find(Url, FileGroupDescriptor("picture.png"), null));
      Assert.Null(DroppedLinkTitle.Find(Url, FileGroupDescriptor("One.url", "Two.url"), null));
    }

    [Fact]
    public void Broken_or_short_drop_data_is_ignored() {
      Assert.Null(DroppedLinkTitle.Find(Url, new byte[] { 1, 0 }, null));
      Assert.Null(DroppedLinkTitle.Find(Url, FileGroupDescriptor("Example.url").Take(100).ToArray(), null));
      Assert.Null(DroppedLinkTitle.Find(Url, new byte[] { 0xFF, 0xFF, 0xFF, 0xFF }, null));
      Assert.Null(DroppedLinkTitle.Find(Url, null, new byte[] { 0x41 }));
      Assert.Null(DroppedLinkTitle.Find(Url, null, null));
    }

    [Fact]
    public void Firefox_url_and_title_gives_the_title() {
      Assert.Equal("Example Domain", DroppedLinkTitle.Find(Url, null, MozUrl(Url + "\nExample Domain")));
    }

    [Fact]
    public void Firefox_title_of_another_url_is_ignored() {
      Assert.Null(DroppedLinkTitle.Find(Url, null, MozUrl("https://other.example/\nOther page")));
    }

    [Fact]
    public void Firefox_with_several_links_uses_the_first_only_when_it_is_the_added_url() {
      Assert.Equal("First", DroppedLinkTitle.Find(Url, null, MozUrl(Url + "\nFirst\nhttps://other.example/\nSecond")));
    }

    [Fact]
    public void Firefox_url_may_differ_only_in_how_it_is_written() {
      // Uri writes "https://EXAMPLE.org" as "https://example.org/".
      Assert.Equal("Home", DroppedLinkTitle.Find("https://example.org/", null, MozUrl("https://EXAMPLE.org\r\nHome")));
    }

    [Fact]
    public void The_file_group_comes_before_Firefox_data() {
      Assert.Equal("From Chrome", DroppedLinkTitle.Find(Url, FileGroupDescriptor("From Chrome.url"), MozUrl(Url + "\nFrom Firefox")));
    }

    [Fact]
    public void Firefox_data_is_used_when_the_file_group_has_no_title() {
      Assert.Equal("From Firefox", DroppedLinkTitle.Find(Url, FileGroupDescriptor(".url"), MozUrl(Url + "\nFrom Firefox")));
    }

    // ---- The name ----

    [Fact]
    public void Without_a_title_the_name_is_the_url() {
      Assert.Equal(Url, DroppedLinkTitle.NameFor(Url, null));
      Assert.Equal(Url, DroppedLinkTitle.NameFor(Url, ""));
      Assert.Equal(Url, DroppedLinkTitle.NameFor(Url, " \r\n\t "));
    }

    [Fact]
    public void A_title_that_is_just_the_url_is_ignored() {
      Assert.Equal(Url, DroppedLinkTitle.NameFor(Url, Url));
      Assert.Equal(Url, DroppedLinkTitle.NameFor(Url, "example.org/a"));
      Assert.Equal(Url, DroppedLinkTitle.NameFor(Url, "HTTPS://www.example.org/a/"));
      // Chrome names an untitled page's file after its address with the file name characters replaced.
      Assert.Equal(Url, DroppedLinkTitle.NameFor(Url, "https___example.org_a"));
    }

    [Fact]
    public void The_title_is_trimmed_and_its_spaces_and_line_breaks_collapse() {
      Assert.Equal("Example Domain - Home", DroppedLinkTitle.NameFor(Url, "  Example \r\n Domain\t-   Home  "));
    }

    [Fact]
    public void Control_characters_are_dropped() {
      Assert.Equal("AB", DroppedLinkTitle.NameFor(Url, "A\u0000\u0007B"));
    }

    [Fact]
    public void A_long_title_is_cut_at_the_name_limit_on_a_whole_character() {
      string longTitle = new string('a', 150);
      string name = DroppedLinkTitle.NameFor(Url, longTitle);
      Assert.Equal(DroppedLinkTitle.MaxNameLength, name.Length);
      // A surrogate pair is never split.
      string emoji = new string('b', DroppedLinkTitle.MaxNameLength - 1) + "\U0001F600" + "c";
      string cut = DroppedLinkTitle.NameFor(Url, emoji);
      Assert.False(char.IsHighSurrogate(cut[cut.Length - 1]));
      Assert.True(cut.Length <= DroppedLinkTitle.MaxNameLength);
    }

    [Fact]
    public void A_title_cut_at_the_limit_loses_its_trailing_space() {
      string title = new string('a', DroppedLinkTitle.MaxNameLength - 1) + " b";
      Assert.Equal(new string('a', DroppedLinkTitle.MaxNameLength - 1), DroppedLinkTitle.NameFor(Url, title));
    }

    [Fact]
    public void The_file_name_replacement_character_is_kept_because_it_could_be_real() {
      // Chrome writes "A: B" as "A_ B"; an underscore can also be a real one, so it is not changed back.
      Assert.Equal("A_ B", DroppedLinkTitle.NameFor(Url, "A_ B"));
    }

    // ---- The panel ----

    [Fact]
    public void A_dropped_link_with_a_title_is_named_after_it_and_keeps_the_url_as_target() {
      using (TempDb db = new TempDb()) {
        DesktopWindowViewModel win = new AppViewModel().AddWindow("TEST", 0, 0);
        List<ShortcutViewModel> created = new List<ShortcutViewModel>();
        win.AddDroppedText(Url, "Example Domain", null, win, created);
        ShortcutViewModel s = Assert.Single(created);
        Assert.Equal("Example Domain", s.Name);
        Assert.Equal(Url, s.Target);
        Assert.Equal(AppConstants.UrlExtension, s.Ext);
      }
    }

    [Fact]
    public void A_dropped_link_with_an_unusable_title_is_named_after_the_url() {
      using (TempDb db = new TempDb()) {
        DesktopWindowViewModel win = new AppViewModel().AddWindow("TEST", 0, 0);
        List<ShortcutViewModel> created = new List<ShortcutViewModel>();
        win.AddDroppedText(Url, "  ", null, win, created);
        Assert.Equal(Url, Assert.Single(created).Name);
      }
    }

    /// <summary>Drops the data on an empty panel; the data object is made on the WPF thread, like a real drop.</summary>
    private static ShortcutViewModel DropOnEmptyPanel(Action<DataObject> fill) {
      ShortcutViewModel added = null;
      using (TempDb db = new TempDb()) {
        WpfAppHost.Run(() => {
          DesktopWindowViewModel win = new AppViewModel().AddWindow("TEST", 0, 0);
          DataObject data = new DataObject();
          fill(data);
          var drop = new DropHandlerTests.FakeDropInfo { Data = data, TargetCollection = win.RootShortcuts, InsertIndex = 0, UnfilteredInsertIndex = 0 };
          ShortcutDropHandler handler = new ShortcutDropHandler(win);
          handler.DragOver(drop);
          handler.Drop(drop);
          added = Assert.Single(win.RootShortcuts);
        });
      }
      return added;
    }

    [Fact]
    public void A_link_dragged_from_Chrome_or_Edge_gets_the_page_title() {
      ShortcutViewModel added = DropOnEmptyPanel(data => {
        data.SetData(DataFormats.UnicodeText, Url);
        data.SetData("FileGroupDescriptorW", new MemoryStream(FileGroupDescriptor("Example Domain.url")));
      });
      Assert.Equal("Example Domain", added.Name);
      Assert.Equal(Url, added.Target);
    }

    [Fact]
    public void A_link_dragged_from_Firefox_gets_the_link_title() {
      ShortcutViewModel added = DropOnEmptyPanel(data => {
        data.SetData(DataFormats.UnicodeText, Url);
        data.SetData("text/x-moz-url", new MemoryStream(MozUrl(Url + "\nExample Domain")));
      });
      Assert.Equal("Example Domain", added.Name);
      Assert.Equal(Url, added.Target);
    }

    [Fact]
    public void Plain_dropped_text_is_still_named_after_the_url() {
      ShortcutViewModel added = DropOnEmptyPanel(data => data.SetData(DataFormats.UnicodeText, Url));
      Assert.Equal(Url, added.Name);
    }
  }
}
