using System;
using System.Collections.Generic;

namespace Race1.Core
{
    /// <summary>
    /// Deterministic weather over game time. The weather is a Markov chain: every segment lasts a few minutes and
    /// changes to a neighbouring kind (clear turns cloudy, rain turns to storm, and so on). The same seed always
    /// gives the same sequence, so a route and its weather can be replayed. Parameters are not switched here:
    /// <see cref="WeatherState"/> already blends from one preset to the next, so the schedule only decides kinds.
    /// </summary>
    public sealed class WeatherSchedule
    {
        private struct Segment
        {
            public double Start;
            public double End;
            public WeatherKind Kind;
        }

        // Row = current kind, column = next kind. Order: Clear, Cloudy, Rain, Storm, Fog, Snow.
        // The diagonal is zero: a segment always ends in a different kind. Storm never clears straight to sun.
        private static readonly double[,] Transitions =
        {
            // Clear  Cloudy  Rain  Storm  Fog  Snow
            { 0.00, 0.80, 0.00, 0.00, 0.20, 0.00 }, // Clear
            { 0.45, 0.00, 0.35, 0.05, 0.10, 0.05 }, // Cloudy
            { 0.00, 0.60, 0.00, 0.30, 0.10, 0.00 }, // Rain
            { 0.00, 0.30, 0.70, 0.00, 0.00, 0.00 }, // Storm
            { 0.60, 0.40, 0.00, 0.00, 0.00, 0.00 }, // Fog
            { 0.00, 0.70, 0.00, 0.00, 0.30, 0.00 }, // Snow
        };

        private readonly List<Segment> _segments = new List<Segment>();
        private readonly ulong _seed;
        private readonly bool _allowSnow;
        private readonly double _minSegment;
        private readonly double _maxSegment;

        /// <param name="seed">World seed. The same seed gives the same weather.</param>
        /// <param name="allowSnow">Snow only happens in winter routes.</param>
        /// <param name="startKind">Kind of the first segment.</param>
        /// <param name="minSegmentSeconds">Shortest segment, in game seconds.</param>
        /// <param name="maxSegmentSeconds">Longest segment, in game seconds.</param>
        public WeatherSchedule(ulong seed, bool allowSnow = false, WeatherKind startKind = WeatherKind.Clear,
            double minSegmentSeconds = 240.0, double maxSegmentSeconds = 600.0)
        {
            if (!(minSegmentSeconds > 0.0) || maxSegmentSeconds < minSegmentSeconds)
                throw new ArgumentOutOfRangeException(nameof(maxSegmentSeconds));
            _seed = seed;
            _allowSnow = allowSnow;
            _minSegment = minSegmentSeconds;
            _maxSegment = maxSegmentSeconds;
            if (!allowSnow && startKind == WeatherKind.Snow) startKind = WeatherKind.Cloudy;
            AppendSegment(0.0, startKind, 0);
        }

        /// <summary>Weather kind at game time <paramref name="t"/> (seconds, t &gt;= 0).</summary>
        public WeatherKind KindAt(double t)
        {
            return SegmentAt(t).Kind;
        }

        /// <summary>When the segment containing <paramref name="t"/> ends (game seconds).</summary>
        public double SegmentEndAt(double t)
        {
            return SegmentAt(t).End;
        }

        /// <summary>Sets the weather state's target kind for game time <paramref name="t"/>. Parameters blend on their own.</summary>
        public void Apply(WeatherState state, double t)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            WeatherKind kind = KindAt(t);
            if (state.Kind != kind) state.SetKind(kind);
        }

        /// <summary>Time of day in hours (0..24) for game time <paramref name="t"/>, starting at <paramref name="startHour"/>.</summary>
        public static double HourAt(double t, double startHour, double dayLengthSeconds)
        {
            if (!(dayLengthSeconds > 0.0)) throw new ArgumentOutOfRangeException(nameof(dayLengthSeconds));
            double hour = (startHour + t / dayLengthSeconds * 24.0) % 24.0;
            if (hour < 0.0) hour += 24.0;
            return hour;
        }

        private Segment SegmentAt(double t)
        {
            if (t < 0.0) t = 0.0;
            while (_segments[_segments.Count - 1].End <= t)
            {
                Segment last = _segments[_segments.Count - 1];
                WeatherKind next = PickNext(last.Kind, _segments.Count);
                AppendSegment(last.End, next, _segments.Count);
            }

            // Segments are in time order, so a binary search finds the one that contains t.
            int lo = 0;
            int hi = _segments.Count - 1;
            while (lo < hi)
            {
                int mid = (lo + hi) / 2;
                if (_segments[mid].End <= t) lo = mid + 1;
                else hi = mid;
            }
            return _segments[lo];
        }

        private void AppendSegment(double start, WeatherKind kind, int index)
        {
            var rng = new DeterministicRng(DeterministicRng.Combine(_seed, 0xC1EA7E000UL + (ulong)index));
            double duration = rng.Range(_minSegment, _maxSegment);
            _segments.Add(new Segment { Start = start, End = start + duration, Kind = kind });
        }

        private WeatherKind PickNext(WeatherKind current, int index)
        {
            int row = (int)current;
            double sum = 0.0;
            for (int col = 0; col < 6; col++)
            {
                if (!_allowSnow && col == (int)WeatherKind.Snow) continue;
                sum += Transitions[row, col];
            }
            if (sum <= 0.0) return WeatherKind.Cloudy;

            var rng = new DeterministicRng(DeterministicRng.Combine(_seed, 0x7A4D0FF000UL + (ulong)index));
            double pick = rng.NextDouble() * sum;
            WeatherKind chosen = WeatherKind.Cloudy;
            for (int col = 0; col < 6; col++)
            {
                if (!_allowSnow && col == (int)WeatherKind.Snow) continue;
                double w = Transitions[row, col];
                if (w <= 0.0) continue;
                chosen = (WeatherKind)col;
                pick -= w;
                if (pick < 0.0) break;
            }
            return chosen;
        }
    }
}
