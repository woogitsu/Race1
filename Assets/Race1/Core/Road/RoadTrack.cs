using System;
using System.Collections.Generic;

namespace Race1.Core
{
    /// <summary>
    /// The endless road as seen by the rest of the game. Samples are generated on demand, kept in a
    /// sliding window, and regenerated from checkpoints when an earlier point is requested again
    /// (for example after a rewind, replay or restart with the same seed). The result is identical either way.
    /// </summary>
    public sealed class RoadTrack : IRoadProvider
    {
        private readonly RoadSettings _settings;
        private readonly TerrainField _terrain;
        private readonly BiomeField _biomes;
        private readonly long _checkpointIntervalSamples;
        private readonly List<RoadState> _checkpoints = new List<RoadState>();
        private readonly List<RoadSample> _buffer = new List<RoadSample>();
        private RoadGenerator _generator;
        private long _bufferStart;

        public RoadTrack(RoadSettings settings, TerrainField terrain, BiomeField biomes)
        {
            _settings = settings;
            _terrain = terrain;
            _biomes = biomes;
            _checkpointIntervalSamples = Math.Max(1L, (long)Math.Round(settings.CheckpointSpacing / settings.SampleSpacing));
            RoadState initial = RoadGenerator.CreateInitialState(settings);
            _checkpoints.Add(initial);
            _generator = new RoadGenerator(settings, terrain, biomes, initial);
            _bufferStart = 0;
        }

        public RoadSettings Settings => _settings;
        public double SampleSpacing => _settings.SampleSpacing;
        public int BufferedSampleCount => _buffer.Count;
        public int CheckpointCount => _checkpoints.Count;
        public long GeneratedUpToIndex => _generator.State.Index;

        /// <summary>Road description at arc length s, linearly interpolated between samples.</summary>
        public RoadSample Sample(double s)
        {
            double u = s / _settings.SampleSpacing;
            if (u < 0.0)
            {
                u = 0.0;
            }
            long i = (long)Math.Floor(u);
            double f = u - i;
            RoadSample a = GetSample(i);
            if (f <= 1e-12)
            {
                return a;
            }
            RoadSample b = GetSample(i + 1);
            return RoadSample.Lerp(a, b, f);
        }

        /// <summary>Exact sample at a sample index. Generates or rewinds as needed.</summary>
        public RoadSample GetSample(long index)
        {
            if (index < 0)
            {
                index = 0;
            }
            if (index < _bufferStart)
            {
                Rewind(index);
            }
            while (index >= _bufferStart + _buffer.Count)
            {
                GenerateNext();
            }
            return _buffer[(int)(index - _bufferStart)];
        }

        /// <summary>Generates the road ahead of the given arc length so the physics never waits on it.</summary>
        public void Prefetch(double s, double lookaheadMeters)
        {
            long index = (long)Math.Ceiling((s + lookaheadMeters) / _settings.SampleSpacing);
            if (index < _bufferStart)
            {
                return;
            }
            while (index >= _bufferStart + _buffer.Count)
            {
                GenerateNext();
            }
        }

        /// <summary>Drops samples that are no longer needed. Keeps memory bounded on endless roads.</summary>
        public void TrimBefore(double s)
        {
            long index = (long)Math.Floor(s / _settings.SampleSpacing);
            long limit = Math.Min(index, _generator.State.Index);
            long remove = limit - _bufferStart;
            if (remove <= 0)
            {
                return;
            }
            remove = Math.Min(remove, _buffer.Count);
            _buffer.RemoveRange(0, (int)remove);
            _bufferStart += remove;
        }

        private void GenerateNext()
        {
            long idx = _generator.State.Index;
            if (idx % _checkpointIntervalSamples == 0 && idx / _checkpointIntervalSamples == _checkpoints.Count)
            {
                _checkpoints.Add(_generator.State);
            }
            _buffer.Add(_generator.Next());
        }

        private void Rewind(long index)
        {
            long cp = index / _checkpointIntervalSamples;
            RoadState state = _checkpoints[(int)cp];
            _generator = new RoadGenerator(_settings, _terrain, _biomes, state);
            _buffer.Clear();
            _bufferStart = state.Index;
        }
    }
}
