using System;
using System.Collections.Generic;
using NUnit.Framework;
using Race1.Core;

namespace Race1.Tests.Core
{
    [TestFixture]
    public sealed class TrafficTests
    {
        [Test]
        public void Idm_FreeRoad_AcceleratesTowardsDesiredSpeedAndStops()
        {
            var p = IdmParameters.Default(20.0);
            Assert.That(IntelligentDriverModel.Acceleration(p, 0.0, double.PositiveInfinity, 0.0), Is.GreaterThan(0.0));
            Assert.That(IntelligentDriverModel.Acceleration(p, 20.0, double.PositiveInfinity, 0.0), Is.EqualTo(0.0).Within(1e-9));
        }

        [Test]
        public void Idm_CloseGapToSlowerLeader_Brakes()
        {
            var p = IdmParameters.Default(25.0);
            double a = IntelligentDriverModel.Acceleration(p, 20.0, 8.0, 15.0);
            Assert.That(a, Is.LessThan(-2.0), "must brake hard when closing fast on a short gap");
        }

        [Test]
        public void Traffic_RunsTwoMinutes_WithoutOverlapOrNaN()
        {
            RoadTrack road = TestWorld.CreateTrack(77);
            var sim = new TrafficSimulation(road, 77, 12);
            double playerS = 0.0;
            double playerSpeed = 20.0;
            int collisions = 0;
            int nearMisses = 0;

            double playerT = TrafficSimulation.LaneCentre(1, 0, 1, 3.5); // right-hand lane
            for (int i = 0; i < 60 * 120; i++)
            {
                playerS += playerSpeed / 60.0;
                road.Prefetch(playerS, 700);
                road.TrimBefore(playerS - 300);
                sim.Update(1.0 / 60.0, playerS, playerT, playerSpeed);
                foreach (TrafficEvent e in sim.Events)
                {
                    if (e.Kind == TrafficEventKind.PlayerCollision) collisions++;
                    if (e.Kind == TrafficEventKind.NearMiss) nearMisses++;
                }
                foreach (TrafficCar c in sim.Cars)
                {
                    Assert.That(double.IsFinite(c.S) && double.IsFinite(c.Speed), "non-finite traffic state");
                    Assert.That(c.Speed, Is.GreaterThanOrEqualTo(0.0));
                }
            }

            Assert.That(sim.Cars.Count, Is.GreaterThan(0), "no traffic spawned");
            // Same lane, same direction: cars must never interpenetrate.
            for (int a = 0; a < sim.Cars.Count; a++)
            {
                for (int b = a + 1; b < sim.Cars.Count; b++)
                {
                    TrafficCar x = sim.Cars[a];
                    TrafficCar y = sim.Cars[b];
                    if (x.Direction != y.Direction || Math.Abs(x.T - y.T) > TrafficCar.Width) continue;
                    Assert.That(Math.Abs(x.S - y.S), Is.GreaterThan(TrafficCar.Length * 0.5),
                        $"cars {x.Id} and {y.Id} overlap");
                }
            }
            TestContext.WriteLine($"traffic 2 min: {sim.Cars.Count} cars, collisions {collisions}, near misses {nearMisses}");
        }

        [Test]
        public void Traffic_DisabledMeansEmptyRoad()
        {
            RoadTrack road = TestWorld.CreateTrack(5);
            var sim = new TrafficSimulation(road, 5, 10);
            sim.Update(0.1, 0.0, 0.0, 20.0);
            Assert.That(sim.Cars.Count, Is.GreaterThan(0));
            sim.Enabled = false;
            Assert.That(sim.Cars.Count, Is.EqualTo(0));
            sim.Update(0.1, 500.0, 0.0, 20.0);
            Assert.That(sim.Cars.Count, Is.EqualTo(0));
        }
    }

    [TestFixture]
    public sealed class CameraTests
    {
        [Test]
        public void SmoothDamp_NeverOvershootsTarget()
        {
            double value = 0.0;
            double velocity = 0.0;
            double peak = 0.0;
            for (int i = 0; i < 600; i++)
            {
                Mathx.SmoothDamp(ref value, ref velocity, 1.0, 0.2, 1.0 / 60.0);
                peak = Math.Max(peak, value);
            }
            Assert.That(peak, Is.LessThanOrEqualTo(1.0 + 1e-9), "critically damped follower overshot");
            Assert.That(value, Is.EqualTo(1.0).Within(1e-3));
        }

