using BOLL7708;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Valve.VR;
using static BOLL7708.EasyOpenVRSingleton;
using BOLL7708.EasyCSUtils;
using Newtonsoft.Json;
using SuperScreenShotterVR.Remote;

namespace SuperScreenShotterVR
{
    public class MainController
    {
        private Properties.Settings _settings = Properties.Settings.Default;
        private EasyOpenVRSingleton _ovr = EasyOpenVRSingleton.Instance;
        private Thread _workerThread;
        private bool _initComplete = false;
        private bool _isHookedForScreenshots = false;
        private string _currentAppId = "";
        private ulong _notificationOverlayHandle = 0;
        private Dictionary<uint, ScreenshotData> _screenshotQueue = new Dictionary<uint, ScreenshotData>();
        private uint _lastScreenshotHandle = 0;
        private bool _shouldShutDown = false;
        private MediaPlayer _mediaPlayer;
        private string _currentAudio = string.Empty;
        private Stopwatch _stopWatch = new Stopwatch();

        private const string VIEWFINDER_OVERLAY_UNIQUE_KEY = "boll7708.superscreenshottervr.overlay.viewfinder";
        private const string ROLL_INDICATOR_OVERLAY_UNIQUE_KEY = "boll7708.superscreenshottervr.overlay.rollindicator";
        private const string PITCH_INDICATOR_OVERLAY_UNIQUE_KEY = "boll7708.superscreenshottervr.overlay.pitchindicator";
        private const string RETICLE_OVERLAY_UNIQUE_KEY = "boll7708.superscreenshottervr.overlay.reticle";
        private ulong _viewfinderOverlayHandle = 0;
        private ulong _rollIndicatorOverlayHandle = 0;
        private ulong _pitchIndicatorOverlayHandle = 0;
        private ulong _reticleOverlayHandle = 0;

        private uint _trackedDeviceIndex = 0;
        private OverlayTextureSize _reticleTextureSize = new OverlayTextureSize();
        private float _displayFrequency = 90f;
        private bool _overlayIsVisible = false;
        private float _screenshotFoV = 0;

        private SuperServer _server = new SuperServer();

        // Actions
        public Action<bool> StatusUpdateAction { get; set; } = (status) => { Debug.WriteLine("No status action set."); };
        public Action<string> AppUpdateAction { get; set; } = (appId) => { Debug.WriteLine("No appID action set."); };
        public Action ExitAction { get; set; } = () => { Debug.WriteLine("No exit action set."); };

        public void Init()
        {
            StatusUpdateAction.Invoke(false);
            AppUpdateAction.Invoke("");

            _mediaPlayer = new MediaPlayer();

            _workerThread = new Thread(WorkerThread);
            _workerThread.Start();

            if(_settings.EnableServer && _settings.ServerPort != 0)
            {
                _server.StartOrRestart(_settings.ServerPort);
            }
            _server.MessageReceievedAction = (session, message) =>
            {
                var msg = new Remote.ScreenshotMessage();
                try
                {
                    msg = JsonConvert.DeserializeObject<Remote.ScreenshotMessage>(message);
                } catch(JsonReaderException e)
                {
                    Debug.WriteLine(e.Message);
                    _server.SendMessage(session, "Could not parse JSON");
                }
                if (_initComplete && !OpenVR.Overlay.IsDashboardVisible())
                {
                    if(msg.nonce != string.Empty)
                    {
                        msg.session = session;
                        if (msg.delay > 0) TakeDelayedScreenshot(true, msg); else TakeScreenshot(true, msg); // byUser is true as this should show viewfinder etc.
                    }
                }
            };
            _server.StatusMessageAction = (session, connected, status) =>
            {
                Debug.WriteLine($"Session: {session}, connected: {connected}, status: {status}");
            };
            _server.StatusAction = (status, count) =>
            {
            };
        }

        public void SetDebugLogAction(Action<string> action)
        {
            _ovr.SetDebugLogAction(action);
        }

