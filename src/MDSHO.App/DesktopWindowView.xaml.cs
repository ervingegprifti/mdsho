using MDSHO.ViewModels;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using MDSHO.Data;
using MDSHO.Helpers;
using System.Collections.Generic;


namespace MDSHO {
  /// <summary>
  /// Interaction logic for DesktopWindowView.xaml
  /// </summary>
  public partial class DesktopWindowView : Window {

		/// <summary>Creates the window for a view model (a real window or a clone) and moves it back on screen if its saved position is no longer visible.</summary>
		public DesktopWindowView(DesktopWindowViewModel desktopWindow) {
      InitializeComponent();
			this.InitializeWindowEvents();
			this.InitializeControlEvents();
			// Restore: a window saved on a disconnected monitor must not reopen invisible.
			try {
				ScreenInfoProvider.EnsureWindowOnScreen(desktopWindow);
			} catch (Exception ex) {
				ErrorDialog.ShowDialog(ex);
			}
			this.DataContext = desktopWindow;
			// Drops on the shortcut tree go through the App's drop handler, which uses this window's view model.
			GongSolutions.Wpf.DragDrop.DragDrop.SetDropHandler(this.TreeViewShortcuts, new ShortcutDropHandler(desktopWindow));
			// A window restored at a docked position starts with square corners.
			RefreshDocked();
		}

		/// <summary>Works out again whether the window is docked to a screen side, which decides rounded or square corners.</summary>
		private void RefreshDocked() {
			try {
				(this.DataContext as DesktopWindowViewModel)?.RefreshDocked(AppServices.Current.Screens.AllScreens());
			} catch (Exception ex) {
				ErrorDialog.ShowDialog(ex);
			}
		}


		/// <summary>Coming back to the window (e.g. from Explorer) rechecks the shortcut targets for the missing-target icon.</summary>
		protected override void OnActivated(EventArgs e) {
			base.OnActivated(e);
			(this.DataContext as DesktopWindowViewModel)?.RefreshTargetKinds();
		}

		#region INITIALIZERS
		private void InitializeWindowEvents() {
      this.Loaded += DesktopWindowView_Loaded;
      this.StateChanged += DesktopWindowView_StateChanged;
		}
    private void InitializeControlEvents() {
      // Header
      this.ContentControlHeader.MouseDoubleClick += ContentControlHeader_MouseDoubleClick;
			this.GridHeader.MouseLeftButtonDown += GridHeader_MouseLeftButtonDown;
      this.GridHeader.ContextMenuOpening += GridHeader_ContextMenuOpening;
      // Header MenuItems.
      this.MenuItemHeaderCloneThisWindowToAllScreens.Click += MenuItemHeaderCloneThisWindowToAllScreens_Click;
      this.MenuItemHeaderRenameWindowTitle.Click += MenuItemHeaderRenameWindowTitle_Click;
      this.MenuItemHeaderNewWindow.Click += MenuItemHeaderNewWindow_Click;
			this.MenuItemHeaderDockWindowLeft.Click += MenuItemHeaderDockWindowLeft_Click;
      this.MenuItemHeaderDockWindowRight.Click += MenuItemHeaderDockWindowRight_Click;
			this.MenuItemHeaderExportData.Click += MenuItemHeaderExportData_Click;
			this.MenuItemHeaderImportData.Click += MenuItemHeaderImportData_Click;
			this.MenuItemHeaderBrowseAppData.Click += MenuItemHeaderBrowseAppData_Click;
			this.MenuItemHeaderRate.Click += MenuItemHeaderRate_Click;
			this.MenuItemHeaderShare.Click += MenuItemHeaderShare_Click;
			this.MenuItemHeaderAboutWindow.Click += MenuItemHeaderAboutWindow_Click;
      this.MenuItemHeaderDeleteWindow.Click += MenuItemHeaderDeleteWindow_Click;
      this.MenuItemHeaderExitApplication.Click += MenuItemHeaderExitApplication_Click;
      // Header Controls
      this.ButtonHeaderDockWindowLeft.Click += ButtonHeaderDockWindowLeft_Click;
      this.ButtonHeaderDockWindowRight.Click += ButtonHeaderDockWindowRight_Click;
      this.ButtonHeaderCloneThisWindowToAllScreens.Click += ButtonHeaderCloneThisWindowToAllScreens_Click;
      // Body Controls.

			// Footer Controls.
			this.ButtonFooterSettings.Click += ButtonFooterSettings_Click;
      this.ButtonFooterPRO.Click += ButtonFooterPRO_Click;
		}


