using System;
using System.Diagnostics;
using NUnit.Framework;
using Race1.Core;

namespace Race1.Tests.Core
{
    /// <summary>Rough cost of the world-generation primitives that run on the streaming path. Not a benchmark gate.</summary>
    [TestFixture]
    public sealed class PerformanceProbeTests
    {
        [Test]
        public void StreamingPrimitives_CostPerChunkIsReported()
        {
            RoadTrack road = TestWorld.CreateTrack(99);
            var settings = new RoadSettings { Seed = 99 };
            var biomes = new BiomeField(99);
            var terrain = new TerrainField(99, biomes);

            // Warm up, then time 700 natural-height lookups (what one 100 m chunk needs for far rings).
            double sink = 0;
            for (int i = 0; i < 100; i++) sink += terrain.NaturalHeight(i * 3.1, i * 7.7);
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < 700; i++) sink += terrain.NaturalHeight(1000 + i * 2.3, -500 + i * 1.7);
            double naturalMs = sw.Elapsed.TotalMilliseconds;

            // Time generation of 10 km of road samples (cost of extending the road ahead of the car).
            var fresh = TestWorld.CreateTrack(123);
            sw.Restart();
            for (long i = 0; i < 5000; i++) sink += fresh.GetSample(i).HalfWidth;
            double roadMs = sw.Elapsed.TotalMilliseconds;

            TestContext.WriteLine($"natural height x700 (one chunk, far rings): {naturalMs:F2} ms; " +
                                  $"road generation 10 km: {roadMs:F1} ms; sink {sink:F0}");
            Assert.That(settings.SampleSpacing, Is.EqualTo(2.0));
            Assert.That(double.IsFinite(sink));
        }
    }
}
