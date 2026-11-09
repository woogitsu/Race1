using System;
using NUnit.Framework;
using Race1.Core;

namespace Race1.Tests.Core
{
    [TestFixture]
    public sealed class TyreConditionTests
    {
        private const double Dt = 1.0 / 60.0;

        [Test]
        public void WearFactor_FallsMonotonicallyFromNewToWorn()
        {
            Assert.That(TyreCondition.WearFactor(0.0), Is.EqualTo(1.0).Within(1e-12));
            double prev = double.PositiveInfinity;
            for (double w = 0.0; w <= 1.0 + 1e-9; w += 0.01)
            {
                double f = TyreCondition.WearFactor(w);
                Assert.That(f, Is.LessThanOrEqualTo(prev + 1e-12), $"grip rose with wear {w:F2}");
                prev = f;
            }
            Assert.That(TyreCondition.WearFactor(1.0), Is.InRange(0.76, 0.80));
        }

        [Test]
        public void TemperatureFactor_IsOneInsideTheWindowAndFallsAwayFromIt()
        {
            for (double t = TyreCondition.OptimalMinTemperature; t <= TyreCondition.OptimalMaxTemperature; t += 1.0)
            {
                Assert.That(TyreCondition.TemperatureFactor(t), Is.EqualTo(1.0).Within(1e-12), $"not full grip at {t} °C");
            }

            double prevCold = double.PositiveInfinity;
            for (double t = TyreCondition.OptimalMinTemperature; t >= 0.0; t -= 1.0)
            {
                double f = TyreCondition.TemperatureFactor(t);
                Assert.That(f, Is.LessThanOrEqualTo(prevCold + 1e-12), $"colder tyre grips more at {t} °C");
                prevCold = f;
            }

            double prevHot = double.PositiveInfinity;
            for (double t = TyreCondition.OptimalMaxTemperature; t <= 200.0; t += 1.0)
            {
                double f = TyreCondition.TemperatureFactor(t);
                Assert.That(f, Is.LessThanOrEqualTo(prevHot + 1e-12), $"hotter tyre grips more at {t} °C");
                prevHot = f;
            }
        }

        [Test]
        public void GripMultiplier_StaysWithinSaneBounds()
        {
            for (double w = 0.0; w <= 1.0; w += 0.05)
            {
                for (double t = -10.0; t <= 200.0; t += 5.0)
                {
                    double g = TyreCondition.GripMultiplier(w, t);
                    Assert.That(g, Is.InRange(0.5, 1.0), $"grip {g} for wear {w} at {t} °C");
                }
            }
        }

        [Test]
        public void Wear_NeverDecreasesAndIsClamped()
        {
            Assert.That(TyreCondition.AccumulateWear(0.3, 0.0, 1.0), Is.EqualTo(0.3).Within(1e-12), "no sliding, no wear");
            double low = TyreCondition.AccumulateWear(0.0, 1000.0, 1.0);
            double high = TyreCondition.AccumulateWear(0.0, 5000.0, 1.0);
            Assert.That(high, Is.GreaterThan(low), "more sliding power must wear more");
            Assert.That(TyreCondition.AccumulateWear(0.99, 1e9, 10.0), Is.EqualTo(1.0).Within(1e-12));
        }

        [Test]
        public void Temperature_SettlesAtAmbientPlusHeatOverCooling()
        {
            // Steady state for constant sliding power P: T = ambient + HeatFraction * P / Cooling = 20 + 0.5 * 2000 / 15.
            double t = TyreCondition.AmbientTemperature;
            for (int i = 0; i < 3000 * 10; i++)
            {
                t = TyreCondition.Heat(t, 2000.0, 0.1, TyreCondition.AmbientTemperature);
            }
            Assert.That(t, Is.EqualTo(20.0 + 0.5 * 2000.0 / 15.0).Within(0.5));
        }

        [Test]
        public void Temperature_RelaxesBackToAmbientWithoutSliding()
        {
            double t = 150.0;
            for (int i = 0; i < 1500 * 10; i++)
            {
                t = TyreCondition.Heat(t, 0.0, 0.1, TyreCondition.AmbientTemperature);
            }
            Assert.That(t, Is.EqualTo(TyreCondition.AmbientTemperature).Within(1.0));
        }