    private void GridHeader_ContextMenuOpening(object sender, ContextMenuEventArgs e) {
      this.UpdateHeaderMenuItems();
    }

    /// <summary>Shows or hides the title menu items that depend on the window: Delete Window is hidden on clones and on the last window.</summary>
    public void UpdateHeaderMenuItems() {
			DesktopWindowViewModel desktopWindow = (DesktopWindowViewModel)this.DataContext;
			if (desktopWindow != null) {
				if (desktopWindow.IsClone) {
					this.MenuItemHeaderDeleteWindow.Visibility = Visibility.Collapsed;
				} else {
					// Prevent Delete Window if there is only one left.
					this.MenuItemHeaderDeleteWindow.Visibility = (desktopWindow.AppViewModel.CountWindows() > 1) ? Visibility.Visible : Visibility.Collapsed;
        }
				// With neither "Close this clone" nor "Delete window" shown, their separator would sit next to the following one.
				bool closeOrDeleteShown = desktopWindow.IsClone || this.MenuItemHeaderDeleteWindow.Visibility == Visibility.Visible;
				this.SeparatorHeaderCloseDelete.Visibility = closeOrDeleteShown ? Visibility.Visible : Visibility.Collapsed;
			}
		}
    #endregion INITIALIZERS


    #region WINDOW EVENTS
    private void DesktopWindowView_StateChanged(object sender, EventArgs e) {
			// Prevent window state from other than normal.
			if (this.WindowState != WindowState.Normal) {
				this.WindowState = WindowState.Normal;
			}
		}
		#endregion WINDOW EVENTS


