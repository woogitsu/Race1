using NUnit.Framework;
using Race1.Core;

namespace Race1.Tests.Core
{
    [TestFixture]
    public sealed class ShorelineTests
    {
        [Test]
        public void Depth_IsZeroAboveTheSea_AndGrowsBelowIt()
        {
            Assert.That(Shoreline.Depth(3.0), Is.EqualTo(0.0));
            Assert.That(Shoreline.Depth(0.0), Is.EqualTo(0.0));
            Assert.That(Shoreline.Depth(-5.0), Is.EqualTo(5.0).Within(1e-12));
            Assert.That(Shoreline.Depth(double.NaN), Is.EqualTo(0.0));
        }

        [Test]
        public void IsWaterAt_NeedsBothCoastAndDepth()
        {
            Assert.That(Shoreline.IsWaterAt(-3.0, 0.6), Is.True, "coast and below the sea");
            Assert.That(Shoreline.IsWaterAt(-3.0, 0.4), Is.False, "below the sea but not coast");
            Assert.That(Shoreline.IsWaterAt(1.0, 0.9), Is.False, "coast but above the sea");
            Assert.That(Shoreline.IsWaterAt(-0.1, 0.9), Is.False, "too shallow to hold water");
            Assert.That(Shoreline.IsWaterAt(double.NaN, 0.9), Is.False);
            Assert.That(Shoreline.IsWaterAt(-3.0, double.NaN), Is.False);
        }

        [Test]
        public void IsWaterAt_CoastThresholdIsInclusive()
        {
            Assert.That(Shoreline.IsWaterAt(-3.0, Shoreline.CoastThreshold), Is.True);
            Assert.That(Shoreline.IsWaterAt(-3.0, Shoreline.CoastThreshold - 1e-9), Is.False);
        }

        [Test]
        public void FoamStrength_IsFullAtTheWaterlineAndFadesToNothing()
        {
            Assert.That(Shoreline.FoamStrength(0.0), Is.EqualTo(1.0).Within(1e-12));
            Assert.That(Shoreline.FoamStrength(Shoreline.FoamBandM), Is.EqualTo(0.0).Within(1e-12));
            Assert.That(Shoreline.FoamStrength(50.0), Is.EqualTo(0.0));
            Assert.That(Shoreline.FoamStrength(double.NaN), Is.EqualTo(0.0));
        }

        [Test]
        public void FoamStrength_DecreasesMonotonicallyWithDepth()
        {
            double previous = double.PositiveInfinity;
            for (double d = 0.0; d <= 6.0; d += 0.1)
            {
                double f = Shoreline.FoamStrength(d);
                Assert.That(f, Is.InRange(0.0, 1.0));
                Assert.That(f, Is.LessThanOrEqualTo(previous + 1e-12), $"foam rose with depth {d:F1}");
                previous = f;
            }
        }

        [Test]
        public void MinimumDepth_IsShallowerThanTheFoamBand()
        {
            Assert.That(Shoreline.MinDepthM, Is.LessThan(Shoreline.FoamBandM));
            Assert.That(Shoreline.FoamStrength(Shoreline.MinDepthM), Is.GreaterThan(0.0), "the first water has visible foam");
        }
    }
}
