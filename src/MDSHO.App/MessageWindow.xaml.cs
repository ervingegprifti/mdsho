using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using MDSHO.Common;

namespace MDSHO {

  /// <summary>
  /// The one MDSHO dialog for every message (see <see cref="MdshoMessage"/>): the title bar with the MDSHO logo, an Info or
  /// Warning icon, a bold first line, the text and an optional note, then OK, OK/Cancel, Yes/No or Yes/No/Cancel. The
  /// error windows build on it. Which buttons, the default and the answer of Esc or the close button come from
  /// <see cref="MessageRules"/>.
  /// </summary>
  public partial class MessageWindow : Window {
    private readonly MessageButtons buttons;

    /// <summary>The answer: the button clicked, or the cancel answer when the window was closed another way.</summary>
    public MessageResult Result { get; private set; }

    /// <summary>Creates the dialog.</summary>
    /// <param name="title">The title; empty gives "MDSHO".</param>
    /// <param name="heading">The bold first line.</param>
    /// <param name="text">The text below it (hidden when empty).</param>
    /// <param name="icon">Info or Warning.</param>
    /// <param name="buttons">The buttons.</param>
    /// <param name="defaultResult">The default button; one the set has not gives the first.</param>
    /// <param name="solidIcons">The icon set: the panel's Solid icons setting for a message from a panel, solid otherwise.</param>
    public MessageWindow(string title, string heading, string text, MessageIcon icon, MessageButtons buttons, MessageResult defaultResult, bool solidIcons = true) {
      InitializeComponent();
      this.buttons = buttons;
      this.Title = string.IsNullOrWhiteSpace(title) ? "MDSHO" : title;
#if DEBUG
      this.Title += " DEBUG";
#endif
      ViewModels.IconGlyphs icons = new ViewModels.IconGlyphs(solidIcons);
      this.TitleBar.Glyph = icons.MDSHO;
      this.TitleBar.CloseGlyph = icons.Times;
      this.TextIcon.Text = icon == MessageIcon.Info ? icons.Info : icons.Warning;
      AutomationProperties.SetName(this.TextIcon, icon == MessageIcon.Info ? "Information" : "Warning");
      SetText(this.TextHeading, heading);
      SetText(this.TextMessage, text);
      SetText(this.TextNote, null);
      if (this.TextHeading.Visibility != Visibility.Visible) {
        this.TextMessage.Margin = new Thickness(0);
      }

      MessageResult cancel = MessageRules.CancelResultOf(buttons);
      MessageResult chosen = MessageRules.DefaultOf(buttons, defaultResult);
      bool first = true;
      foreach (MessageResult result in MessageRules.ButtonsOf(buttons)) {
        Button button = NewButton("Button" + result, MessageRules.Label(result), first ? "StyleDarkButton" : "StyleLightButton");
        button.Margin = new Thickness(first ? 0 : 8, 0, 0, 0);
        button.IsDefault = result == chosen;
        button.IsCancel = result == cancel;
        MessageResult answer = result;
        button.Click += (s, e) => {
          this.Result = answer;
          Close();
        };
        this.PanelButtons.Children.Add(button);
        first = false;
      }
    }

    /// <summary>A line below the text, e.g. where the details of an error went (hidden when empty).</summary>
    public string Note {
      get { return this.TextNote.Text; }
      set { SetText(this.TextNote, value); }
    }

    /// <summary>Adds a light button on the left of the button bar that runs <paramref name="onClick"/> and keeps the window open.</summary>
    public Button AddExtraButton(string name, string content, Action onClick) {
      Button button = NewButton(name, content, "StyleLightButton");
      button.MinWidth = 0;
      button.Margin = new Thickness(0, 0, 8, 0);
      button.Click += (s, e) => onClick?.Invoke();
      this.PanelExtraButtons.Children.Add(button);
      return button;
    }

    /// <summary>Closed without a button: Esc on a set without a cancel button, or the title bar's close button.</summary>
    protected override void OnClosing(CancelEventArgs e) {
      base.OnClosing(e);
      if (!e.Cancel && this.Result == MessageResult.None) {
        this.Result = MessageRules.CancelResultOf(this.buttons);
      }
    }

    private Button NewButton(string name, string content, string style) {
      Button button = new Button {
        Name = name,
        Content = content,
        MinWidth = 80,
        Style = (Style)FindResource(style),
      };
      RegisterName(name, button);
      return button;
    }

    private static void SetText(TextBlock block, string text) {
      block.Text = text ?? "";
      block.Visibility = string.IsNullOrWhiteSpace(text) ? Visibility.Collapsed : Visibility.Visible;
    }
  }
}
