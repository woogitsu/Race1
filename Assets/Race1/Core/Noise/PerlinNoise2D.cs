using System;

namespace Race1.Core
{
    /// <summary>
    /// Seeded 2D gradient (Perlin-style) noise. Pure function of (seed, x, z): the same
    /// coordinates always give the same value, which is what makes terrain chunks and
    /// road biomes reproducible and seamless across chunk borders.
    /// </summary>
    public sealed class PerlinNoise2D
    {
        private static readonly double[] GradX =
        {
            1.0, -1.0, 0.0, 0.0, 0.70710678118, -0.70710678118, 0.70710678118, -0.70710678118
        };

        private static readonly double[] GradZ =
        {
            0.0, 0.0, 1.0, -1.0, 0.70710678118, 0.70710678118, -0.70710678118, -0.70710678118
        };

        private readonly ulong _seed;

        public PerlinNoise2D(ulong seed)
        {
            _seed = seed;
        }

        /// <summary>Single octave, approximately in [-1, 1].</summary>
        public double Sample(double x, double z)
        {
            long x0 = (long)Math.Floor(x);
            long z0 = (long)Math.Floor(z);
            double fx = x - x0;
            double fz = z - z0;
            double u = Fade(fx);
            double v = Fade(fz);

            double n00 = Gradient(x0, z0, fx, fz);
            double n10 = Gradient(x0 + 1, z0, fx - 1.0, fz);
            double n01 = Gradient(x0, z0 + 1, fx, fz - 1.0);
            double n11 = Gradient(x0 + 1, z0 + 1, fx - 1.0, fz - 1.0);

            double nx0 = n00 + (n10 - n00) * u;
            double nx1 = n01 + (n11 - n01) * u;
            return (nx0 + (nx1 - nx0) * v) * 1.41421356;
        }

        /// <summary>Fractal sum of octaves, normalised to roughly [-1, 1].</summary>
        public double Fbm(double x, double z, int octaves, double lacunarity, double gain)
        {
            double sum = 0.0;
            double amp = 1.0;
            double freq = 1.0;
            double norm = 0.0;
            for (int i = 0; i < octaves; i++)
            {
                // Per-octave offset avoids lattice alignment between octaves.
                sum += amp * Sample(x * freq + 31.7 * i, z * freq + 17.3 * i);
                norm += amp;
                amp *= gain;
                freq *= lacunarity;
            }
            return norm > 0.0 ? sum / norm : 0.0;
        }

        private double Gradient(long ix, long iz, double dx, double dz)
        {
            ulong key = ((ulong)ix << 32) ^ ((ulong)iz & 0xFFFFFFFFUL);
            ulong h = DeterministicRng.Combine(_seed, key);
            int g = (int)(h & 7UL);
            return GradX[g] * dx + GradZ[g] * dz;
        }

        private static double Fade(double t) => t * t * t * (t * (t * 6.0 - 15.0) + 10.0);
    }
}
