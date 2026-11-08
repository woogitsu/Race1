using System;
using NUnit.Framework;
using Race1.Core;

namespace Race1.Tests.Core
{
    [TestFixture]
    public sealed class VehicleDynamicsTests
    {
        private const double Kmh100 = 100.0 / 3.6;

        private static VehicleDynamics CarOn(IRoadProvider road, VehicleSpec spec, AssistSettings assists, double speed)
        {
            var car = new VehicleDynamics(spec, road, assists);
            car.Reset(0.0, 0.0, speed);
            return car;
        }

        [Test]
        public void Acceleration_0_to_100_IsSportCarRealistic()
        {
            var straight = new AnalyticRoad();
            VehicleDynamics car = CarOn(straight, TestWorld.SportRwd(), AssistSettings.Sport, 0.0);
            double t = 0.0;
            double reached = -1.0;
            while (t < 15.0)
            {
                car.Advance(1.0 / 60.0, new VehicleInput { Throttle = 1.0, AutoGearbox = true });
                t += 1.0 / 60.0;
                if (reached < 0 && car.Speed >= Kmh100) reached = t;
                AssertFinite(car);
            }
            Assert.That(reached, Is.GreaterThan(0), "never reached 100 km/h");
            Assert.That(reached, Is.InRange(3.0, 6.5), $"0-100 took {reached:F2} s");
            TestContext.WriteLine($"0-100 km/h: {reached:F2} s (RWD, Sport assists)");
        }

        [Test]
        public void TopSpeed_ExceedsThreeHundredKmhOnLongStraight()
        {
            var straight = new AnalyticRoad();
            VehicleDynamics car = CarOn(straight, TestWorld.SportRwd(), AssistSettings.Simulation, 0.0);
            for (int i = 0; i < 60 * 90; i++)
            {
                car.Advance(1.0 / 60.0, new VehicleInput { Throttle = 1.0, AutoGearbox = true });
                AssertFinite(car);
            }
            TestContext.WriteLine($"top speed after 90 s: {car.Telemetry.SpeedKmh:F1} km/h, gear {car.Telemetry.Gear}");
            Assert.That(car.Telemetry.SpeedKmh, Is.GreaterThan(290.0));
            Assert.That(car.Telemetry.SpeedKmh, Is.LessThan(340.0), "top speed above the gearing limit");
        }

        [Test]
        public void Braking_From100_WithAbs_StopsInRealisticDistance()
        {
            var straight = new AnalyticRoad();
            VehicleDynamics car = CarOn(straight, TestWorld.SportRwd(), AssistSettings.Sport, Kmh100);
            double startS = car.S;
            for (int i = 0; i < 60 * 12 && car.Speed > 0.05; i++)
            {
                car.Advance(1.0 / 60.0, new VehicleInput { Brake = 1.0, AutoGearbox = true });
                AssertFinite(car);
            }
            double distance = car.S - startS;
            TestContext.WriteLine($"braking 100->0 with ABS: {distance:F1} m");
            Assert.That(car.Speed, Is.LessThan(0.5), "car did not stop");
            Assert.That(distance, Is.InRange(30.0, 48.0));
        }

        [Test]
        public void Braking_LoadsFrontSuspensionMoreThanRear()
        {
            var straight = new AnalyticRoad();
            VehicleDynamics car = CarOn(straight, TestWorld.SportRwd(), AssistSettings.Sport, Kmh100);
            for (int i = 0; i < 60; i++)
            {
                car.Advance(1.0 / 60.0, new VehicleInput { Brake = 0.9, AutoGearbox = true });
            }
            VehicleTelemetry t = car.Telemetry;
            Assert.That(t.Compression[0] + t.Compression[1], Is.GreaterThan(t.Compression[2] + t.Compression[3]),
                "front should dive under braking");
            Assert.That(t.Load[0] + t.Load[1], Is.GreaterThan(t.Load[2] + t.Load[3]));
        }