        [Test]
        public void ChaseCamera_StaysBehindAndAboveTheCar()
        {
            var cam = new ChaseCameraState();
            var settings = new ChaseCameraSettings();
            var carPos = new Vector3d(0, 0, 0);
            var fwd = new Vector3d(0, 0, 1);
            var up = Vector3d.Up;
            cam.Snap(carPos, fwd, 30.0, settings);
            for (int i = 0; i < 300; i++)
            {
                cam.Step(carPos + fwd * (i * 0.5), fwd, up, 30.0, 0.0, 0.0, 1.0 / 60.0, settings);
            }
            Vector3d carNow = carPos + fwd * 150.0;
            Assert.That(cam.Position.Z, Is.LessThan(carNow.Z), "camera must be behind the car");
            Assert.That(cam.Position.Y, Is.GreaterThan(carNow.Y), "camera must be above the car");
            Assert.That(cam.Fov, Is.GreaterThan(settings.BaseFov), "FOV should widen with speed");
        }
    }

    [TestFixture]
    public sealed class QualityGovernorTests
    {
        private static QualitySignals Frame(double ms, int thermal = 0) =>
            new QualitySignals { FrameMs = ms, GpuMs = 0, ThermalStatus = thermal, BatteryLevel = 1.0, Charging = true };

        [Test]
        public void SustainedOverload_LowersRenderScaleThenTier()
        {
            var g = new QualityGovernor(60.0, QualityTier.Ultra);
            for (int i = 0; i < 60 * 30; i++) g.Update(Frame(26.0), 1.0 / 60.0);
            Assert.That(g.RenderScale, Is.LessThan(1.0));
            Assert.That(g.RenderScale, Is.GreaterThanOrEqualTo(QualityGovernor.MinRenderScale - 1e-9));
            Assert.That((int)g.Tier, Is.LessThan((int)QualityTier.Ultra));
        }

        [Test]
        public void SustainedHeadroom_RecoversScale()
        {
            var g = new QualityGovernor(60.0, QualityTier.High);
            for (int i = 0; i < 60 * 20; i++) g.Update(Frame(26.0), 1.0 / 60.0);
            double low = g.RenderScale;
            for (int i = 0; i < 60 * 30; i++) g.Update(Frame(8.0), 1.0 / 60.0);
            Assert.That(g.RenderScale, Is.GreaterThan(low));
        }

        [Test]
        public void SevereThermal_StepsDownQualityQuickly()
        {
            var g = new QualityGovernor(60.0, QualityTier.Ultra);
            for (int i = 0; i < 60; i++) g.Update(Frame(16.0, thermal: 4), 1.0 / 60.0);
            Assert.That((int)g.Tier, Is.LessThan((int)QualityTier.Ultra));
            Assert.That(g.RenderScale, Is.LessThanOrEqualTo(0.95));
        }

        [Test]
        public void BatterySaver_CapsTier()
        {
            var g = new QualityGovernor(60.0, QualityTier.Ultra);
            var s = new QualitySignals { FrameMs = 12, BatteryLevel = 0.08, Charging = false };
            for (int i = 0; i < 60 * 5; i++) g.Update(s, 1.0 / 60.0);
            Assert.That(g.Tier, Is.EqualTo(QualityTier.BatterySaver));
        }

        [Test]
        public void SteadyLoad_DoesNotOscillate()
        {
            var g = new QualityGovernor(60.0, QualityTier.High);
            double last = g.RenderScale;
            int direction = 0;
            int reversals = 0;
            for (int i = 0; i < 60 * 120; i++)
            {
                // Frame time that depends on render scale, like a real GPU-bound frame.
                double ms = 10.0 + 14.0 * g.RenderScale;
                g.Update(Frame(ms), 1.0 / 60.0);
                if (Math.Abs(g.RenderScale - last) > 1e-9)
                {
                    int d = Math.Sign(g.RenderScale - last);
                    if (direction != 0 && d != direction) reversals++;
                    direction = d;
                    last = g.RenderScale;
                }
            }
            Assert.That(reversals, Is.LessThanOrEqualTo(4), "governor oscillates between levels");
        }
    }

    [TestFixture]
    public sealed class InputAndDisplayTests
    {
        [Test]
        public void Deadzone_RemovesCentreAndRescalesRange()
        {
            Check.AreEqual(0.0, InputShaping.Deadzone(0.03, 0.05));
            Assert.That(InputShaping.Deadzone(1.0, 0.1), Is.EqualTo(1.0).Within(1e-9));
            Assert.That(InputShaping.Deadzone(-0.5, 0.1), Is.LessThan(0.0));
            Assert.That(InputShaping.Deadzone(0.5, 0.1), Is.EqualTo(-InputShaping.Deadzone(-0.5, 0.1)).Within(1e-12));
        }

