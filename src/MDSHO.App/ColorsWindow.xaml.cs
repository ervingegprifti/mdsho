using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using MDSHO.Helpers;
using MDSHO.ViewModels;
using MDSHO.Data;
using MDSHO.Common;

namespace MDSHO {
  /// <summary>
  /// The Colours window of a panel (the brush button in its title bar) or of a note (the brush button in the Note window):
  /// five colour buttons and one palette for the selected colour. Changes show live; Apply saves them, closing without
  /// Apply puts the old colours back. Default colours puts back the saved defaults (else MDSHO's own colours), and Set as
  /// default saves the colours on screen as the defaults of panels, or of notes, at once.
  /// </summary>
  public partial class ColorsWindow : Window {
    private bool closeWithoutApply = true;
    private IconGlyphs icons;
    // Puts back what was there when the window opened (closed without Apply), and saves the new values (Apply).
    private Action revert = () => { };
    private Action save = () => { };

    /// <summary>Creates the window as "Window colours" for a panel (a clone edits its window's colours).</summary>
    public ColorsWindow(DesktopWindowViewModel desktopWindow) {
      DesktopWindowAppearance a = desktopWindow.Appearance;
      Init(desktopWindow, a.Icons);
      this.Title = "Window colours: " + a.Title;
      SetTitle();
      this.ButtonDefaultColours.Click += (s, e) => a.ResetColorsToDefaults();
      this.ButtonSetAsDefault.Click += (s, e) => a.SaveColorsAsDefaults();
      this.ButtonSetAsDefault.ToolTip = "Use these colours for new windows and for Default colours";
      (string border, string background, string text, string titleBackground, string titleText) old =
        (a.BorderColor, a.BackgroundColor, a.TextColor, a.TitleBackgroundColor, a.TitleTextColor);
      this.revert = () => {
        // The colours are saved only on Apply, as they change many times while the palette is dragged.
        a.BorderColor = old.border;
        a.BackgroundColor = old.background;
        a.TextColor = old.text;
        a.TitleBackgroundColor = old.titleBackground;
        a.TitleTextColor = old.titleText;
      };
      this.save = () => {
        long windowID = (desktopWindow.IsClone) ? desktopWindow.ParentWindow.ID : desktopWindow.ID;
        using (MDSHO.Core.ITransactionScope tx = DataStore.BeginTransaction()) {
          DataStore.Windows.UpdateBorderColor(windowID, a.BorderColor);
          DataStore.Windows.UpdateBackgroundColor(windowID, a.BackgroundColor);
          DataStore.Windows.UpdateTextColor(windowID, a.TextColor);
          DataStore.Windows.UpdateTitleBackgroundColor(windowID, a.TitleBackgroundColor);
          DataStore.Windows.UpdateTitleTextColor(windowID, a.TitleTextColor);
          tx.Commit();
        }
      };
    }

    /// <summary>Creates the window as "Note colours" for a note.</summary>
    public ColorsWindow(ShortcutViewModel note) {
      Init(new NoteColorsTarget(note), note.OwningWindow?.Appearance?.Icons ?? new IconGlyphs(true));
      this.Title = "Note colours: " + note.Name;
      SetTitle();
      this.LabelAppearanceHeading.Content = "Note colours";
      this.ButtonDefaultColours.Click += (s, e) => note.NoteColors.ResetToDefaults();
      this.ButtonSetAsDefault.Click += (s, e) => note.NoteColors.SaveAsDefaults();
      this.ButtonSetAsDefault.ToolTip = "Use these colours for new notes and for Default colours";
      NoteColors c = note.NoteColors;
      (string border, string titleBackground, string titleText, string background, string text) old =
        (c.BorderColor, c.TitleBackgroundColor, c.TitleTextColor, c.BackgroundColor, c.TextColor);
      this.revert = () => c.Load(old.border, old.titleBackground, old.titleText, old.background, old.text);
      this.save = () => note.SaveNoteColors();
    }

