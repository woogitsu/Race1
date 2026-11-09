using System;
using NUnit.Framework;
using Race1.Core;

namespace Race1.Tests.Core
{
    [TestFixture]
    public sealed class WetReflectionTests
    {
        private static double Deg(double d) => d * Math.PI / 180.0;

        [Test]
        public void DryRoad_HasNoReflection()
        {
            Assert.That(WetReflection.Strength(0.0, Deg(20.0), 0.0), Is.EqualTo(0.0));
            Assert.That(WetReflection.Strength(WetReflection.FilmFrom, Deg(20.0), 0.0), Is.EqualTo(0.0).Within(1e-12));
            Assert.That(WetReflection.Strength(double.NaN, Deg(20.0), 0.0), Is.EqualTo(0.0));
        }

        [Test]
        public void Reflection_RisesWithWetness_AndReachesItsMaximumWhenTheFilmIsComplete()
        {
            double previous = -1.0;
            for (double w = 0.0; w <= 1.0; w += 0.02)
            {
                double r = WetReflection.Strength(w, Deg(20.0), 0.0);
                Assert.That(r, Is.InRange(0.0, 1.0));
                Assert.That(r, Is.GreaterThanOrEqualTo(previous - 1e-12), $"reflection fell at wetness {w:F2}");
                previous = r;
            }
            Assert.That(WetReflection.Strength(WetReflection.FilmFull, Deg(20.0), 0.0),
                Is.EqualTo(WetReflection.Strength(1.0, Deg(20.0), 0.0)).Within(1e-12));
        }

        [Test]
        public void LowSun_ReflectsMoreThanHighSun()
        {
            double low = WetReflection.Strength(1.0, Deg(4.0), 0.0);
            double high = WetReflection.Strength(1.0, Deg(70.0), 0.0);
            Assert.That(low, Is.GreaterThan(high));
            Assert.That(high, Is.EqualTo(1.0 - WetReflection.HighSunReduction).Within(1e-9));
        }

        [Test]
        public void SunBelowTheHorizon_IsTheLowestSun()
        {
            Assert.That(WetReflection.Strength(1.0, Deg(-10.0), 0.0), Is.EqualTo(WetReflection.Strength(1.0, Deg(2.0), 0.0)).Within(1e-12));
        }

        [Test]
        public void ElevationFalls_MonotonicallyBetweenTheLimits()
        {
            double previous = double.PositiveInfinity;
            for (double e = 0.0; e <= 70.0; e += 1.0)
            {
                double r = WetReflection.Strength(1.0, Deg(e), 0.0);
                Assert.That(r, Is.LessThanOrEqualTo(previous + 1e-12), $"reflection rose at {e} deg");
                previous = r;
            }
        }

        [Test]
        public void Night_ReducesReflectionOnlySlightly()
        {
            double day = WetReflection.Strength(1.0, Deg(20.0), 0.0);
            double night = WetReflection.Strength(1.0, Deg(20.0), 1.0);
            Assert.That(night, Is.LessThan(day));
            Assert.That(night, Is.EqualTo(day * (1.0 - WetReflection.NightReduction)).Within(1e-12));
            Assert.That(night, Is.GreaterThan(0.0), "wet roads still reflect the lamps at night");
        }

        [Test]
        public void Output_StaysInRange_ForAnyInput()
        {
            var rng = new Random(21);
            for (int i = 0; i < 3000; i++)
            {
                double w = rng.NextDouble() * 3.0 - 1.0;
                double elev = rng.NextDouble() * 4.0 - 2.0;
                double night = rng.NextDouble() * 3.0 - 1.0;
                Assert.That(WetReflection.Strength(w, elev, night), Is.InRange(0.0, 1.0), $"case {i}");
            }
            Assert.That(WetReflection.Strength(1.0, double.NaN, 0.0), Is.InRange(0.0, 1.0));
            Assert.That(WetReflection.Strength(1.0, Deg(20.0), double.NaN), Is.InRange(0.0, 1.0));
        }
    }
}
