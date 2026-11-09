using System;

namespace Race1.Core
{
    /// <summary>One tyre mark on the road: where it was laid, how dark it is, and how old it is.</summary>
    public struct SkidMark
    {
        /// <summary>Arc length along the route (m).</summary>
        public double S;

        /// <summary>Lateral position across the road (m, positive right).</summary>
        public double T;

        /// <summary>How dark the mark is when laid, 0..1.</summary>
        public double Intensity;

        /// <summary>Seconds since it was laid.</summary>
        public double Age;
    }

    /// <summary>How hard a tyre has to squeal before it leaves a mark, and how the mark fades.</summary>
    public static class SkidMarkModel
    {
        /// <summary>Squeal level where marks start to appear.</summary>
        public const double MarkFrom = 0.2;

        /// <summary>Squeal level where marks are fully dark.</summary>
        public const double MarkFull = 0.8;

        /// <summary>Darkness of a mark from the tyre's squeal level (0..1). Zero below <see cref="MarkFrom"/>.</summary>
        public static double Intensity(double squealLevel)
        {
            if (double.IsNaN(squealLevel)) return 0.0;
            return Mathx.SmoothStep(MarkFrom, MarkFull, Mathx.Clamp01(squealLevel));
        }

        /// <summary>How visible a mark still is: full when laid, fading to nothing at the end of its lifetime.</summary>
        public static double Visibility(SkidMark mark, double lifetimeSeconds)
        {
            if (!(lifetimeSeconds > 0.0) || double.IsNaN(mark.Age)) return 0.0;
            double remaining = 1.0 - Math.Max(0.0, mark.Age) / lifetimeSeconds;
            return Mathx.Clamp01(remaining) * Mathx.Clamp01(mark.Intensity);
        }
    }

    /// <summary>
    /// The tyre marks of one car. Each tyre lays a mark every <c>spacing</c> metres while it squeals, and the newest
    /// marks replace the oldest once the buffer is full. Marks expire after their lifetime. The buffer is fixed-size, so
    /// a long drive never grows the memory or the work per frame.
    /// </summary>
    public sealed class SkidMarks
    {
        private readonly SkidMark[] _marks;
        private readonly double _lifetime;
        private readonly double _spacing;
        private readonly double[] _lastS;
        private readonly bool[] _hasLast;
        private int _head;
        private int _count;

        public SkidMarks(int capacity, double lifetimeSeconds, double spacingM, int tyres = 4)
        {
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
            if (!(lifetimeSeconds > 0.0)) throw new ArgumentOutOfRangeException(nameof(lifetimeSeconds));
            if (!(spacingM > 0.0)) throw new ArgumentOutOfRangeException(nameof(spacingM));
            if (tyres < 1) throw new ArgumentOutOfRangeException(nameof(tyres));

            _marks = new SkidMark[capacity];
            _lifetime = lifetimeSeconds;
            _spacing = spacingM;
            Tyres = tyres;
            _lastS = new double[tyres];
            _hasLast = new bool[tyres];
        }

        public int Capacity => _marks.Length;

        public int Tyres { get; }

        /// <summary>Marks alive now, oldest first.</summary>
        public int Count => _count;

        public SkidMark this[int index]
        {
            get
            {
                if (index < 0 || index >= _count) throw new ArgumentOutOfRangeException(nameof(index));
                return _marks[(_head + index) % _marks.Length];
            }
        }

        public void Clear()
        {
            _head = 0;
            _count = 0;
            Array.Clear(_hasLast, 0, _hasLast.Length);
        }

        /// <summary>
        /// Ages the marks, expires the old ones, and lays new ones for tyres that squeal. <paramref name="s"/>,
        /// <paramref name="t"/> and <paramref name="squeal"/> hold one entry per tyre.
        /// </summary>
        public void Update(double dt, double[] s, double[] t, double[] squeal)
        {
            if (s == null || t == null || squeal == null) throw new ArgumentNullException(nameof(s));
            if (s.Length < Tyres || t.Length < Tyres || squeal.Length < Tyres)
            {
                throw new ArgumentException("one entry per tyre is required");
            }

            double step = dt > 0.0 && !double.IsInfinity(dt) ? dt : 0.0;
            for (int i = 0; i < _count; i++)
            {
                _marks[(_head + i) % _marks.Length].Age += step;
            }
            while (_count > 0 && _marks[_head].Age >= _lifetime)
            {
                _head = (_head + 1) % _marks.Length;
                _count--;
            }

            for (int tyre = 0; tyre < Tyres; tyre++)
            {
                double intensity = SkidMarkModel.Intensity(squeal[tyre]);
                if (intensity <= 0.0 || double.IsNaN(s[tyre]) || double.IsNaN(t[tyre]))
                {
                    _hasLast[tyre] = false;
                    continue;
                }

                // The first mark of a skid is laid at once; the rest follow at the spacing.
                if (!_hasLast[tyre] || Math.Abs(s[tyre] - _lastS[tyre]) >= _spacing)
                {
                    Add(new SkidMark { S = s[tyre], T = t[tyre], Intensity = intensity, Age = 0.0 });
                    _lastS[tyre] = s[tyre];
                    _hasLast[tyre] = true;
                }
            }
        }

        private void Add(SkidMark mark)
        {
            if (_count == _marks.Length)
            {
                // Full: drop the oldest to make room.
                _head = (_head + 1) % _marks.Length;
                _count--;
            }
            _marks[(_head + _count) % _marks.Length] = mark;
            _count++;
        }
    }
}