    private void Init(object dataContext, IconGlyphs iconGlyphs) {
      InitializeComponent();
      // Every outer margin of the content and of the Apply bar equals the Apply button height, like in the Note window.
      this.ButtonApply.SizeChanged += (s, e) => {
        Thickness margin = new Thickness(this.ButtonApply.ActualHeight);
        this.PanelBody.Margin = margin;
        this.FooterContent.Margin = margin;
      };
      this.DataContext = dataContext;
      // The window icon is the brush of this window's icon set and changes as soon as Solid/Outlined is toggled.
      this.icons = iconGlyphs;
      ShowIcons();
      this.icons.PropertyChanged += Icons_PropertyChanged;
      this.Closed += (s, e) => this.icons.PropertyChanged -= Icons_PropertyChanged;

      // Window Events.
      this.Closing += MainWindow_Closing;

      // Control Events
      this.ButtonApply.Click += ButtonApply_Click;
      this.ListColorTargets.SelectionChanged += ListColorTargets_SelectionChanged;
      // The palette: the square picks saturation and brightness (mouse and arrow keys); the swatch, R G B A values,
      // square colour and thumb follow the palette's colour state.
      this.SquareSaturationValue.MouseLeftButtonDown += Square_MouseLeftButtonDown;
      this.SquareSaturationValue.MouseMove += Square_MouseMove;
      this.SquareSaturationValue.MouseLeftButtonUp += (s, e) => this.SquareSaturationValue.ReleaseMouseCapture();
      this.SquareSaturationValue.KeyDown += Square_KeyDown;
      this.SquareSaturationValue.GotKeyboardFocus += (s, e) => this.SquareFocus.Visibility = Visibility.Visible;
      this.SquareSaturationValue.LostKeyboardFocus += (s, e) => this.SquareFocus.Visibility = Visibility.Hidden;
      this.SquareSaturationValue.SizeChanged += (s, e) => UpdatePaletteParts();
      System.ComponentModel.DependencyPropertyDescriptor colorState =
        System.ComponentModel.DependencyPropertyDescriptor.FromProperty(ColorPicker.PickerControlBase.ColorStateProperty, typeof(ColorPicker.PickerControlBase));
      EventHandler colorStateChanged = (s, e) => UpdatePaletteParts();
      colorState.AddValueChanged(this.PickerColor, colorStateChanged);
      this.Closed += (s, e) => colorState.RemoveValueChanged(this.PickerColor, colorStateChanged);
      // Border is selected first; the palette follows the selected colour button.
      this.ListColorTargets.SelectedIndex = 0;
    }

    private void SetTitle() {
#if DEBUG
      this.Title += " DEBUG";
#endif
    }


#region WINDOW EVENTS
    private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e) {
      if (closeWithoutApply) {
        // Put the old colours back.
        this.revert();
      }
    }
#endregion WINDOW EVENTS


#region CONTROL EVENTS
    private void ButtonApply_Click(object sender, RoutedEventArgs e) {
      this.save();
      closeWithoutApply = false;
      this.Close();
    }
    private void Icons_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e) {
      if (e.PropertyName == nameof(IconGlyphs.Brush) || e.PropertyName == nameof(IconGlyphs.Times)) {
        ShowIcons();
      }
    }

    // The window icon and the title bar's icon (the brush) and close glyph, in the window's icon set.
    private void ShowIcons() {
      string glyph = this.icons.Brush;
      this.Icon = GlyphIcon.Create(glyph);
      this.TitleBar.Glyph = glyph;
      this.TitleBar.CloseGlyph = this.icons.Times;
    }

    private void ListColorTargets_SelectionChanged(object sender, SelectionChangedEventArgs e) {
      if (this.ListColorTargets.SelectedItem == null) {
        // One colour button is always selected (a Ctrl+click would otherwise unselect it): keep the previous one.
        this.ListColorTargets.SelectedItem = e.RemovedItems.Count > 0 ? e.RemovedItems[0] : this.ListColorTargets.Items[0];
        return;
      }
      BindPaletteToSelectedColor();
    }
    private void Square_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
      this.SquareSaturationValue.Focus();
      this.SquareSaturationValue.CaptureMouse();
      PickFromSquare(e.GetPosition(this.SquareSaturationValue));
      e.Handled = true;
    }

    private void Square_MouseMove(object sender, MouseEventArgs e) {
      if (this.SquareSaturationValue.IsMouseCaptured) {
        PickFromSquare(e.GetPosition(this.SquareSaturationValue));
      }
    }

    // Arrow keys move the thumb by 1 % (10 % with Shift).
    private void Square_KeyDown(object sender, KeyEventArgs e) {
      double step = (Keyboard.Modifiers & ModifierKeys.Shift) != 0 ? 0.1 : 0.01;
      ColorPicker.Models.ColorState state = this.PickerColor.ColorState;
      switch (e.Key) {
        case Key.Left: PickSaturationAndValue(state.HSV_S - step, state.HSV_V); break;
        case Key.Right: PickSaturationAndValue(state.HSV_S + step, state.HSV_V); break;
        case Key.Up: PickSaturationAndValue(state.HSV_S, state.HSV_V + step); break;
        case Key.Down: PickSaturationAndValue(state.HSV_S, state.HSV_V - step); break;
        default: return;
      }
      e.Handled = true;
    }
