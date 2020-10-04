using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using BOLL7708;
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
        private NotifyIcon _notifyIcon;
        private static Mutex _mutex = null;
        private bool _settingsLoaded = false;

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
            CheckBox_SubmitToSteam.IsChecked = _settings.SubmitToSteam;
            Label_Directory.Content = _settings.Directory;
            Label_Directory.ToolTip = _settings.Directory;

            CheckBox_Notifications.IsChecked = _settings.Notifications;
            CheckBox_Thumbnail.IsChecked = _settings.Thumbnail;
            CheckBox_Audio.IsChecked = _settings.Audio;

            CheckBox_ReplaceShortcut.IsChecked = _settings.ReplaceShortcut;
            Button_RehookShortcut.IsEnabled = _settings.ReplaceShortcut;
            CheckBox_LaunchMinimized.IsChecked = _settings.LaunchMinimized;
            CheckBox_Tray.IsChecked = _settings.Tray;
            CheckBox_ExitWithSteamVR.IsChecked = _settings.ExitWithSteamVR;

            Slider_OverlayDistance.Value = _settings.OverlayDistanceGui;
            Slider_OverlayOpacity.Value = _settings.OverlayOpacity;
            Slider_ReticleSize.Value = _settings.ReticleSize;
            _settingsLoaded = true;
        }

        private void ExitApplication() {
            if (_notifyIcon != null) _notifyIcon.Dispose();
            System.Windows.Application.Current.Shutdown();
        }
        
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
                    // TODO: Should also relaunch it.
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
            _settings.TimerSeconds = result;
            TextBox_TimerSeconds.Text = result.ToString();
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

        private void CheckBox_SaveRightImage_Checked(object sender, RoutedEventArgs e)
        {
            _settings.SaveRightImage = CheckboxValue(e);
            _settings.Save();
        }
    }
}