        [Test]
        public void Vehicle_WearGrowsWhileCornering()
        {
            var road = new AnalyticRoad { Curvature = 1.0 / 100.0 };
            var car = new VehicleDynamics(TestWorld.SportRwd(), road, AssistSettings.Sport);
            car.Reset(0.0, 0.0, 20.0);
            double w5 = 0.0, w10 = 0.0, w20 = 0.0;
            for (int i = 0; i < 60 * 20; i++)
            {
                car.Advance(Dt, TestWorld.Autopilot(car, road, 30.0));
                if (i == 5 * 60 - 1) w5 = car.Telemetry.TyreWear[2];
                if (i == 10 * 60 - 1) w10 = car.Telemetry.TyreWear[2];
                if (i == 20 * 60 - 1) w20 = car.Telemetry.TyreWear[2];
            }
            TestContext.WriteLine($"rear-left wear: 5 s {w5:E3}, 10 s {w10:E3}, 20 s {w20:E3}");
            Assert.That(w5, Is.GreaterThan(0.0));
            Assert.That(w10, Is.GreaterThan(w5));
            Assert.That(w20, Is.GreaterThan(w10));
        }

        [Test]
        public void Vehicle_FreshTyresHoldTheLineThatWornOrColdTyresLose()
        {
            double fresh = MaxOffsetOnLine(wear: 0.0, temperature: 90.0);
            double worn = MaxOffsetOnLine(wear: 1.0, temperature: 90.0);
            double cold = MaxOffsetOnLine(wear: 0.0, temperature: 25.0);
            TestContext.WriteLine($"max lateral offset on a 60 m corner: fresh {fresh:F3} m, worn {worn:F3} m, cold {cold:F3} m");
            Assert.That(fresh, Is.LessThan(0.5), "fresh tyres should hold the line");
            Assert.That(worn, Is.GreaterThan(1.0), "fully worn tyres should lose the line");
            Assert.That(cold, Is.GreaterThan(1.0), "cold tyres should lose the line");
        }

        [Test]
        public void ResetTyres_ReplacesWearOnTheTelemetry()
        {
            var road = new AnalyticRoad { Curvature = 1.0 / 100.0 };
            var car = new VehicleDynamics(TestWorld.SportRwd(), road, AssistSettings.Sport);
            car.Reset(0.0, 0.0, 20.0);
            for (int i = 0; i < 60 * 10; i++) car.Advance(Dt, TestWorld.Autopilot(car, road, 30.0));
            Assert.That(car.Telemetry.TyreWear[0], Is.GreaterThan(0.0));

            car.ResetTyres(0.0, 90.0);
            car.Advance(0.0, VehicleInput.Idle);
            for (int i = 0; i < 4; i++)
            {
                Assert.That(car.Telemetry.TyreWear[i], Is.EqualTo(0.0).Within(1e-12));
                Assert.That(car.Telemetry.TyreTemperature[i], Is.EqualTo(90.0).Within(1e-9));
            }
        }

        /// <summary>
        /// Largest distance from the lane centre while the autopilot holds a 60 m corner (about 0.85 g demand)
        /// with tyres in the given state. The first seconds are ignored while the car settles onto the line.
        /// </summary>
        private static double MaxOffsetOnLine(double wear, double temperature)
        {
            var road = new AnalyticRoad { Curvature = 1.0 / 60.0 };
            var car = new VehicleDynamics(TestWorld.SportRwd(), road, AssistSettings.Sport);
            car.Reset(0.0, 0.0, 20.0);
            car.ResetTyres(wear, temperature);
            double max = 0.0;
            for (int i = 0; i < 60 * 25; i++)
            {
                car.Advance(Dt, TestWorld.Autopilot(car, road, 30.0));
                if (i > 60 * 5) max = Math.Max(max, Math.Abs(car.T));
            }
            return max;
        }
    }
}
