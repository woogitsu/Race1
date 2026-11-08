namespace Race1.Core
{
    /// <summary>
    /// Natural (pre-road) terrain height. The road generator and the terrain mesher both call this,
    /// so the road is designed against the same ground the player will see.
    /// </summary>
    public sealed class TerrainField
    {
        private const double MacroScale = 6000.0;
        private const double MacroHeight = 60.0;

        private readonly PerlinNoise2D _macro;
        private readonly PerlinNoise2D _detail;
        private readonly BiomeField _biomes;
        private readonly double[] _weights = new double[Biomes.Count];

        public TerrainField(ulong seed, BiomeField biomes)
        {
            _biomes = biomes;
            _macro = new PerlinNoise2D(DeterministicRng.Combine(seed, 0x7E22A10000UL));
            _detail = new PerlinNoise2D(DeterministicRng.Combine(seed, 0x7E22A20000UL));
        }

        public BiomeField BiomeSource => _biomes;

        /// <summary>Ground height in metres above the world origin plane.</summary>
        public double NaturalHeight(double x, double z)
        {
            _biomes.Weights(x, z, _weights);
            BiomeProfile p = Race1.Core.Biomes.Blend(_weights, out _);
            double macro = _macro.Fbm(x / MacroScale, z / MacroScale, 2, 2.0, 0.5) * MacroHeight;
            double detail = _detail.Fbm(x * p.TerrainFrequency, z * p.TerrainFrequency, 5, 2.0, 0.5);
            return macro + detail * p.TerrainAmplitude;
        }
    }
}