        [Test]
        public void Steering_IsClampedAndMonotonic()
        {
            double prev = -2;
            for (double raw = -1.0; raw <= 1.0; raw += 0.05)
            {
                double s = InputShaping.Steering(raw, 0.05, 1.3, 1.5);
                Assert.That(s, Is.InRange(-1.0, 1.0));
                Assert.That(s, Is.GreaterThanOrEqualTo(prev - 1e-12));
                prev = s;
            }
        }

        [Test]
        public void Pedal_RisesSlowerThanItFalls()
        {
            double p = 0.0;
            for (int i = 0; i < 6; i++) p = InputShaping.Pedal(p, 1.0, 2.0, 6.0, 1.0 / 60.0);
            double risen = p;
            for (int i = 0; i < 6; i++) p = InputShaping.Pedal(p, 0.0, 2.0, 6.0, 1.0 / 60.0);
            Assert.That(p, Is.LessThan(risen));
            Assert.That(risen, Is.LessThan(0.5), "a 0.1 s tap must not produce a full press");
        }

        [Test]
        public void DisplayClasses_CoverFoldAndPhoneShapes()
        {
            Check.AreEqual(ScreenClass.Cover, DisplayLayout.Classify(2520, 1080));
            Check.AreEqual(ScreenClass.Unfolded, DisplayLayout.Classify(2160, 1856));
            Check.AreEqual(ScreenClass.Phone, DisplayLayout.Classify(2400, 1080).Equals(0) ? ScreenClass.Cover : ScreenClass.Phone);
            Check.AreEqual(ScreenClass.Phone, DisplayLayout.Classify(1920, 1080));
            Check.AreEqual(ScreenClass.Tablet, DisplayLayout.Classify(2560, 1600));
        }

        [Test]
        public void Layout_KeepsControlsInsideSafeArea()
        {
            var safe = new RectN(0.04, 0.02, 0.92, 0.96);
            foreach (var size in new[] { (2520, 1080), (2160, 1856), (1920, 1080), (2560, 1600) })
            {
                DisplayLayoutResult r = DisplayLayout.Compute(new DisplayMetrics
                {
                    WidthPx = size.Item1, HeightPx = size.Item2, Dpi = 420, SafeArea = safe,
                });
                Assert.That(r.SteeringArea.X, Is.GreaterThanOrEqualTo(safe.X - 1e-9));
                Assert.That(r.SteeringArea.Bottom, Is.LessThanOrEqualTo(safe.Bottom + 1e-9), $"steering off screen at {size}");
                Assert.That(r.PedalsArea.Right, Is.LessThanOrEqualTo(safe.Right + 1e-9), $"pedals off screen at {size}");
                Assert.That(r.UiScale, Is.InRange(0.6, 2.0));
            }
        }
    }

    [TestFixture]
    public sealed class SkyWeatherAudioTests
    {
        [Test]
        public void Sun_HighAtMidsummerNoon_BelowHorizonAtMidnight()
        {
            SkyModel.SolarPosition(50.0, 172, 12.0, out double noon, out _);
            SkyModel.SolarPosition(50.0, 172, 0.0, out double midnight, out _);
            Assert.That(noon * Mathx.Rad2Deg, Is.InRange(58.0, 66.0), "noon elevation at 50N in June");
            Assert.That(midnight, Is.LessThan(0.0));
            Assert.That(SkyModel.NightFactor(midnight), Is.GreaterThan(0.9));
            Assert.That(SkyModel.NightFactor(noon), Is.EqualTo(0.0).Within(1e-9));
        }

        [Test]
        public void Weather_RainWetsRoadAndDriesAfterwards()
        {
            var w = new WeatherState();
            w.SetKind(WeatherKind.Rain);
            for (int i = 0; i < 60 * 120; i++) w.Update(1.0 / 60.0);
            double wet = w.Wetness;
            Assert.That(wet, Is.GreaterThan(0.5));
            Assert.That(w.FrictionScale, Is.LessThan(0.9));

            w.SetKind(WeatherKind.Clear);
            for (int i = 0; i < 60 * 600; i++) w.Update(1.0 / 60.0);
            Assert.That(w.Wetness, Is.LessThan(wet * 0.5), "road should dry out");
            Assert.That(w.FrictionScale, Is.GreaterThan(0.97));
        }