        private void WorkerThread()
        {
            Thread.CurrentThread.IsBackground = true;
            while (true)
            {
                Thread.Sleep(1000/(int)_displayFrequency);
                if (!_ovr.IsInitialized())
                {
                    _ovr.Init();
                    StatusUpdateAction(_ovr.IsInitialized());
                    Thread.Sleep(1000);
                }
                else
                {
                    if (!_initComplete)
                    { // Initialization
                        _initComplete = true;

                        // Screenshots
                        UpdateScreenshotHook();
                        PlayScreenshotSound(true);
                        _currentAppId = _ovr.GetRunningApplicationId();
                        AppUpdateAction.Invoke(_currentAppId);
                        // ToggleViewfinder(true); // DEBUG
                        UpdateTrackedDeviceIndex();
                        UpdateDisplayFrequency();

                        // App
                        _ovr.AddApplicationManifest("./app.vrmanifest", "boll7708.superscreenshottervr", true);

                        // Input
                        _ovr.LoadActionManifest("./actions.json");
                        _ovr.RegisterActionSet("/actions/screenshots");

                        Action<InputDigitalActionData_t, InputActionInfo> takeScreenshotAction = (data, info) => 
                        {
                            var ok = data.bState && !OpenVR.Overlay.IsDashboardVisible();
                            if (_settings.DelayCapture) TakeDelayedScreenshot(ok); else if (ok) TakeScreenshot();
                        };
                        Action<InputDigitalActionData_t, InputActionInfo> showViewfinderAction = (data, info) =>
                        {
                            var ok = data.bState && !OpenVR.Overlay.IsDashboardVisible();
                            ToggleViewfinder(ok);
                        };
                        Action<InputDigitalActionData_t, InputActionInfo> takeDelayedScreenshotAction = (data, info) =>
                        {
                            var ok = data.bState && !OpenVR.Overlay.IsDashboardVisible();
                            TakeDelayedScreenshot(ok);
                        };
                        _ovr.RegisterDigitalAction("/actions/screenshots/in/take_screenshot", takeScreenshotAction);
                        _ovr.RegisterDigitalAction("/actions/screenshots/in/show_viewfinder", showViewfinderAction);
                        _ovr.RegisterDigitalAction("/actions/screenshots/in/take_delayed_screenshot", takeDelayedScreenshotAction);
                        _ovr.RegisterDigitalAction("/actions/screenshots/in/take_screenshot_chord", takeScreenshotAction, true);
                        _ovr.RegisterDigitalAction("/actions/screenshots/in/show_viewfinder_chord", showViewfinderAction, true);
                        _ovr.RegisterDigitalAction("/actions/screenshots/in/take_delayed_screenshot_chord", takeDelayedScreenshotAction, true);
                        
                        _notificationOverlayHandle = _ovr.InitNotificationOverlay("SuperScreenShotterVR");
                        _currentAppId = _ovr.GetRunningApplicationId();

                        // Events
                        _ovr.RegisterEvent(EVREventType.VREvent_RequestScreenshot, (data) => { 
                            Debug.WriteLine("OBS! Screenshot request.");
                            // This happens after running TakeScreenshot() with no application running
                            // It leaves us with an error akin to ScreenshotAlreadyInProgress until
                            // we submit an empty result to Steam, we do that in ScreenShotTriggered().
                        });
                        _ovr.RegisterEvent(EVREventType.VREvent_ScreenshotTriggered, (data) => {
                            Debug.WriteLine($"Screenshot triggered, handle: {data.data.screenshot.handle}");
                            if (_isHookedForScreenshots)
                            {
                                if (_settings.DelayCapture) TakeDelayedScreenshot(); else TakeScreenshot();
                            }
                        });
                        _ovr.RegisterEvent(EVREventType.VREvent_ScreenshotTaken, (data) => {
                            Debug.WriteLine($"Screenshot taken, handle: {data.data.screenshot.handle}");
                            ScreenShotTaken(data.data);
                        });
                        _ovr.RegisterEvent(EVREventType.VREvent_ScreenshotFailed, (data) => {
                            _screenshotQueue.Remove(data.data.screenshot.handle);
                            Debug.WriteLine("Screenshot failed");
                        });
                        _ovr.RegisterEvent(EVREventType.VREvent_ScreenshotProgressToDashboard, (data) => {
                            Debug.WriteLine("Screenshot progress to dashboard");
                        });
                        _ovr.RegisterEvent(EVREventType.VREvent_SceneApplicationChanged, (data) => {
                            _currentAppId = _ovr.GetRunningApplicationId();
                            AppUpdateAction.Invoke(_currentAppId);
                            _isHookedForScreenshots = false; // To enable rehooking
                            UpdateScreenshotHook(); // Hook at new application as it seems to occasionally get dropped
                            UpdateOutputFolder();
                            _screenshotQueue.Clear(); // To not have left-overs
                            Debug.WriteLine($"New application running: {_currentAppId}");
                        });
                        _ovr.RegisterEvent(EVREventType.VREvent_Quit, (data) =>
                        {
                            _ovr.AcknowledgeShutdown();
                            _shouldShutDown = true;
                        });
                        _ovr.RegisterEvent(EVREventType.VREvent_TrackedDeviceActivated, (data) =>
                        {
                            UpdateTrackedDeviceIndex();
                        });
                        _ovr.RegisterEvent(EVREventType.VREvent_SteamVRSectionSettingChanged, (data) =>
                        {
                            // This is triggered when someone changes their headset display frequency in SteamVR
                            // (as well as other settings)
                            UpdateDisplayFrequency();
                        });
                        _ovr.RegisterEvent(EVREventType.VREvent_DashboardActivated, (data) =>
                        {
                            ToggleViewfinder(false);
                        });
                        Debug.WriteLine("Init complete.");
                    }
                    else
                    { // Per frame loop
                        _ovr.UpdateActionStates();
                        _ovr.UpdateEvents();

                        if(_settings.CaptureTimer)
                        {
                            if (!_stopWatch.IsRunning) _stopWatch.Start();
                            if(_stopWatch.Elapsed.TotalSeconds >= _settings.TimerSeconds)
                            {
                                TakeScreenshot(false);
                                _stopWatch.Restart();
                            }
                        } else if(_stopWatch.IsRunning)
                        {
                            _stopWatch.Stop();
                        }

                        if(_overlayIsVisible) UpdateOverlays();

                        ShutdownIfWeShould();
                    }
                }
            }
        }

