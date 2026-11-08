using Race1.Core;
using UnityEngine;

namespace Race1.Game
{
    /// <summary>
    /// Reads the device state the game reacts to: screen size and safe area (folds, cutouts), refresh
    /// rate, battery, and on Android the thermal status from PowerManager. Values are polled, not assumed,
    /// so folding or unfolding is picked up at runtime.
    /// </summary>
    public sealed class DevicePlatform
    {
        private const float PollInterval = 0.5f;
        private float _timer;
        private int _lastW = -1;
        private int _lastH = -1;
        private int _thermal;
        private float _thermalTimer;

        public DisplayMetrics Display { get; private set; }
        public bool DisplayChanged { get; private set; }
        public int ThermalStatus => _thermal;
        public float BatteryLevel => SystemInfo.batteryLevel;
        public bool Charging => SystemInfo.batteryStatus == BatteryStatus.Charging || SystemInfo.batteryStatus == BatteryStatus.Full;
        public int RefreshRateHz { get; private set; } = 60;

        public void Tick(float dt)
        {
            DisplayChanged = false;
            _timer -= dt;
            if (_timer > 0f) return;
            _timer = PollInterval;

            int w = Screen.width;
            int h = Screen.height;
            if (w != _lastW || h != _lastH)
            {
                _lastW = w;
                _lastH = h;
                Rect safe = Screen.safeArea;
                double sx = w > 0 ? safe.x / w : 0.0;
                double sy = h > 0 ? safe.y / h : 0.0;
                double sw = w > 0 ? safe.width / w : 1.0;
                double sh = h > 0 ? safe.height / h : 1.0;
                Display = new DisplayMetrics
                {
                    WidthPx = w,
                    HeightPx = h,
                    Dpi = Screen.dpi > 0 ? Screen.dpi : 420.0,
                    SafeArea = new RectN(sx, sy, sw, sh),
                };
                DisplayChanged = true;
            }

            try
            {
#if UNITY_6000_0_OR_NEWER
                double hz = Screen.currentResolution.refreshRateRatio.value;
#else
                double hz = Screen.currentResolution.refreshRate;
#endif
                RefreshRateHz = hz > 0 ? (int)System.Math.Round(hz) : 60;
            }
            catch (System.Exception)
            {
                RefreshRateHz = 60;
            }

            _thermalTimer -= PollInterval;
            if (_thermalTimer <= 0f)
            {
                _thermalTimer = 2.0f;
                _thermal = ReadThermalStatus();
            }
        }

        /// <summary>Android PowerManager.getCurrentThermalStatus(): 0 none .. 6 shutdown. 0 elsewhere.</summary>
        private static int ReadThermalStatus()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var power = activity.Call<AndroidJavaObject>("getSystemService", "power"))
                {
                    if (power != null && new AndroidJavaClass("android.os.Build$VERSION").GetStatic<int>("SDK_INT") >= 29)
                    {
                        return power.Call<int>("getCurrentThermalStatus");
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Thermal status unavailable: {e.Message}");
            }
#endif
            return 0;
        }

        public QualitySignals Signals(float frameMs, float gpuMs) => new QualitySignals
        {
            FrameMs = frameMs,
            GpuMs = gpuMs,
            ThermalStatus = _thermal,
            BatteryLevel = BatteryLevel,
            Charging = Charging,
        };
    }
}
