using System;
using System.Collections.Generic;
using NUnit.Framework;
using Race1.Core;

namespace Race1.Tests.Core
{
    [TestFixture]
    public sealed class LampBudgetTests
    {
        private static readonly Vector3d Origin = new Vector3d(0.0, 0.0, 0.0);

        [Test]
        public void Update_LightsExactlyTheNearestLampsUpToTheLimit()
        {
            Vector3d[] lamps = LineOfLamps(20, 30.0);
            Vector3d viewer = new Vector3d(100.0, 0.0, 0.0);
            var budget = new LampBudget();
            budget.Update(lamps, lamps.Length, viewer, 8);

            List<int> expected = NearestIndices(lamps, viewer, 8);
            Assert.That(budget.LitCount, Is.EqualTo(8));
            for (int i = 0; i < lamps.Length; i++)
            {
                Assert.That(budget.IsLit(i), Is.EqualTo(expected.Contains(i)), $"lamp {i}");
            }
        }

        [Test]
        public void Update_LightsEveryLampWhenThereAreFewerThanTheLimit()
        {
            Vector3d[] lamps = LineOfLamps(3, 30.0);
            var budget = new LampBudget();
            budget.Update(lamps, lamps.Length, Origin, 8);

            Assert.That(budget.LitCount, Is.EqualTo(3));
            for (int i = 0; i < 3; i++) Assert.That(budget.IsLit(i), Is.True);
        }

        [Test]
        public void Update_WithNoLimit_LightsNothing()
        {
            Vector3d[] lamps = LineOfLamps(20, 30.0);
            var budget = new LampBudget();
            budget.Update(lamps, lamps.Length, Origin, 0);
            Assert.That(budget.LitCount, Is.EqualTo(0));

            budget.Update(lamps, lamps.Length, Origin, -3);
            Assert.That(budget.LitCount, Is.EqualTo(0));
            for (int i = 0; i < lamps.Length; i++) Assert.That(budget.IsLit(i), Is.False);
        }

        [Test]
        public void Update_CountBeyondTheArrayIsClamped()
        {
            Vector3d[] lamps = LineOfLamps(4, 30.0);
            var budget = new LampBudget();
            budget.Update(lamps, 100, Origin, 8);
            Assert.That(budget.LitCount, Is.EqualTo(4));
            Assert.That(budget.IsLit(4), Is.False, "index past the count is never lit");
        }

        [Test]
        public void Update_LitLampStaysLitUntilAnUnlitOneIsClearlyNearer()
        {
            var budget = new LampBudget();

            // Lamp 0 at 20 m, lamp 1 at 20.5 m from the viewer: lamp 0 is lit.
            var pair = new[] { new Vector3d(10.0, 0.0, 0.0), new Vector3d(9.5, 0.0, 0.0) };
            budget.Update(pair, 2, new Vector3d(30.0, 0.0, 0.0), 1);
            Assert.That(budget.IsLit(0), Is.True);
            Assert.That(budget.IsLit(1), Is.False);

            // Lamp 1 is now 5% nearer (9.5 m against 10 m). That is inside the band, so lamp 0 stays lit.
            budget.Update(pair, 2, Origin, 1);
            Assert.That(budget.IsLit(0), Is.True, "lit lamp dropped inside the hysteresis band");
            Assert.That(budget.IsLit(1), Is.False);

            // Lamp 1 at 8 m is 20% nearer than lamp 0 at 10 m: it takes over.
            var overtaken = new[] { new Vector3d(10.0, 0.0, 0.0), new Vector3d(8.0, 0.0, 0.0) };
            budget.Update(overtaken, 2, Origin, 1);
            Assert.That(budget.IsLit(1), Is.True);
            Assert.That(budget.IsLit(0), Is.False);
        }

        [Test]
        public void Sweep_AlongARow_NeverExceedsTheLimit_AndEachLampSwitchesAtMostTwice()
        {
            Vector3d[] lamps = LineOfLamps(60, 30.0);
            var budget = new LampBudget();
            var before = new bool[lamps.Length];
            var switches = new int[lamps.Length];

            for (int step = 0; step <= 1800; step++)
            {
                budget.Update(lamps, lamps.Length, new Vector3d(step, 0.0, 0.0), 8);
                int lit = 0;
                for (int i = 0; i < lamps.Length; i++)
                {
                    bool now = budget.IsLit(i);
                    if (now) lit++;
                    if (now != before[i])
                    {
                        switches[i]++;
                        before[i] = now;
                    }
                }
                Assert.That(lit, Is.EqualTo(8), $"lit count at step {step}");
            }

            int worst = 0;
            foreach (int s in switches) worst = Math.Max(worst, s);
            TestContext.WriteLine($"most switches for one lamp while passing {lamps.Length} lamps: {worst}");
            Assert.That(worst, Is.LessThanOrEqualTo(2), "a lamp flickers while the car drives past it");
        }

        private static Vector3d[] LineOfLamps(int count, double spacing)
        {
            var lamps = new Vector3d[count];
            for (int i = 0; i < count; i++) lamps[i] = new Vector3d(i * spacing, 9.0, 0.0);
            return lamps;
        }

        private static List<int> NearestIndices(Vector3d[] lamps, Vector3d viewer, int n)
        {
            var indices = new List<int>();
            for (int i = 0; i < lamps.Length; i++) indices.Add(i);
            indices.Sort((a, b) =>
            {
                int c = Squared(lamps[a], viewer).CompareTo(Squared(lamps[b], viewer));
                return c != 0 ? c : a.CompareTo(b);
            });
            return indices.GetRange(0, Math.Min(n, indices.Count));
        }

        private static double Squared(Vector3d p, Vector3d v)
        {
            double dx = p.X - v.X, dy = p.Y - v.Y, dz = p.Z - v.Z;
            return dx * dx + dy * dy + dz * dz;
        }
    }
}
