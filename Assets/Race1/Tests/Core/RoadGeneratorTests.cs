using System;
using System.Collections.Generic;
using NUnit.Framework;
using Race1.Core;

namespace Race1.Tests.Core
{
    [TestFixture]
    public sealed class RoadGeneratorTests
    {
        private const double Spacing = 2.0;

        [Test]
        public void SameSeed_ProducesIdenticalSamples()
        {
            RoadTrack a = TestWorld.CreateTrack(42);
            RoadTrack b = TestWorld.CreateTrack(42);
            for (long i = 0; i < 20000; i++)
            {
                RoadSample sa = a.GetSample(i);
                RoadSample sb = b.GetSample(i);
                Check.AreEqual(sa.Position.X, sb.Position.X, 0.0, $"X differs at {i}");
                Check.AreEqual(sa.Position.Y, sb.Position.Y, 0.0, $"Y differs at {i}");
                Check.AreEqual(sa.Position.Z, sb.Position.Z, 0.0, $"Z differs at {i}");
                Check.AreEqual(sa.Curvature, sb.Curvature, 0.0);
            }
        }

        [Test]
        public void DifferentSeeds_ProduceDifferentRoads()
        {
            RoadTrack a = TestWorld.CreateTrack(1);
            RoadTrack b = TestWorld.CreateTrack(2);
            RoadSample sa = a.GetSample(15000);
            RoadSample sb = b.GetSample(15000);
            Assert.That(Vector3d.Distance(sa.Position, sb.Position), Is.GreaterThan(1.0));
        }

        [Test]
        public void ConsecutiveSamples_AreConnectedAtExactSpacing()
        {
            RoadTrack road = TestWorld.CreateTrack(7);
            RoadSample prev = road.GetSample(0);
            for (long i = 1; i < 30000; i++)
            {
                RoadSample cur = road.GetSample(i);
                double chord = Vector3d.Distance(prev.Position, cur.Position);
                Assert.That(double.IsNaN(chord) == false, $"NaN at {i}");
                Check.AreEqual(Spacing, chord, 1e-6, $"gap at sample {i}");
                prev = cur;
            }
        }

        [Test]
        public void Curvature_StaysWithinDrivableLimits()
        {
            RoadTrack road = TestWorld.CreateTrack(11);
            double maxAbs = 0.0;
            double maxRate = 0.0;
            RoadSample prev = road.GetSample(0);
            for (long i = 1; i < 50000; i++)
            {
                RoadSample cur = road.GetSample(i);
                maxAbs = Math.Max(maxAbs, Math.Abs(cur.Curvature));
                maxRate = Math.Max(maxRate, Math.Abs(cur.Curvature - prev.Curvature) / Spacing);
                prev = cur;
            }
            Assert.That(maxAbs, Is.LessThanOrEqualTo(1.0 / 28.0 + 1e-12), "tighter than the 28 m design limit");
            Assert.That(maxRate, Is.LessThanOrEqualTo(RoadSettings.Default.MaxCurvatureRate + 1e-9), "curvature changes too abruptly");
        }

        [Test]
        public void Elevation_HasNoJumpsAndRespectsGrade()
        {
            RoadTrack road = TestWorld.CreateTrack(19);
            double maxPitch = 0.0;
            double maxDy = 0.0;
            RoadSample prev = road.GetSample(0);
            for (long i = 1; i < 50000; i++)
            {
                RoadSample cur = road.GetSample(i);
                maxPitch = Math.Max(maxPitch, Math.Abs(cur.Pitch));
                maxDy = Math.Max(maxDy, Math.Abs(cur.Position.Y - prev.Position.Y));
                prev = cur;
            }
            Assert.That(maxPitch, Is.LessThanOrEqualTo(Math.Atan(0.10) + 1e-9), "grade above 10%");
            Assert.That(maxDy, Is.LessThan(Spacing * Math.Sin(Math.Atan(0.10)) + 1e-6), "vertical step between samples");
        }

        [Test]
        public void Bank_IsContinuousAndBounded()
        {
            RoadTrack road = TestWorld.CreateTrack(23);
            double maxBank = 0.0;
            double maxStep = 0.0;
            RoadSample prev = road.GetSample(0);
            for (long i = 1; i < 50000; i++)
            {
                RoadSample cur = road.GetSample(i);
                maxBank = Math.Max(maxBank, Math.Abs(cur.Bank));
                maxStep = Math.Max(maxStep, Math.Abs(cur.Bank - prev.Bank));
                prev = cur;
            }
            Assert.That(maxBank, Is.LessThanOrEqualTo(RoadSettings.Default.MaxBank + 1e-12));
            // Bank follows curvature, so its per-sample change is bounded by the curvature rate limit.
            double bankPerSample = RoadSettings.Default.ReferenceSpeed * RoadSettings.Default.ReferenceSpeed / Mathx.Gravity
                                   * RoadSettings.Default.MaxCurvatureRate * Spacing;
            Assert.That(maxStep, Is.LessThanOrEqualTo(bankPerSample * 1.01), "superelevation jumps");
        }

