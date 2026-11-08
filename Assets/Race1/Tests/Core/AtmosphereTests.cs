using System;
using NUnit.Framework;
using Race1.Core;

namespace Race1.Tests.Core
{
    /// <summary>Behavioural checks for the pure C# sky model in <see cref="Atmosphere"/>.</summary>
    [TestFixture]
    public sealed class AtmosphereTests
    {
        [Test]
        public void Noon_ZenithIsBlueDominant()
        {
            SkyLook sky = Atmosphere.Evaluate(Deg(60.0), 0.0, 0.0, 0.0, 2.5);

            Assert.That(sky.Zenith.B, Is.GreaterThan(sky.Zenith.R));
        }

        [Test]
        public void Sunset_SunTintIsRedDominant()
        {
            SkyLook sky = Atmosphere.Evaluate(Deg(0.5), 0.0, 0.0, 0.0, 2.5);

            Assert.That(sky.SunTint.R, Is.GreaterThan(sky.SunTint.B));
        }

        [Test]
        public void StarVisibility_NearOneAtMinus20AndZeroAtPlus10()
        {
            SkyLook night = Atmosphere.Evaluate(Deg(-20.0), 0.0, 0.0, 0.0, 2.5);
            SkyLook day = Atmosphere.Evaluate(Deg(10.0), 0.0, 0.0, 0.0, 2.5);

            Assert.That(night.StarVisibility, Is.EqualTo(1.0).Within(0.01));
            Assert.That(day.StarVisibility, Is.EqualTo(0.0).Within(0.01));
        }

        [Test]
        public void SunIntensity_IsZeroAtMinus20()
        {
            SkyLook sky = Atmosphere.Evaluate(Deg(-20.0), 0.0, 0.0, 0.0, 2.5);

            Assert.That(sky.SunIntensity, Is.EqualTo(0.0).Within(1e-9));
        }

        [Test]
        public void Overcast_LowersSunIntensityComparedWithClear()
        {
            double clear = Atmosphere.Evaluate(Deg(30.0), 0.0, 0.0, 0.0, 2.5).SunIntensity;
            double overcast = Atmosphere.Evaluate(Deg(30.0), 1.0, 0.0, 0.0, 2.5).SunIntensity;

            Assert.That(overcast, Is.LessThan(clear));
        }

        [Test]
        public void Fog_BrightensHorizonInMeanChannel()
        {
            double[] elevations = { -20.0, 0.5, 10.0, 60.0 };
            foreach (double degrees in elevations)
            {
                double clearHorizon = Mean(Atmosphere.Evaluate(Deg(degrees), 0.0, 0.0, 0.0, 2.5).Horizon);
                double foggyHorizon = Mean(Atmosphere.Evaluate(Deg(degrees), 0.0, 0.0, 1.0, 2.5).Horizon);

                Assert.That(foggyHorizon, Is.GreaterThan(clearHorizon), $"fog should brighten the horizon at {degrees} deg");
            }
        }

        [Test]
        public void Sweep_AllFieldsFiniteAndInRange()
        {
            double[] clouds = { 0.0, 0.3, 0.7, 1.0 };
            double[] rains = { 0.0, 0.5, 1.0 };
            double[] fogs = { 0.0, 0.5, 1.0 };
            double[] turbidities = { 1.0, 2.5, 10.0 };

            int badCount = 0;
            string firstBad = null;
            for (int degrees = -90; degrees <= 90; degrees++)
            {
                double elevation = Deg(degrees);
                foreach (double cloud in clouds)
                {
                    foreach (double rain in rains)
                    {
                        foreach (double fog in fogs)
                        {
                            foreach (double turbidity in turbidities)
                            {
                                SkyLook sky = Atmosphere.Evaluate(elevation, cloud, rain, fog, turbidity);
                                string problem = FirstProblem(sky);
                                if (problem != null)
                                {
                                    badCount++;
                                    firstBad ??= $"{problem} at {degrees} deg, cloud {cloud}, rain {rain}, fog {fog}, turbidity {turbidity}";
                                }
                            }
                        }
                    }
                }
            }

            Assert.That(badCount, Is.EqualTo(0), firstBad ?? "no problems");
        }

        private static double Deg(double degrees)
        {
            return degrees * Math.PI / 180.0;
        }

        private static double Mean(LinearRgb c)
        {
            return (c.R + c.G + c.B) / 3.0;
        }

        /// <summary>Returns the name of the first field that is not finite or out of range, or null if all are fine.</summary>
        private static string FirstProblem(SkyLook sky)
        {
            if (!ColourOk(sky.Zenith)) return "Zenith";
            if (!ColourOk(sky.Horizon)) return "Horizon";
            if (!ColourOk(sky.Ground)) return "Ground";
            if (!ColourOk(sky.SunTint)) return "SunTint";
            if (!ColourOk(sky.FogTint)) return "FogTint";
            if (!ColourOk(sky.Ambient)) return "Ambient";
            if (!ScalarOk(sky.SunIntensity)) return "SunIntensity";
            if (!ScalarOk(sky.StarVisibility)) return "StarVisibility";
            if (!ScalarOk(sky.CloudCoverage)) return "CloudCoverage";
            if (!ScalarOk(sky.CloudDensity)) return "CloudDensity";
            if (!ScalarOk(sky.CloudBrightness)) return "CloudBrightness";
            if (!ScalarOk(sky.SunGlow)) return "SunGlow";
            return null;
        }

        private static bool ColourOk(LinearRgb c)
        {
            return InRange(c.R, 0.0, 2.0) && InRange(c.G, 0.0, 2.0) && InRange(c.B, 0.0, 2.0);
        }

        private static bool ScalarOk(double v)
        {
            return InRange(v, 0.0, 1.0);
        }

        private static bool InRange(double v, double lo, double hi)
        {
            return !double.IsNaN(v) && !double.IsInfinity(v) && v >= lo && v <= hi;
        }
    }
}