		#region CONTROL EVENTS
		// Header
		private void ContentControlHeader_MouseDoubleClick(object sender, MouseButtonEventArgs e) {
			CenterWindowToItsOwnScreen();
		}
		private void MenuItemHeaderNewWindow_Click(object sender, RoutedEventArgs e) {
			try {
				Common.ScreenInfo screen = WindowLookup.GetScreenFromWindow(this);
				App app = (App)Application.Current;
				app.NewWindow(screen);
			} catch (Exception ex) {
				ErrorDialog.ShowDialog(ex);
			}
		}
		private void MenuItemHeaderDeleteWindow_Click(object sender, RoutedEventArgs e) {
      try {
				MessageBoxResult messageBoxResult1 = MessageBox.Show("This window and all its shortcuts will be deleted. Do you want to delete this window?", "Delete window confirmation", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
				if (messageBoxResult1 == MessageBoxResult.Yes) {
					MessageBoxResult messageBoxResult2 = MessageBox.Show("One last warning. Do you want to delete this window? All its shortcuts will be gone.", "Delete window warning", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
					if (messageBoxResult2 == MessageBoxResult.Yes) {
						DesktopWindowViewModel desktopWindow = (DesktopWindowViewModel)this.DataContext;
						if (desktopWindow.IsClone) {
							// Just check and make sure we do not delete a window from its clone.
							return;
						}

						// So one thing is for sure that this is not a clone.

						// Delete all clones belonging to this window.
						for (int i = desktopWindow.Clones.Count - 1; i >= 0; i--) {
							DesktopWindowViewModel cloneWindow = desktopWindow.Clones[i];
							cloneWindow.CloseThisWindow(null);
							// The implementation of cloneWindow.CloseThisWindow(null); is at DesktopWindowView_Loaded.
						}

						// Delete all shortcuts and this window (one transaction, then view models and files).
						desktopWindow.DeleteWindowAndShortcuts();
						this.Close();
					}
				}
			} catch (Exception ex) {
				ErrorDialog.ShowDialog(ex);
			}
		}
		private void MenuItemHeaderExportData_Click(object sender, RoutedEventArgs e) {
			try {
				((App)Application.Current).ExportData();
			} catch (Exception ex) {
				ErrorDialog.ShowDialog(ex);
			}
		}
		private void MenuItemHeaderImportData_Click(object sender, RoutedEventArgs e) {
			try {
				((App)Application.Current).ImportData();
			} catch (Exception ex) {
				ErrorDialog.ShowDialog(ex);
			}
		}
		private void MenuItemHeaderBrowseAppData_Click(object sender, RoutedEventArgs e) {
			App.BrowseAppData();
		}
		private void MenuItemHeaderRate_Click(object sender, RoutedEventArgs e) {
			_ = AppServices.Current.StoreActions.RateAsync();
		}
		private void MenuItemHeaderShare_Click(object sender, RoutedEventArgs e) {
			_ = AppServices.Current.StoreActions.ShareAsync();
		}
		private void MenuItemHeaderAboutWindow_Click(object sender, RoutedEventArgs e) {
      try {
				App app = (App)Application.Current;
				app.ShowAboutWindow(((DesktopWindowViewModel)this.DataContext).Appearance.Icons);
			} catch (Exception ex) {
				ErrorDialog.ShowDialog(ex);
			}
		}
		private void MenuItemHeaderExitApplication_Click(object sender, RoutedEventArgs e) {
      try {
				App app = (App)Application.Current;
				app.ExitApplication(silent: false);
      } catch (Exception ex) {
				ErrorDialog.ShowDialog(ex);
			}
		}
		// Footer
		private void ButtonFooterSettings_Click(object sender, RoutedEventArgs e) {
			ShowSettingsWindow();
		}
		private void ButtonFooterPRO_Click(object sender, RoutedEventArgs e) {
			MessageBox.Show("PRO features will be freely available until Professional License implementation.", "Professional license", MessageBoxButton.OK, MessageBoxImage.Information);
		}
		#endregion CONTROL EVENTS


		#region MOVE & SIZE WINDOW
		private void GridHeader_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
			this.DragMove();
		}
		private HwndSource hwndSource;
		// The window is saved only when the user lets go of it (WM_EXITSIZEMOVE), not on every pixel of a drag.
		// WM_MOVING. Sent to a window that the user is moving.
		private const int WM_MOVING = 0x0216;
		// WM_SIZING. Sent to a window that the user is resizing.
		private const int WM_SIZING = 0x214;
		// WM_EXITSIZEMOVE. Sent one time to a window, after it has exited the moving or sizing modal loop.
		private const int WM_EXITSIZEMOVE = 0x232;
		//
		private bool windowWasMoved = false;
		private bool windowWasResized = false;
		private void DesktopWindowView_Loaded(object sender, RoutedEventArgs e) {
			try {
				if (this.DataContext is DesktopWindowViewModel keyedWindow && !keyboardWired) {
					keyboardWired = true;
					this.TreeViewShortcuts.PreviewKeyDown += TreeViewShortcuts_PreviewKeyDown;
				}
				if (this.DataContext is ICloseWindow vm) {
					vm.CloseWindow += () => {
						DesktopWindowViewModel desktopWindow = (DesktopWindowViewModel)(this.DataContext);
						if (desktopWindow.IsClone) {
							// Database first, then the parent's Clones (frees the screen), then close.
							desktopWindow.DeleteClone();
							this.Close();
            }
					};
				}
				//
				hwndSource = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
				hwndSource.AddHook(new HwndSourceHook(WndProc));
			} catch (Exception ex) {
				ErrorDialog.ShowDialog(ex);
			}
		}
		// WM_SYSCOMMAND with SC_CLOSE: Alt+F4 or the taskbar's "Close window". Close() from code (Delete Window, app exit,
		// shutdown) sends WM_CLOSE directly and never comes through here.
		private const int WM_SYSCOMMAND = 0x0112;
		private const int SC_CLOSE = 0xF060;
		/// <summary>
		/// Alt+F4: on a clone it does exactly what "Close this clone" does (deletes it and frees its screen); on a real window
		/// it is ignored, because a window only goes away through Delete Window or Exit.
		/// </summary>
		private void SystemCloseRequested() {
			try {
				if (this.DataContext is DesktopWindowViewModel desktopWindow && desktopWindow.IsClone) {
					desktopWindow.CloseThisWindow(null);
				}
			} catch (Exception ex) {
				ErrorDialog.ShowDialog(ex);
			}
		}
		private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) {
			if (msg == WM_SYSCOMMAND && ((int)wParam.ToInt64() & 0xFFF0) == SC_CLOSE) {
				handled = true;
				SystemCloseRequested();
				return IntPtr.Zero;
			}
			// Detect moving
			if (msg == WM_MOVING) {
				if (windowWasMoved == false) {
					// indicate that the user is moving and not resizing the window
					windowWasMoved = true;
				}
			}
			// Detect sizing
			if (msg == WM_SIZING) {
				if (windowWasResized == false) {
					// indicate that the user is resizing and not moving the window
					windowWasResized = true;
				}
			}
			// Detect move or size stopped
			if (msg == WM_EXITSIZEMOVE) {
				// check that this is the end of move and not resize operation
				if (windowWasMoved == true) {
					MoveWindowStoped();
					RefreshDocked();
					// set it back to false for the next move
					windowWasMoved = false;
				}
				// check that this is the end of resize and not move operation
				if (windowWasResized == true) {
					SizeWindowStoped();
					RefreshDocked();
					// set it back to false for the next resize
					windowWasResized = false;
				}
			}
			return IntPtr.Zero;
		}
		private void MoveWindowStoped() {
			try {
				// Save.
				DesktopWindowViewModel desktopWindow = (DesktopWindowViewModel)this.DataContext;
				Common.ScreenInfo screen = WindowLookup.GetScreenFromWindow(this);
				string screenDeviceName = screen.DeviceName;
				if (desktopWindow.IsClone) {
					DataStore.Clones.UpdateLeftTopScreenDeviceName(desktopWindow.ID, desktopWindow.Left, desktopWindow.Top, screenDeviceName);
				} else {
					DataStore.Windows.UpdateLeftTopScreenDeviceName(desktopWindow.ID, desktopWindow.Left, desktopWindow.Top, screenDeviceName);
				}
			} catch (Exception ex) {
				ErrorDialog.ShowDialog(ex);
			}
		}
		private void SizeWindowStoped() {
			try {
				// Save.
				DesktopWindowViewModel desktopWindow = (DesktopWindowViewModel)this.DataContext;
				Common.ScreenInfo screen = WindowLookup.GetScreenFromWindow(this);
				string screenDeviceName = screen.DeviceName;
				if (desktopWindow.IsClone) {
					DataStore.Clones.UpdateWidthHeightScreenDeviceName(desktopWindow.ID, desktopWindow.Width, desktopWindow.Height, screenDeviceName);
				} else {
					DataStore.Windows.UpdateWidthHeightScreenDeviceName(desktopWindow.ID, desktopWindow.Width, desktopWindow.Height, screenDeviceName);
				}
			} catch (Exception ex) {
				ErrorDialog.ShowDialog(ex);
			}
		}
		#endregion MOVE & SIZE WINDOW


