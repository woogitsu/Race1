using System;
using Race1.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Race1.Game
{
    public enum GameMode
    {
        EndlessDrive = 0,
        FreeRide = 1,
        TimeAttack = 2,
        TrafficChallenge = 3,
        AiRace = 4,
    }

    public enum MenuAction
    {
        StartRun,
        Continue,
        Resume,
        Restart,
        MainMenu,
        Photo,
        PhotoBack,
        PhotoCloser,
        PhotoFarther,
        PhotoTele,
        PhotoWide,
        PhotoShoot,
    }

    /// <summary>Choices made on the menu, carried into the run.</summary>
    public sealed class MenuOptions
    {
        public GameMode Mode = GameMode.EndlessDrive;
        public int CarIndex;
        public int PaintIndex;
        public int WeatherIndex;
        public int HourIndex = 3;     // index into Hours
        public int AssistIndex = 1;   // 0 arcade, 1 sport, 2 simulation
        public bool TrafficOn = true;

        public static readonly string[] Weather = { "Clear", "Cloudy", "Rain", "Storm", "Fog", "Snow" };
        public static readonly float[] Hours = { 6f, 9f, 12f, 17f, 19.5f, 22f };
        public static readonly string[] Assists = { "Arcade", "Sport", "Simulation" };
        public static readonly string[] Paints = { "Crimson", "Midnight", "Silver", "Azure", "Sand" };
        public static readonly Color[] PaintColours =
        {
            new Color(0.78f, 0.06f, 0.12f), new Color(0.05f, 0.07f, 0.12f), new Color(0.74f, 0.76f, 0.8f),
            new Color(0.1f, 0.42f, 0.78f), new Color(0.76f, 0.66f, 0.45f),
        };
    }

    /// <summary>
    /// Start, pause and garage menus, built in code. Keeps no game state of its own: it reports actions and
    /// option changes to the session.
    /// </summary>
    public sealed class MenuController
    {
        public MenuOptions Options { get; } = new MenuOptions();
        public Action<MenuAction> Chosen;

        /// <summary>Drag across the photo-mode surface, as pixel deltas (x right, y down).</summary>
        public Action<double, double> PhotoDragged;

        /// <summary>Saved tuning of a car, for the garage to show. Null or missing values read as neutral.</summary>
        public Func<string, VehicleTuningData> TuningLookup;

        /// <summary>A tuning slider moved: the knob and its position (0..1).</summary>
        public Action<TuningKnob, double> TuningSlid;

        public Action TyresCycled;
        public Action TuningReset;

        private Canvas _canvas;
        private RectTransform _mainPanel;
        private RectTransform _pausePanel;
        private RectTransform _photoPanel;
        private RectTransform _garagePanel;
        private RectTransform _tuningPanel;
        private Text _tuningCar;
        private Text[] _knobLabels;
        private TuningSlider[] _knobSliders;
        private Text _tyreLabel;
        private Text _photoInfo;
        private Text _photoStatus;
        private Text _carLine;
        private Text _specLine;
        private Text _optionLine;
        private Text _bestLine;
        private Font _font;
        private bool _hasSave;

        public bool IsOpen => _canvas != null && _canvas.enabled;

        public void Build(bool hasSave)
        {
            _hasSave = hasSave;
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var go = new GameObject("Menu", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _canvas = go.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 20;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            var rootRt = go.GetComponent<RectTransform>();

            _mainPanel = Panel(rootRt, new RectN(0.06, 0.08, 0.36, 0.84), new Color(0.04f, 0.05f, 0.07f, 0.78f));
            TextLabel(_mainPanel, new RectN(0.05, 0.02, 0.9, 0.12), "HORIZON DRIVE", 64, TextAnchor.MiddleLeft);
            TextLabel(_mainPanel, new RectN(0.05, 0.12, 0.9, 0.04), "endless asphalt · seeded world · simcade physics", 26, TextAnchor.MiddleLeft);

            float y = 0.2f;
            if (_hasSave) { MenuButton(_mainPanel, new RectN(0.05, y, 0.9, 0.08), "CONTINUE", () => Chosen?.Invoke(MenuAction.Continue)); y += 0.095f; }
            MenuButton(_mainPanel, new RectN(0.05, y, 0.9, 0.08), "ENDLESS DRIVE", () => { Options.Mode = GameMode.EndlessDrive; Chosen?.Invoke(MenuAction.StartRun); }); y += 0.095f;
            MenuButton(_mainPanel, new RectN(0.05, y, 0.9, 0.08), "FREE RIDE", () => { Options.Mode = GameMode.FreeRide; Chosen?.Invoke(MenuAction.StartRun); }); y += 0.095f;
            MenuButton(_mainPanel, new RectN(0.05, y, 0.9, 0.08), "TIME ATTACK", () => { Options.Mode = GameMode.TimeAttack; Chosen?.Invoke(MenuAction.StartRun); }); y += 0.095f;
            MenuButton(_mainPanel, new RectN(0.05, y, 0.9, 0.08), "TRAFFIC CHALLENGE", () => { Options.Mode = GameMode.TrafficChallenge; Chosen?.Invoke(MenuAction.StartRun); }); y += 0.095f;
            MenuButton(_mainPanel, new RectN(0.05, y, 0.9, 0.08), "AI RACE", () => { Options.Mode = GameMode.AiRace; Chosen?.Invoke(MenuAction.StartRun); });

            var garage = Panel(rootRt, new RectN(0.6, 0.08, 0.34, 0.84), new Color(0.04f, 0.05f, 0.07f, 0.6f));
            _garagePanel = garage;
            _carLine = TextLabel(garage, new RectN(0.05, 0.03, 0.9, 0.07), "", 44, TextAnchor.MiddleLeft);
            _specLine = TextLabel(garage, new RectN(0.05, 0.11, 0.9, 0.12), "", 26, TextAnchor.UpperLeft);
            MenuButton(garage, new RectN(0.05, 0.3, 0.42, 0.07), "< CAR", () => { Options.CarIndex = Wrap(Options.CarIndex - 1, VehicleCatalog.Ids.Count); Refresh(); });
            MenuButton(garage, new RectN(0.53, 0.3, 0.42, 0.07), "CAR >", () => { Options.CarIndex = Wrap(Options.CarIndex + 1, VehicleCatalog.Ids.Count); Refresh(); });
            MenuButton(garage, new RectN(0.05, 0.4, 0.42, 0.07), "PAINT", () => { Options.PaintIndex = Wrap(Options.PaintIndex + 1, MenuOptions.Paints.Length); Refresh(); });
            MenuButton(garage, new RectN(0.53, 0.4, 0.42, 0.07), "WEATHER", () => { Options.WeatherIndex = Wrap(Options.WeatherIndex + 1, MenuOptions.Weather.Length); Refresh(); });
            MenuButton(garage, new RectN(0.05, 0.5, 0.42, 0.07), "TIME OF DAY", () => { Options.HourIndex = Wrap(Options.HourIndex + 1, MenuOptions.Hours.Length); Refresh(); });
            MenuButton(garage, new RectN(0.53, 0.5, 0.42, 0.07), "ASSISTS", () => { Options.AssistIndex = Wrap(Options.AssistIndex + 1, MenuOptions.Assists.Length); Refresh(); });
            MenuButton(garage, new RectN(0.05, 0.6, 0.42, 0.07), "TRAFFIC", () => { Options.TrafficOn = !Options.TrafficOn; Refresh(); });
            MenuButton(garage, new RectN(0.53, 0.6, 0.42, 0.07), "TUNING", () => { RefreshTuning(); ShowTuning(); });
            _optionLine = TextLabel(garage, new RectN(0.05, 0.7, 0.9, 0.12), "", 26, TextAnchor.UpperLeft);
            _bestLine = TextLabel(garage, new RectN(0.05, 0.84, 0.9, 0.12), "", 26, TextAnchor.UpperLeft);
            BuildTuningPanel(rootRt);

            _pausePanel = Panel(rootRt, new RectN(0.35, 0.22, 0.3, 0.6), new Color(0.02f, 0.03f, 0.05f, 0.9f));
            TextLabel(_pausePanel, new RectN(0.05, 0.03, 0.9, 0.12), "PAUSED", 56, TextAnchor.MiddleCenter);
            MenuButton(_pausePanel, new RectN(0.1, 0.2, 0.8, 0.12), "RESUME", () => Chosen?.Invoke(MenuAction.Resume));
            MenuButton(_pausePanel, new RectN(0.1, 0.36, 0.8, 0.12), "PHOTO", () => Chosen?.Invoke(MenuAction.Photo));
            MenuButton(_pausePanel, new RectN(0.1, 0.52, 0.8, 0.12), "RESTART", () => Chosen?.Invoke(MenuAction.Restart));
            MenuButton(_pausePanel, new RectN(0.1, 0.68, 0.8, 0.12), "MAIN MENU", () => Chosen?.Invoke(MenuAction.MainMenu));

            // Photo mode: a full-screen drag surface sits under the controls, so drags orbit the camera and taps
            // reach the buttons.
            _photoPanel = Panel(rootRt, new RectN(0, 0, 1, 1), new Color(0f, 0f, 0f, 0f));
            _photoPanel.gameObject.AddComponent<PhotoDragSurface>().Dragged = (dx, dy) => PhotoDragged?.Invoke(dx, dy);
            _photoInfo = TextLabel(_photoPanel, new RectN(0.02, 0.02, 0.72, 0.06), "", 34, TextAnchor.MiddleLeft);
            _photoStatus = TextLabel(_photoPanel, new RectN(0.02, 0.085, 0.72, 0.05), "", 26, TextAnchor.MiddleLeft);
            MenuButton(_photoPanel, new RectN(0.77, 0.02, 0.21, 0.06), "BACK", () => Chosen?.Invoke(MenuAction.PhotoBack));
            MenuButton(_photoPanel, new RectN(0.02, 0.86, 0.18, 0.09), "CLOSER", () => Chosen?.Invoke(MenuAction.PhotoCloser));
            MenuButton(_photoPanel, new RectN(0.21, 0.86, 0.18, 0.09), "FARTHER", () => Chosen?.Invoke(MenuAction.PhotoFarther));
            MenuButton(_photoPanel, new RectN(0.40, 0.86, 0.18, 0.09), "WIDE", () => Chosen?.Invoke(MenuAction.PhotoWide));
            MenuButton(_photoPanel, new RectN(0.59, 0.86, 0.18, 0.09), "TELE", () => Chosen?.Invoke(MenuAction.PhotoTele));
            MenuButton(_photoPanel, new RectN(0.78, 0.86, 0.20, 0.09), "SHOOT", () => Chosen?.Invoke(MenuAction.PhotoShoot));
            _photoPanel.gameObject.SetActive(false);

            Refresh();
            ShowMain();
        }

        public void ShowMain()
        {
            _canvas.enabled = true;
            _mainPanel.gameObject.SetActive(true);
            _pausePanel.gameObject.SetActive(false);
            _photoPanel.gameObject.SetActive(false);
            _garagePanel.gameObject.SetActive(true);
            _tuningPanel.gameObject.SetActive(false);
        }

        public void ShowPause()
        {
            _canvas.enabled = true;
            _mainPanel.gameObject.SetActive(false);
            _pausePanel.gameObject.SetActive(true);
            _photoPanel.gameObject.SetActive(false);
            _garagePanel.gameObject.SetActive(false);
            _tuningPanel.gameObject.SetActive(false);
        }

        public void ShowPhoto()
        {
            _canvas.enabled = true;
            _mainPanel.gameObject.SetActive(false);
            _pausePanel.gameObject.SetActive(false);
            _photoPanel.gameObject.SetActive(true);
            _garagePanel.gameObject.SetActive(false);
            _tuningPanel.gameObject.SetActive(false);
        }

        /// <summary>Hides the photo controls while a shot is taken, so they stay out of the picture.</summary>
        public void SetPhotoUiVisible(bool visible)
        {
            _photoPanel.gameObject.SetActive(visible);
        }

        /// <summary>Updates the photo-mode lines. A null argument leaves that line unchanged.</summary>
        public void SetPhotoInfo(string info, string status)
        {
            if (info != null) _photoInfo.text = info;
            if (status != null) _photoStatus.text = status;
        }

        public void Hide()
        {
            _canvas.enabled = false;
        }

        public void SetBest(double timeAttackSeconds)
        {
            _bestLine.text = timeAttackSeconds > 0
                ? $"Best time attack: {timeAttackSeconds:F2} s"
                : "Best time attack: none yet";
        }

        public void Refresh()
        {
            string carId = VehicleCatalog.Ids[Options.CarIndex];
            VehicleSpec spec = VehicleTuning.Apply(VehicleCatalog.Create(carId), TuningLookup?.Invoke(carId));
            _carLine.text = spec.DisplayName;
            _specLine.text = $"Mass {spec.Mass:F0} kg\nDrive {spec.Drive}\nGears {spec.GearRatios.Length}\n" +
                             $"Peak torque {MaxOf(spec.TorquePoints):F0} Nm\nGrip {spec.GripDry:F2}";
            _optionLine.text = $"Paint {MenuOptions.Paints[Options.PaintIndex]}\n" +
                               $"Weather {MenuOptions.Weather[Options.WeatherIndex]}\n" +
                               $"Time {MenuOptions.Hours[Options.HourIndex]:F1} h\n" +
                               $"Assists {MenuOptions.Assists[Options.AssistIndex]}\n" +
                               $"Traffic {(Options.TrafficOn ? "on" : "off")}";
            RefreshTuning();
        }

        private static double MaxOf(double[] values)
        {
            double m = double.MinValue;
            foreach (double v in values) m = Math.Max(m, v);
            return m;
        }

        private static int Wrap(int v, int count) => ((v % count) + count) % count;

        /// <summary>
        /// Tuning overlay on top of the garage: a slider per knob, a tyre choice, reset and back. It edits the car
        /// selected in the garage, through <see cref="TuningSlid"/>, <see cref="TyresCycled"/> and <see cref="TuningReset"/>.
        /// </summary>
        private void BuildTuningPanel(RectTransform rootRt)
        {
            _tuningPanel = Panel(rootRt, new RectN(0.6, 0.08, 0.34, 0.84), new Color(0.04f, 0.05f, 0.07f, 0.97f));
            TextLabel(_tuningPanel, new RectN(0.05, 0.02, 0.9, 0.07), "TUNING", 44, TextAnchor.MiddleLeft);
            _tuningCar = TextLabel(_tuningPanel, new RectN(0.05, 0.09, 0.9, 0.06), "", 28, TextAnchor.MiddleLeft);

            _knobLabels = new Text[TuningControls.KnobCount];
            _knobSliders = new TuningSlider[TuningControls.KnobCount];
            for (int i = 0; i < TuningControls.KnobCount; i++)
            {
                var knob = (TuningKnob)i;
                double y = 0.18 + i * 0.13;
                _knobLabels[i] = TextLabel(_tuningPanel, new RectN(0.05, y, 0.9, 0.05), "", 30, TextAnchor.MiddleLeft);
                RectTransform bar = Panel(_tuningPanel, new RectN(0.05, y + 0.06, 0.9, 0.035), new Color(0.16f, 0.18f, 0.22f, 1f));
                RectTransform fill = Panel(bar, new RectN(0, 0, 1, 1), new Color(0.85f, 0.2f, 0.18f, 1f));
                fill.GetComponent<Image>().raycastTarget = false;
                TuningSlider slider = bar.gameObject.AddComponent<TuningSlider>();
                slider.Init(bar, fill);
                slider.Moved = p => TuningSlid?.Invoke(knob, p);
                _knobSliders[i] = slider;
            }

            _tyreLabel = MenuButton(_tuningPanel, new RectN(0.05, 0.72, 0.9, 0.08), "", () => TyresCycled?.Invoke());
            MenuButton(_tuningPanel, new RectN(0.05, 0.84, 0.43, 0.08), "RESET", () => TuningReset?.Invoke());
            MenuButton(_tuningPanel, new RectN(0.52, 0.84, 0.43, 0.08), "BACK", HideTuning);
            _tuningPanel.gameObject.SetActive(false);
        }

        private void ShowTuning() => _tuningPanel.gameObject.SetActive(true);

        private void HideTuning() => _tuningPanel.gameObject.SetActive(false);

        /// <summary>Shows the saved tuning of the car selected in the garage on the sliders and the tyre line.</summary>
        private void RefreshTuning()
        {
            if (_knobSliders == null) return;
            string id = VehicleCatalog.Ids[Options.CarIndex];
            VehicleTuningData data = TuningLookup?.Invoke(id);
            _tuningCar.text = VehicleCatalog.Create(id).DisplayName;
            for (int i = 0; i < TuningControls.KnobCount; i++)
            {
                var knob = (TuningKnob)i;
                float value = TuningControls.Read(data, knob);
                _knobSliders[i].SetPosition(TuningControls.ToPosition(knob, value));
                _knobLabels[i].text = TuningControls.Label(knob) + "   " + TuningControls.Readout(value);
            }
            _tyreLabel.text = "TYRES: " + TuningControls.TyreName(data == null ? VehicleTuning.TireStandard : (int)Math.Round(data.TireCompound));
        }

        /// <summary>
        /// A horizontal slider: a bar with a fill whose right edge marks the position. The pointer's x on the bar is
        /// the value, reported as 0..1 while the pointer is pressed or dragged.
        /// </summary>
        public sealed class TuningSlider : MonoBehaviour, IPointerDownHandler, IDragHandler
        {
            public Action<double> Moved;

            private RectTransform _bar;
            private RectTransform _fill;

            public void Init(RectTransform bar, RectTransform fill)
            {
                _bar = bar;
                _fill = fill;
            }

            public void SetPosition(double position)
            {
                if (_fill == null) return;
                float p = (float)Math.Max(0.0, Math.Min(1.0, position));
                _fill.anchorMin = Vector2.zero;
                _fill.anchorMax = new Vector2(p, 1f);
                _fill.offsetMin = Vector2.zero;
                _fill.offsetMax = Vector2.zero;
            }

            public void OnPointerDown(PointerEventData e) => Report(e);

            public void OnDrag(PointerEventData e) => Report(e);

            private void Report(PointerEventData e)
            {
                if (_bar == null) return;
                var corners = new Vector3[4];
                _bar.GetWorldCorners(corners);
                float width = corners[2].x - corners[0].x;
                if (!(width > 0f)) return;
                double p = Math.Max(0.0, Math.Min(1.0, (e.position.x - corners[0].x) / width));
                SetPosition(p);
                Moved?.Invoke(p);
            }
        }

        /// <summary>Full-screen drag area for photo mode. Reports each drag step as a pixel delta.</summary>
        public sealed class PhotoDragSurface : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
        {
            public Action<double, double> Dragged;

            private Vector2 _last;
            private bool _active;

            public void OnPointerDown(PointerEventData e)
            {
                _last = e.position;
                _active = true;
            }

            public void OnDrag(PointerEventData e)
            {
                if (!_active) return;
                Vector2 now = e.position;
                Dragged?.Invoke(now.x - _last.x, now.y - _last.y);
                _last = now;
            }

            public void OnPointerUp(PointerEventData e) => _active = false;
        }

        private RectTransform Panel(RectTransform parent, RectN r, Color colour)
        {
            var go = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = colour;
            OnScreenControls.Place(go.GetComponent<RectTransform>(), r);
            return go.GetComponent<RectTransform>();
        }

        private Text TextLabel(RectTransform parent, RectN r, string content, int size, TextAnchor anchor)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = _font;
            t.text = content;
            t.fontSize = size;
            t.alignment = anchor;
            t.color = new Color(0.94f, 0.95f, 0.97f, 1f);
            OnScreenControls.Place(go.GetComponent<RectTransform>(), r);
            return t;
        }

        private Text MenuButton(RectTransform parent, RectN r, string label, Action onClick)
        {
            var go = new GameObject("Button_" + label, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = new Color(0.16f, 0.18f, 0.22f, 0.95f);
            go.GetComponent<Button>().onClick.AddListener(() => onClick());
            OnScreenControls.Place(go.GetComponent<RectTransform>(), r);
            return TextLabel(go.GetComponent<RectTransform>(), new RectN(0, 0, 1, 1), label, 34, TextAnchor.MiddleCenter);
        }
    }
}
