using System;
using NUnit.Framework;
using Race1.Core;

namespace Race1.Tests.Core
{
    [TestFixture]
    public sealed class PerformanceBudgetTests
    {
        [Test]
        public void Defaults_HaveOneRowPerTier_AndAreMonotone()
        {
            PerformanceLimits[] table = PerformanceBudget.Defaults();
            Assert.That(table.Length, Is.EqualTo(PerformanceBudget.TierCount));
            Assert.That(PerformanceBudget.TierCount, Is.EqualTo(Enum.GetValues(typeof(QualityTier)).Length));
            Assert.That(PerformanceBudget.IsMonotone(table), Is.True);
        }

        [Test]
        public void Defaults_AreAlreadyInsideTheSafeRanges()
        {
            foreach (PerformanceLimits l in PerformanceBudget.Defaults())
            {
                PerformanceLimits clamped = PerformanceBudget.Sanitize(l);
                Assert.That(clamped.ChunksAhead, Is.EqualTo(l.ChunksAhead));
                Assert.That(clamped.BuildsPerFrame, Is.EqualTo(l.BuildsPerFrame));
                Assert.That(clamped.MaxVegetationInstances, Is.EqualTo(l.MaxVegetationInstances));
                Assert.That(clamped.MaxLitLights, Is.EqualTo(l.MaxLitLights));
                Assert.That(clamped.ShadowDistanceM, Is.EqualTo(l.ShadowDistanceM));
            }
        }

        [Test]
        public void Defaults_MatchTheLimitsTheDirectorUsedBeforeTheBudget()
        {
            PerformanceLimits[] t = PerformanceBudget.Defaults();
            Assert.That(new[] { t[0].ChunksAhead, t[1].ChunksAhead, t[2].ChunksAhead, t[3].ChunksAhead }, Is.EqualTo(new[] { 7, 9, 11, 14 }));
            Assert.That(new[] { t[0].BuildsPerFrame, t[1].BuildsPerFrame, t[2].BuildsPerFrame, t[3].BuildsPerFrame }, Is.EqualTo(new[] { 1, 1, 2, 2 }));
            Assert.That(new[] { t[0].ShadowDistanceM, t[1].ShadowDistanceM, t[2].ShadowDistanceM, t[3].ShadowDistanceM }, Is.EqualTo(new[] { 60f, 120f, 220f, 220f }));
        }

        [Test]
        public void Sanitize_ClampsEveryFieldIntoItsRange()
        {
            var high = PerformanceBudget.Sanitize(new PerformanceLimits
            {
                ChunksAhead = 500, BuildsPerFrame = 99, MaxVegetationInstances = 9_000_000, MaxLitLights = 400, ShadowDistanceM = 5000f,
            });
            Assert.That(high.ChunksAhead, Is.EqualTo(PerformanceBudget.MaxChunksAhead));
            Assert.That(high.BuildsPerFrame, Is.EqualTo(PerformanceBudget.MaxBuildsPerFrame));
            Assert.That(high.MaxVegetationInstances, Is.EqualTo(PerformanceBudget.MaxVegetation));
            Assert.That(high.MaxLitLights, Is.EqualTo(PerformanceBudget.MaxLitLights));
            Assert.That(high.ShadowDistanceM, Is.EqualTo(PerformanceBudget.MaxShadowDistance));

            var low = PerformanceBudget.Sanitize(new PerformanceLimits
            {
                ChunksAhead = -3, BuildsPerFrame = 0, MaxVegetationInstances = 0, MaxLitLights = -1, ShadowDistanceM = float.NaN,
            });
            Assert.That(low.ChunksAhead, Is.EqualTo(PerformanceBudget.MinChunksAhead));
            Assert.That(low.BuildsPerFrame, Is.EqualTo(PerformanceBudget.MinBuildsPerFrame));
            Assert.That(low.MaxVegetationInstances, Is.EqualTo(PerformanceBudget.MinVegetation));
            Assert.That(low.MaxLitLights, Is.EqualTo(PerformanceBudget.MinLitLights));
            Assert.That(low.ShadowDistanceM, Is.EqualTo(PerformanceBudget.MinShadowDistance));
        }

