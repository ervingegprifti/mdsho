using MDSHO.Common;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// The choices of the one MDSHO dialog: which buttons a set has, which answer Esc and the close button give, which button
  /// is the default, and how a message splits into a bold first line and the text below it (the first line is the heading).
  /// </summary>
  public class MessageRulesTests {

    [Fact]
    public void Each_button_set_has_its_buttons_in_order() {
      Assert.Equal(new[] { MessageResult.OK }, MessageRules.ButtonsOf(MessageButtons.OK));
      Assert.Equal(new[] { MessageResult.OK, MessageResult.Cancel }, MessageRules.ButtonsOf(MessageButtons.OKCancel));
      Assert.Equal(new[] { MessageResult.Yes, MessageResult.No }, MessageRules.ButtonsOf(MessageButtons.YesNo));
      Assert.Equal(new[] { MessageResult.Yes, MessageResult.No, MessageResult.Cancel }, MessageRules.ButtonsOf(MessageButtons.YesNoCancel));
    }

    [Fact]
    public void Esc_and_the_close_button_answer_cancel_or_no() {
      Assert.Equal(MessageResult.OK, MessageRules.CancelResultOf(MessageButtons.OK));
      Assert.Equal(MessageResult.Cancel, MessageRules.CancelResultOf(MessageButtons.OKCancel));
      Assert.Equal(MessageResult.No, MessageRules.CancelResultOf(MessageButtons.YesNo));
      Assert.Equal(MessageResult.Cancel, MessageRules.CancelResultOf(MessageButtons.YesNoCancel));
    }

    [Fact]
    public void The_default_is_the_asked_button_when_the_set_has_it_else_the_first() {
      Assert.Equal(MessageResult.No, MessageRules.DefaultOf(MessageButtons.YesNo, MessageResult.No));
      Assert.Equal(MessageResult.Yes, MessageRules.DefaultOf(MessageButtons.YesNo, MessageResult.None));
      Assert.Equal(MessageResult.Yes, MessageRules.DefaultOf(MessageButtons.YesNo, MessageResult.Cancel));
      Assert.Equal(MessageResult.OK, MessageRules.DefaultOf(MessageButtons.OKCancel, MessageResult.OK));
      Assert.Equal(MessageResult.Cancel, MessageRules.DefaultOf(MessageButtons.YesNoCancel, MessageResult.Cancel));
    }

    [Fact]
    public void Buttons_say_ok_cancel_yes_and_no() {
      Assert.Equal("OK", MessageRules.Label(MessageResult.OK));
      Assert.Equal("Cancel", MessageRules.Label(MessageResult.Cancel));
      Assert.Equal("Yes", MessageRules.Label(MessageResult.Yes));
      Assert.Equal("No", MessageRules.Label(MessageResult.No));
    }

    [Fact]
    public void The_first_line_is_the_bold_heading_and_blank_lines_after_it_are_dropped() {
      (string heading, string text) = MessageRules.Split("Exit anyway?\r\n\r\nWhat you typed\nwill be lost.");
      Assert.Equal("Exit anyway?", heading);
      Assert.Equal("What you typed\nwill be lost.", text);
    }

    [Fact]
    public void The_rest_keeps_its_own_line_breaks_and_blank_lines() {
      (string heading, string text) = MessageRules.Split("Exported 1 windows to:\nD:/x.zip\n\nNote.");
      Assert.Equal("Exported 1 windows to:", heading);
      Assert.Equal("D:/x.zip\n\nNote.", text);
      Assert.Equal(("Your data was exported.", "D:/x.zip"), MessageRules.Split("Your data was exported.\nD:/x.zip"));
    }

    [Fact]
    public void A_one_line_message_is_all_heading_and_nothing_is_lost() {
      Assert.Equal(("Delete this panel?", ""), MessageRules.Split("Delete this panel?"));
      Assert.Equal(("", ""), MessageRules.Split(null));
      Assert.Equal(("", ""), MessageRules.Split("  "));
    }
  }
}