        [Test]
        public void SteadyCorner_FollowsRoadAndReachesRequiredLateralAcceleration()
        {
            var road = new AnalyticRoad { Curvature = 1.0 / 100.0 };
            VehicleDynamics car = CarOn(road, TestWorld.SportRwd(), AssistSettings.Sport, 20.0);
            double maxLateral = 0.0;
            double maxOffset = 0.0;
            for (int i = 0; i < 60 * 25; i++)
            {
                VehicleInput input = TestWorld.Autopilot(car, road, 20.0);
                car.Advance(1.0 / 60.0, input);
                AssertFinite(car);
                if (i > 60 * 5)
                {
                    maxLateral = Math.Max(maxLateral, Math.Abs(car.Telemetry.LateralG));
                    maxOffset = Math.Max(maxOffset, Math.Abs(car.T));
                }
            }
            double expected = 20.0 * 20.0 / 100.0 / Mathx.Gravity;
            TestContext.WriteLine($"R=100 m @20 m/s: lateral {maxLateral:F3} g (expected {expected:F3}), offset {maxOffset:F2} m");
            Assert.That(maxOffset, Is.LessThan(1.0), "autopilot left the lane");
            Assert.That(maxLateral, Is.InRange(expected * 0.85, expected * 1.25));
        }

        [Test]
        public void Cornering_GripIsCappedNearTyreFriction()
        {
            var road = new AnalyticRoad { Curvature = 1.0 / 60.0 };
            VehicleDynamics car = CarOn(road, TestWorld.SportRwd(), AssistSettings.Simulation, 25.0);
            double maxLateral = 0.0;
            for (int i = 0; i < 60 * 8; i++)
            {
                // Hard steer with the throttle off: the car is pushed to its limit and the tyres saturate.
                car.Advance(1.0 / 60.0, new VehicleInput { Steer = 0.35, AutoGearbox = true });
                AssertFinite(car);
                if (i > 60) maxLateral = Math.Max(maxLateral, Math.Abs(car.Telemetry.LateralG));
            }
            TestContext.WriteLine($"saturated corner lateral: {maxLateral:F2} g");
            Assert.That(maxLateral, Is.LessThan(1.35), "lateral grip exceeds the friction limit");
        }

        [Test]
        public void Physics_IsIndependentOfFrameRate()
        {
            VehicleInput Input(double t) => new VehicleInput
            {
                Throttle = t < 4 ? 0.8 : 0.3,
                Steer = Math.Sin(t * 0.9) * 0.25,
                AutoGearbox = true,
            };

            // Same held input, different frame lengths: only the sub-step rounding may differ.
            VehicleInput steady = new VehicleInput { Throttle = 0.8, Steer = 0.2, AutoGearbox = true };
            VehicleDynamics a = CarOn(new AnalyticRoad { Curvature = 0.01 }, TestWorld.SportRwd(), AssistSettings.Sport, 15.0);
            VehicleDynamics b = CarOn(new AnalyticRoad { Curvature = 0.01 }, TestWorld.SportRwd(), AssistSettings.Sport, 15.0);

            double total = 8.0;
            for (double t = 0.0; t < total - 1e-9; t += 1.0 / 30.0) a.Advance(1.0 / 30.0, steady);
            for (double t = 0.0; t < total - 1e-9; t += 1.0 / 150.0) b.Advance(1.0 / 150.0, steady);

            Assert.That(Math.Abs(a.S - b.S), Is.LessThan(0.01), $"distance differs: {a.S} vs {b.S}");
            Assert.That(Math.Abs(a.Speed - b.Speed), Is.LessThan(0.005));
            Assert.That(Math.Abs(a.T - b.T), Is.LessThan(0.005));

            // Varying input sampled at the physics step must match exactly, whatever the frame length.
            VehicleDynamics c = CarOn(new AnalyticRoad { Curvature = 0.01 }, TestWorld.SportRwd(), AssistSettings.Sport, 15.0);
            VehicleDynamics d = CarOn(new AnalyticRoad { Curvature = 0.01 }, TestWorld.SportRwd(), AssistSettings.Sport, 15.0);
            for (int i = 0; i < 600 * 4; i++) c.Advance(VehicleDynamics.PhysicsStep, Input((i / 4) * 4 * VehicleDynamics.PhysicsStep));
            for (int i = 0; i < 600 * 4 / 4; i++) d.Advance(4 * VehicleDynamics.PhysicsStep, Input(i * 4 * VehicleDynamics.PhysicsStep));
            Assert.That(Math.Abs(c.S - d.S), Is.LessThan(1e-6), "same physics steps must give the same result");
        }

