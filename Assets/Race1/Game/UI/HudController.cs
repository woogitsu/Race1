using Race1.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Race1.Game
{
    /// <summary>Snapshot of the run for the HUD. Filled once per frame by the game session.</summary>
    public struct HudState
    {
        public double SpeedKmh;
        public double Rpm;
        public int Gear;
        public double DistanceKm;
        public double RedlineRpm;
        public bool Abs;
        public bool Tc;
        public bool Esp;
        public bool Shifting;
        public string ModeLine;
        public string WeatherLine;
        public string Status;
    }

    /// <summary>Speed, gear, rev bar, mode line and assist lights. Positioned from the core DisplayLayout result.</summary>
    public sealed class HudController
    {
        private Canvas _canvas;
        private RectTransform _root;
        private Text _speed;
        private Text _gear;
        private Text _distance;
        private Text _mode;
        private Text _weather;
        private Text _status;
        private Text _assists;
        private Image _rpmFill;
        private Image _flash;
        private float _flashTimer;
        private Font _font;

        public OnScreenControls Controls { get; } = new OnScreenControls();

        public void Build(DisplayLayoutResult layout)
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var canvasGo = new GameObject("HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _canvas = canvasGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 10;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            _root = canvasGo.GetComponent<RectTransform>();

            _speed = Label(layout.HudTopLeft, _font, TextAnchor.UpperLeft, 96);
            _gear = Label(new RectN(layout.HudTopLeft.X + layout.HudTopLeft.W * 0.66, layout.HudTopLeft.Y,
                layout.HudTopLeft.W * 0.34, layout.HudTopLeft.H * 0.6), _font, TextAnchor.UpperRight, 110);
            _distance = Label(new RectN(layout.HudTopLeft.X, layout.HudTopLeft.Bottom - 0.07, layout.HudTopLeft.W, 0.06), _font, TextAnchor.UpperLeft, 34);
            _mode = Label(new RectN(layout.HudTopRight.X, layout.HudTopRight.Y, layout.HudTopRight.W, 0.05), _font, TextAnchor.UpperRight, 34);
            _weather = Label(new RectN(layout.HudTopRight.X, layout.HudTopRight.Y + 0.05, layout.HudTopRight.W, 0.05), _font, TextAnchor.UpperRight, 28);
            _status = Label(new RectN(0.3, 0.08, 0.4, 0.06), _font, TextAnchor.LowerCenter, 30);
            _assists = Label(new RectN(0.3, 0.14, 0.4, 0.05), _font, TextAnchor.LowerCenter, 26);

            // Rev bar along the bottom centre, below the status line.
            var barBg = new GameObject("RevBar", typeof(RectTransform), typeof(Image));
            barBg.transform.SetParent(_root, false);
            barBg.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.35f);
            barBg.GetComponent<Image>().raycastTarget = false;
            OnScreenControls.Place(barBg.GetComponent<RectTransform>(), new RectN(0.3, 0.02, 0.4, 0.018));
            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(barBg.transform, false);
            _rpmFill = fillGo.GetComponent<Image>();
            _rpmFill.color = new Color(0.95f, 0.45f, 0.1f, 0.9f);
            _rpmFill.raycastTarget = false;
            _rpmFill.type = Image.Type.Filled;
            _rpmFill.fillMethod = Image.FillMethod.Horizontal;
            var fillRt = fillGo.GetComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;

            // Full-screen flash for collisions.
            var flashGo = new GameObject("Flash", typeof(RectTransform), typeof(Image));
            flashGo.transform.SetParent(_root, false);
            _flash = flashGo.GetComponent<Image>();
            _flash.color = new Color(1f, 0.2f, 0.1f, 0f);
            _flash.raycastTarget = false;
            OnScreenControls.Place(flashGo.GetComponent<RectTransform>(), new RectN(0, 0, 1, 1));

            Controls.Build(_root, layout);
        }

        /// <summary>Rebuilds the HUD for a new screen layout (for example after a fold or unfold).</summary>
        public void Rebuild(DisplayLayoutResult layout)
        {
            if (_canvas != null) Object.Destroy(_canvas.gameObject);
            Build(layout);
        }

        public void SetVisible(bool visible)
        {
            if (_canvas != null) _canvas.enabled = visible;
        }

        public void Flash() => _flashTimer = 0.35f;

        public void Tick(float dt, HudState s)
        {
            if (_canvas == null) return;
            _speed.text = ((int)System.Math.Round(s.SpeedKmh)).ToString();
            _gear.text = s.Gear.ToString();
            _distance.text = $"{s.DistanceKm:F1} km";
            _mode.text = s.ModeLine;
            _weather.text = s.WeatherLine;
            _status.text = s.Status;
            _assists.text = (s.Abs ? "ABS  " : "") + (s.Tc ? "TC  " : "") + (s.Esp ? "ESP  " : "") + (s.Shifting ? "SHIFT" : "");
            float fill = Mathf.Clamp01((float)(s.Rpm / System.Math.Max(1000.0, s.RedlineRpm)));
            _rpmFill.fillAmount = fill;
            _rpmFill.color = fill > 0.92f ? new Color(1f, 0.15f, 0.1f, 0.95f) : new Color(0.95f, 0.45f, 0.1f, 0.9f);

            if (_flashTimer > 0f)
            {
                _flashTimer -= dt;
                _flash.color = new Color(1f, 0.2f, 0.1f, Mathf.Clamp01(_flashTimer / 0.35f) * 0.35f);
            }
        }

        private Text Label(RectN r, Font font, TextAnchor anchor, int size)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(_root, false);
            var t = go.GetComponent<Text>();
            t.font = font;
            t.alignment = anchor;
            t.fontSize = size;
            t.color = new Color(1f, 1f, 1f, 0.92f);
            t.raycastTarget = false;
            OnScreenControls.Place(go.GetComponent<RectTransform>(), r);
            return t;
        }
    }
}
