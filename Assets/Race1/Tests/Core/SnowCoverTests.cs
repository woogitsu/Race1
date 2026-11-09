using System;
using NUnit.Framework;
using Race1.Core;

namespace Race1.Tests.Core
{
    [TestFixture]
    public sealed class SnowCoverTests
    {
        [Test]
        public void Target_IsZeroForNoSnow_OneForHeavySnow_AndRisesBetween()
        {
            Assert.That(SnowCover.Target(0.0), Is.EqualTo(0.0));
            Assert.That(SnowCover.Target(SnowCover.BareBelow), Is.EqualTo(0.0).Within(1e-12));
            Assert.That(SnowCover.Target(SnowCover.FullAbove), Is.EqualTo(1.0).Within(1e-12));
            Assert.That(SnowCover.Target(1.0), Is.EqualTo(1.0));
            Assert.That(SnowCover.Target(double.NaN), Is.EqualTo(0.0));

            double previous = -1.0;
            for (double s = 0.0; s <= 1.0; s += 0.01)
            {
                double t = SnowCover.Target(s);
                Assert.That(t, Is.InRange(0.0, 1.0));
                Assert.That(t, Is.GreaterThanOrEqualTo(previous - 1e-12), $"target fell at snow {s:F2}");
                previous = t;
            }
        }

        [Test]
        public void Step_NeverOvershoots_AndStaysInRange()
        {
            var rng = new Random(3);
            for (int i = 0; i < 2000; i++)
            {
                double current = rng.NextDouble() * 1.4 - 0.2;   // some inputs outside 0..1 on purpose
                double target = rng.NextDouble() * 1.4 - 0.2;
                double dt = rng.NextDouble() * 2.0;
                double next = SnowCover.Step(current, target, dt);
                Assert.That(next, Is.InRange(0.0, 1.0));
                double clampedCurrent = Math.Max(0.0, Math.Min(1.0, current));
                double clampedTarget = Math.Max(0.0, Math.Min(1.0, target));
                double lo = Math.Min(clampedCurrent, clampedTarget), hi = Math.Max(clampedCurrent, clampedTarget);
                Assert.That(next, Is.InRange(lo - 1e-12, hi + 1e-12), $"step {i} overshot");
            }
        }

        [Test]
        public void Step_RespectsTheRiseAndMeltRates()
        {
            double risen = SnowCover.Step(0.0, 1.0, 1.0);
            Assert.That(risen, Is.EqualTo(SnowCover.RiseRatePerSecond).Within(1e-12));

            double melted = SnowCover.Step(1.0, 0.0, 1.0);
            Assert.That(melted, Is.EqualTo(1.0 - SnowCover.MeltRatePerSecond).Within(1e-12));
        }

        [Test]
        public void SnowBuildsUpFasterThanItMelts()
        {
            double secondsToCover = Seconds(0.0, 1.0);
            double secondsToMelt = Seconds(1.0, 0.0);
            TestContext.WriteLine($"full cover in {secondsToCover:F1} s, bare again in {secondsToMelt:F1} s");
            Assert.That(secondsToCover, Is.EqualTo(25.0).Within(0.5));
            Assert.That(secondsToMelt, Is.GreaterThan(secondsToCover));
        }

        [Test]
        public void Step_ConvergesToTheTarget()
        {
            double cover = 0.0;
            for (int i = 0; i < 60 * 120; i++) cover = SnowCover.Step(cover, 0.7, 1.0 / 60.0);
            Assert.That(cover, Is.EqualTo(0.7).Within(1e-9));
        }

        [Test]
        public void Step_ZeroTimeOrBadInputs_LeaveTheCoverUnchanged()
        {
            Assert.That(SnowCover.Step(0.4, 1.0, 0.0), Is.EqualTo(0.4));
            Assert.That(SnowCover.Step(0.4, 1.0, -1.0), Is.EqualTo(0.4));
            Assert.That(SnowCover.Step(0.4, double.NaN, 1.0), Is.EqualTo(0.4));
            Assert.That(SnowCover.Step(0.4, 1.0, double.NaN), Is.EqualTo(0.4));
            Assert.That(SnowCover.Step(0.4, 1.0, double.PositiveInfinity), Is.EqualTo(0.4));
            Assert.That(SnowCover.Step(double.NaN, 0.0, 0.0), Is.EqualTo(0.0));
        }

        /// <summary>Seconds for the cover to travel from <paramref name="from"/> to <paramref name="to"/>.</summary>
        private static double Seconds(double from, double to)
        {
            double cover = from;
            double dt = 1.0 / 60.0;
            int frames = 0;
            while (Math.Abs(cover - to) > 1e-6 && frames < 60 * 600)
            {
                cover = SnowCover.Step(cover, to, dt);
                frames++;
            }
            return frames * dt;
        }
    }
}