        [Test]
        public void AllWheelDrive_AccelerateFasterThanRearWheelOnWetRoad()
        {
            double Time(VehicleSpec spec)
            {
                var straight = new AnalyticRoad();
                VehicleDynamics car = CarOn(straight, spec, AssistSettings.Simulation, 0.0);
                car.FrictionScale = 0.6;
                double t = 0.0;
                while (t < 30.0 && car.Speed < 20.0)
                {
                    car.Advance(1.0 / 120.0, new VehicleInput { Throttle = 1.0, AutoGearbox = true });
                    t += 1.0 / 120.0;
                }
                return t;
            }
            double rwd = Time(TestWorld.SportRwd());
            double awd = Time(TestWorld.AllWheel());
            TestContext.WriteLine($"0-72 km/h wet: RWD {rwd:F2} s, AWD {awd:F2} s");
            Assert.That(awd, Is.LessThan(rwd));
        }

        [Test]
        public void HeavyBarrierHit_CountsCollisionAndStaysFinite()
        {
            var road = new AnalyticRoad { Curvature = 0.0 };
            VehicleDynamics car = CarOn(road, TestWorld.SportRwd(), AssistSettings.Simulation, 25.0);
            car.Reset(0.0, 0.0, 25.0);
            for (int i = 0; i < 60 * 3; i++)
            {
                car.Advance(1.0 / 60.0, new VehicleInput { Steer = 1.0, Throttle = 0.5, AutoGearbox = true });
                AssertFinite(car);
            }
            Assert.That(car.Telemetry.CollisionCount, Is.GreaterThanOrEqualTo(1));
            Assert.That(car.Telemetry.Damage, Is.GreaterThan(0.0));
            Assert.That(Math.Abs(car.T), Is.LessThan(10.0), "barrier did not contain the car");
        }

        [Test]
        public void Autopilot_DrivesGeneratedRoadWithoutLeavingIt()
        {
            RoadTrack road = TestWorld.CreateTrack(20260101);
            VehicleDynamics car = CarOn(road, TestWorld.SportRwd(), AssistSettings.Arcade, 15.0);
            double maxOffset = 0.0;
            for (int i = 0; i < 60 * 240; i++)
            {
                VehicleInput input = TestWorld.Autopilot(car, road, 30.0);
                car.Advance(1.0 / 60.0, input);
                road.Prefetch(car.S, 400.0);
                road.TrimBefore(car.S - 300.0);
                AssertFinite(car);
                maxOffset = Math.Max(maxOffset, Math.Abs(car.T) - (road.Sample(car.S).HalfWidth));
            }
            TestContext.WriteLine($"240 s autopilot: distance {car.S:F0} m, collisions {car.Telemetry.CollisionCount}, " +
                                  $"max excursion beyond carriageway {maxOffset:F2} m");
            Assert.That(car.S, Is.GreaterThan(2000.0), "car made little progress");
            Assert.That(car.Telemetry.CollisionCount, Is.LessThanOrEqualTo(3));
        }

        private static void AssertFinite(VehicleDynamics car)
        {
            Assert.That(double.IsFinite(car.S) && double.IsFinite(car.T) && double.IsFinite(car.Speed) &&
                        double.IsFinite(car.HeadingError) && double.IsFinite(car.EngineRpm),
                $"non-finite state: s={car.S} t={car.T} v={car.Speed}");
        }
    }
}
