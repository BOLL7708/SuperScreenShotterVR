using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
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
    public partial class MainWindow
    {
        private readonly MainController _controller = new();
        private readonly Settings _settings = Settings.Default;
        private bool _settingsLoaded = false;
        private bool _viewfinderOn = false;

        // For hotkey support
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vlc);
        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        private HwndSource? _source;
        private const int HotkeyIdScreenshot = 1111;
        private const int HotkeyIdViewfinder = 2222;

        public MainWindow()
        {
            InitializeComponent();
            
            // Prevent multiple instances running at once
            WindowUtils.CheckIfAlreadyRunning(Properties.Resources.AppName);
            WindowUtils.CreateTrayIcon(
                this, 
                Properties.Resources.app_logo, 
                Properties.Resources.AppName, 
                Properties.Resources.Version
            );

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

            if (!_settings.LaunchMinimized) return;
            WindowUtils.Minimize(this, !_settings.Tray);
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
            var success1 = UnregisterHotKey(HotkeyIdScreenshot);
            var success2 = UnregisterHotKey(HotkeyIdViewfinder);
            Debug.WriteLine($"Unregistering hotkeys: {success1}, {success2}");

            WindowUtils.DestroyTrayIcon();
            base.OnClosing(e);
        }

        // Need to add this event to the window object
        private void Window_StateChanged(object sender, EventArgs e)
        {
            WindowUtils.OnStateChange(this, !_settings.Tray);
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
            WindowUtils.DestroyTrayIcon();
            Application.Current.Shutdown();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var helper = new WindowInteropHelper(this);
            _source = HwndSource.FromHwnd(helper.Handle);
            _source?.AddHook(HwndHook);
            UpdateHotkey(HotkeyIdScreenshot);
            UpdateHotkey(HotkeyIdViewfinder);
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
            const int wmHotkey = 0x0312;
            switch (msg)
            {
                case wmHotkey:
                    Debug.WriteLine($"Reacting to hotkey {wParam.ToInt32()}!");
                    switch (wParam.ToInt32())
                    {
                        case HotkeyIdScreenshot:
                            _controller.HotkeyScreenshot();
                            _viewfinderOn = false;
                            handled = true;
                            break;
                        case HotkeyIdViewfinder:
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
            int.TryParse(TextBoxTimerSeconds.Text, out var result);
            if (result < 0) result *= -1;
            _settings.TimerSeconds = result;
            TextBoxTimerSeconds.Text = result.ToString();
            _settings.Save();
        }

        private void TextBox_Delay_LostFocus(object sender, RoutedEventArgs e)
        {
            int.TryParse(TextBoxDelaySeconds.Text, out var result);
            if (result < 0) result *= -1;
            _settings.DelaySeconds = result;
            TextBoxDelaySeconds.Text = result.ToString();
            _settings.Save();
        }

        private void Slider_OverlayDistance_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            var value = Math.Pow(e.NewValue, 3)/10000;
            var valueStr = value switch
            {
                < 2 => $"{value:0.00}",
                < 10 => $"{value:0.0}",
                _ => $"{value:0}"
            };
            LabelOverlayDistance.Content = $"{valueStr}m";
            
            if (!_settingsLoaded) return;
            _settings.OverlayDistanceGui = (float) e.NewValue;
            _settings.OverlayDistance = (float) value;
            _settings.Save();
        }

        private void Slider_OverlayOpacity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            LabelOverlayOpacity.Content = $"{Math.Round(e.NewValue)}%";
            
            if (!_settingsLoaded) return;
            _settings.OverlayOpacity = (float)e.NewValue;
            _settings.Save();
        }

        private void Slider_ReticleSize_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            LabelReticleSize.Content = $"{Math.Round(e.NewValue)}%";
            
            if (!_settingsLoaded) return;
            _settings.ReticleSize = (float)e.NewValue;
            _settings.Save();
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
            
            if (result != true) return;
            if (!Int32.TryParse(dialog.Value, out var port)) return;
            TextBoxServerPort.Text = dialog.Value;
            
            if (_settings.ServerPort == port) return;
            _settings.ServerPort = port;
            _settings.Save();
            _controller.UpdateServer();
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
            UpdateHotkey(HotkeyIdScreenshot);
            UpdateHotkey(HotkeyIdViewfinder);
        }

        private void CheckBox_ScreenshotHotkeyAlt_Checked(object sender, RoutedEventArgs e)
        {
            _settings.HotkeyScreenshotAlt = CheckboxValue(e);
            _settings.Save();
            UpdateHotkey(HotkeyIdScreenshot);
        }

        private void CheckBox_ScreenshotHotkeyControl_Checked(object sender, RoutedEventArgs e)
        {
            _settings.HotkeyScreenshotControl = CheckboxValue(e);
            _settings.Save();
            UpdateHotkey(HotkeyIdScreenshot);
        }

        private void CheckBox_ScreenshotHotkeyShift_Checked(object sender, RoutedEventArgs e)
        {
            _settings.HotkeyScreenshotShift = CheckboxValue(e);
            _settings.Save();
            UpdateHotkey(HotkeyIdScreenshot);
        }

        private void ComboBox_ScreenshotHotkey_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _settings.HotkeyScreenshot = ComboBoxScreenshotHotkey.SelectedIndex;
            _settings.Save();
            UpdateHotkey(HotkeyIdScreenshot);
        }
        public static readonly int[] ResMap =
        [
            128,
            256,
            512,
            1024,
            -1,
            0
        ];

        private void CheckBox_ViewfinderHotkeyAlt_Checked(object sender, RoutedEventArgs e)
        {
            _settings.HotkeyViewfinderAlt = CheckboxValue(e);
            _settings.Save();
            UpdateHotkey(HotkeyIdViewfinder);
        }

        private void CheckBox_ViewfinderHotkeyControl_Checked(object sender, RoutedEventArgs e)
        {
            _settings.HotkeyViewfinderControl = CheckboxValue(e);
            _settings.Save();
            UpdateHotkey(HotkeyIdViewfinder);
        }
        
        private void CheckBox_ViewfinderHotkeyShift_Checked(object sender, RoutedEventArgs e)
        {
            _settings.HotkeyViewfinderShift = CheckboxValue(e);
            _settings.Save();
            UpdateHotkey(HotkeyIdViewfinder);
        }

        private void ComboBox_ViewfinderHotkey_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _settings.HotkeyViewfinder = ComboBoxViewfinderHotkey.SelectedIndex;
            _settings.Save();
            UpdateHotkey(HotkeyIdViewfinder);
        }

        // HotKeys https://stackoverflow.com/a/11378213/2076423
        private readonly int[] _keyMap = { // https://stackoverflow.com/a/1153059
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

        private void UpdateHotkey(int id) {
            if (!_settingsLoaded) return;
            var unregistered = UnregisterHotKey(id);
            Debug.WriteLine($"Unregistered key: {id} ({unregistered})");
            var keyIndex = 0;
            var modifiers = 0;
            switch (id) {
                case HotkeyIdScreenshot:
                    keyIndex = _settings.HotkeyScreenshot;
                    modifiers = GetModifiers(_settings.HotkeyScreenshotAlt, _settings.HotkeyScreenshotControl, _settings.HotkeyScreenshotShift);
                    break;
                case HotkeyIdViewfinder:
                    keyIndex = _settings.HotkeyViewfinder;
                    modifiers = GetModifiers(_settings.HotkeyViewfinderAlt, _settings.HotkeyViewfinderControl, _settings.HotkeyViewfinderShift);
                    break;
            }

            var key = _keyMap[keyIndex];
            if (_settings.HotkeysEnabled && key != 0)
            {
                var registered = RegisterHotKey(id, key, modifiers);
                Debug.WriteLine($"  Registered key: {id} ({registered}), {key}, {modifiers}");
                if(!registered) MessageBox.Show($"Could not register this global hotkey ({key}+{modifiers}), some other application might already be using it.");
            }

            return;

            int GetModifiers(bool alt, bool control, bool shift) {
                // https://stackoverflow.com/a/32179433/2076423
                var mod = 0;
                if (alt) mod |= 1;
                if (control) mod |= 2;
                if (shift) mod |= 4;
                return mod;
            }
        }
    }
}
