using System;
using System.Collections.Generic;
using System.Diagnostics;
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

namespace SuperScreenShotterVR
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private EasyOpenVRSingleton ovr = EasyOpenVRSingleton.Instance;
        private bool initComplete = false;
        public MainWindow()
        {
            InitializeComponent();
            var workerThread = new Thread(WorkerThread);
            workerThread.Start();
        }

        private void WorkerThread()
        {
            Thread.CurrentThread.IsBackground = true;
            while(true)
            {
                Thread.Sleep(10); // 100 Hz
                if(!ovr.IsInitialized())
                {
                    ovr.Init();
                    Thread.Sleep(1000);
                } else
                {
                    if(!initComplete)
                    {
                        ovr.LoadAppManifest("./app.vrmanifest");
                        ovr.LoadActionManifest("./actions.json");
                        ovr.RegisterActionSet("/actions/default");
                        ovr.RegisterDigitalAction(
                            "/actions/default/in/take_screenshot", 
                            (state) => { if(state) TakeScreenshot(); }
                        );
                        initComplete = true;
                        Debug.WriteLine("Init complete.");
                    } else
                    {
                        ovr.UpdateActionStates();
                    }
                }
            }
        }

        private float originalScale = 1;

        private void TakeScreenshot()
        {
            Debug.WriteLine("Taking screenshot!");
            originalScale = ovr.GetRenderTargetForCurrentApp();
            ovr.SetRenderScaleForCurrentApp(5f); // Clamped to 500%
            Thread.Sleep(100); // Needs at least 50ms to change render scale before taking screenshot
            ovr.TakeScreenshot();
            ovr.SetRenderScaleForCurrentApp(originalScale);
            Debug.WriteLine($"Screenshot taken! Original scale: {originalScale}");
        }
    }
}
