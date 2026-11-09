using System;

namespace Race1.Core
{
    /// <summary>
    /// Chooses which street lamps may use a real light source. At most a fixed number are lit at once, and the
    /// choice is sticky: a lit lamp stays lit until an unlit lamp is clearly nearer (by <see cref="Hysteresis"/>
    /// in distance), so lights do not pop on and off while the car moves. Deterministic: equal distances go to the
    /// lower index.
    /// </summary>
    public sealed class LampBudget
    {
        /// <summary>A lit lamp is kept unless an unlit one is this many times nearer.</summary>
        public const double Hysteresis = 1.15;

        private bool[] _lit = new bool[0];
        private bool[] _next = new bool[0];
        private double[] _keys = new double[0];
        private int _count;

        /// <summary>Number of lamps lit by the last <see cref="Update"/>.</summary>
        public int LitCount { get; private set; }

        /// <summary>True when the lamp at <paramref name="index"/> is lit by the last <see cref="Update"/>.</summary>
        public bool IsLit(int index) => index >= 0 && index < _count && _lit[index];

        /// <summary>
        /// Recomputes the lit set for the first <paramref name="count"/> lamps, seen from <paramref name="viewer"/>.
        /// At most <paramref name="limit"/> lamps are lit; a limit of zero or less lights none.
        /// Positions and viewer must share one coordinate frame.
        /// </summary>
        public void Update(Vector3d[] positions, int count, Vector3d viewer, int limit)
        {
            if (positions == null) throw new ArgumentNullException(nameof(positions));
            count = Math.Max(0, Math.Min(count, positions.Length));
            EnsureCapacity(count);
            int previousCount = _count;
            int target = Math.Min(Math.Max(limit, 0), count);

            // A lit lamp is compared as if it were closer by the hysteresis factor.
            for (int i = 0; i < count; i++)
            {
                double dx = positions[i].X - viewer.X;
                double dy = positions[i].Y - viewer.Y;
                double dz = positions[i].Z - viewer.Z;
                double d2 = dx * dx + dy * dy + dz * dz;
                bool wasLit = i < previousCount && _lit[i];
                _keys[i] = wasLit ? d2 / (Hysteresis * Hysteresis) : d2;
                _next[i] = false;
            }

            // Pick the nearest keys one at a time. The picked lamps are flagged in _next.
            for (int k = 0; k < target; k++)
            {
                int best = -1;
                double bestKey = double.PositiveInfinity;
                for (int i = 0; i < count; i++)
                {
                    if (!_next[i] && _keys[i] < bestKey)
                    {
                        bestKey = _keys[i];
                        best = i;
                    }
                }
                if (best < 0) break;
                _next[best] = true;
            }

            bool[] previous = _lit;
            _lit = _next;
            _next = previous;
            _count = count;

            int lit = 0;
            for (int i = 0; i < count; i++)
            {
                if (_lit[i]) lit++;
            }
            LitCount = lit;
        }

        private void EnsureCapacity(int count)
        {
            if (_lit.Length >= count) return;
            int size = Math.Max(count, _lit.Length * 2);
            Array.Resize(ref _lit, size);
            Array.Resize(ref _next, size);
            Array.Resize(ref _keys, size);
        }
    }
}
