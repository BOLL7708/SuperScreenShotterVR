using System;
using System.Collections.Generic;
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
        public MainWindow()
        {
            InitializeComponent();
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
            _controller.Init();
        }

        private void InitSettings()
        {
            Label_Version.Content = _settings.Version;
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
            CheckBox_SuperSampling.IsChecked = _settings.SuperSampling;
            CheckBox_CaptureTimer.IsChecked = _settings.CaptureTimer;
            TextBox_TimerSeconds.Text = _settings.TimerSeconds.ToString();
            CheckBox_SubmitToSteam.IsChecked = _settings.SubmitToSteam;
            Label_Directory.Content = _settings.Directory;
            Label_Directory.ToolTip = _settings.Directory;

            CheckBox_Notifications.IsChecked = _settings.Notifications;
            CheckBox_Thumbnail.IsChecked = _settings.Thumbnail;
            CheckBox_Audio.IsChecked = _settings.Audio;
            Label_CustomAudio.Content = _settings.CustomAudio;
            Label_CustomAudio.ToolTip = _settings.CustomAudio;

            CheckBox_ReplaceShortcut.IsChecked = _settings.ReplaceShortcut;
            CheckBox_LaunchMinimized.IsChecked = _settings.LaunchMinimized;
            CheckBox_Tray.IsChecked = _settings.Tray;
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

        private void CheckBox_SuperSampling_Checked(object sender, RoutedEventArgs e)
        {
            _settings.SuperSampling = CheckboxValue(e);
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

        private void Button_BrowseAudio_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.OpenFileDialog();
            dialog.Filter = "Waveform Audio File|*.wav";
            System.Windows.Forms.DialogResult result = dialog.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                _settings.CustomAudio = dialog.FileName;
                _settings.Save();
                Label_CustomAudio.Content = _settings.CustomAudio;
                Label_CustomAudio.ToolTip = _settings.CustomAudio;
            }
        }

        private void CheckBox_ReplaceShortcut_Checked(object sender, RoutedEventArgs e)
        {
            var value = CheckboxValue(e);
            _settings.ReplaceShortcut = value;
            _settings.Save();
            if (value)
            {
                _controller.UpdateScreenshotHook();
            } else {
                var result = MessageBox.Show("You need to restart this application to restore original screenshot functionality, do it now?", "SuperScreenShotterVR", MessageBoxButton.YesNo, MessageBoxImage.Exclamation);
                if(result == MessageBoxResult.Yes)
                {
                    Application.Current.Shutdown();
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
    }
}