		#region FUNCTIONS
		private void ShowSettingsWindow() {
			try {
				DesktopWindowViewModel desktopWindow = (DesktopWindowViewModel)this.DataContext;
				SettingsWindow settingsWindow = new SettingsWindow(desktopWindow);
				settingsWindow.PositionRelativeToParent(this);
				settingsWindow.ShowDialog();
			} catch (Exception ex) {
				ErrorDialog.ShowDialog(ex);
			}
		}
		private void CenterWindowToItsOwnScreen() {
			DesktopWindowViewModel desktopWindow = (DesktopWindowViewModel)this.DataContext;
			Common.ScreenInfo screen = WindowLookup.GetScreenFromWindow(this);
			Common.ScreenPoint center = Common.ScreenGeometry.CenterNewWindow(screen);
			int centerLeft = (int)center.X;
			int centerTop = (int)center.Y;
			int centerWidth = Common.AppConstants.InitialWindowWidth;
			int centerHeight = Common.AppConstants.InitialWindowHeight;

			if (desktopWindow.Left == (long)centerLeft && desktopWindow.Top == (long)centerTop && desktopWindow.Width == (long)centerWidth && desktopWindow.Height == (long)centerHeight) {
				// Do nothing.
      } else {
				// Center Window
				desktopWindow.Left = (long)centerLeft;
				desktopWindow.Top = (long)centerTop;
				desktopWindow.Width = (long)centerWidth;
				desktopWindow.Height = (long)centerHeight;
      }

			// Save to Database.
			if (desktopWindow.IsClone) {
				DataStore.Clones.UpdateLeftTopWidthHeight(desktopWindow.ID, desktopWindow.Left, desktopWindow.Top, desktopWindow.Width, desktopWindow.Height);
			} else {
				DataStore.Windows.UpdateLeftTopWidthHeight(desktopWindow.ID, desktopWindow.Left, desktopWindow.Top, desktopWindow.Width, desktopWindow.Height);
      }
			RefreshDocked();
		}
		#endregion FUNCTIONS


