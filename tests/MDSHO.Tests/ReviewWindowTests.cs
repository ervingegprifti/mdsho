using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using MDSHO.Common;
using MDSHO.Controls;
using MDSHO.Data;
using MDSHO.Helpers;
using MDSHO.ViewModels;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>The "Enjoying MDSHO?" window: its look, its buttons, where it opens and the "is a dialog open" check.</summary>
  public class ReviewWindowTests {

    private static ReviewPrompt NewPrompt(StoreActions store) {
      return new ReviewPrompt(new FakeClock(), new ScreenSetupTests.FakeTimer(), () => false, p => { }, store, false);
    }

    private static T ShowOffScreen<T>(T window) where T : Window {
      window.WindowStartupLocation = WindowStartupLocation.Manual;
      window.Left = -10000;
      window.Top = -10000;
      window.ShowActivated = false;
      window.ShowInTaskbar = false;
      window.Show();
      WpfAppHost.Pump();
      return window;
    }

    private static T Named<T>(FrameworkElement root, string name) where T : class {
      return Assert.IsAssignableFrom<T>(root.FindName(name));
    }

    private static void Click(Button button) {
      button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, button));
      WpfAppHost.Pump();
    }

    [Fact]
    public void Has_the_title_star_heading_text_and_Later_then_Rate_MDSHO() {
      using (TempDb db = new TempDb())
      using (FakeAppServices fake = new FakeAppServices()) {
        WpfAppHost.Run(() => {
          ReviewWindow window = ShowOffScreen(new ReviewWindow(NewPrompt(fake.Services.StoreActions)));
          try {
            Assert.StartsWith("MDSHO", window.Title);
            Assert.Equal(340, window.Width);
            Assert.True(WindowThemes.GetIsThemed(window));
            DialogTitleBar bar = Named<DialogTitleBar>(window, "TitleBar");
            Assert.Equal(new IconGlyphs(true).Star, bar.Glyph);
            Assert.Equal(new IconGlyphs(true).Times, bar.CloseGlyph);

            TextBlock heading = Named<TextBlock>(window, "TextHeading");
            Assert.Equal("Enjoying MDSHO?", heading.Text);
            Assert.Equal(15, heading.FontSize);
            Assert.Equal(FontWeights.SemiBold, heading.FontWeight);
            Assert.Same(window.FindResource("Theme.TextStrong"), heading.Foreground);
            TextBlock text = Named<TextBlock>(window, "TextMessage");
            Assert.Equal("A rating in the Microsoft Store helps other people find it. It only takes a minute.", text.Text);
            Assert.Same(window.FindResource("Theme.TextSecondary"), text.Foreground);

            Button later = Named<Button>(window, "ButtonLater");
            Button rate = Named<Button>(window, "ButtonRate");
            Assert.Equal("Later", later.Content);
            Assert.Equal("Rate MDSHO", rate.Content);
            Assert.True(rate.IsDefault);
            Assert.True(later.IsCancel);
            Assert.Same(window.FindResource("StyleDarkButton"), later.Style);
            Assert.Same(window.FindResource("StyleDarkButton"), rate.Style);
            Assert.True(later.TranslatePoint(new Point(0, 0), window).X < rate.TranslatePoint(new Point(0, 0), window).X);
            // Right-aligned: Rate MDSHO ends at the footer's right margin.
            Grid footerContent = Named<Grid>(window, "FooterContent");
            double rateRight = rate.TranslatePoint(new Point(rate.ActualWidth, 0), footerContent).X;
            Assert.Equal(footerContent.ActualWidth, rateRight, 1);
          } finally {
            window.Close();
            WpfAppHost.Pump();
          }
        });
      }
    }

    [Fact]
    public void The_footer_is_like_Run_commands_with_margins_equal_to_the_button_height() {
      using (TempDb db = new TempDb())
      using (FakeAppServices fake = new FakeAppServices()) {
        WpfAppHost.Run(() => {
          ReviewWindow window = ShowOffScreen(new ReviewWindow(NewPrompt(fake.Services.StoreActions)));
          try {
            Border footer = Named<Border>(window, "Footer");
            Assert.Same(window.FindResource("Theme.BarBackground"), footer.Background);
            double h = Named<Button>(window, "ButtonRate").ActualHeight;
            Assert.True(h > 10);
            Assert.Equal(new Thickness(h), Named<Grid>(window, "FooterContent").Margin);
            Assert.Equal(new Thickness(h), Named<FrameworkElement>(window, "BodyContent").Margin);
          } finally {
            window.Close();
            WpfAppHost.Pump();
          }
        });
      }
    }

    [Fact]
    public void Rate_MDSHO_opens_the_store_review_page_records_it_and_closes() {
      using (TempDb db = new TempDb())
      using (FakeAppServices fake = new FakeAppServices()) {
        WpfAppHost.Run(() => {
          ReviewWindow window = ShowOffScreen(new ReviewWindow(NewPrompt(fake.Services.StoreActions)));
          bool closed = false;
          window.Closed += (s, e) => closed = true;
          Click(Named<Button>(window, "ButtonRate"));
          Assert.True(closed);
          Assert.Equal(new[] { StoreActions.ReviewUri }, fake.Launched);
          Assert.Equal("Yes", DataStore.AppSettings.Get(ReviewPrompt.RatedKey));
        });
      }
    }

    [Fact]
    public void On_a_Store_install_Rate_MDSHO_closes_the_prompt_before_the_Store_dialog_opens() {
      using (TempDb db = new TempDb())
      using (FakeAppServices fake = new FakeAppServices()) {
        WpfAppHost.Run(() => {
          ReviewPromptTests.FakeStoreRating rating = new ReviewPromptTests.FakeStoreRating();
          StoreActions store = new StoreActions(new ReviewPromptTests.StoreHost(), rating);
          ReviewWindow window = ShowOffScreen(new ReviewWindow(NewPrompt(store)));
          bool? visibleAtDialog = null;
          rating.OnCall = () => visibleAtDialog = window.IsVisible;
          Click(Named<Button>(window, "ButtonRate"));
          Assert.False(visibleAtDialog);
          Assert.Equal(1, rating.Calls);
          Assert.Equal("Yes", DataStore.AppSettings.Get(ReviewPrompt.RatedKey));
        });
      }
    }

    [Fact]
    public void Later_closes_without_rating() {
      using (TempDb db = new TempDb())
      using (FakeAppServices fake = new FakeAppServices()) {
        WpfAppHost.Run(() => {
          ReviewWindow window = ShowOffScreen(new ReviewWindow(NewPrompt(fake.Services.StoreActions)));
          bool closed = false;
          window.Closed += (s, e) => closed = true;
          Click(Named<Button>(window, "ButtonLater"));
          Assert.True(closed);
          Assert.Empty(fake.Launched);
          Assert.Null(DataStore.AppSettings.Get(ReviewPrompt.RatedKey));
        });
      }
    }

    [Fact]
    public void Opens_without_focus_beside_the_panel_used_last() {
      using (TempDb db = new TempDb())
      using (FakeAppServices fake = new FakeAppServices()) {
        WpfAppHost.Run(() => {
          DesktopWindowViewModel w = new AppViewModel().AddWindow("TEST", 100, 100);
          DesktopWindowView panel = ShowOffScreen(new DesktopWindowView(w));
          Func<Window> oldPanel = ReviewWindow.LastUsedPanel;
          Action<ReviewWindow> oldShow = ReviewWindow.ShowWindow;
          List<ReviewWindow> shown = new List<ReviewWindow>();
          ReviewWindow.LastUsedPanel = () => panel;
          ReviewWindow.ShowWindow = shown.Add;
          try {
            ReviewWindow.ShowFor(NewPrompt(fake.Services.StoreActions));
            ReviewWindow window = Assert.Single(shown);
            Assert.False(window.ShowActivated);
            Assert.Same(panel, window.Owner);
            Assert.Equal(panel.Left + panel.ActualWidth + DesktopWindowView.DialogGap, window.Left, 1);
          } finally {
            ReviewWindow.LastUsedPanel = oldPanel;
            ReviewWindow.ShowWindow = oldShow;
            panel.Close();
            WpfAppHost.Pump();
          }
        });
      }
    }

    [Fact]
    public void Without_a_panel_it_opens_bottom_right_of_the_primary_work_area() {
      using (TempDb db = new TempDb())
      using (FakeAppServices fake = new FakeAppServices()) {
        WpfAppHost.Run(() => {
          // Far off the real screens, so the window never shows on the desktop during the test.
          ScreenInfo primary = new ScreenInfo("PRIMARY", new ScreenRect(-20000, -20000, 1920, 1080), new ScreenRect(-20000, -20000, 1920, 1040), 1.25, true);
          Func<Window> oldPanel = ReviewWindow.LastUsedPanel;
          Action<ReviewWindow> oldShow = ReviewWindow.ShowWindow;
          Func<ScreenInfo> oldPrimary = DialogPlacement.PrimaryScreen;
          Func<Window, Rect> oldBounds = DialogPlacement.PhysicalBoundsOf;
          Action<Window, Point> oldMove = DialogPlacement.MoveToPhysical;
          List<Point> moves = new List<Point>();
          ReviewWindow window = null;
          ReviewWindow.LastUsedPanel = () => null;
          ReviewWindow.ShowWindow = rw => { window = rw; rw.ShowInTaskbar = false; rw.Show(); WpfAppHost.Pump(); };
          DialogPlacement.PrimaryScreen = () => primary;
          DialogPlacement.PhysicalBoundsOf = win => new Rect(-10000, -10000, 425, 250);
          DialogPlacement.MoveToPhysical = (win, p) => moves.Add(p);
          try {
            ReviewWindow.ShowFor(NewPrompt(fake.Services.StoreActions));
            Assert.NotNull(window);
            Assert.False(window.ShowActivated);
            Assert.Null(window.Owner);
            // 20 DIPs from the right and bottom edges at 125 %: 25 px.
            Assert.Contains(new Point(-20000 + 1920 - 425 - 25, -20000 + 1040 - 250 - 25), moves);
          } finally {
            ReviewWindow.LastUsedPanel = oldPanel;
            ReviewWindow.ShowWindow = oldShow;
            DialogPlacement.PrimaryScreen = oldPrimary;
            DialogPlacement.PhysicalBoundsOf = oldBounds;
            DialogPlacement.MoveToPhysical = oldMove;
            window?.Close();
            WpfAppHost.Pump();
          }
        });
      }
    }

    [Fact]
    public void Bottom_right_of_a_work_area_keeps_the_gap() {
      ScreenInfo screen = new ScreenInfo("S", new ScreenRect(-1920, 0, 1920, 1080), new ScreenRect(-1920, 40, 1920, 1040), 1, false);
      Assert.Equal(new ScreenPoint(-20 - 340, 1080 - 20 - 200), ScreenGeometry.BottomRightInWorkAreaPhysical(screen, 340, 200, 20));
    }

    [Fact]
    public void A_panel_becomes_the_panel_used_last_when_activated() {
      using (TempDb db = new TempDb())
      using (FakeAppServices fake = new FakeAppServices()) {
        WpfAppHost.Run(() => {
          DesktopWindowViewModel w = new AppViewModel().AddWindow("TEST", 100, 100);
          DesktopWindowView a = ShowOffScreen(new DesktopWindowView(w));
          DesktopWindowView b = ShowOffScreen(new DesktopWindowView(w));
          MethodInfo onActivated = typeof(Window).GetMethod("OnActivated", BindingFlags.NonPublic | BindingFlags.Instance);
          try {
            onActivated.Invoke(a, new object[] { EventArgs.Empty });
            Assert.Same(a, ReviewWindow.LastUsedPanel());
            onActivated.Invoke(b, new object[] { EventArgs.Empty });
            Assert.Same(b, ReviewWindow.LastUsedPanel());
            b.Close();
            WpfAppHost.Pump();
            Assert.Null(ReviewWindow.LastUsedPanel());
          } finally {
            a.Close();
            if (b.IsVisible) { b.Close(); }
            WpfAppHost.Pump();
          }
        });
      }
    }

    [Fact]
    public void A_dialog_is_open_while_Settings_is_shown_but_not_for_panels_or_notes() {
      using (TempDb db = new TempDb())
      using (FakeAppServices fake = new FakeAppServices()) {
        WpfAppHost.Run(() => {
          DesktopWindowViewModel w = new AppViewModel().AddWindow("TEST", 100, 100);
          DesktopWindowView panel = ShowOffScreen(new DesktopWindowView(w));
          Action<ShortcutViewModel> oldNoteEditor = ShortcutViewModel.OpenNoteEditor;
          ShortcutViewModel.OpenNoteEditor = s => { };
          try {
            w.NewNoteShortcutCommand.Execute(null);
          } finally {
            ShortcutViewModel.OpenNoteEditor = oldNoteEditor;
          }
          TextEditWindow note = ShowOffScreen(TextEditWindow.ForNote(w.RootShortcuts.Last()));
          try {
            Assert.False(ReviewWindow.IsDialogOpen());
            SettingsWindow settings = ShowOffScreen(new SettingsWindow(w));
            Assert.True(ReviewWindow.IsDialogOpen());
            settings.Close();
            WpfAppHost.Pump();
            Assert.False(ReviewWindow.IsDialogOpen());
            ErrorWindow error = ShowOffScreen(new ErrorWindow(new InvalidOperationException("Test")));
            Assert.True(ReviewWindow.IsDialogOpen());
            error.Close();
            WpfAppHost.Pump();
            Assert.False(ReviewWindow.IsDialogOpen());
          } finally {
            note.Close();
            panel.Close();
            WpfAppHost.Pump();
          }
        });
      }
    }

#if !DEBUG
    [Fact]
    public void The_app_prompt_is_on_in_the_Release_build_and_uses_the_apps_Rate_action() {
      using (TempDb db = new TempDb()) {
        AppServices services = AppServices.Defaults();
        Assert.False(services.Review.DebugBuild);
        // The panel menu's Rate... and About's Rate link use these StoreActions: the prompt hears about them.
        Assert.Same(services.StoreActions, services.Review.Store);
      }
    }
#endif
  }
}
