using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using EasyFramework;
using SuperScreenShotterVR.Properties;
using Application = System.Windows.Application;
using Brushes = System.Windows.Media.Brushes;
using MessageBox = System.Windows.MessageBox;

namespace SuperScreenShotterVR
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    [SupportedOSPlatform("windows7.0")]
    public partial class MainWindow : Window
    {
        private MainController _controller = new MainController();
        private Settings _settings = Settings.Default;
        private NotifyIcon _notifyIcon;
        private static Mutex _mutex = null;
        private bool _settingsLoaded = false;
        private bool _viewfinderOn = false;

        // For hotkey support
        [DllImport("user32.dll")]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vlc);
        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        private HwndSource _source;
        private const int HOTKEY_ID_SCREENSHOT = 1111;
        private const int HOTKEY_ID_VIEWFINDER = 2222;

        public MainWindow()
        {
            InitializeComponent();
            
            // Prevent multiple instances running at once
            _mutex = new Mutex(true, Properties.Resources.AppName, out bool createdNew);
            if (!createdNew)
            {
                MessageBox.Show(
                Application.Current.MainWindow,
                "This application is already running!",
                Properties.Resources.AppName,
                MessageBoxButton.OK,
                MessageBoxImage.Information
                );
                Application.Current.Shutdown();
            }

            InitSettings();
            _controller.StatusUpdateAction = (status) =>
            {
                Dispatcher.Invoke(() =>
                {
                    LabelStatus.Content = status ? "Connected" : "Disconnected";
                    LabelStatus.Background = status ? Brushes.OliveDrab : Brushes.Tomato;
                });
            };
            _controller.AppUpdateAction = (appId) =>
            {
                var appIdFixed = appId.Replace("_", "__"); // Single underscores are interpret to show the next char as shortcut
                Dispatcher.Invoke(() =>
                {
                    LabelAppId.Content = appIdFixed != string.Empty ? appIdFixed : "None";
                    LabelAppId.Background = appIdFixed != string.Empty ? Brushes.OliveDrab : Brushes.Gray;
                });
            };
            _controller.ExitAction = () =>
            {
                Dispatcher.Invoke(() => { 
                    if (_settings.ExitWithSteamVR)
                    {
                        ExitApplication();
                    }
                });
            };
            _controller.SetDebugLogAction((message) => {
                Dispatcher.Invoke(()=>{
                    var time = DateTime.Now.ToString("HH:mm:ss");
                    Debug.WriteLine($"{time}: {message}");
                });
            });
            _controller.Init();

            var icon = Properties.Resources.app_logo as Icon;
            _notifyIcon = new NotifyIcon();
            _notifyIcon.Click += NotifyIcon_Click;
            _notifyIcon.Text = $"Click to show the {Properties.Resources.AppName} window";
            _notifyIcon.Icon = icon;
            _notifyIcon.Visible = true;

            if(_settings.LaunchMinimized)
            {
                Hide();
                WindowState = WindowState.Minimized;
                ShowInTaskbar = !_settings.Tray;
            }
        }

        // Restore window
        private void NotifyIcon_Click(object sender, EventArgs e)
        {
            WindowState = WindowState.Normal;
            ShowInTaskbar = true;
            Show();
            Activate();           
        }

        // Not doing this will leave the icon after app closure
        protected override void OnClosing(CancelEventArgs e)
        {
            // Disengage hotkeys
            if (_source != null)
            {
                _source.RemoveHook(HwndHook);
                _source = null;
            }
            var success1 = UnregisterHotKey(HOTKEY_ID_SCREENSHOT);
            var success2 = UnregisterHotKey(HOTKEY_ID_VIEWFINDER);
            Debug.WriteLine($"Unregistering hotkeys: {success1}, {success2}");

            _notifyIcon.Dispose();
            base.OnClosing(e);
        }

        // Need to add this event to the window object
        private void Window_StateChanged(object sender, EventArgs e)
        {
            switch (WindowState)
            {
                case WindowState.Minimized: ShowInTaskbar = !_settings.Tray; break;
                default: ShowInTaskbar = true; Show(); break;
            }
        }

        private void InitSettings()
        {
#if DEBUG
            LabelVersion.Content = $"{Properties.Resources.Version}d";
#else
            LabelVersion.Content = Properties.Resources.Version;
#endif
            TextBoxTimerSeconds.IsEnabled = !_settings.CaptureTimer;

            if (_settings.Directory == string.Empty)
            {
                _settings.Directory = Directory.GetCurrentDirectory();
                _settings.Save();
            }

            if (_settings.CustomAudio == string.Empty)
            {
                _settings.CustomAudio = $"{Directory.GetCurrentDirectory()}\\resources\\screenshot.wav";
                _settings.Save();
            }

            CheckBoxViewFinder.IsChecked = _settings.ViewFinder;
            CheckBoxRestrictToBox.IsChecked = _settings.RestrictToBox;
            CheckBoxLockHorizon.IsChecked = _settings.LockHorizon;
            CheckBoxIndicateDegrees.IsChecked = _settings.IndicateDegrees;
            CheckBoxSaveRightImage.IsChecked = _settings.SaveRightImage;
            CheckBoxCaptureTimer.IsChecked = _settings.CaptureTimer;
            TextBoxTimerSeconds.Text = _settings.TimerSeconds.ToString();
            CheckBoxDelayCapture.IsChecked = _settings.DelayCapture;
            TextBoxDelaySeconds.Text = _settings.DelaySeconds.ToString();
            CheckBoxSubmitToSteam.IsChecked = _settings.SubmitToSteam;
            LabelDirectory.Content = _settings.Directory;
            LabelDirectory.ToolTip = _settings.Directory;

            CheckBoxEnableHotkeys.IsChecked = _settings.HotkeysEnabled;
            CheckBoxScreenshotHotkeyAlt.IsChecked = _settings.HotkeyScreenshotAlt;
            CheckBoxScreenshotHotkeyControl.IsChecked = _settings.HotkeyScreenshotControl;
            CheckBoxScreenshotHotkeyShift.IsChecked = _settings.HotkeyScreenshotShift;
            CheckBoxViewfinderHotkeyAlt.IsChecked = _settings.HotkeyViewfinderAlt;
            CheckBoxViewfinderHotkeyControl.IsChecked = _settings.HotkeyViewfinderControl;
            CheckBoxViewfinderHotkeyShift.IsChecked = _settings.HotkeyViewfinderShift;
            ComboBoxScreenshotHotkey.SelectedIndex = _settings.HotkeyScreenshot;
            ComboBoxViewfinderHotkey.SelectedIndex = _settings.HotkeyViewfinder;

            CheckBoxNotifications.IsChecked = _settings.Notifications;
            CheckBoxThumbnail.IsChecked = _settings.Thumbnail;
            CheckBoxAudio.IsChecked = _settings.Audio;

            CheckBoxReplaceShortcut.IsChecked = _settings.ReplaceShortcut;
            ButtonRehookShortcut.IsEnabled = _settings.ReplaceShortcut;
            CheckBoxLaunchMinimized.IsChecked = _settings.LaunchMinimized;
            CheckBoxTray.IsChecked = _settings.Tray;
            CheckBoxExitWithSteamVr.IsChecked = _settings.ExitWithSteamVR;
            CheckBoxEnableServer.IsChecked = _settings.EnableServer;
            CheckBoxAddTag.IsChecked = _settings.AddTag;
            CheckBoxTransmitAll.IsChecked = _settings.TransmitAll;
            TextBoxServerPort.Text = _settings.ServerPort.ToString();
            ComboBoxResponseResolution.SelectedIndex = _settings.ResponseResolution;

            SliderOverlayDistance.Value = _settings.OverlayDistanceGui;
            SliderOverlayOpacity.Value = _settings.OverlayOpacity;
            SliderReticleSize.Value = _settings.ReticleSize;
            _settingsLoaded = true;

            Debug.WriteLine("Settings initiated");
        }

        private void ExitApplication() {
            if (_notifyIcon != null) _notifyIcon.Dispose();
            Application.Current.Shutdown();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var helper = new WindowInteropHelper(this);
            _source = HwndSource.FromHwnd(helper.Handle);
            _source?.AddHook(HwndHook);
            UpdateHotkey(HOTKEY_ID_SCREENSHOT);
            UpdateHotkey(HOTKEY_ID_VIEWFINDER);
        }

        private bool RegisterHotKey(int id, int key, int modifiers)
        {
            var helper = new WindowInteropHelper(this);
            return RegisterHotKey(helper.Handle, id, modifiers, key);
        }

        private bool UnregisterHotKey(int id)
        {
            var helper = new WindowInteropHelper(this);
            return UnregisterHotKey(helper.Handle, id);
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;
            switch (msg)
            {
                case WM_HOTKEY:
                    Debug.WriteLine($"Reacting to hotkey {wParam.ToInt32()}!");
                    switch (wParam.ToInt32())
                    {
                        case HOTKEY_ID_SCREENSHOT:
                            _controller.HotkeyScreenshot();
                            _viewfinderOn = false;
                            handled = true;
                            break;
                        case HOTKEY_ID_VIEWFINDER:
                            _viewfinderOn = !_viewfinderOn;
                            _controller.HotkeyViewFinder(_viewfinderOn);
                            handled = true;
                            break;
                    }
                    break;
            }
            return IntPtr.Zero;
        }

        // GUI listeners
        private bool CheckboxValue(RoutedEventArgs e)
        {
            var name = e.RoutedEvent.Name;
            return name == "Checked";
        }

        private void CheckBox_ViewFinder_Checked(object sender, RoutedEventArgs e)
        {
            _settings.ViewFinder = CheckboxValue(e);
            _settings.Save();
        }

        private void CheckBox_RestrictToBox_Checked(object sender, RoutedEventArgs e)
        {
            _settings.RestrictToBox = CheckboxValue(e);
            _settings.Save();
        }

        private void CheckBox_LockHorizon_Checked(object sender, RoutedEventArgs e)
        {
            _settings.LockHorizon = CheckboxValue(e);
            _settings.Save();
        }

        private void CheckBox_IndicateDegrees_Checked(object sender, RoutedEventArgs e)
        {
            _settings.IndicateDegrees = CheckboxValue(e);
            _settings.Save();
        }

        private void CheckBox_CaptureTimer_Checked(object sender, RoutedEventArgs e)
        {
            var value = CheckboxValue(e);
            _settings.CaptureTimer = value;
            _settings.Save();
            TextBoxTimerSeconds.IsEnabled = !value;
        }
        private void CheckBox_DelayCapture_Checked(object sender, RoutedEventArgs e)
        {
            var value = CheckboxValue(e);
            _settings.DelayCapture = value;
            _settings.Save();
        }

        private void CheckBox_SubmitToSteam_Checked(object sender, RoutedEventArgs e)
        {
            _settings.SubmitToSteam = CheckboxValue(e);
            _settings.Save();
        }

        private void Button_BrowseDirectory_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new FolderBrowserDialog();
            DialogResult result = dialog.ShowDialog();
            if(result == System.Windows.Forms.DialogResult.OK)
            {
                _settings.Directory = dialog.SelectedPath;
                _settings.Save();
                _controller.UpdateOutputFolder();
                LabelDirectory.Content = _settings.Directory;
                LabelDirectory.ToolTip = _settings.Directory;
            }
        }

        private void CheckBox_Notifications_Checked(object sender, RoutedEventArgs e)
        {
            _settings.Notifications = CheckboxValue(e);
            _settings.Save();
        }

        private void CheckBox_Thumbnail_Checked(object sender, RoutedEventArgs e)
        {
            _settings.Thumbnail = CheckboxValue(e);
            _settings.Save();
        }

        private void CheckBox_Audio_Checked(object sender, RoutedEventArgs e)
        {
            _settings.Audio = CheckboxValue(e);
            _settings.Save();
        }

        private void CheckBox_ReplaceShortcut_Checked(object sender, RoutedEventArgs e)
        {
            var value = CheckboxValue(e);
            _settings.ReplaceShortcut = value;
            _settings.Save();
            ButtonRehookShortcut.IsEnabled = value;
            if (value)
            {
                _controller.UpdateScreenshotHook();
            } else {
                var result = MessageBox.Show("You need to restart this application to restore original screenshot functionality, do it now?", Properties.Resources.AppName, MessageBoxButton.YesNo, MessageBoxImage.Exclamation);
                if(result == MessageBoxResult.Yes)
                {
                    ExitApplication();
                    // TODO: Should also relaunch it. Maybe launch an invisible command prompt to execute the application again?
                }
            }
        }

        private void CheckBox_LaunchMinimized_Checked(object sender, RoutedEventArgs e)
        {
            _settings.LaunchMinimized = CheckboxValue(e);
            _settings.Save();
        }

        private void CheckBox_Tray_Checked(object sender, RoutedEventArgs e)
        {
            _settings.Tray = CheckboxValue(e);
            _settings.Save();
        }

        private void ClickedUrl(object sender, RoutedEventArgs e)
        {
            var link = (Hyperlink)sender;
            MiscUtils.OpenUrl(link.NavigateUri.ToString());
        }

        private void TextBox_TimerSeconds_LostFocus(object sender, RoutedEventArgs e)
        {
            int.TryParse(TextBoxTimerSeconds.Text, out int result);
            if (result < 0) result *= -1;
            _settings.TimerSeconds = result;
            TextBoxTimerSeconds.Text = result.ToString();
            _settings.Save();
        }

        private void TextBox_Delay_LostFocus(object sender, RoutedEventArgs e)
        {
            int.TryParse(TextBoxDelaySeconds.Text, out int result);
            if (result < 0) result *= -1;
            _settings.DelaySeconds = result;
            TextBoxDelaySeconds.Text = result.ToString();
            _settings.Save();
        }

        private void Slider_OverlayDistance_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            var value = Math.Pow(e.NewValue, 3)/10000;
            string valueStr;
            if (value < 2) valueStr = string.Format("{0:0.00}", value);
            else if (value < 10) valueStr = string.Format("{0:0.0}", value);
            else valueStr = string.Format("{0:0}", value);
            LabelOverlayDistance.Content = $"{valueStr}m";
            if(_settingsLoaded)
            {
                _settings.OverlayDistanceGui = (float) e.NewValue;
                _settings.OverlayDistance = (float) value;
                _settings.Save();
            }
        }

        private void Slider_OverlayOpacity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            LabelOverlayOpacity.Content = $"{Math.Round(e.NewValue)}%";
            if(_settingsLoaded)
            {
                _settings.OverlayOpacity = (float)e.NewValue;
                _settings.Save();
            }
        }

        private void Slider_ReticleSize_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            LabelReticleSize.Content = $"{Math.Round(e.NewValue)}%";
            if (_settingsLoaded)
            {
                _settings.ReticleSize = (float)e.NewValue;
                _settings.Save();
            }
        }

        private void Button_RehookShortcut_Click(object sender, RoutedEventArgs e)
        {
            _controller.UpdateScreenshotHook(true);
        }

        private void CheckBox_ExitWithSteamVR_Checked(object sender, RoutedEventArgs e)
        {
            _settings.ExitWithSteamVR = CheckboxValue(e);
            _settings.Save();
        }

        private void CheckBox_EnableServer_Checked(object sender, RoutedEventArgs e)
        {
            _settings.EnableServer = CheckboxValue(e);
            _settings.Save();
            _controller.UpdateServer();
        }
        private void CheckBox_AddTag_Checked(object sender, RoutedEventArgs e)
        {
            _settings.AddTag = CheckboxValue(e);
            _settings.Save();
        }
        private void CheckBox_TransmitAll_Checked(object sender, RoutedEventArgs e)
        {
            _settings.TransmitAll = CheckboxValue(e);
            _settings.Save();
        }
        private void Button_SetServerPort_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SingleInputDialog(this, _settings.ServerPort.ToString(), "Server Port");
            var result = dialog.ShowDialog();
            if(result == true)
            {
                if(Int32.TryParse(dialog.Value, out var port))
                {
                    TextBoxServerPort.Text = dialog.Value;
                    if(_settings.ServerPort != port)
                    {
                        _settings.ServerPort = port;
                        _settings.Save();
                        _controller.UpdateServer();
                    }
                }
            }
        }
        private void ComboBox_ResponseResolution_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _settings.ResponseResolution = ComboBoxResponseResolution.SelectedIndex;
            _settings.Save();
        }

        private void CheckBox_SaveRightImage_Checked(object sender, RoutedEventArgs e)
        {
            _settings.SaveRightImage = CheckboxValue(e);
            _settings.Save();
        }

        private void CheckBox_EnableHotkeys_Checked(object sender, RoutedEventArgs e)
        {
            _settings.HotkeysEnabled = CheckboxValue(e);
            _settings.Save();
            UpdateHotkey(HOTKEY_ID_SCREENSHOT);
            UpdateHotkey(HOTKEY_ID_VIEWFINDER);
        }

        private void CheckBox_ScreenshotHotkeyAlt_Checked(object sender, RoutedEventArgs e)
        {
            _settings.HotkeyScreenshotAlt = CheckboxValue(e);
            _settings.Save();
            UpdateHotkey(HOTKEY_ID_SCREENSHOT);
        }

        private void CheckBox_ScreenshotHotkeyControl_Checked(object sender, RoutedEventArgs e)
        {
            _settings.HotkeyScreenshotControl = CheckboxValue(e);
            _settings.Save();
            UpdateHotkey(HOTKEY_ID_SCREENSHOT);
        }

        private void CheckBox_ScreenshotHotkeyShift_Checked(object sender, RoutedEventArgs e)
        {
            _settings.HotkeyScreenshotShift = CheckboxValue(e);
            _settings.Save();
            UpdateHotkey(HOTKEY_ID_SCREENSHOT);
        }

        private void ComboBox_ScreenshotHotkey_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _settings.HotkeyScreenshot = ComboBoxScreenshotHotkey.SelectedIndex;
            _settings.Save();
            UpdateHotkey(HOTKEY_ID_SCREENSHOT);
        }
        public static readonly int[] RES_MAP = {
            128,
            256,
            512,
            1024,
            -1
        };

        private void CheckBox_ViewfinderHotkeyAlt_Checked(object sender, RoutedEventArgs e)
        {
            _settings.HotkeyViewfinderAlt = CheckboxValue(e);
            _settings.Save();
            UpdateHotkey(HOTKEY_ID_VIEWFINDER);
        }

        private void CheckBox_ViewfinderHotkeyControl_Checked(object sender, RoutedEventArgs e)
        {
            _settings.HotkeyViewfinderControl = CheckboxValue(e);
            _settings.Save();
            UpdateHotkey(HOTKEY_ID_VIEWFINDER);
        }
        
        private void CheckBox_ViewfinderHotkeyShift_Checked(object sender, RoutedEventArgs e)
        {
            _settings.HotkeyViewfinderShift = CheckboxValue(e);
            _settings.Save();
            UpdateHotkey(HOTKEY_ID_VIEWFINDER);
        }

        private void ComboBox_ViewfinderHotkey_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _settings.HotkeyViewfinder = ComboBoxViewfinderHotkey.SelectedIndex;
            _settings.Save();
            UpdateHotkey(HOTKEY_ID_VIEWFINDER);
        }

        // HotKeys https://stackoverflow.com/a/11378213/2076423
        private readonly int[] KEY_MAP = { // https://stackoverflow.com/a/1153059
            0,
            KeyInterop.VirtualKeyFromKey(Key.F1),
            KeyInterop.VirtualKeyFromKey(Key.F2),
            KeyInterop.VirtualKeyFromKey(Key.F3),
            KeyInterop.VirtualKeyFromKey(Key.F4),
            KeyInterop.VirtualKeyFromKey(Key.F5),
            KeyInterop.VirtualKeyFromKey(Key.F6),
            KeyInterop.VirtualKeyFromKey(Key.F7),
            KeyInterop.VirtualKeyFromKey(Key.F8),
            KeyInterop.VirtualKeyFromKey(Key.F9),
            KeyInterop.VirtualKeyFromKey(Key.F10),
            KeyInterop.VirtualKeyFromKey(Key.F11),
            KeyInterop.VirtualKeyFromKey(Key.F12),
            KeyInterop.VirtualKeyFromKey(Key.F13),
            KeyInterop.VirtualKeyFromKey(Key.F14),
            KeyInterop.VirtualKeyFromKey(Key.F15)
        };

        private void UpdateHotkey(int ID) {
            if (!_settingsLoaded) return;
            var unregistered = UnregisterHotKey(ID);
            Debug.WriteLine($"Unregistered key: {ID} ({unregistered})");
            var keyIndex = 0;
            var modifiers = 0;
            switch (ID) {
                case HOTKEY_ID_SCREENSHOT:
                    keyIndex = _settings.HotkeyScreenshot;
                    modifiers = GetModifiers(_settings.HotkeyScreenshotAlt, _settings.HotkeyScreenshotControl, _settings.HotkeyScreenshotShift);
                    break;
                case HOTKEY_ID_VIEWFINDER:
                    keyIndex = _settings.HotkeyViewfinder;
                    modifiers = GetModifiers(_settings.HotkeyViewfinderAlt, _settings.HotkeyViewfinderControl, _settings.HotkeyViewfinderShift);
                    break;
            }

            int key = KEY_MAP[keyIndex];
            if (_settings.HotkeysEnabled && key != 0)
            {
                var registered = RegisterHotKey(ID, key, modifiers);
                Debug.WriteLine($"  Registered key: {ID} ({registered}), {key}, {modifiers}");
                if(!registered) MessageBox.Show($"Could not register this global hotkey ({key}+{modifiers}), some other application might already be using it.");
            }

            int GetModifiers(bool alt, bool control, bool shift) {
                // https://stackoverflow.com/a/32179433/2076423
                int mod = 0;
                if (alt) mod |= 1;
                if (control) mod |= 2;
                if (shift) mod |= 4;
                return mod;
            }
        }
    }
}