		#region CLONE THIS WINDOW TO OTHER SCREENS
		private void MenuItemHeaderCloneThisWindowToAllScreens_Click(object sender, RoutedEventArgs e) {
      try {
				CloneThisWindowToAllScreens();
			} catch (Exception ex) {
				ErrorDialog.ShowDialog(ex);
			}
		}
		private void ButtonHeaderCloneThisWindowToAllScreens_Click(object sender, RoutedEventArgs e) {
			try {
				CloneThisWindowToAllScreens();
			} catch (Exception ex) {
				ErrorDialog.ShowDialog(ex);
			}
		}
		private void CloneThisWindowToAllScreens() {
			DesktopWindowViewModel desktopWindow = (DesktopWindowViewModel)(this.DataContext);
			// If invoked from a clone, clone the original window instead.
			if (desktopWindow.IsClone) {
				desktopWindow = desktopWindow.ParentWindow;
				if (desktopWindow == null) {
					return;
				}
			}

			// Screens that already show the original window or one of its clones.
			HashSet<string> occupiedScreenDeviceNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			occupiedScreenDeviceNames.Add(GetScreenDeviceNameOfWindow(desktopWindow));
			foreach (DesktopWindowViewModel existingClone in desktopWindow.Clones) {
				occupiedScreenDeviceNames.Add(GetScreenDeviceNameOfWindow(existingClone));
			}

			// Loop through all available screens and clone only to the free ones.
			List<DesktopWindowViewModel> newCloneWindows = new List<DesktopWindowViewModel>();
			foreach (Common.ScreenInfo screen in AppServices.Current.Screens.AllScreens()) {
				string screenDeviceName = screen.DeviceName;
				if (occupiedScreenDeviceNames.Contains(screenDeviceName)) {
					continue;
				}
				// Position the new window clone at the center of the screen
				Common.ScreenPoint cloneCenter = Common.ScreenGeometry.CenterNewWindow(screen);
				int left = (int)cloneCenter.X;
				int top = (int)cloneCenter.Y;
				int countBefore = desktopWindow.Clones.Count;
				desktopWindow.CloneWindow(screenDeviceName, left, top);
				for (int i = countBefore; i < desktopWindow.Clones.Count; i++) {
					newCloneWindows.Add(desktopWindow.Clones[i]);
				}
				occupiedScreenDeviceNames.Add(screenDeviceName);
			}

			// Create windows only for the clones created in this call.
			foreach (DesktopWindowViewModel cloneWindow in newCloneWindows) {
				DesktopWindowView windowView = new DesktopWindowView(cloneWindow);
				windowView.Show();
			}
		}
		private static string GetScreenDeviceNameOfWindow(DesktopWindowViewModel desktopWindow) {
			// Prefer the screen the open window is actually on, fall back to the saved screen.
			Common.ScreenInfo screen = WindowLookup.GetScreenFromViewModel(desktopWindow);
			if (screen != null) {
				return screen.DeviceName;
			}
			return desktopWindow.ScreenDeviceName ?? string.Empty;
		}
		#endregion CLONE THIS WINDOW TO OTHER SCREENS


