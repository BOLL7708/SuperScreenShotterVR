using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
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
            _controller.Init();
        }

        private void InitSettings()
        {
            CheckBox_DelayCapture.IsChecked = _settings.DelayCapture;
            TextBox_CaptureDelay.Text = _settings.CaptureDelay.ToString();
            CheckBox_SuperSampling.IsChecked = _settings.SuperSampling;
            CheckBox_CaptureTimer.IsChecked = _settings.CaptureTimer;
            TextBox_TimerSeconds.Text = _settings.TimerSeconds.ToString();
            
            CheckBox_FilenamePrefix.IsChecked = _settings.FilenamePrefix;
            CheckBox_Subfolder.IsChecked = _settings.Subfolder;
            TextBox_Directory.Text = _settings.Directory;

            CheckBox_Notifications.IsChecked = _settings.Notifications;
            CheckBox_Thumbnail.IsChecked = _settings.Thumbnail;
            CheckBox_Audio.IsChecked = _settings.Audio;
            TextBox_CustomAudio.Text = _settings.CustomAudio;

            CheckBox_ReplaceShortcut.IsChecked = _settings.ReplaceShortcut;
            CheckBox_LaunchMinimized.IsChecked = _settings.LaunchMinimized;
            CheckBox_Tray.IsChecked = _settings.Tray;
        }

        private bool CheckboxValue(RoutedEventArgs e)
        {
            var name = e.RoutedEvent.Name;
            return name == "Checked";
        }

        private void CheckBox_DelayCapture_Checked(object sender, RoutedEventArgs e)
        {
            _settings.DelayCapture = CheckboxValue(e);
            _settings.Save();
        }

        private void CheckBox_SuperSampling_Checked(object sender, RoutedEventArgs e)
        {
            _settings.SuperSampling = CheckboxValue(e);
            _settings.Save();
        }

        private void CheckBox_CaptureTimer_Checked(object sender, RoutedEventArgs e)
        {
            _settings.SuperSampling = CheckboxValue(e);
            _settings.Save();
        }

        private void CheckBox_FilenamePrefix_Checked(object sender, RoutedEventArgs e)
        {
            _settings.FilenamePrefix = CheckboxValue(e);
            _settings.Save();
        }

        private void CheckBox_Subfolder_Checked(object sender, RoutedEventArgs e)
        {
            _settings.Subfolder = CheckboxValue(e);
            _settings.Save();
        }

        private void Button_BrowseDirectory_Click(object sender, RoutedEventArgs e)
        {

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

        }

        private void CheckBox_ReplaceShortcut_Checked(object sender, RoutedEventArgs e)
        {
            _settings.ReplaceShortcut = CheckboxValue(e);
            _settings.Save();
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
    }
}