        internal void HotkeyViewFinder(bool visible)
        {
            if (_initComplete) ToggleViewfinder(visible && !OpenVR.Overlay.IsDashboardVisible());
        }

        internal void HotkeyScreenshot()
        {
            if (_initComplete && !OpenVR.Overlay.IsDashboardVisible())
            {
                if (_settings.DelayCapture) TakeDelayedScreenshot(); else TakeScreenshot();
            }
        }

        private void UpdateDisplayFrequency()
        {
            _displayFrequency = _ovr.GetFloatTrackedDeviceProperty(_trackedDeviceIndex, ETrackedDeviceProperty.Prop_DisplayFrequency_Float);
        }

        private void UpdateTrackedDeviceIndex()
        {
            var indexes = _ovr.GetIndexesForTrackedDeviceClass(ETrackedDeviceClass.HMD);
            if (indexes.Length > 0) _trackedDeviceIndex = indexes[0];
            _screenshotFoV = _ovr.GetFloatTrackedDeviceProperty(_trackedDeviceIndex, ETrackedDeviceProperty.Prop_ScreenshotHorizontalFieldOfViewDegrees_Float);
        }

        private void ToggleViewfinder(bool visible)
        {
            _viewfinderOverlayHandle = _ovr.FindOverlay(VIEWFINDER_OVERLAY_UNIQUE_KEY);
            _rollIndicatorOverlayHandle = _ovr.FindOverlay(ROLL_INDICATOR_OVERLAY_UNIQUE_KEY);
            _pitchIndicatorOverlayHandle = _ovr.FindOverlay(PITCH_INDICATOR_OVERLAY_UNIQUE_KEY);
            _reticleOverlayHandle = _ovr.FindOverlay(RETICLE_OVERLAY_UNIQUE_KEY);

            if (_viewfinderOverlayHandle == 0) _viewfinderOverlayHandle = CreateOverlay("viewfinder", VIEWFINDER_OVERLAY_UNIQUE_KEY, "SSSVRVF");
            if (_rollIndicatorOverlayHandle == 0) _rollIndicatorOverlayHandle = CreateOverlay("rollindicator", ROLL_INDICATOR_OVERLAY_UNIQUE_KEY, "SSSVRRI");
            if (_pitchIndicatorOverlayHandle == 0) _pitchIndicatorOverlayHandle = CreateOverlay("pitchindicator", PITCH_INDICATOR_OVERLAY_UNIQUE_KEY, "SSSVRPI");
            if (_reticleOverlayHandle == 0) _reticleOverlayHandle = CreateOverlay("reticle", RETICLE_OVERLAY_UNIQUE_KEY, "SSSVRR");
            UpdateOverlays();

            var shouldBeVisible = visible && _settings.ViewFinder && !_screenshotQueue.ContainsKey(_lastScreenshotHandle); // Screenshot queue meant to prevent it to flicker on after capture
            _ovr.SetOverlayVisibility(_viewfinderOverlayHandle, shouldBeVisible);
            _ovr.SetOverlayVisibility(_rollIndicatorOverlayHandle, shouldBeVisible);
            _ovr.SetOverlayVisibility(_pitchIndicatorOverlayHandle, shouldBeVisible);
            _ovr.SetOverlayVisibility(_reticleOverlayHandle, shouldBeVisible);
            _overlayIsVisible = shouldBeVisible;

            if (shouldBeVisible) UpdateScreenshotHook(true); // Fix: During long sessions the hook can release, re-hooking often has so far not shown to have any adverse effects.
        }

