using BOLL7708;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Valve.VR;
using static BOLL7708.EasyOpenVRSingleton;

namespace SuperScreenShotterVR
{
    public class MainController
    {
        private Properties.Settings _settings = Properties.Settings.Default;
        private EasyOpenVRSingleton _ovr = EasyOpenVRSingleton.Instance;
        private bool _initComplete = false;
        private bool _isHookedForScreenshots = false;
        private string _currentAppId = "";
        private ulong _overlayHandle = 0;
        private ScreenshotResult _lastScreenshotResult = null;
        private bool _isTakingScreenshot = false;
        private const string OUTPUT_PATH = "E:\\Temp\\ScreenshotTest";
        private string _currentOutputPath = OUTPUT_PATH;
        
        public void Init()
        {
            var workerThread = new Thread(WorkerThread);
            workerThread.Start();
        }

        private void WorkerThread()
        {
            Thread.CurrentThread.IsBackground = true;
            while (true)
            {
                Thread.Sleep(10); // 100 Hz
                if (!_ovr.IsInitialized())
                {
                    _ovr.SetDebugLogAction((message) => {
                        Debug.WriteLine(message);
                    });
                    _ovr.Init();
                    Thread.Sleep(1000);
                }
                else
                {
                    if (!_initComplete)
                    {
                        _ovr.SetScreenshotOutputFolder(_currentOutputPath);
                        _isHookedForScreenshots = _ovr.HookScreenshots();
                        _ovr.LoadAppManifest("./app.vrmanifest");
                        _ovr.LoadActionManifest("./actions.json");
                        _ovr.RegisterActionSet("/actions/screenshots");
                        _ovr.RegisterDigitalAction(
                            "/actions/screenshots/in/take_screenshot",
                            (data, handle) => { if (data.bState) TakeScreenshot(); }
                        );
                        _overlayHandle = _ovr.InitNotificationOverlay("SuperScreenShotterVR");
                        _currentAppId = _ovr.GetRunningApplicationId();
                        _initComplete = true;
                        Debug.WriteLine("Init complete.");
                    }
                    else
                    {
                        _ovr.UpdateActionStates();
                        var events = _ovr.GetNewEvents();
                        foreach (var e in events)
                        {
                            switch ((EVREventType)e.eventType)
                            {
                                case EVREventType.VREvent_RequestScreenshot:
                                    Debug.WriteLine("Screenshot requested");
                                    break;
                                case EVREventType.VREvent_ScreenshotTriggered:
                                    Debug.WriteLine("Screenshot triggered");
                                    if (_isHookedForScreenshots && !_isTakingScreenshot) ScreenshotTriggered();
                                    break;
                                case EVREventType.VREvent_ScreenshotTaken:
                                    Debug.WriteLine("Screenshot taken");
                                    ScreenShotTaken();
                                    break;
                                case EVREventType.VREvent_ScreenshotFailed:
                                    Debug.WriteLine("Screenshot failed");
                                    _isTakingScreenshot = false;
                                    break;
                                case EVREventType.VREvent_ScreenshotProgressToDashboard:
                                    Debug.WriteLine("Screenshot progress to dashboard");
                                    break;
                                case EVREventType.VREvent_SceneApplicationChanged:
                                    _currentAppId = _ovr.GetRunningApplicationId();
                                    // if (_currentAppId != string.Empty) _currentOutputPath = $"{OUTPUT_PATH}\\{_currentAppId.Split('.').Last()}";
                                    // Debug.WriteLine($"Current output path: {_currentOutputPath}");
                                    // _ovr.SetScreenshotOutputFolder(_currentOutputPath);
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
            // _ovr.TakeScreenshot(id);
            _ovr.SetRenderScaleForCurrentApp(originalScale);
            Debug.WriteLine($"Screenshot taken! Original scale: {originalScale}");
        }

        private void ScreenshotTriggered()
        {
            _isTakingScreenshot = true;

            var prefix = _currentAppId.StartsWith("steam.app.") ? _currentAppId.Split('.').Last() : string.Empty;
            if(prefix != string.Empty)
            {
                // if (!Directory.Exists(_currentOutputPath)) Directory.CreateDirectory(_currentOutputPath);
                _ovr.TakeScreenshot(out var result, prefix);
                // _ovr.RequestScreenshot(out var result, prefix);
                _lastScreenshotResult = result;
            } else
            {
                Debug.WriteLine("No application is running.");
            }
        }

        private void ScreenShotTaken()
        {
            var notificationBitmap = new NotificationBitmap_t();
            if (_lastScreenshotResult != null)
            {
                var filePath = $"{_lastScreenshotResult.filePath}.png";
                if(File.Exists(filePath))
                {
                    if(_currentAppId.StartsWith("steam.app."))
                    {
                        var submitted = _ovr.SubmitScreenshotToSteam(_lastScreenshotResult);
                        Debug.WriteLine($"Managed to submit screenshot: {submitted}");
                    } else Debug.WriteLine("The running application is not a Steam application.");
                    var image = System.Drawing.Image.FromFile(filePath);
                    var bitmap = new Bitmap(image);
                    notificationBitmap = BitmapUtils.NotificationBitmapFromBitmap(bitmap);
                }
            }
            _ovr.EnqueueNotification(_overlayHandle, "Screenshot taken!", notificationBitmap);
            _isTakingScreenshot = false;
        }
    }
}