#endregion CONTROL EVENTS


#region FUNCTIONS
    /// <summary>
    /// Sets the palette's saturation and brightness (value), each from 0 to 1, keeping its hue and alpha; the selected
    /// colour follows through the palette's binding. Used by the square (mouse and arrow keys).
    /// </summary>
    public void PickSaturationAndValue(double saturation, double value) {
      ColorPicker.Models.ColorState state = this.PickerColor.ColorState;
      state.HSV_S = Math.Clamp(saturation, 0, 1);
      state.HSV_V = Math.Clamp(value, 0, 1);
      this.PickerColor.ColorState = state;
    }

    /// <summary>Draws the square in the current hue, moves its thumb and shows the colour as a swatch and R G B A values.</summary>
    private void UpdatePaletteParts() {
      ColorPicker.Models.ColorState state = this.PickerColor.ColorState;
      this.SquareHue.Background = new SolidColorBrush(HueColor(state.HSV_H));
      double width = this.SquareSaturationValue.ActualWidth;
      double height = this.SquareSaturationValue.ActualHeight;
      Canvas.SetLeft(this.SquareThumb, state.HSV_S * width - this.SquareThumb.Width / 2);
      Canvas.SetTop(this.SquareThumb, (1 - state.HSV_V) * height - this.SquareThumb.Height / 2);
      Color color = this.PickerColor.SelectedColor;
      this.SwatchCurrent.Background = new SolidColorBrush(color);
      this.TextRgba.Text = $"R {color.R}   G {color.G}   B {color.B}   A {color.A}";
    }

    /// <summary>The fully saturated, full-brightness colour of a hue (0 to 360).</summary>
    private static Color HueColor(double hue) {
      double h = ((hue % 360) + 360) % 360 / 60;
      double x = 1 - Math.Abs(h % 2 - 1);
      (double r, double g, double b) = h < 1 ? (1.0, x, 0.0) : h < 2 ? (x, 1.0, 0.0) : h < 3 ? (0.0, 1.0, x) : h < 4 ? (0.0, x, 1.0) : h < 5 ? (x, 0.0, 1.0) : (1.0, 0.0, x);
      return Color.FromRgb((byte)Math.Round(r * 255), (byte)Math.Round(g * 255), (byte)Math.Round(b * 255));
    }

    private void PickFromSquare(Point point) {
      double width = this.SquareSaturationValue.ActualWidth;
      double height = this.SquareSaturationValue.ActualHeight;
      if (width > 0 && height > 0) {
        PickSaturationAndValue(point.X / width, 1 - point.Y / height);
      }
    }
    /// <summary>
    /// Points the palette at the selected button's colour, two way and live like the old pickers. The old binding is
    /// removed before the new one is made, so the palette's colour is never written into the newly selected colour or
    /// back into the old one while it switches.
    /// </summary>
    private void BindPaletteToSelectedColor() {
      ListBoxItem item = (ListBoxItem)this.ListColorTargets.SelectedItem;
      string property = (string)item.Tag;
      BindingOperations.ClearBinding(this.PickerColor, ColorPicker.PickerControlBase.SelectedColorProperty);
      Binding binding = new Binding("Appearance." + property) {
        Mode = BindingMode.TwoWay,
        UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        Converter = (IValueConverter)this.FindResource("HexToColor")
      };
      BindingOperations.SetBinding(this.PickerColor, ColorPicker.PickerControlBase.SelectedColorProperty, binding);
      this.TextEditing.Text = "Editing: " + ColorLabel(item);
    }

    /// <summary>The visible label of a colour button ("Border", "Title background", ...).</summary>
    private static string ColorLabel(ListBoxItem item) {
      return ((TextBlock)((Panel)item.Content).Children[1]).Text;
    }

#endregion FUNCTIONS
  }

}