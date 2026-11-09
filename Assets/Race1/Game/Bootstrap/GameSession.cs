using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Race1.Core;
using UnityEngine;

namespace Race1.Game
{
    /// <summary>
    /// Composition root and main loop. Creates the world, views and systems at startup, with no scene setup
    /// required. Runs three states: menu (with the garage showroom), driving, and paused. Physics and road
    /// maths come from the core; this class connects them to the engine.
    /// </summary>
    public sealed class GameSession : MonoBehaviour
    {
        private enum AppState
        {
            Menu = 0,
            Driving = 1,
            Paused = 2,
            Photo = 3,
        }

        private const float AutosaveSeconds = 30f;
        private const double RunStartSpeed = 15.0;
        private const double LatitudeDeg = 47.0;
        private const int DayOfYear = 172;
        private const double TimeAttackLength = 3000.0;
        private const float TimeAttackRestartDelay = 4.0f;
        private const double RaceStartS = 100.0;           // grid start on the route; rivals sit behind it
        private const double RaceCountdownSeconds = 3.0;
        private const float RaceResultSeconds = 6.0f;      // standings stay on screen before the menu returns
        private const double MaxGridBackM = 96.0;          // rivals never start more than this far behind the start
        private const double Turbidity = 3.0;

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
        private CameraEffects _cameraEffects;
        private Camera _cam;
        private Light _sun;
        private RoadSettings _roadSettings;
        private BiomeField _biomes;
        private TerrainField _terrain;
        private RoadTrack _road;
        private RoadChunkStreamer _chunks;
        private StreetLampLights _lampLights;
        private VegetationStreamer _vegetation;
        private RoadMaterialSet _roadMats;
        private TerrainMaterialSet _terrainMats;
        private Material _railMat;
        private Material _postMat;
        private Material _lampMat;
        private Material _skyMat;
        private WeatherVisuals _weatherVisuals;
        private ReflectionProbeRig _reflectionRig;
        private PostProcessingController _post;
        private Transform _worldRoot;
        private Transform _showroomRoot;
        private CarView _showroomCar;
        private string _showroomCarId;
        private int _showroomPaint = -1;
        private CarView _playerView;
        private EngineAudio _engineAudio;
        private AmbientAudio _ambient;
        private double _snowCover;
        private WindscreenOverlay _windscreen;
        private double _wetness;
        private SkidMarkRenderer _skidMarks;
        private TrafficView _trafficView;
        private TrafficSimulation _traffic;
        private VehicleDynamics _player;
        private VehicleSpec _spec;
        private WeatherState _weather;
        private TrafficChallengeScorer _scorer;
        private TimeAttackTimer _timer;
        private GameMode _mode;
        private RivalRace _rivalRace;
        private double _raceOriginS;
        private float _raceEndTimer = -1f;
        private readonly List<CarView> _rivalViews = new List<CarView>();
        private PhotoMode _photo;
        private int _photoShots;
        private AppState _state = AppState.Menu;
        private Vector3d _origin;
        private double _hour = 12.0;
        private float _night01;
        private float _golden01;
        private float _autosaveTimer = AutosaveSeconds;
        private float _skyTimer;
        private float _showroomAngle;
        private float _restartTimer = -1f;
        private double _distanceReported;
        private readonly double[] _prevCompression = new double[4];

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
            _menu.PhotoDragged = OnPhotoDragged;
            _menu.TuningLookup = id => VehicleTuningStore.Read(_save, id);
            _menu.TuningSlid = OnTuningSlid;
            _menu.TyresCycled = OnTyresCycled;
            _menu.TuningReset = OnTuningReset;
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

