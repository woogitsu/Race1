using System;

namespace Race1.Core
{
    /// <summary>
    /// Continuous biome weights over the world plane. Each biome has its own low-frequency
    /// noise; weights are squared, clamped and normalised so they always sum to one.
    /// </summary>
    public sealed class BiomeField
    {
        private const double FieldScale = 2600.0;   // metres per noise cell
        private const double Bias = 0.35;

        private readonly PerlinNoise2D[] _noise = new PerlinNoise2D[Biomes.Count];

        public BiomeField(ulong seed)
        {
            for (int i = 0; i < Biomes.Count; i++)
            {
                _noise[i] = new PerlinNoise2D(DeterministicRng.Combine(seed, 0xB10E000000UL + (ulong)i));
            }
        }

        /// <summary>Fills <paramref name="weights"/> (length <see cref="Biomes.Count"/>) with normalised weights.</summary>
        public void Weights(double x, double z, double[] weights)
        {
            double sum = 0.0;
            for (int i = 0; i < Biomes.Count; i++)
            {
                double n = _noise[i].Fbm(x / FieldScale + 13.1 * i, z / FieldScale - 7.9 * i, 3, 2.0, 0.5);
                double v = Math.Max(0.0, n + Bias);
                double w = v * v + 1e-6;
                weights[i] = w;
                sum += w;
            }
            for (int i = 0; i < Biomes.Count; i++)
            {
                weights[i] /= sum;
            }
        }
    }
}