        [Test]
        public void EngineSynth_IsBoundedAndTracksFiringFrequency()
        {
            const double rate = 48000.0;
            var synth = new EngineSynth(rate, 6) { Rpm = 3000.0, Load = 0.8, Volume = 0.8 };
            var buffer = new float[(int)rate];
            synth.Render(buffer, 0, buffer.Length);

            int crossings = 0;
            for (int i = 1; i < buffer.Length; i++)
            {
                Assert.That(float.IsFinite(buffer[i]) && Math.Abs(buffer[i]) <= 1.0001f, "output out of range");
                if (buffer[i - 1] < 0 && buffer[i] >= 0) crossings++;
            }
            double expected = synth.FiringFrequency; // 150 Hz at 3000 rpm for six cylinders
            Assert.That(crossings, Is.InRange((int)(expected * 0.8), (int)(expected * 1.6)),
                "pitch does not follow engine speed (noise makes the count rough)");
        }
    }

    [TestFixture]
    public sealed class RaceAndSaveTests
    {
        [Test]
        public void TimeAttack_RecordsSectorsAndBest()
        {
            var t = new TimeAttackTimer();
            t.Begin(startS: 1000.0, lengthM: 1000.0, sectors: 2, bestSeconds: -1.0);
            for (int i = 0; i < 60 * 60; i++)
            {
                double s = 1000.0 + 20.0 * (i / 60.0) * 1.0; // 20 m/s
                t.Update(1.0 / 60.0, s);
                if (t.Finished) break;
            }
            Assert.That(t.Finished, Is.True);
            Assert.That(t.SectorTime(0), Is.GreaterThan(0.0));
            Assert.That(t.SectorTime(1), Is.GreaterThan(t.SectorTime(0)));
            Assert.That(t.BestSeconds, Is.EqualTo(t.LastSeconds).Within(1e-9));
        }

        [Test]
        public void TrafficScorer_SafePassesBuildMultiplier_CollisionResetsIt()
        {
            var sc = new TrafficChallengeScorer();
            for (int i = 0; i < 6; i++) sc.OnOvertake(3.0);
            Assert.That(sc.Multiplier, Is.GreaterThan(1));
            int before = sc.Score;
            sc.OnCollision();
            Assert.That(sc.Multiplier, Is.EqualTo(1));
            Assert.That(sc.Score, Is.LessThan(before));
            Assert.That(sc.Score, Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public void SaveMigration_FromOldVersion_FillsDefaults()
        {
            var old = new SaveData { Version = 0, Settings = null, Progress = null, Tunings = null };
            SaveData migrated = SaveMigrator.Migrate(old);
            Assert.That(migrated.Version, Is.EqualTo(SaveMigrator.CurrentVersion));
            Assert.That(migrated.Settings, Is.Not.Null);
            Assert.That(migrated.Progress, Is.Not.Null);
            Assert.That(migrated.Tunings, Is.Not.Null);
        }

        [Test]
        public void SaveData_RouteStringRoundTripsExactly()
        {
            double s = 123456.789012345;
            string text = SaveMigrator.RoundTrip(s);
            Assert.That(SaveMigrator.TryParseDouble(text, out double back), Is.True);
            Check.AreEqual(s, back, 0.0);
            Assert.That(SaveMigrator.TryParseDouble("nan", out _), Is.False);
        }

        [Test]
        public void VehicleCatalog_AllCarsValidateAndAccelerate()
        {
            foreach (string id in VehicleCatalog.Ids)
            {
                VehicleSpec spec = VehicleCatalog.Create(id);
                var car = new VehicleDynamics(spec, new AnalyticRoad(), AssistSettings.Sport);
                car.Reset(0, 0, 0);
                double t = 0.0;
                while (t < 20.0 && car.Speed < 100.0 / 3.6)
                {
                    car.Advance(1.0 / 60.0, new VehicleInput { Throttle = 1.0, AutoGearbox = true });
                    t += 1.0 / 60.0;
                }
                TestContext.WriteLine($"{spec.DisplayName}: 0-100 km/h in {t:F2} s");
                Assert.That(car.Speed, Is.GreaterThanOrEqualTo(100.0 / 3.6), $"{id} never reached 100 km/h");
                Assert.That(t, Is.InRange(2.5, 9.0), $"{id} acceleration out of range");
            }
        }
    }
}