            // Snow on the ground follows the falling snow, building and melting at a limited rate.
            double snowNext = SnowCover.Step(_snowCover, SnowCover.Target(_weather.Snow), dt);
            if (System.Math.Abs(snowNext - _snowCover) > 0.001 && _chunks != null)
            {
                _snowCover = snowNext;
                _chunks.SetSnowCover(_snowCover);
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
                case AppState.Photo:
                    TickPhoto(dt);
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
            _cam.clearFlags = CameraClearFlags.Skybox;
            camGo.AddComponent<AudioListener>();

            var sunGo = new GameObject("Sun");
            _sun = sunGo.AddComponent<Light>();
            _sun.type = LightType.Directional;
            _sun.shadows = LightShadows.Soft;
            _sun.shadowStrength = 0.85f;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;

            // Procedural sky: the Race1/Sky shader is driven by the atmosphere model every second.
            Shader sky = Shader.Find("Race1/Sky");
            if (sky != null)
            {
                _skyMat = new Material(sky) { name = "Race1Sky" };
                RenderSettings.skybox = _skyMat;
            }

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

            _roadMats = RoadMaterialFactory.Create(seed);
            _terrainMats = TerrainMaterialFactory.Create(seed);
            _railMat = RenderMaterials.Create(new Color(0.55f, 0.57f, 0.6f), 0.9f, 0.55f, null);
            _postMat = RenderMaterials.Create(new Color(0.25f, 0.26f, 0.28f), 0.6f, 0.4f, null, true);
            _lampMat = RenderMaterials.Emissive(new Color(0.9f, 0.9f, 0.85f), Color.black);
            _lampMat.enableInstancing = true;

            Material bark = RenderMaterials.Create(new Color(0.32f, 0.22f, 0.14f), 0f, 0.2f, null, true);
            Material needles = RenderMaterials.Create(new Color(0.08f, 0.24f, 0.12f), 0f, 0.3f, null, true);
            Material foliage = RenderMaterials.Create(new Color(0.22f, 0.42f, 0.14f), 0f, 0.3f, null, true);
            Material rock = RenderMaterials.Create(new Color(0.42f, 0.40f, 0.37f), 0f, 0.25f, null, true);
            _vegetation = new VegetationStreamer(seed, bark, needles, foliage, rock);

            _chunks = new RoadChunkStreamer(_road, _terrain, _biomes, _worldRoot, _roadMats, _terrainMats,
                _railMat, _postMat, _lampMat, _vegetation, seed);
            _chunks.BuildsPerFrame = _config.BuildsPerFrame;
            _chunks.WaterMaterial = CreateWaterMaterial();
            _chunks.TunnelMaterial = RenderMaterials.Create(new Color(0.42f, 0.41f, 0.39f), 0f, 0.25f, null, false);
            _lampLights = new StreetLampLights(_worldRoot, _config.MaxLitStreetLamps);
        }

        /// <summary>
        /// Sea surface material. Uses the Race1/Water shader when it is in the build. Otherwise falls back to an opaque
        /// blue-green surface, so the sea is still visible.
        /// </summary>
        private static Material CreateWaterMaterial()
        {
            Shader water = Shader.Find("Race1/Water");
            if (water != null) return new Material(water) { name = "CoastWater" };
            return RenderMaterials.Create(new Color(0.05f, 0.2f, 0.28f), 0f, 0.9f, null, true);
        }

        private void CreateViews()
        {
            _hud = new HudController();
            _hud.Build(DisplayLayout.Compute(_device.Display));

            _inputSettings = new PlayerInputReader.Settings();
            _input = new PlayerInputReader(_hud.Controls, _inputSettings);
            _camera = new CameraDirector(_cam);
            _cameraEffects = new CameraEffects();
            _weather = new WeatherState();
            _perf = new PerformanceDirector(QualityTier.High, QualityTier.Ultra);
            _perf.Budgets = _config.Budgets;
            _menu = new MenuController();
            _trafficView = new TrafficView(_worldRoot, VehicleCatalog.Create(VehicleCatalog.AuroraVeloce));

            _weatherVisuals = new WeatherVisuals(_cam, _worldRoot);
            _weatherVisuals.BindRoads(_roadMats);
            _reflectionRig = new ReflectionProbeRig(_worldRoot);
            _post = new PostProcessingController();
            _photo = new PhotoMode(_cam);
            var ambientGo = new GameObject("AmbientAudio");
            ambientGo.transform.SetParent(_worldRoot, false);
            _ambient = ambientGo.AddComponent<AmbientAudio>();
            _windscreen = new WindscreenOverlay(_config.Seed);
            Material skidMaterial = WeatherVisuals.CreateParticleMaterial(1f);
            if (skidMaterial != null) _skidMarks = new SkidMarkRenderer(_worldRoot, skidMaterial);
        }

