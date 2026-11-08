using System;
using System.IO;
using Race1.Core;
using UnityEngine;

namespace Race1.Game
{
    /// <summary>
    /// Composition root and main loop. Creates the world, views and systems at startup, with no scene setup
    /// required. Runs three states: menu (with the garage showroom), driving, and paused. Physics and road
    /// maths come from the core; this class only connects them to the engine.
    /// </summary>
    public sealed class GameSession : MonoBehaviour
    {
        private enum AppState
        {
            Menu = 0,
            Driving = 1,
            Paused = 2,
        }

        private const float AutosaveSeconds = 30f;
        private const double RunStartSpeed = 15.0;
        private const double LatitudeDeg = 47.0;
        private const int DayOfYear = 172;
        private const double TimeAttackLength = 3000.0;
        private const float TimeAttackRestartDelay = 4.0f;

        public static GameSession Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (Instance != null) return;
            new GameObject("Race1").AddComponent<GameSession>();
        }

        private RaceConfig _config;
        private SaveService _saveService;
        private SaveData _save;
        private DevicePlatform _device;
        private PerformanceDirector _perf;
        private MenuController _menu;
        private HudController _hud;
        private PlayerInputReader _input;
        private PlayerInputReader.Settings _inputSettings;
        private CameraDirector _camera;
        private Camera _cam;
        private Light _sun;
        private RoadSettings _roadSettings;
        private BiomeField _biomes;
        private TerrainField _terrain;
        private RoadTrack _road;
        private RoadChunkStreamer _chunks;
        private VegetationStreamer _vegetation;
        private Transform _worldRoot;
        private Transform _showroomRoot;
        private CarView _showroomCar;
        private string _showroomCarId;
        private int _showroomPaint = -1;
        private CarView _playerView;
        private EngineAudio _engineAudio;
        private TrafficView _trafficView;
        private TrafficSimulation _traffic;
        private VehicleDynamics _player;
        private VehicleSpec _spec;
        private WeatherState _weather;
        private TrafficChallengeScorer _scorer;
        private TimeAttackTimer _timer;
        private GameMode _mode;
        private AppState _state = AppState.Menu;
        private Vector3d _origin;
        private double _hour = 12.0;
        private float _autosaveTimer = AutosaveSeconds;
        private float _skyTimer;
        private float _showroomAngle;
        private float _restartTimer = -1f;
        private double _distanceReported;

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Screen.orientation = ScreenOrientation.LandscapeLeft;

            _config = Resources.Load<RaceConfig>("RaceConfig");
            if (_config == null) _config = ScriptableObject.CreateInstance<RaceConfig>();

            _saveService = new SaveService();
            _save = _saveService.Load();
            _device = new DevicePlatform();
            _device.Tick(0f);

            CreateEnvironment();
            CreateWorld();
            CreateViews();

