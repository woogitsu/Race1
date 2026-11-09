using System;

namespace Race1.Core
{
    /// <summary>One water drop on the windscreen. Position and radius are in texture units, so 0..1 across the glass.</summary>
    public struct WindscreenDroplet
    {
        public double X;
        public double Y;
        public double Radius;
    }

    /// <summary>
    /// The layout of the drops on the windscreen overlay. Seeded, so the same seed always gives the same glass. Most
    /// drops are small: the radius is drawn with a square bias towards the minimum.
    /// </summary>
    public static class WindscreenDroplets
    {
        public const double MinRadius = 0.008;
        public const double MaxRadius = 0.04;
        public const int MaxCount = 2000;

        /// <summary>Places <paramref name="count"/> drops (clamped to 0..<see cref="MaxCount"/>) from a seed.</summary>
        public static WindscreenDroplet[] Generate(ulong seed, int count)
        {
            int n = Math.Max(0, Math.Min(MaxCount, count));
            var drops = new WindscreenDroplet[n];
            var rng = new DeterministicRng(seed);
            for (int i = 0; i < n; i++)
            {
                double x = rng.NextDouble();
                double y = rng.NextDouble();
                double u = rng.NextDouble();
                drops[i] = new WindscreenDroplet
                {
                    X = x,
                    Y = y,
                    Radius = MinRadius + (MaxRadius - MinRadius) * u * u,
                };
            }
            return drops;
        }
    }
}