		#region RENAME WINDOW TITLE
		private void MenuItemHeaderRenameWindowTitle_Click(object sender, RoutedEventArgs e) {
			try {
				((DesktopWindowViewModel)this.DataContext).BeginEditTitle();
			} catch (Exception ex) {
				ErrorDialog.ShowDialog(ex);
			}
		}
		#endregion RENAME WINDOW TITLE


		#region KEYBOARD SHORTCUTS
		private bool keyboardWired;
		private void TreeViewShortcuts_PreviewKeyDown(object sender, KeyEventArgs e) {
			try {
				// Ignore while typing in any TextBox (rename etc.): it handles its own Enter/Esc.
				bool isEditing = Keyboard.FocusedElement is TextBox;
				ShortcutViewModel selected = this.TreeViewShortcuts.SelectedItem as ShortcutViewModel;
				if (this.DataContext is DesktopWindowViewModel desktopWindow && desktopWindow.HandleKey(KeyInput.ToShortcutKey(e.Key), KeyInput.ToShortcutModifiers(Keyboard.Modifiers), selected, isEditing)) {
					e.Handled = true;
				}
			} catch (Exception ex) {
				ErrorDialog.ShowDialog(ex);
			}
		}
		#endregion KEYBOARD SHORTCUTS


    #region RUN SHORTCUT
		private void ButtonShortcut_Click(object sender, RoutedEventArgs e) {
			try {
				ShortcutViewModel shortcut = (ShortcutViewModel)((Button)sender).DataContext;
				shortcut.IsSelected = true;
				bool singleClick = shortcut.OwningWindow.Appearance.SingleClickToOpen;
				shortcut.Click(singleClick);
				if (!singleClick) {
					e.Handled = true;
				}
			} catch (Exception ex) {
				ErrorDialog.ShowDialog(ex);
			}
		}
		private void ButtonShortcut_MouseDoubleClick(object sender, MouseButtonEventArgs e) {
			try {
				ShortcutViewModel shortcut = (ShortcutViewModel)((Button)sender).DataContext;
				shortcut.IsSelected = true;
				bool singleClick = shortcut.OwningWindow.Appearance.SingleClickToOpen;
				shortcut.DoubleClick(singleClick);
				if (singleClick) {
					e.Handled = true;
				}
			} catch (Exception ex) {
				ErrorDialog.ShowDialog(ex);
			}
		}
		#endregion RUN SHORTCUT


		#region DOCK WINDOW
		// Dock Window Left
		private void MenuItemHeaderDockWindowLeft_Click(object sender, RoutedEventArgs e) {
			DockWindowLeft();
		}
		private void ButtonHeaderDockWindowLeft_Click(object sender, RoutedEventArgs e) {
			DockWindowLeft();
		}
		// Dock Window Right
		private void MenuItemHeaderDockWindowRight_Click(object sender, RoutedEventArgs e) {
			DockWindowRight();
		}
		private void ButtonHeaderDockWindowRight_Click(object sender, RoutedEventArgs e) {
			DockWindowRight();
		}
		private void DockWindowLeft() {
			try {
				DesktopWindowViewModel desktopWindow = (DesktopWindowViewModel)(this.DataContext);
				Common.ScreenInfo screen = WindowLookup.GetScreenFromWindow(this);
				Common.ScreenRect dock = Common.ScreenGeometry.DockLeft(screen, desktopWindow.Width);
				desktopWindow.ApplyDock((long)dock.Left, (long)dock.Top, (long)dock.Height); // saves geometry, corners turn square
			} catch (Exception ex) {
				ErrorDialog.ShowDialog(ex);
			}
		}
		private void DockWindowRight() {
			try {
				DesktopWindowViewModel desktopWindow = (DesktopWindowViewModel)(this.DataContext);
				Common.ScreenInfo screen = WindowLookup.GetScreenFromWindow(this);
				Common.ScreenRect dock = Common.ScreenGeometry.DockRight(screen, desktopWindow.Width);
				desktopWindow.ApplyDock((long)dock.Left, (long)dock.Top, (long)dock.Height); // saves geometry, corners turn square
			} catch (Exception ex) {
				ErrorDialog.ShowDialog(ex);
			}
		}
		#endregion DOCK WINDOW


	}

}