            _menu.Build(File.Exists(_saveService.FilePath));
            _menu.Chosen = OnMenuChoice;
            EnterMenu();
        }

        private void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            _device.Tick(dt);
            if (_device.DisplayChanged && _hud != null)
            {
                _hud.Rebuild(DisplayLayout.Compute(_device.Display));
            }
            _perf.Tick(dt, _device);
            _chunks.ChunksAhead = _perf.ChunksAhead;
            _chunks.BuildsPerFrame = _perf.BuildsPerFrame;

            _skyTimer -= dt;
            if (_skyTimer <= 0f)
            {
                _skyTimer = 1f;
                UpdateSky();
            }

            switch (_state)
            {
                case AppState.Menu:
                    TickMenu(dt);
                    break;
                case AppState.Driving:
                    TickDriving(dt);
                    break;
                case AppState.Paused:
                    // The frozen run stays on screen behind the pause panel; the camera is not moved.
                    break;
            }

            _autosaveTimer -= dt;
            if (_autosaveTimer <= 0f)
            {
                _autosaveTimer = AutosaveSeconds;
                SaveNow();
            }
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                if (_state == AppState.Driving) Pause();
                SaveNow();
            }
        }

        private void OnApplicationQuit()
        {
            SaveNow();
        }

        // ----- Setup -----

        private void CreateEnvironment()
        {
            var camGo = new GameObject("MainCamera");
            camGo.tag = "MainCamera";
            _cam = camGo.AddComponent<Camera>();
            _cam.clearFlags = CameraClearFlags.SolidColor;
            camGo.AddComponent<AudioListener>();

            var sunGo = new GameObject("Sun");
            _sun = sunGo.AddComponent<Light>();
            _sun.type = LightType.Directional;
            _sun.shadows = LightShadows.Soft;
            _sun.shadowStrength = 0.85f;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;

            _worldRoot = new GameObject("World").transform;
            _showroomRoot = new GameObject("Showroom").transform;
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "ShowroomFloor";
            ground.transform.SetParent(_showroomRoot, false);
            ground.transform.localScale = Vector3.one * 2.5f;
            Destroy(ground.GetComponent<Collider>());
            ground.GetComponent<Renderer>().sharedMaterial = RenderMaterials.Create(new Color(0.11f, 0.115f, 0.12f), 0f, 0.3f, null);
        }

        private void CreateWorld()
        {
            ulong seed = 20260101UL;
            if (ulong.TryParse(_save.RouteSeed, out ulong saved)) seed = saved;
            _roadSettings = _config.ToRoadSettings();
            _roadSettings.Seed = seed;

            _biomes = new BiomeField(seed);
            _terrain = new TerrainField(seed, _biomes);
            _road = new RoadTrack(_roadSettings, _terrain, _biomes);

            Material bark = RenderMaterials.Create(new Color(0.32f, 0.22f, 0.14f), 0f, 0.2f, null, true);
            Material needles = RenderMaterials.Create(new Color(0.08f, 0.24f, 0.12f), 0f, 0.3f, null, true);
            Material foliage = RenderMaterials.Create(new Color(0.22f, 0.42f, 0.14f), 0f, 0.3f, null, true);
            Material rock = RenderMaterials.Create(new Color(0.42f, 0.40f, 0.37f), 0f, 0.25f, null, true);
            _vegetation = new VegetationStreamer(seed, bark, needles, foliage, rock);

            Material asphalt = RenderMaterials.Create(Color.white, 0f, 0.35f, ProceduralTextures.Asphalt(seed));
            Material marking = RenderMaterials.Create(new Color(0.92f, 0.92f, 0.9f), 0f, 0.5f, null);
            _chunks = new RoadChunkStreamer(_road, _terrain, _biomes, _worldRoot, asphalt, marking, _vegetation);
            _chunks.BuildsPerFrame = _config.BuildsPerFrame;
        }

        private void CreateViews()
        {
            _hud = new HudController();
            _hud.Build(DisplayLayout.Compute(_device.Display));

            _inputSettings = new PlayerInputReader.Settings();
            _input = new PlayerInputReader(_hud.Controls, _inputSettings);
            _camera = new CameraDirector(_cam);
            _weather = new WeatherState();
            _perf = new PerformanceDirector(QualityTier.High, QualityTier.Ultra);
            _menu = new MenuController();
            _trafficView = new TrafficView(_worldRoot, VehicleCatalog.Create(VehicleCatalog.AuroraVeloce));
        }

        // ----- States -----

        private void EnterMenu()
        {
            _state = AppState.Menu;
            _worldRoot.gameObject.SetActive(false);
            _showroomRoot.gameObject.SetActive(true);
            _hud.SetVisible(false);
            _menu.ShowMain();
            _menu.Refresh();
            _menu.SetBest(_save.Progress.BestTimeAttackSeconds);
            RefreshShowroom();
        }

        private void OnMenuChoice(MenuAction action)
        {
            switch (action)
            {
                case MenuAction.StartRun:
                    StartRun(_menu.Options.Mode, false);
                    break;
                case MenuAction.Continue:
                    StartRun(_menu.Options.Mode, true);
                    break;
                case MenuAction.Resume:
                    Resume();
                    break;
                case MenuAction.Restart:
                    StartRun(_mode, false);
                    break;
                case MenuAction.MainMenu:
                    SaveNow();
                    EnterMenu();
                    break;
            }
        }

        private void StartRun(GameMode mode, bool continueSave)
        {
            MenuOptions opts = _menu.Options;
            _mode = mode;
            _spec = VehicleCatalog.Create(VehicleCatalog.Ids[opts.CarIndex]);
            _save.SelectedVehicleId = _spec.Id;
            _weather.SetKind((WeatherKind)opts.WeatherIndex);
            _hour = MenuOptions.Hours[opts.HourIndex];
            AssistSettings assists = opts.AssistIndex == 0 ? AssistSettings.Arcade
                : opts.AssistIndex == 1 ? AssistSettings.Sport : AssistSettings.Simulation;

            double startS = 0.0;
            double startT = _road.Sample(0).HalfWidth * 0.5;
            if (continueSave && SaveMigrator.TryParseDouble(_save.RouteS, out double savedS)) startS = savedS;
            if (continueSave && SaveMigrator.TryParseDouble(_save.RouteT, out double savedT)) startT = savedT;
            double startSpeed = mode == GameMode.TimeAttack ? 0.0 : RunStartSpeed;

            if (_playerView != null) Destroy(_playerView.gameObject);
            _playerView = CarView.Create(_worldRoot, _spec, MenuOptions.PaintColours[opts.PaintIndex], "Player");
            _engineAudio = _playerView.gameObject.AddComponent<EngineAudio>();

            _player = new VehicleDynamics(_spec, _road, assists);
            _player.Reset(startS, startT, startSpeed);

            _traffic = new TrafficSimulation(_road, _roadSettings.Seed, _config.MaxTrafficCars)
            {
                Enabled = opts.TrafficOn && mode != GameMode.TimeAttack,
            };
            _trafficView.Clear();
            _scorer = new TrafficChallengeScorer();
            _timer = new TimeAttackTimer();
            if (mode == GameMode.TimeAttack)
            {
                _timer.Begin(startS, TimeAttackLength, 3, _save.Progress.BestTimeAttackSeconds);
            }

            RoadSample start = _road.Sample(startS);
            _origin = start.PointAt(startT);
            _chunks.Clear();
            // Pre-fill the road around the start so the first frames are not a blank strip.
            for (int i = 0; i < 6; i++) _chunks.Update(startS, _origin);
            _playerView.Place(start, startT, 0.0, _origin, _player.Telemetry, 0f);
            _camera.Snap(_playerView);

            _worldRoot.gameObject.SetActive(true);
            _showroomRoot.gameObject.SetActive(false);
            _menu.Hide();
            _hud.SetVisible(true);
            _restartTimer = -1f;
            _distanceReported = _player.Odometer;
            _state = AppState.Driving;
        }

        private void Pause()
        {
            _state = AppState.Paused;
            _menu.ShowPause();
            _hud.SetVisible(false);
            SaveNow();
        }

        private void Resume()
        {
            _state = AppState.Driving;
            _menu.Hide();
            _hud.SetVisible(true);
        }

        // ----- Loops -----

        private void TickMenu(float dt)
        {
            // Menu choices drive the showroom sky and weather live, so the player sees what they picked.
            MenuOptions opts = _menu.Options;
            _hour = MenuOptions.Hours[opts.HourIndex];
            _weather.SetKind((WeatherKind)opts.WeatherIndex);
            _weather.Update(dt);
            RefreshShowroom();

            _showroomAngle += dt * 18f;
            if (_showroomCar != null)
            {
                _showroomCar.transform.localRotation = Quaternion.Euler(0f, _showroomAngle, 0f);
            }

            // Showroom orbit: the camera circles the car slowly, close enough to read the paint.
            float a = _showroomAngle * Mathf.Deg2Rad * 0.35f;
            _cam.fieldOfView = 36f;
            _cam.transform.position = new Vector3(Mathf.Sin(a) * 5.2f, 1.6f, Mathf.Cos(a) * 5.2f);
            _cam.transform.LookAt(new Vector3(0f, 0.55f, 0f));
        }

        private void TickDriving(float dt)
        {
            PlayerInputReader.Frame frame = _input.Read(dt, true);
            if (frame.PausePressed)
            {
                Pause();
                return;
            }
            if (frame.CameraPressed)
            {
                _camera.Cycle();
            }

            _player.FrictionScale = _weather.FrictionScale;
            _player.Advance(dt, frame.Vehicle);
            _weather.Update(dt);
            VehicleTelemetry tm = _player.Telemetry;
            double s = _player.S;

            _traffic.Update(dt, s, _player.T, tm.SpeedMs);
            foreach (TrafficEvent e in _traffic.Events)
            {
                if (e.Kind == TrafficEventKind.PlayerCollision)
                {
                    _scorer.OnCollision();
                    _hud.Flash();
                }
                else if (e.Kind == TrafficEventKind.NearMiss)
                {
                    _scorer.OnNearMiss(e.LateralClearance);
                }
                else if (e.Kind == TrafficEventKind.Overtake)
                {
                    _scorer.OnOvertake(e.LateralClearance);
                }
            }

            if (_mode == GameMode.TimeAttack && !_timer.Finished)
            {
                _timer.Update(dt, s);
                if (_timer.Finished)
                {
                    if (_timer.BestSeconds > 0) _save.Progress.BestTimeAttackSeconds = (float)_timer.BestSeconds;
                    _menu.SetBest(_timer.BestSeconds);
                    _restartTimer = TimeAttackRestartDelay;
                }
            }
            if (_mode == GameMode.TrafficChallenge)
            {
                _scorer.OnFrame(dt, frame.Vehicle.Steer, tm.SpeedMs * dt);
            }

            // Road and world.
            _chunks.Update(s, _origin);
            _vegetation.Draw(_chunks.Active, _origin);
            _playerView.Place(_road.Sample(s), _player.T, _player.HeadingError, _origin, tm, dt);
            _trafficView.Tick(_traffic.Cars, _road, _origin);

            double throttle = tm.Throttle;
            _engineAudio.Set(tm.EngineRpm, throttle, 0.55);
            _camera.Tick(dt, _playerView, tm, _origin);

            // Floating origin: when the car is far from the origin, move the origin to the car.
            if (Vector3d.Distance(_playerView.WorldPosition, _origin) > 400.0)
            {
                _origin = _playerView.WorldPosition;
                _chunks.Rebase(_origin);
            }

            _save.Progress.TotalDistanceKm += (float)((_player.Odometer - _distanceReported) / 1000.0);
            _distanceReported = _player.Odometer;

            if (_restartTimer >= 0f)
            {
                _restartTimer -= dt;
                if (_restartTimer < 0f) StartRun(GameMode.TimeAttack, false);
            }

            _hud.Tick(dt, BuildHudState(tm));
        }

        private HudState BuildHudState(VehicleTelemetry tm)
        {
            string modeLine;
            switch (_mode)
            {
                case GameMode.TimeAttack:
                    modeLine = $"TIME ATTACK  {_timer.Elapsed:F1} s  best {(_timer.BestSeconds > 0 ? _timer.BestSeconds.ToString("F2") : "-")}";
                    break;
                case GameMode.TrafficChallenge:
                    modeLine = $"TRAFFIC CHALLENGE  {_scorer.FinalScore}  x{_scorer.Multiplier}";
                    break;
                case GameMode.FreeRide:
                    modeLine = "FREE RIDE";
                    break;
                default:
                    modeLine = "ENDLESS DRIVE";
                    break;
            }

            string status = tm.Damage > 0.5 ? $"damage {tm.Damage:F0}" : string.Empty;
            if (tm.EspActive) status = "stability";
            return new HudState
            {
                SpeedKmh = tm.SpeedKmh,
                Rpm = tm.EngineRpm,
                Gear = tm.Gear,
                DistanceKm = _player.Odometer / 1000.0,
                RedlineRpm = _spec.LimiterRpm,
                Abs = tm.AbsActive,
                Tc = tm.TractionControlActive,
                Esp = tm.EspActive,
                Shifting = tm.ShiftInProgress,
                ModeLine = modeLine,
                WeatherLine = $"{(int)_hour:D2}:00  {_weather.Kind}  grip {_weather.FrictionScale:F2}",
                Status = status,
            };
        }

        // ----- Environment, showroom, save -----

        private void RefreshShowroom()
        {
            MenuOptions opts = _menu.Options;
            string id = VehicleCatalog.Ids[opts.CarIndex];
            if (_showroomCar != null && _showroomCarId == id && _showroomPaint == opts.PaintIndex) return;
            if (_showroomCar != null) Destroy(_showroomCar.gameObject);
            _showroomCar = CarView.Create(_showroomRoot, VehicleCatalog.Create(id), MenuOptions.PaintColours[opts.PaintIndex], "ShowroomCar");
            _showroomCarId = id;
            _showroomPaint = opts.PaintIndex;
            _showroomCar.Place(new RoadSample { S = 0, Heading = 0, Pitch = 0, Bank = 0, Curvature = 0, HalfWidth = 3.5, Shoulder = 1.5, Lanes = 2 },
                0.0, 0.0, Vector3d.Zero, null, 0f);
            _showroomCar.transform.localPosition = Vector3.zero;
        }

        private void UpdateSky()
        {
            SkyModel.SolarPosition(LatitudeDeg, DayOfYear, _hour, out double elev, out double az);
            Vector3 towardsSun = new Vector3((float)(Math.Sin(az) * Math.Cos(elev)), (float)Math.Sin(elev),
                (float)(Math.Cos(az) * Math.Cos(elev)));
            _sun.transform.rotation = Quaternion.LookRotation(-towardsSun);

            double cloud = _weather.Cloud;
            double day = SkyModel.SunIntensity(elev) * (1.0 - 0.55 * cloud);
            _sun.intensity = (float)(0.12 + 1.15 * day);
            double golden = SkyModel.GoldenHourFactor(elev);
            _sun.color = Color.Lerp(new Color(1f, 0.97f, 0.92f), new Color(1f, 0.6f, 0.36f), (float)golden);

            double night = SkyModel.NightFactor(elev);
            Color dayCol = new Color(0.52f, 0.70f, 0.92f);
            Color nightCol = new Color(0.02f, 0.03f, 0.07f);
            Color sky = Color.Lerp(dayCol, nightCol, (float)night);
            sky = Color.Lerp(sky, new Color(0.95f, 0.55f, 0.35f), (float)(golden * 0.6));
            sky = Color.Lerp(sky, new Color(0.55f, 0.57f, 0.6f), (float)Mathx.Clamp01(_weather.Rain * 0.5 + _weather.Fog * 0.8));
            _cam.backgroundColor = sky;
            RenderSettings.fogColor = sky;
            RenderSettings.fogDensity = (float)(0.00035 + 0.0012 * _weather.Fog + 0.0006 * _weather.Rain);
            RenderSettings.ambientLight = Color.Lerp(new Color(0.05f, 0.06f, 0.09f), new Color(0.42f, 0.45f, 0.5f), (float)(1.0 - night));
        }

        private void SaveNow()
        {
            if (_save == null) return;
            if (_player != null && _state != AppState.Menu)
            {
                _save.RouteS = SaveMigrator.RoundTrip(_player.S);
                _save.RouteT = SaveMigrator.RoundTrip(_player.T);
            }
            _save.RouteSeed = _roadSettings != null ? _roadSettings.Seed.ToString() : _save.RouteSeed;
            _save.Settings.Weather = _menu != null ? _menu.Options.WeatherIndex : 0;
            _save.Settings.TrafficEnabled = _menu != null && _menu.Options.TrafficOn;
            _save.Settings.DrivingMode = _menu != null ? _menu.Options.AssistIndex : 1;
            _saveService.Save(_save);
        }
    }
}