        [Test]
        public void Hundred_Kilometres_StaysConnectedAndBounded()
        {
            RoadTrack road = TestWorld.CreateTrack(20260101);
            long samples = (long)(100000.0 / Spacing);
            RoadSample first = road.GetSample(0);
            RoadSample last = road.GetSample(samples);

            double maxHeading = 0.0;
            double maxAltitude = double.MinValue;
            double minAltitude = double.MaxValue;
            RoadSample prev = first;
            for (long i = 1; i <= samples; i++)
            {
                RoadSample cur = road.GetSample(i);
                Assert.That(double.IsFinite(cur.Position.X) && double.IsFinite(cur.Position.Z), $"non-finite at {i}");
                Check.AreEqual(Spacing, Vector3d.Distance(prev.Position, cur.Position), 1e-6);
                maxHeading = Math.Max(maxHeading, Math.Abs(cur.Heading));
                maxAltitude = Math.Max(maxAltitude, cur.Position.Y);
                minAltitude = Math.Min(minAltitude, cur.Position.Y);
                if (i % 5000 == 0) road.TrimBefore(cur.S - 500.0);
                prev = cur;
            }

            // One design segment can overshoot the cone by at most its turn budget.
            Assert.That(maxHeading, Is.LessThanOrEqualTo(RoadSettings.Default.MaxHeadingDrift + RoadSettings.Default.MaxTurnPerKnot + 0.1),
                "heading drift escaped the cone, road may loop");
            Assert.That(last.Position.Length, Is.GreaterThan(30000.0),
                "100 km of road should move far from the origin");
            Assert.That(minAltitude, Is.GreaterThan(RoadSettings.Default.AltitudeFloor - 250.0));
            Assert.That(maxAltitude, Is.LessThan(RoadSettings.Default.AltitudeCeiling + 250.0));
        }

        [Test]
        public void Rewind_RegeneratesIdenticalSamples()
        {
            RoadTrack road = TestWorld.CreateTrack(31);
            RoadSample atFirstVisit = road.GetSample(40000);
            RoadSample early = road.GetSample(1000);   // rewinds through a checkpoint
            RoadSample atSecondVisit = road.GetSample(40000);

            Check.AreEqual(atFirstVisit.Position.X, atSecondVisit.Position.X, 0.0);
            Check.AreEqual(atFirstVisit.Position.Z, atSecondVisit.Position.Z, 0.0);
            Check.AreEqual(atFirstVisit.Heading, atSecondVisit.Heading, 0.0);
            Check.AreEqual(atFirstVisit.Pitch, atSecondVisit.Pitch, 0.0);
            Check.AreEqual(early.Position.X, road.GetSample(1000).Position.X, 0.0);
        }

        [Test]
        public void SlidingWindow_KeepsMemoryBounded()
        {
            RoadTrack road = TestWorld.CreateTrack(37);
            int maxBuffered = 0;
            for (long i = 0; i < 60000; i += 25)
            {
                road.GetSample(i);
                road.TrimBefore(i * Spacing - 300.0);
                maxBuffered = Math.Max(maxBuffered, road.BufferedSampleCount);
            }
            Assert.That(maxBuffered, Is.LessThan(1200), "buffer grows with distance travelled");
            Assert.That(road.CheckpointCount, Is.GreaterThan(10));
        }

        [Test]
        public void Biomes_ChangeGraduallyAlongTheRoad()
        {
            RoadTrack road = TestWorld.CreateTrack(41);
            var seen = new HashSet<BiomeKind>();
            for (long i = 0; i < 100000; i += 10)
            {
                seen.Add(road.GetSample(i).Biome);
            }
            Assert.That(seen.Count, Is.GreaterThanOrEqualTo(3), "a 200 km road should cross several landscapes");
        }

        [Test]
        public void SampleFrames_AreOrthonormal()
        {
            RoadTrack road = TestWorld.CreateTrack(43);
            for (long i = 0; i < 5000; i += 37)
            {
                RoadSample s = road.GetSample(i);
                Vector3d f = s.Forward;
                Vector3d r = s.Right;
                Vector3d u = s.Up;
                Check.AreEqual(1.0, f.Length, 1e-9);
                Check.AreEqual(1.0, r.Length, 1e-9);
                Check.AreEqual(1.0, u.Length, 1e-9);
                Check.AreEqual(0.0, Vector3d.Dot(f, r), 1e-9);
                Check.AreEqual(0.0, Vector3d.Dot(f, u), 1e-9);
                Check.AreEqual(0.0, Vector3d.Dot(r, u), 1e-9);
            }
        }

        [Test]
        public void TerrainBlend_IsContinuousAtRoadEdgeAndNaturalFarAway()
        {
            RoadTrack road = TestWorld.CreateTrack(53);
            RoadSample s = road.GetSample(9000);
            double edge = s.HalfWidth + s.Shoulder;
            double natural = 37.0;

            double onEdge = RoadTerrainBlend.GroundHeight(s, edge - 1e-6, natural);
            double pastEdge = RoadTerrainBlend.GroundHeight(s, edge + 1e-6, natural);
            Check.AreEqual(onEdge, pastEdge, 1e-3, "terrain steps at the shoulder edge");

            double far = RoadTerrainBlend.GroundHeight(s, edge + RoadTerrainBlend.FalloffMeters + 50.0, natural);
            Check.AreEqual(natural, far, 1e-9, "far from the road the ground must be natural");

            double centre = RoadTerrainBlend.GroundHeight(s, 0.0, natural);
            Check.AreEqual(s.PointAt(0.0).Y, centre, 1e-9, "carriageway must match the road exactly");
        }

        [Test]
        public void InterpolatedSample_MatchesExactSampleAtIndex()
        {
            RoadTrack road = TestWorld.CreateTrack(47);
            RoadSample exact = road.GetSample(777);
            RoadSample interp = road.Sample(777 * Spacing);
            Check.AreEqual(exact.Position.X, interp.Position.X, 1e-9);
            Check.AreEqual(exact.Position.Z, interp.Position.Z, 1e-9);
        }
    }
}