        [Test]
        public void ForTier_ReturnsTheRowForThatTier_OrTheDefaultsWhenTheTableIsBad()
        {
            PerformanceLimits[] table = PerformanceBudget.Defaults();
            table[2].ChunksAhead = 13;
            Assert.That(PerformanceBudget.ForTier(table, QualityTier.High).ChunksAhead, Is.EqualTo(13));

            Assert.That(PerformanceBudget.ForTier(null, QualityTier.High).ChunksAhead, Is.EqualTo(11), "no table: defaults");
            Assert.That(PerformanceBudget.ForTier(new PerformanceLimits[2], QualityTier.Ultra).ChunksAhead, Is.EqualTo(14), "short table: defaults");
        }

        [Test]
        public void IsMonotone_RejectsABudgetThatShrinksAsTheTierRises()
        {
            PerformanceLimits[] table = PerformanceBudget.Defaults();
            table[3].MaxVegetationInstances = table[2].MaxVegetationInstances - 1;
            Assert.That(PerformanceBudget.IsMonotone(table), Is.False);
            Assert.That(PerformanceBudget.IsMonotone(null), Is.False);
        }

        [Test]
        public void Allocate_NeverExceedsTheLimit_AndFavoursTheEarlierGroups()
        {
            var allocated = new int[3];
            DrawBudget.Allocate(new[] { 5, 5, 5 }, 3, 8, allocated);
            Assert.That(allocated, Is.EqualTo(new[] { 5, 3, 0 }));
        }

        [Test]
        public void Allocate_ZeroLimit_DrawsNothing_AndLargeLimitDrawsEverything()
        {
            var allocated = new int[3];
            DrawBudget.Allocate(new[] { 4, 2, 9 }, 3, 0, allocated);
            Assert.That(allocated, Is.EqualTo(new[] { 0, 0, 0 }));

            DrawBudget.Allocate(new[] { 4, 2, 9 }, 3, 1000, allocated);
            Assert.That(allocated, Is.EqualTo(new[] { 4, 2, 9 }));
        }

        [Test]
        public void Allocate_NegativeCountsDrawNothing()
        {
            var allocated = new int[2];
            DrawBudget.Allocate(new[] { -5, 3 }, 2, 10, allocated);
            Assert.That(allocated, Is.EqualTo(new[] { 0, 3 }));
        }

        [Test]
        public void Allocate_RandomInputs_TotalIsTheLesserOfLimitAndDemand()
        {
            var rng = new Random(11);
            for (int trial = 0; trial < 500; trial++)
            {
                int n = rng.Next(0, 30);
                var available = new int[n];
                var allocated = new int[n];
                long demand = 0;
                for (int i = 0; i < n; i++)
                {
                    available[i] = rng.Next(-3, 200);
                    demand += Math.Max(0, available[i]);
                }
                int limit = rng.Next(-5, 3000);
                DrawBudget.Allocate(available, n, limit, allocated);

                long total = 0;
                for (int i = 0; i < n; i++)
                {
                    Assert.That(allocated[i], Is.InRange(0, Math.Max(0, available[i])), $"trial {trial}, group {i}");
                    total += allocated[i];
                }
                Assert.That(total, Is.EqualTo(Math.Min(Math.Max(0, limit), demand)), $"trial {trial}");
            }
        }

        [Test]
        public void Allocate_RejectsCountsBeyondTheArrays()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => DrawBudget.Allocate(new[] { 1 }, 2, 5, new[] { 0, 0 }));
            Assert.Throws<ArgumentOutOfRangeException>(() => DrawBudget.Allocate(new[] { 1, 2 }, 2, 5, new[] { 0 }));
        }
    }
}
