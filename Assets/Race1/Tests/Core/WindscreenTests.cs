using System;
using NUnit.Framework;
using Race1.Core;

namespace Race1.Tests.Core
{
    [TestFixture]
    public sealed class WindscreenTests
    {
        private const double Dt = 1.0 / 60.0;

        [Test]
        public void Equilibrium_RisesWithRain_AndFallsWithSpeed()
        {
            double previous = -1.0;
            for (double rain = 0.0; rain <= 1.0; rain += 0.05)
            {
                double e = WindscreenWetness.Equilibrium(rain, 10.0);
                Assert.That(e, Is.InRange(0.0, 1.0));
                Assert.That(e, Is.GreaterThanOrEqualTo(previous - 1e-12), $"wetness fell with more rain at {rain:F2}");
                previous = e;
            }
            Assert.That(WindscreenWetness.Equilibrium(0.0, 10.0), Is.EqualTo(0.0));

            Assert.That(WindscreenWetness.Equilibrium(1.0, 30.0), Is.LessThan(WindscreenWetness.Equilibrium(1.0, 0.0)),
                "a fast car stays clearer in the same rain");
        }

        [Test]
        public void Step_ConvergesToTheEquilibrium()
        {
            double w = 0.0;
            for (int i = 0; i < 60 * 600; i++) w = WindscreenWetness.Step(w, 0.8, 12.0, Dt);
            Assert.That(w, Is.EqualTo(WindscreenWetness.Equilibrium(0.8, 12.0)).Within(1e-9));
        }

        [Test]
        public void Step_MatchesTheExactSolution()
        {
            double gain = WindscreenWetness.AccumulationPerSecond * 1.0;
            double clear = WindscreenWetness.ClearingRate(0.0);
            double k = gain + clear;
            double equilibrium = gain / k;
            double expected = equilibrium + (0.0 - equilibrium) * Math.Exp(-k * 2.0);
            Assert.That(WindscreenWetness.Step(0.0, 1.0, 0.0, 2.0), Is.EqualTo(expected).Within(1e-12));
        }

        [Test]
        public void Step_NeverOvershoots_AndStaysInRange()
        {
            var rng = new Random(9);
            for (int i = 0; i < 3000; i++)
            {
                double w0 = rng.NextDouble();
                double rain = rng.NextDouble();
                double speed = rng.NextDouble() * 60.0;
                double dt = rng.NextDouble() * 5.0;
                double next = WindscreenWetness.Step(w0, rain, speed, dt);
                Assert.That(next, Is.InRange(0.0, 1.0));
                double eq = WindscreenWetness.Equilibrium(rain, speed);
                double lo = Math.Min(w0, eq), hi = Math.Max(w0, eq);
                Assert.That(next, Is.InRange(lo - 1e-12, hi + 1e-12), $"step {i} overshot");
            }
        }

        [Test]
        public void Speed_DriesTheGlassAfterTheRainStops()
        {
            double w = 1.0;
            for (int i = 0; i < 60 * 120; i++) w = WindscreenWetness.Step(w, 0.0, 25.0, Dt);
            Assert.That(w, Is.LessThan(0.001));
        }

        [Test]
        public void Standing_StillInDryAir_EvaporatesSlowly()
        {
            double w = WindscreenWetness.Step(1.0, 0.0, 0.0, 60.0);
            Assert.That(w, Is.EqualTo(Math.Exp(-WindscreenWetness.IdleClearingPerSecond * 60.0)).Within(1e-12));
            Assert.That(w, Is.GreaterThan(0.2), "a parked car does not dry in a minute");
        }

        [Test]
        public void Step_BadInputs_LeaveTheWetnessAsItIs()
        {
            Assert.That(WindscreenWetness.Step(0.4, 1.0, 10.0, 0.0), Is.EqualTo(0.4));
            Assert.That(WindscreenWetness.Step(0.4, 1.0, 10.0, -1.0), Is.EqualTo(0.4));
            Assert.That(WindscreenWetness.Step(0.4, 1.0, 10.0, double.NaN), Is.EqualTo(0.4));
            Assert.That(WindscreenWetness.Step(0.4, 1.0, 10.0, double.PositiveInfinity), Is.EqualTo(0.4));
            Assert.That(WindscreenWetness.Step(double.NaN, 0.0, 0.0, 1.0), Is.EqualTo(0.0));
            Assert.That(WindscreenWetness.Equilibrium(double.NaN, 10.0), Is.EqualTo(0.0));
        }

        [Test]
        public void Droplets_AreSeeded_BoundedAndCountClamped()
        {
            WindscreenDroplet[] a = WindscreenDroplets.Generate(42UL, 300);
            WindscreenDroplet[] b = WindscreenDroplets.Generate(42UL, 300);
            WindscreenDroplet[] other = WindscreenDroplets.Generate(43UL, 300);
            Assert.That(a.Length, Is.EqualTo(300));
            Assert.That(WindscreenDroplets.Generate(1UL, -5).Length, Is.EqualTo(0));
            Assert.That(WindscreenDroplets.Generate(1UL, 10_000_000).Length, Is.EqualTo(WindscreenDroplets.MaxCount));

            bool differs = false;
            for (int i = 0; i < a.Length; i++)
            {
                Assert.That(a[i].X, Is.EqualTo(b[i].X), "same seed, same glass");
                Assert.That(a[i].Y, Is.EqualTo(b[i].Y));
                Assert.That(a[i].X, Is.InRange(0.0, 1.0));
                Assert.That(a[i].Y, Is.InRange(0.0, 1.0));
                Assert.That(a[i].Radius, Is.InRange(WindscreenDroplets.MinRadius, WindscreenDroplets.MaxRadius));
                if (a[i].X != other[i].X) differs = true;
            }
            Assert.That(differs, Is.True, "a different seed gives a different layout");
        }

        [Test]
        public void Droplets_MostAreSmall()
        {
            WindscreenDroplet[] drops = WindscreenDroplets.Generate(5UL, 1000);
            int small = 0;
            double midRadius = 0.5 * (WindscreenDroplets.MinRadius + WindscreenDroplets.MaxRadius);
            foreach (WindscreenDroplet d in drops)
            {
                if (d.Radius < midRadius) small++;
            }
            Assert.That(small, Is.GreaterThan(600), "the square bias keeps most drops below the middle radius");
        }
    }
}
