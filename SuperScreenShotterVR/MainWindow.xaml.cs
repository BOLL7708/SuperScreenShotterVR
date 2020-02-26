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
using Valve.VR;

namespace SuperScreenShotterVR
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private EasyOpenVRSingleton _ovr = EasyOpenVRSingleton.Instance;
        private bool _initComplete = false;
        private bool _isHookedForScreenshots = false;
        private string _currentAppId = "";
        private ulong _overlayHandle = 0;
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
                if(!_ovr.IsInitialized())
                {
                    _ovr.SetDebugLogAction((message) => {
                        Debug.WriteLine(message);
                    });
                    _ovr.Init();
                    Thread.Sleep(1000);
                } else
                {
                    if(!_initComplete)
                    {
                        _ovr.SetScreenshotOutputFolder("E:\\Temp\\ScreenshotTest");
                        _isHookedForScreenshots = _ovr.HookScreenshots();
                        _ovr.LoadAppManifest("./app.vrmanifest");
                        _ovr.LoadActionManifest("./actions.json");
                        _ovr.RegisterActionSet("/actions/default");
                        _ovr.RegisterDigitalAction(
                            "/actions/default/in/take_screenshot", 
                            (data, handle) => { if(data.bState) TakeScreenshot(); }
                        );
                        _overlayHandle = _ovr.InitNotificationOverlay("SuperScreenShotterVR");
                        _currentAppId = _ovr.GetRunningApplicationId();
                        _initComplete = true;
                        Debug.WriteLine("Init complete.");
                    } else
                    {
                        _ovr.UpdateActionStates();
                        var events = _ovr.GetNewEvents();
                        foreach(var e in events)
                        {
                            switch((EVREventType) e.eventType) {
                                case EVREventType.VREvent_RequestScreenshot:
                                    Debug.WriteLine("Screenshot requested");
                                    break;
                                case EVREventType.VREvent_ScreenshotTriggered:
                                    Debug.WriteLine("Screenshot triggered");
                                    if(_isHookedForScreenshots ) _ovr.TakeScreenshot(_currentAppId);
                                    break;
                                case EVREventType.VREvent_ScreenshotTaken:
                                    Debug.WriteLine("Screenshot taken, use this for notifications!");
                                    _ovr.EnqueueNotification(_overlayHandle, "Screenshot taken!");
                                    break;
                                case EVREventType.VREvent_ScreenshotFailed:
                                    Debug.WriteLine("Screenshot failed");
                                    break;
                                case EVREventType.VREvent_SceneApplicationChanged:
                                    _currentAppId = _ovr.GetRunningApplicationId();
                                    break;
                            }
                        }
                    }
                }
            }
        }

        private float originalScale = 1;

        private void TakeScreenshot()
        {
            Debug.WriteLine("Taking screenshot!");
            originalScale = _ovr.GetRenderTargetForCurrentApp();
            _ovr.SetRenderScaleForCurrentApp(5f); // Clamped to 500%
            Thread.Sleep(100); // Needs at least 50ms to change render scale before taking screenshot
            var id = _ovr.GetRunningApplicationId();
            if (id != string.Empty) id = id.Split('.').Last();
            _ovr.TakeScreenshot(id);
            _ovr.SetRenderScaleForCurrentApp(originalScale);
            Debug.WriteLine($"Screenshot taken! Original scale: {originalScale}");
        }
    }
}
