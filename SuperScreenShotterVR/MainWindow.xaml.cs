using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using BOLL7708;
using BOLL7708.EasyCSUtils;
using Valve;
using Valve.VR;

namespace SuperScreenShotterVR
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private MainController _controller = new MainController();
        private Properties.Settings _settings = Properties.Settings.Default;
        private System.Windows.Forms.NotifyIcon _notifyIcon;
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
                System.Windows.MessageBox.Show(
                System.Windows.Application.Current.MainWindow,
                "This application is already running!",
                Properties.Resources.AppName,
                MessageBoxButton.OK,
                MessageBoxImage.Information
                );
                System.Windows.Application.Current.Shutdown();
            }

            InitSettings();
            _controller.StatusUpdateAction = (status) =>
            {
                Dispatcher.Invoke(() =>
                {
                    Label_Status.Content = status ? "Connected" : "Disconnected";
                    Label_Status.Background = status ? System.Windows.Media.Brushes.OliveDrab : System.Windows.Media.Brushes.Tomato;
                });
            };
            _controller.AppUpdateAction = (appId) =>
            {
                var appIdFixed = appId.Replace("_", "__"); // Single underscores are interpret to show the next char as shortcut
                Dispatcher.Invoke(() =>
                {
                    Label_AppId.Content = appIdFixed != string.Empty ? appIdFixed : "None";
                    Label_AppId.Background = appIdFixed != string.Empty ? System.Windows.Media.Brushes.OliveDrab : System.Windows.Media.Brushes.Gray;
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

            var icon = Properties.Resources.app_logo as System.Drawing.Icon;
            _notifyIcon = new System.Windows.Forms.NotifyIcon();
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
            Label_Version.Content = $"{Properties.Resources.Version}d";
#else
            Label_Version.Content = Properties.Resources.Version;
#endif
            TextBox_TimerSeconds.IsEnabled = !_settings.CaptureTimer;

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

            CheckBox_ViewFinder.IsChecked = _settings.ViewFinder;
            CheckBox_SaveRightImage.IsChecked = _settings.SaveRightImage;
            CheckBox_CaptureTimer.IsChecked = _settings.CaptureTimer;
            TextBox_TimerSeconds.Text = _settings.TimerSeconds.ToString();
            CheckBox_DelayCapture.IsChecked = _settings.DelayCapture;
            TextBox_DelaySeconds.Text = _settings.DelaySeconds.ToString();
            CheckBox_SubmitToSteam.IsChecked = _settings.SubmitToSteam;
            Label_Directory.Content = _settings.Directory;
            Label_Directory.ToolTip = _settings.Directory;

            CheckBox_EnableHotkeys.IsChecked = _settings.HotkeysEnabled;
            CheckBox_ScreenshotHotkeyAlt.IsChecked = _settings.HotkeyScreenshotAlt;
            CheckBox_ScreenshotHotkeyControl.IsChecked = _settings.HotkeyScreenshotControl;
            CheckBox_ScreenshotHotkeyShift.IsChecked = _settings.HotkeyScreenshotShift;
            CheckBox_ViewfinderHotkeyAlt.IsChecked = _settings.HotkeyViewfinderAlt;
            CheckBox_ViewfinderHotkeyControl.IsChecked = _settings.HotkeyViewfinderControl;
            CheckBox_ViewfinderHotkeyShift.IsChecked = _settings.HotkeyViewfinderShift;
            ComboBox_ScreenshotHotkey.SelectedIndex = _settings.HotkeyScreenshot;
            ComboBox_ViewfinderHotkey.SelectedIndex = _settings.HotkeyViewfinder;

            CheckBox_Notifications.IsChecked = _settings.Notifications;
            CheckBox_Thumbnail.IsChecked = _settings.Thumbnail;
            CheckBox_Audio.IsChecked = _settings.Audio;

            CheckBox_ReplaceShortcut.IsChecked = _settings.ReplaceShortcut;
            Button_RehookShortcut.IsEnabled = _settings.ReplaceShortcut;
            CheckBox_LaunchMinimized.IsChecked = _settings.LaunchMinimized;
            CheckBox_Tray.IsChecked = _settings.Tray;
            CheckBox_ExitWithSteamVR.IsChecked = _settings.ExitWithSteamVR;
            CheckBox_EnableServer.IsChecked = _settings.EnableServer;
            CheckBox_AddTag.IsChecked = _settings.AddTag;
            CheckBox_TransmitAll.IsChecked = _settings.TransmitAll;
            TextBox_ServerPort.Text = _settings.ServerPort.ToString();
            ComboBox_ResponseResolution.SelectedIndex = _settings.ResponseResolution;

            Slider_OverlayDistance.Value = _settings.OverlayDistanceGui;
            Slider_OverlayOpacity.Value = _settings.OverlayOpacity;
            Slider_ReticleSize.Value = _settings.ReticleSize;
            _settingsLoaded = true;

            Debug.WriteLine("Settings initiated");
        }

        private void ExitApplication() {
            if (_notifyIcon != null) _notifyIcon.Dispose();
            System.Windows.Application.Current.Shutdown();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var helper = new WindowInteropHelper(this);
            _source = HwndSource.FromHwnd(helper.Handle);
            _source.AddHook(HwndHook);
            UpdateHotkey(HOTKEY_ID_SCREENSHOT);
            UpdateHotkey(HOTKEY_ID_VIEWFINDER);
        }

        private bool RegisterHotKey(int ID, int key, int modifiers)
        {
            var helper = new WindowInteropHelper(this);
            return RegisterHotKey(helper.Handle, ID, modifiers, key);
        }

        private bool UnregisterHotKey(int ID)
        {
            var helper = new WindowInteropHelper(this);
            return UnregisterHotKey(helper.Handle, ID);
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

        private void CheckBox_CaptureTimer_Checked(object sender, RoutedEventArgs e)
        {
            var value = CheckboxValue(e);
            _settings.CaptureTimer = value;
            _settings.Save();
            TextBox_TimerSeconds.IsEnabled = !value;
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
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            System.Windows.Forms.DialogResult result = dialog.ShowDialog();
            if(result == System.Windows.Forms.DialogResult.OK)
            {
                _settings.Directory = dialog.SelectedPath;
                _settings.Save();
                _controller.UpdateOutputFolder();
                Label_Directory.Content = _settings.Directory;
                Label_Directory.ToolTip = _settings.Directory;
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
            Button_RehookShortcut.IsEnabled = value;
            if (value)
            {
                _controller.UpdateScreenshotHook();
            } else {
                var result = System.Windows.MessageBox.Show("You need to restart this application to restore original screenshot functionality, do it now?", Properties.Resources.AppName, MessageBoxButton.YesNo, MessageBoxImage.Exclamation);
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

        private void ClickedURL(object sender, RoutedEventArgs e)
        {
            var link = (Hyperlink)sender;
            Process.Start(link.NavigateUri.ToString());
        }

        private void TextBox_TimerSeconds_LostFocus(object sender, RoutedEventArgs e)
        {
            int.TryParse(TextBox_TimerSeconds.Text, out int result);
            if (result < 0) result *= -1;
            _settings.TimerSeconds = result;
            TextBox_TimerSeconds.Text = result.ToString();
            _settings.Save();
        }

        private void TextBox_Delay_LostFocus(object sender, RoutedEventArgs e)
        {
            int.TryParse(TextBox_DelaySeconds.Text, out int result);
            if (result < 0) result *= -1;
            _settings.DelaySeconds = result;
            TextBox_DelaySeconds.Text = result.ToString();
            _settings.Save();
        }

        private void Slider_OverlayDistance_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            var value = Math.Pow(e.NewValue, 3)/10000;
            string valueStr;
            if (value < 2) valueStr = string.Format("{0:0.00}", value);
            else if (value < 10) valueStr = string.Format("{0:0.0}", value);
            else valueStr = string.Format("{0:0}", value);
            Label_OverlayDistance.Content = $"{valueStr}m";
            if(_settingsLoaded)
            {
                _settings.OverlayDistanceGui = (float) e.NewValue;
                _settings.OverlayDistance = (float) value;
                _settings.Save();
            }
        }

        private void Slider_OverlayOpacity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            Label_OverlayOpacity.Content = $"{Math.Round(e.NewValue)}%";
            if(_settingsLoaded)
            {
                _settings.OverlayOpacity = (float)e.NewValue;
                _settings.Save();
            }
        }

        private void Slider_ReticleSize_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            Label_ReticleSize.Content = $"{Math.Round(e.NewValue)}%";
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
            var dialog = new InputDialog(this, "Set Server Port", "Server port", _settings.ServerPort.ToString());
            var result = dialog.ShowDialog();
            if(result == true)
            {
                if(Int32.TryParse(dialog.value, out int port))
                {
                    TextBox_ServerPort.Text = dialog.value;
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
            _settings.ResponseResolution = ComboBox_ResponseResolution.SelectedIndex;
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
            _settings.HotkeyScreenshot = ComboBox_ScreenshotHotkey.SelectedIndex;
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
            _settings.HotkeyViewfinder = ComboBox_ViewfinderHotkey.SelectedIndex;
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