        private ulong CreateOverlay(string imageFileName, string uniqueKey, string title) {
            // Instantiate overlay, width and transform is set in UpdateOverlays()
            ulong handle = _ovr.CreateOverlay(uniqueKey, title, EasyOpenVRSingleton.Utils.GetEmptyTransform(), 1, _trackedDeviceIndex);

            // Apply texture
            var path = $"{Directory.GetCurrentDirectory()}\\resources\\{imageFileName}.png";
            _ovr.SetOverlayTextureFromFile(handle, path);
            return handle;
        }

        private void UpdateOverlays()
        {
            var poses = _ovr.GetDeviceToAbsoluteTrackingPose();
            if(poses.Length > _trackedDeviceIndex && _reticleTextureSize.aspectRatio != 0)
            {
                // From settings and device values
                var alpha = _settings.OverlayOpacity / 100f;
                var distance = _settings.OverlayDistance;
                var fov = _screenshotFoV;
                var width = (float)Math.Tan(fov / 2f * Math.PI / 180) * distance * 2;

                // Pose & orientation
                var pose = poses[_trackedDeviceIndex];
                var hmdTransform = pose.mDeviceToAbsoluteTracking;
                var YPR = new YPR(hmdTransform.EulerAngles());

                // Static overlay
                var overlayTransform = EasyOpenVRSingleton.Utils.GetEmptyTransform().Translate(new HmdVector3_t() { v2 = -distance });

                // Roll indicator
                var rollTransform = overlayTransform.RotateZ(-YPR.roll, false);

                // Pitch indicator
                var reticleSizeFactor = _settings.ReticleSize / 100;
                var limitY = width * reticleSizeFactor / 2 / _reticleTextureSize.aspectRatio;

                var pitchTransform = new HmdMatrix34_t();
                float pitchY = (float)(distance * Math.Tan(-YPR.pitch));
                
                if(true) // TODO: Make this a setting for limiting to reticle bounding box
                {
                    if (pitchY > limitY) pitchY = limitY;
                    if (pitchY < -limitY) pitchY = -limitY;
                }
                if (false) // TODO: Make this a setting for locking pitch indicator to horizon
                {
                    pitchTransform = overlayTransform.Translate(new HmdVector3_t() { v1 = pitchY });
                }
                else
                {                    
                    pitchTransform = rollTransform.Translate(new HmdVector3_t() { v1 = pitchY });
                }

                // Update
                if (_ovr.FindOverlay(VIEWFINDER_OVERLAY_UNIQUE_KEY) != 0)
                {
                    _ovr.SetOverlayWidth(_viewfinderOverlayHandle, width);
                    _ovr.SetOverlayTransform(_viewfinderOverlayHandle, overlayTransform, _trackedDeviceIndex);
                    _ovr.SetOverlayAlpha(_viewfinderOverlayHandle, alpha);
                }
                if (_ovr.FindOverlay(RETICLE_OVERLAY_UNIQUE_KEY) != 0)
                {
                    _ovr.SetOverlayWidth(_reticleOverlayHandle, width * reticleSizeFactor);
                    _ovr.SetOverlayTransform(_reticleOverlayHandle, overlayTransform, _trackedDeviceIndex);
                    _ovr.SetOverlayAlpha(_reticleOverlayHandle, alpha);
                }
                if (_ovr.FindOverlay(PITCH_INDICATOR_OVERLAY_UNIQUE_KEY) != 0)
                {
                    _ovr.SetOverlayWidth(_pitchIndicatorOverlayHandle, width * reticleSizeFactor);
                    _ovr.SetOverlayTransform(_pitchIndicatorOverlayHandle, pitchTransform, _trackedDeviceIndex);
                    _ovr.SetOverlayAlpha(_pitchIndicatorOverlayHandle, alpha);
                }
                if (_ovr.FindOverlay(ROLL_INDICATOR_OVERLAY_UNIQUE_KEY) != 0)
                {
                    _ovr.SetOverlayWidth(_rollIndicatorOverlayHandle, width * reticleSizeFactor);
                    _ovr.SetOverlayTransform(_rollIndicatorOverlayHandle, rollTransform, _trackedDeviceIndex);
                    _ovr.SetOverlayAlpha(_rollIndicatorOverlayHandle, alpha);
                }
            } else
            {
                _reticleTextureSize = _ovr.GetOverlayTextureSize(_reticleOverlayHandle);
            }
        }