        // ----- States -----

        private void EnterMenu()
        {
            _state = AppState.Menu;
            QuietDriveEffects();
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
                case MenuAction.Photo:
                    EnterPhoto();
                    break;
                case MenuAction.PhotoBack:
                    ExitPhoto();
                    break;
                case MenuAction.PhotoCloser:
                    _photo.Orbit.ZoomStepIn();
                    UpdatePhotoInfo(null);
                    break;
                case MenuAction.PhotoFarther:
                    _photo.Orbit.ZoomStepOut();
                    UpdatePhotoInfo(null);
                    break;
                case MenuAction.PhotoTele:
                    _photo.Orbit.StepFocal(1);
                    UpdatePhotoInfo(null);
                    break;
                case MenuAction.PhotoWide:
                    _photo.Orbit.StepFocal(-1);
                    UpdatePhotoInfo(null);
                    break;
                case MenuAction.PhotoShoot:
                    StartCoroutine(CapturePhoto());
                    break;
            }
        }

        private void StartRun(GameMode mode, bool continueSave)
        {
            MenuOptions opts = _menu.Options;
            _mode = mode;
            string carId = VehicleCatalog.Ids[opts.CarIndex];
            _spec = VehicleTuning.Apply(VehicleCatalog.Create(carId), VehicleTuningStore.Read(_save, carId));
            _save.SelectedVehicleId = _spec.Id;
            _weather.SetKind((WeatherKind)opts.WeatherIndex);
            _hour = MenuOptions.Hours[opts.HourIndex];
            AssistSettings assists = opts.AssistIndex == 0 ? AssistSettings.Arcade
                : opts.AssistIndex == 1 ? AssistSettings.Sport : AssistSettings.Simulation;

            // The AI race always starts from the grid, never from a saved route.
            bool isRace = mode == GameMode.AiRace;
            double startS = isRace ? RaceStartS : 0.0;
            double startT = _road.Sample(0).HalfWidth * 0.5;
            if (!isRace && continueSave && SaveMigrator.TryParseDouble(_save.RouteS, out double savedS)) startS = savedS;
            if (!isRace && continueSave && SaveMigrator.TryParseDouble(_save.RouteT, out double savedT)) startT = savedT;
            double startSpeed = mode == GameMode.TimeAttack || isRace ? 0.0 : RunStartSpeed;

            if (_playerView != null) Destroy(_playerView.gameObject);
            _playerView = CarView.Create(_worldRoot, _spec, MenuOptions.PaintColours[opts.PaintIndex], "Player");
            _engineAudio = _playerView.gameObject.AddComponent<EngineAudio>();

            _player = new VehicleDynamics(_spec, _road, assists);
            _player.Reset(startS, startT, startSpeed);

            _traffic = new TrafficSimulation(_road, _roadSettings.Seed, _config.MaxTrafficCars)
            {
                Enabled = opts.TrafficOn && mode != GameMode.TimeAttack && !isRace,
            };
            _trafficView.Clear();
            if (_skidMarks != null) _skidMarks.Clear();
            _scorer = new TrafficChallengeScorer();
            _timer = new TimeAttackTimer();
            if (mode == GameMode.TimeAttack)
            {
                _timer.Begin(startS, TimeAttackLength, 3, _save.Progress.BestTimeAttackSeconds);
            }

            if (isRace) BeginRace(startS);

            RoadSample start = _road.Sample(startS);
            _origin = start.PointAt(startT);
            _chunks.Clear();
            _lampLights.Clear();
            // Pre-fill the road around the start so the first frames are not a blank strip.
            for (int i = 0; i < 6; i++) _chunks.Update(startS, _origin);
            _playerView.Place(start, startT, 0.0, _origin, _player.Telemetry, 0f);
            _playerView.SetNight(_night01);
            _camera.Snap(_playerView);
            _cameraEffects.Reset();
            for (int i = 0; i < 4; i++) _prevCompression[i] = _player.Telemetry.Compression[i];

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
            QuietDriveEffects();
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
            // Menu choices drive the showroom sky, weather and lights live, so the player sees what they picked.
            MenuOptions opts = _menu.Options;
            _hour = MenuOptions.Hours[opts.HourIndex];
            _weather.SetKind((WeatherKind)opts.WeatherIndex);
            _weather.Update(dt);
            RefreshShowroom();
            _weatherVisuals.Tick(dt, _weather, Vector3.zero, Vector3.forward, 0f);
            _post.Tick(dt, new PostState { Night01 = _night01, Golden01 = _golden01, Rain01 = (float)_weather.Rain, Fog01 = (float)_weather.Fog });

            _showroomAngle += dt * 18f;
            if (_showroomCar != null)
            {
                _showroomCar.transform.localRotation = Quaternion.Euler(0f, _showroomAngle, 0f);
                _showroomCar.SetNight(_night01);
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
            // The AI race holds the car on the brakes during the grid countdown.
            VehicleInput input = frame.Vehicle;
            if (_mode == GameMode.AiRace && _rivalRace != null && !_rivalRace.Started)
            {
                input.Throttle = 0.0;
                input.Brake = 1.0;
            }
            _player.Advance(dt, input);
            _weather.Update(dt);
            VehicleTelemetry tm = _player.Telemetry;
            double s = _player.S;

            // Suspension roughness: RMS of the corner compression velocities, used for cockpit vibration.
            double sumSq = 0.0;
            for (int i = 0; i < 4; i++)
            {
                double v = dt > 0f ? (tm.Compression[i] - _prevCompression[i]) / dt : 0.0;
                sumSq += v * v;
                _prevCompression[i] = tm.Compression[i];
            }
            double roughness = Math.Sqrt(sumSq / 4.0);

            _traffic.Update(dt, s, _player.T, tm.SpeedMs);
            foreach (TrafficEvent e in _traffic.Events)
            {
                if (e.Kind == TrafficEventKind.PlayerCollision)
                {
                    _scorer.OnCollision();
                    _hud.Flash();
                    _cameraEffects.TriggerImpact(0.8);
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

            if (_mode == GameMode.AiRace && _rivalRace != null && TickRace(dt, s, tm)) return;

            // Road and world.
            _chunks.Update(s, _origin);
            _chunks.DrawRoadside(_origin);
            _lampLights.Update(_chunks, _origin, _playerView.WorldPosition - _origin, _night01, _perf.LitLightLimit);
            _vegetation.Draw(_chunks.Active, _origin, _perf.VegetationLimit);
            _playerView.Place(_road.Sample(s), _player.T, _player.HeadingError, _origin, tm, dt);
            _playerView.SetNight(_night01);
            _trafficView.Tick(_traffic.Cars, _road, _origin, _night01);

            Vector3 focus = CarView.ToFloat(_playerView.WorldPosition - _origin);
            _weatherVisuals.Tick(dt, _weather, focus, CarView.ToFloat(_playerView.WorldForward), (float)tm.SpeedMs);
            _reflectionRig.Follow(focus, dt);

            _engineAudio.Set(tm.EngineRpm, tm.Throttle, 0.55);

            // Ambient sound: wind from the speed, squeal from the worst tyre's slip, rain from the weather.
            double squeal = 0.0;
            for (int i = 0; i < 4; i++)
            {
                squeal = System.Math.Max(squeal, AmbientSoundModel.SquealLevel(tm.SlipAngle[i], tm.SlipRatio[i]));
            }
            if (_ambient != null)
            {
                _ambient.Set(AmbientSoundModel.WindLevel(tm.SpeedMs), squeal, AmbientSoundModel.RainLevel(_weather.Rain));
            }

            // Windscreen: rain wets the glass, speed and the wipers clear it. Drops show from the cockpit view only.
            _wetness = WindscreenWetness.Step(_wetness, _weather.Rain, tm.SpeedMs, dt);
            _windscreen.SetWetness(_camera.Mode == CameraMode.Cockpit ? _wetness : 0.0);

            // Tyre marks: squealing tyres leave dark marks on the road behind the car.
            if (_skidMarks != null) _skidMarks.Tick(dt, _road, s, _player.T, tm, _spec, _origin);

            // Camera effects: shake, roll and FOV kick from the car's motion.
            CameraEffectsOutput fx = _cameraEffects.Step(new CameraEffectsInput
            {
                SpeedMs = tm.SpeedMs,
                LateralG = tm.LateralG,
                LongitudinalG = tm.LongitudinalG,
                SuspensionRoughness = roughness,
                Airborne = false,
            }, dt);
            _camera.Tick(dt, _playerView, tm, _origin, fx);

            _post.Tick(dt, new PostState
            {
                Night01 = _night01,
                Golden01 = _golden01,
                Rain01 = (float)_weather.Rain,
                Fog01 = (float)_weather.Fog,
                Impact01 = (float)fx.ImpactFlash01,
                Speed01 = (float)Mathx.Clamp01(tm.SpeedKmh / 300.0),
            });

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

        /// <summary>The ambient sound and the windscreen drops stop whenever the car is not being driven: paused, in photo mode, or in the menu.</summary>
        private void QuietDriveEffects()
        {
            if (_ambient != null) _ambient.Set(0.0, 0.0, 0.0);
            if (_windscreen != null) _windscreen.SetWetness(0.0);
        }

        // ----- Garage tuning -----

        /// <summary>The car selected in the garage. Tuning edits apply to it. They reach the disk at the next save.</summary>
        private string CurrentCarId() => VehicleCatalog.Ids[_menu.Options.CarIndex];

        private void OnTuningSlid(TuningKnob knob, double position)
        {
            VehicleTuningStore.SetFromSlider(_save, CurrentCarId(), knob, position);
            _menu.Refresh();
        }

        private void OnTyresCycled()
        {
            VehicleTuningStore.CycleTyres(_save, CurrentCarId());
            _menu.Refresh();
        }

        private void OnTuningReset()
        {
            VehicleTuningStore.Reset(_save, CurrentCarId());
            _menu.Refresh();
        }

        // ----- Photo mode -----

        /// <summary>Freezes the run and opens the photo controls. The pause panel is hidden while they are shown.</summary>
        private void EnterPhoto()
        {
            _state = AppState.Photo;
            _hud.SetVisible(false);
            QuietDriveEffects();
            _menu.ShowPhoto();
            UpdatePhotoInfo(null);
        }

        private void ExitPhoto()
        {
            // The driving camera takes over again on resume, so it starts from the car, not from the photo pose.
            _state = AppState.Paused;
            _camera.Snap(_playerView);
            _menu.ShowPause();
        }

        /// <summary>
        /// The world stays frozen while photo mode runs. The camera follows the orbit around the car, and the
        /// weather particles and post effects keep running so the frame is not a dead still.
        /// </summary>
        private void TickPhoto(float dt)
        {
            Vector3d car = _playerView.WorldPosition - _origin;
            _photo.Apply(car);
            _weatherVisuals.Tick(dt, _weather, CarView.ToFloat(car), CarView.ToFloat(_playerView.WorldForward), 0f);
            _post.Tick(dt, new PostState
            {
                Night01 = _night01,
                Golden01 = _golden01,
                Rain01 = (float)_weather.Rain,
                Fog01 = (float)_weather.Fog,
            });
        }

        private void OnPhotoDragged(double dx, double dy)
        {
            if (_state == AppState.Photo) _photo.Orbit.Drag(dx, dy, Screen.height);
        }

        /// <summary>Shows the orbit settings. A null status leaves the status line as it is.</summary>
        private void UpdatePhotoInfo(string status)
        {
            PhotoOrbit o = _photo.Orbit;
            _menu.SetPhotoInfo($"{o.FocalLengthMm:F0} mm  ·  {o.VerticalFovDegrees:F0}° field  ·  {o.Distance:F1} m", status);
        }

        /// <summary>
        /// Hides the controls for one frame, then writes the screenshot to the app's persistent folder. The
        /// screenshot is taken after the frame has rendered, so the picture contains the world only.
        /// </summary>
        private IEnumerator CapturePhoto()
        {
            _menu.SetPhotoUiVisible(false);
            yield return new WaitForEndOfFrame();
            string name = PhotoShots.FileName(DateTime.Now, _photoShots++);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(Application.persistentDataPath, name));
            yield return null;
            if (_state == AppState.Photo)
            {
                _menu.SetPhotoUiVisible(true);
                UpdatePhotoInfo("Saving " + name);
            }
        }

        /// <summary>Creates the rivals for a new race: the core race, and one visual car per rival.</summary>
        private void BeginRace(double originS)
        {
            _raceOriginS = originS;
            int lanes = Math.Max(1, _road.Sample(originS).Lanes);
            int rivals = Math.Min(8, Math.Max(0, _config.RivalCount));
            double spacing = Math.Max(1.0, Math.Min(_config.RivalGridSpacing, MaxGridBackM / Math.Max(1, rivals)));
            _rivalRace = new RivalRace(Math.Max(100.0, _config.RaceLengthM), rivals, lanes, _config.RivalDifficulty,
                Math.Max(1.0, _config.RivalReferenceSpeedMs), spacing, RaceCountdownSeconds);

            foreach (CarView view in _rivalViews)
            {
                if (view != null) Destroy(view.gameObject);
            }
            _rivalViews.Clear();
            for (int i = 0; i < _rivalRace.Rivals.Count; i++)
            {
                VehicleSpec spec = VehicleCatalog.Create(VehicleCatalog.Ids[(i + 1) % VehicleCatalog.Ids.Count]);
                var paint = MenuOptions.PaintColours[(i + 2) % MenuOptions.PaintColours.Length];
                _rivalViews.Add(CarView.Create(_worldRoot, spec, paint, "Rival " + (i + 1)));
            }
            _raceEndTimer = -1f;
        }

        /// <summary>
        /// One frame of the AI race: the player's place on the route goes to the core race, rivals are moved and
        /// placed on their lanes. Returns true when the result has been shown and the game is back in the menu.
        /// </summary>
        private bool TickRace(float dt, double s, VehicleTelemetry tm)
        {
            int lanes = _rivalRace.Lanes;
            double halfWidth = _road.Sample(s).HalfWidth;
            _rivalRace.SetPlayer(s - _raceOriginS, tm.SpeedMs, RivalRace.LaneOf(_player.T, lanes, halfWidth));
            _rivalRace.Update(dt);

            // Only rivals inside the streamed road are shown and sampled. Sampling far behind the car would rewind
            // the road generator every frame, and far ahead there is no road mesh yet.
            double playerProgress = s - _raceOriginS;
            double viewBehind = _chunks.ChunksBehind * RoadMeshBuilder.ChunkLength;
            double viewAhead = Math.Max(0, _chunks.ChunksAhead - 1) * RoadMeshBuilder.ChunkLength;
            for (int i = 0; i < _rivalViews.Count; i++)
            {
                RaceEntrant rival = _rivalRace.Rivals[i];
                GameObject go = _rivalViews[i].gameObject;
                double gap = rival.Progress - playerProgress;
                bool inView = gap >= -viewBehind && gap <= viewAhead;
                if (go.activeSelf != inView) go.SetActive(inView);
                if (!inView) continue;

                RoadSample sample = _road.Sample(_raceOriginS + rival.Progress);
                double t = RivalRace.LaneCentre(rival.Lane, lanes, sample.HalfWidth);
                _rivalViews[i].Place(sample, t, 0.0, _origin, null, dt);
                _rivalViews[i].SetNight(_night01);
            }

            if (!_rivalRace.IsOver) return false;
            if (_raceEndTimer < 0f) _raceEndTimer = RaceResultSeconds;
            _raceEndTimer -= dt;
            if (_raceEndTimer > 0f) return false;

            _raceEndTimer = -1f;
            SaveNow();
            EnterMenu();
            return true;
        }

        private string RaceModeLine()
        {
            RaceEntrant player = _rivalRace.Player;
            int count = _rivalRace.Rivals.Count + 1;
            if (player.Finished)
            {
                return $"FINISH  P{player.Place}/{count}  {player.FinishTime:F2} s";
            }
            double toGo = Math.Max(0.0, _rivalRace.Director.RaceLength - player.Progress);
            return $"AI RACE  P{player.Place}/{count}  {_rivalRace.Director.Elapsed:F1} s  {toGo:F0} m to go";
        }

        /// <summary>Countdown digits before the start, then GO for a second. Other times keep the normal status.</summary>
        private string RaceStatus(string fallback)
        {
            if (!_rivalRace.Started) return ((int)Math.Ceiling(_rivalRace.Countdown)).ToString();
            if (_rivalRace.Director.Elapsed < 1.0) return "GO";
            return fallback;
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
                case GameMode.AiRace:
                    modeLine = _rivalRace != null ? RaceModeLine() : "AI RACE";
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
            if (_mode == GameMode.AiRace && _rivalRace != null) status = RaceStatus(status);
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

        /// <summary>Sun, sky shader, fog and ambient light from the atmosphere model. Runs once per second.</summary>
        private void UpdateSky()
        {
            SkyModel.SolarPosition(LatitudeDeg, DayOfYear, _hour, out double elev, out double az);
            Vector3 towardsSun = new Vector3((float)(Math.Sin(az) * Math.Cos(elev)), (float)Math.Sin(elev),
                (float)(Math.Cos(az) * Math.Cos(elev)));
            SkyLook look = Atmosphere.Evaluate(elev, _weather.Cloud, _weather.Rain, _weather.Fog, Turbidity);

            _night01 = (float)SkyModel.NightFactor(elev);
            _golden01 = (float)SkyModel.GoldenHourFactor(elev);

            // Wet asphalt reflects the sky, the sun and the lamps by this much (see WetReflection).
            if (_roadMats != null)
            {
                _roadMats.SetReflection((float)WetReflection.Strength(_weather.Wetness, elev, _night01));
            }

            _sun.transform.rotation = Quaternion.LookRotation(-towardsSun);
            _sun.intensity = (float)(0.1 + 1.6 * look.SunIntensity);
            _sun.color = ToColor(look.SunTint);

            _cam.backgroundColor = ToColor(look.Horizon);
            RenderSettings.fogColor = ToColor(look.FogTint);
            RenderSettings.fogDensity = (float)(0.00025 + 0.0012 * _weather.Fog + 0.0005 * _weather.Rain);
            RenderSettings.ambientLight = ToColor(look.Ambient);

            if (_skyMat != null)
            {
                _skyMat.SetColor("_ZenithColor", ToColor(look.Zenith));
                _skyMat.SetColor("_HorizonColor", ToColor(look.Horizon));
                _skyMat.SetColor("_GroundColor", ToColor(look.Ground));
                _skyMat.SetColor("_SunColor", ToColor(look.SunTint));
                _skyMat.SetVector("_SunDirection", new Vector4(towardsSun.x, towardsSun.y, towardsSun.z, 0f));
                _skyMat.SetFloat("_SunSize", 0.03f);
                _skyMat.SetFloat("_SunGlow", (float)look.SunGlow);
                _skyMat.SetFloat("_StarVisibility", (float)look.StarVisibility);
                _skyMat.SetFloat("_CloudCoverage", (float)look.CloudCoverage);
                _skyMat.SetFloat("_CloudDensity", (float)look.CloudDensity);
                _skyMat.SetFloat("_CloudBrightness", (float)look.CloudBrightness);
                _skyMat.SetFloat("_CloudSpeed", 0.01f);
            }

            // Street lamps glow only at night.
            if (_lampMat != null)
            {
                _lampMat.SetColor("_EmissionColor", new Color(1f, 0.8f, 0.5f) * (_night01 * 4f));
            }
        }

        private static Color ToColor(LinearRgb c)
        {
            return new Color(Mathf.Clamp01((float)c.R), Mathf.Clamp01((float)c.G), Mathf.Clamp01((float)c.B), 1f);
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
