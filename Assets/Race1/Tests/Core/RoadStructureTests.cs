using System;
using NUnit.Framework;
using Race1.Core;

namespace Race1.Tests.Core
{
    [TestFixture]
    public sealed class RoadStructureTests
    {
        private const long SampleCount = 25000; // 50 km at 2 m spacing

        [Test]
        public void TunnelsAndBridges_AppearOnGeneratedRoads()
        {
            int tunnels = 0;
            int bridges = 0;
            foreach (ulong seed in new ulong[] { 1, 2, 3, 4 })
            {
                RoadTrack road = TestWorld.CreateTrack(seed);
                for (long i = 0; i < SampleCount; i++)
                {
                    RoadSample s = road.GetSample(i);
                    if (s.Tunnel) tunnels++;
                    if (s.Bridge) bridges++;
                }
            }
            Assert.That(tunnels, Is.GreaterThan(0), "no tunnel on four 50 km roads");
            Assert.That(bridges, Is.GreaterThan(0), "no bridge on four 50 km roads");
        }

        [Test]
        public void StructureRuns_LastAtLeastTheMinimumLength()
        {
            // The sustained-length rule: a flag only switches after its condition holds for StructureMinLength (60 m).
            int minSamples = (int)Math.Round(RoadSettings.Default.StructureMinLength / RoadSettings.Default.SampleSpacing);
            foreach (ulong seed in new ulong[] { 1, 5, 20260101 })
            {
                RoadTrack road = TestWorld.CreateTrack(seed);
                bool tunnelPrev = false, bridgePrev = false;
                long tunnelStart = -1, bridgeStart = -1;
                for (long i = 0; i < SampleCount; i++)
                {
                    RoadSample s = road.GetSample(i);
                    if (s.Tunnel != tunnelPrev)
                    {
                        if (s.Tunnel) tunnelStart = i;
                        else if (tunnelStart > 0) Assert.That(i - tunnelStart, Is.GreaterThanOrEqualTo(minSamples - 1), $"seed {seed}: tunnel at {tunnelStart} too short");
                        tunnelPrev = s.Tunnel;
                    }
                    if (s.Bridge != bridgePrev)
                    {
                        if (s.Bridge) bridgeStart = i;
                        else if (bridgeStart > 0) Assert.That(i - bridgeStart, Is.GreaterThanOrEqualTo(minSamples - 1), $"seed {seed}: bridge at {bridgeStart} too short");
                        bridgePrev = s.Bridge;
                    }
                }
            }
        }

        [Test]
        public void StructureFlags_AreRareEnoughToBeEvents()
        {
            // Fewer than one switch per kilometre on average. Flicker would produce many more switches.
            foreach (ulong seed in new ulong[] { 2, 4, 20260101 })
            {
                RoadTrack road = TestWorld.CreateTrack(seed);
                int switches = 0;
                bool tunnelPrev = false, bridgePrev = false;
                for (long i = 0; i < SampleCount; i++)
                {
                    RoadSample s = road.GetSample(i);
                    if (s.Tunnel != tunnelPrev) switches++;
                    if (s.Bridge != bridgePrev) switches++;
                    tunnelPrev = s.Tunnel;
                    bridgePrev = s.Bridge;
                }
                Assert.That(switches, Is.LessThan(50 * 2), $"seed {seed}: {switches} structure switches in 50 km");
            }
        }

        [Test]
        public void StructureFlags_AreDeterministicAndSurviveRewinds()
        {
            RoadTrack a = TestWorld.CreateTrack(3);
            RoadTrack b = TestWorld.CreateTrack(3);
            bool[] tunnelA = new bool[SampleCount];
            bool[] bridgeA = new bool[SampleCount];
            for (long i = 0; i < SampleCount; i++)
            {
                RoadSample s = a.GetSample(i);
                tunnelA[i] = s.Tunnel;
                bridgeA[i] = s.Bridge;
            }
            // Revisit a late point first, so the other track rewinds through checkpoints.
            b.GetSample(SampleCount - 1);
            for (long i = 0; i < SampleCount; i++)
            {
                RoadSample s = b.GetSample(i);
                Assert.That(s.Tunnel, Is.EqualTo(tunnelA[i]), $"tunnel flag differs at {i}");
                Assert.That(s.Bridge, Is.EqualTo(bridgeA[i]), $"bridge flag differs at {i}");
            }
        }

        [Test]
        public void Interpolation_TakesTheNearestSampleFlag()
        {
            var near = new RoadSample { S = 0, HalfWidth = 3.5, Shoulder = 1.5, Tunnel = true, Bridge = false };
            var far = new RoadSample { S = 2, HalfWidth = 3.5, Shoulder = 1.5, Tunnel = false, Bridge = true };
            RoadSample low = RoadSample.Lerp(near, far, 0.2);
            RoadSample high = RoadSample.Lerp(near, far, 0.8);
            Assert.That(low.Tunnel, Is.True);
            Assert.That(low.Bridge, Is.False);
            Assert.That(high.Tunnel, Is.False);
            Assert.That(high.Bridge, Is.True);
        }
    }
}