        internal void UpdateServer()
        {
            if (_settings.EnableServer) _server.StartOrRestart(_settings.ServerPort);
            else _server.Stop();
        }
        
        public void UpdateScreenshotHook(bool force = false)
        {
            if(_ovr.IsInitialized() && _settings.ReplaceShortcut && (force || !_isHookedForScreenshots))
            {
                _isHookedForScreenshots = _ovr.HookScreenshots();
                Debug.WriteLine($"Hooking for screenshots: {_isHookedForScreenshots}");
            }
        }

        public void UpdateOutputFolder(bool createDirIfNeeded = false, string subfolder = "", string postfix = "")
        {
            if(_settings.Directory != string.Empty)
            {
                var dir = _settings.Directory;
                if (createDirIfNeeded && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                if(_currentAppId != string.Empty)
                {
                    Debug.WriteLine($"Settings subfolder to: {_currentAppId}");
                    dir = $"{dir}\\{_currentAppId}";
                    if (subfolder != string.Empty) dir = $"{dir}\\{subfolder}";
                    if (createDirIfNeeded && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                } else
                {
                    if (subfolder != string.Empty) dir = $"{dir}\\{subfolder}";
                }
                _ovr.SetScreenshotOutputFolder(dir);
            } else
            {
                Debug.WriteLine("No output directory set.");
            }
        }

        private void ShutdownIfWeShould()
        {
            if(_shouldShutDown)
            {
                _shouldShutDown = false;
                _ovr.Shutdown();
                _initComplete = false;
                ExitAction.Invoke();
            }
        }

        private void PlayScreenshotSound(bool onlyLoad = false)
        {
            if(_mediaPlayer == null) _mediaPlayer = new MediaPlayer();
            _mediaPlayer.Dispatcher.Invoke(() => // Always execute tasks on the media player on the thread it was initiated.
            {
                if (_currentAudio != _settings.CustomAudio)
                {
                    _currentAudio = _settings.CustomAudio;
                    if (_currentAudio != string.Empty && File.Exists(_currentAudio))
                    {
                        _mediaPlayer.Open(new Uri(_currentAudio));
                    }
                }
                if (!onlyLoad)
                {
                    _mediaPlayer.Stop();
                    _mediaPlayer.Play();
                }
                return true;
            });
        }

        private class ScreenshotData {
            public ScreenshotResult result;
            public bool byUser;
            public ScreenshotMessage screenshotMessage;
        }

        private string _timerSubfolder = "";
        private uint TakeScreenshot(bool byUser = true, ScreenshotMessage screenshotMessage = null)
        {
            uint resultHandle = 0;
            if (_currentAppId != string.Empty) // There needs to be a running application
            {
                if(_timerSubfolder == string.Empty) _timerSubfolder = DateTime.Now.ToString("yyyyMMdd");
                ToggleViewfinder(false);
                _ovr.SubmitScreenshotToSteam(new ScreenshotResult()); // To make sure we don't have any hanging requests
                var subfolder = byUser ? "" : _timerSubfolder;
                UpdateOutputFolder(true, subfolder); // Output folder
                var tag = _settings.AddTag ? (screenshotMessage?.tag ?? "") : "";
                var success = _ovr.TakeScreenshot(out var result, "", tag); // Capture
                var data = new ScreenshotData {result = result, byUser = byUser, screenshotMessage = screenshotMessage};
                if (result != null)
                {
                    _screenshotQueue.Add(result.handle, data);
                    _lastScreenshotHandle = result.handle;
                    resultHandle = result.handle;
                }
                if (success && byUser)
                {
                    if (_settings.Audio) PlayScreenshotSound(); // Sound effect
                }
                else 
                {
                    Debug.WriteLine("Taking a screenshot failed");
                    _ovr.SubmitScreenshotToSteam(new ScreenshotResult()); // Will fix screenshot in progress limbo when spamming screenshots
                }
            } else Debug.WriteLine("No application is running");
            return resultHandle;
        }

        private void TakeDelayedScreenshot(bool shouldTrigger = true, ScreenshotMessage screenshotMessage = null)
        {
            if(shouldTrigger)
            {
                ToggleViewfinder(true);
                var delay = _settings.DelaySeconds;
                if(screenshotMessage != null && screenshotMessage.delay > 0)
                {
                    delay = screenshotMessage.delay;
                }
                Thread.Sleep(delay * 1000);
                TakeScreenshot(true, screenshotMessage); // byUser set to true as time-lapse does not use delayed shots
            }
        }

        private void ScreenShotTaken(VREvent_Data_t eventData)
        {
            var handle = eventData.screenshot.handle;
            var notificationBitmap = new NotificationBitmap_t();
            if (_screenshotQueue.ContainsKey(handle))
            {
                var data = _screenshotQueue[handle];
                var result = data.result;
                var filePath = $"{result.filePath}.png";
                var filePathVR = $"{result.filePathVR}.png";
                var filePathR = $"{result.filePath}_r.png";
                var rect = new Rectangle();
                if (File.Exists(filePath))
                {
                    rect = GetImageRectangle(filePath);
                    if (_settings.SubmitToSteam && _currentAppId != string.Empty)
                    {
                        var submitted = _ovr.SubmitScreenshotToSteam(result);
                        Debug.WriteLine($"Managed to submit the screenshot to Steam: {submitted}");
                    }
                    else
                    {
                        Debug.WriteLine("Will not submit the screenshot to Steam.");
                    }

                    if(_settings.SaveRightImage && File.Exists(filePathVR))
                    {
                        var bitmapR = GetRightEyeBitmap(rect, filePathVR);
                        SaveBitmapToPngFile(bitmapR, filePathR);
                    }

                    var image = Image.FromFile(filePath);
                    var msg = data.screenshotMessage;
                    if (msg != null || _settings.TransmitAll)
                    {
                        var resIndex = _settings.ResponseResolution;
                        var res = MainWindow.RES_MAP.Length > resIndex ? MainWindow.RES_MAP[resIndex] : 256;
                        if (res < 0) res = image.Width;
                        var bitmap = ResizeImage(image, res, res);
                        SetAlpha(ref bitmap, 255);
                        var imgb64data = GetBase64Bytes(bitmap);
                        if (msg != null)
                        {
                            _server.SendMessage(
                                msg.session,
                                JsonConvert.SerializeObject(new ScreenshotResponse
                                {
                                    nonce = msg.nonce,
                                    image = imgb64data
                                })
                            );
                        }
                        else if(data.byUser)
                        {
                            _server.SendMessageToAll(
                                JsonConvert.SerializeObject(new ScreenshotResponse
                                {
                                    nonce = "",
                                    image = imgb64data
                                })
                            );
                        }
                    }

                    if (_settings.Notifications && _settings.Thumbnail)
                    {
                        var bitmap = ResizeImage(image, 255, 255); // 256x256 should be enough for notification
                        SetAlpha(ref bitmap, 255);
                        notificationBitmap = BitmapUtils.NotificationBitmapFromBitmap(bitmap);
                    } 
                    else 
                    {
                        notificationBitmap = BitmapUtils.NotificationBitmapFromBitmap(Properties.Resources.logo);
                    }
                } 
                else
                {
                    Debug.WriteLine($"Could not find screenshot after taking it: {filePath}");
                }

                if (_settings.Notifications && data.byUser)
                {
                    _ovr.EnqueueNotification(_notificationOverlayHandle, $"Screenshot taken!\n{rect.Width}x{rect.Height}px", notificationBitmap);
                }
                _screenshotQueue.Remove(handle);
            } 
            else
            {
                if (_settings.Notifications)
                {
                    _ovr.EnqueueNotification(_notificationOverlayHandle, "Screenshot taken! (Unknown)", notificationBitmap);
                }
                Debug.WriteLine($"Screenshot handle does not exist in queue? Handle: {handle}");
            }
            Debug.WriteLine($"Screenshot taken done, handle: {eventData.screenshot.handle}");
        }

        // https://stackoverflow.com/q/2419222
        public static void SaveBitmapToPngFile(Bitmap bitmap, String filePath)
        {
            try
            {
                // TODO: This image does not have any (lossless) compression as no encoder options are supported.
                // TODO: Will have to find some library to save a compressed .png to match the SteamVR .png output.
                bitmap.Save(filePath, ImageFormat.Png);
            }
            catch (Exception e)
            {
                Debug.WriteLine($"Failed to save right eye image: {e.Message}");
            }
        }

        // https://stackoverflow.com/a/7939908
        public static Bitmap GetRightEyeBitmap(Rectangle cropRect, String stereoImagePath)
        {
            cropRect.X = cropRect.Width;
            Bitmap bitmap = Image.FromFile(stereoImagePath) as Bitmap;
            Bitmap newBitmap = new Bitmap(cropRect.Width, cropRect.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            using (Graphics gfx = Graphics.FromImage(newBitmap))
            {
                gfx.DrawImage(bitmap, -cropRect.X, -cropRect.Y);
                return newBitmap;
            }
        }

        // https://stackoverflow.com/a/38045852
        public static Rectangle GetImageRectangle(String filePath)
        {
            using (var imageStream = File.OpenRead(filePath))
            {
                var decoder = BitmapDecoder.Create(imageStream, BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.Default);
                var width = decoder.Frames[0].PixelWidth;
                var height = decoder.Frames[0].PixelHeight;
                return new Rectangle(0, 0, width, height);
            }
        }
        

        // https://stackoverflow.com/a/24199315
        public static Bitmap ResizeImage(Image image, int width, int height)
        {
            var destRect = new Rectangle(0, 0, width, height);
            var destImage = new Bitmap(width, height);

            destImage.SetResolution(image.HorizontalResolution, image.VerticalResolution);

            using (var graphics = Graphics.FromImage(destImage))
            {
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                using (var wrapMode = new ImageAttributes())
                {
                    wrapMode.SetWrapMode(WrapMode.TileFlipXY);
                    graphics.DrawImage(image, destRect, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, wrapMode);
                }
            }

            return destImage;
        }

        // https://stackoverflow.com/a/6809677
        public static void SetAlpha(ref Bitmap bmp, byte alpha)
        {
            if (bmp == null) throw new ArgumentNullException("bmp");

            var data = bmp.LockBits(
                new Rectangle(0, 0, bmp.Width, bmp.Height),
                System.Drawing.Imaging.ImageLockMode.ReadWrite,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            var line = data.Scan0;
            var eof = line + data.Height * data.Stride;
            while (line != eof)
            {
                var pixelAlpha = line + 3;
                var eol = pixelAlpha + data.Width * 4;
                while (pixelAlpha != eol)
                {
                    System.Runtime.InteropServices.Marshal.WriteByte(
                        pixelAlpha, alpha);
                    pixelAlpha += 4;
                }
                line += data.Stride;
            }
            bmp.UnlockBits(data);
        }

        // https://stackoverflow.com/a/41578098
        public static string GetBase64Bytes(Bitmap bmp)
        {
            using (var ms = new MemoryStream())
            {
                bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                var b64 = Convert.ToBase64String(ms.GetBuffer());
                return b64;
            }
        }
    }
}
