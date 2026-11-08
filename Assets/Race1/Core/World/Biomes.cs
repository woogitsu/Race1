namespace Race1.Core
{
    public enum BiomeKind
    {
        Meadow = 0,
        Forest = 1,
        Alpine = 2,
        Coast = 3,
        Highway = 4,
        Desert = 5,
        Town = 6,
    }

    /// <summary>
    /// Tuning of one landscape type. Profiles are blended with continuous weights, so a road
    /// or terrain crossing a biome border changes gradually instead of jumping.
    /// </summary>
    public struct BiomeProfile
    {
        public string Name;
        public double TerrainAmplitude;   // metres, fBm amplitude
        public double TerrainFrequency;   // 1/metres, fBm base frequency
        public double MinRadius;          // metres, tightest curve the road design uses
        public double MaxRadius;          // metres, gentlest curve the road design uses
        public double StraightChance;     // probability that a road knot is straight
        public double MaxGrade;           // rise over run
        public double KnotMin;            // metres, shortest design segment
        public double KnotMax;            // metres, longest design segment
        public double Vegetation;         // 0..1 tree density
        public double Rockiness;          // 0..1
        public double TintR, TintG, TintB; // ground colour (linear 0..1)
        public int Lanes;                 // total lanes; taken from the dominant biome, not blended
    }

    public static class Biomes
    {
        public const int Count = 7;

        public static readonly BiomeProfile[] Profiles =
        {
            new BiomeProfile { Name = "Meadow", TerrainAmplitude = 34, TerrainFrequency = 1.0 / 700.0,
                MinRadius = 90, MaxRadius = 500, StraightChance = 0.45, MaxGrade = 0.045, KnotMin = 100, KnotMax = 400,
                Vegetation = 0.6, Rockiness = 0.1, TintR = 0.36, TintG = 0.52, TintB = 0.24, Lanes = 2 },
            new BiomeProfile { Name = "Forest", TerrainAmplitude = 55, TerrainFrequency = 1.0 / 520.0,
                MinRadius = 45, MaxRadius = 220, StraightChance = 0.30, MaxGrade = 0.060, KnotMin = 80, KnotMax = 250,
                Vegetation = 0.9, Rockiness = 0.2, TintR = 0.18, TintG = 0.36, TintB = 0.14, Lanes = 2 },
            new BiomeProfile { Name = "Alpine", TerrainAmplitude = 240, TerrainFrequency = 1.0 / 900.0,
                MinRadius = 28, MaxRadius = 110, StraightChance = 0.25, MaxGrade = 0.095, KnotMin = 60, KnotMax = 180,
                Vegetation = 0.35, Rockiness = 0.9, TintR = 0.45, TintG = 0.42, TintB = 0.38, Lanes = 2 },
            new BiomeProfile { Name = "Coast", TerrainAmplitude = 26, TerrainFrequency = 1.0 / 850.0,
                MinRadius = 80, MaxRadius = 400, StraightChance = 0.40, MaxGrade = 0.035, KnotMin = 100, KnotMax = 350,
                Vegetation = 0.45, Rockiness = 0.4, TintR = 0.62, TintG = 0.60, TintB = 0.42, Lanes = 2 },
            new BiomeProfile { Name = "Highway", TerrainAmplitude = 16, TerrainFrequency = 1.0 / 1200.0,
                MinRadius = 400, MaxRadius = 2000, StraightChance = 0.60, MaxGrade = 0.030, KnotMin = 200, KnotMax = 600,
                Vegetation = 0.25, Rockiness = 0.1, TintR = 0.42, TintG = 0.44, TintB = 0.36, Lanes = 4 },
            new BiomeProfile { Name = "Desert", TerrainAmplitude = 48, TerrainFrequency = 1.0 / 650.0,
                MinRadius = 150, MaxRadius = 700, StraightChance = 0.50, MaxGrade = 0.025, KnotMin = 150, KnotMax = 500,
                Vegetation = 0.03, Rockiness = 0.5, TintR = 0.80, TintG = 0.67, TintB = 0.44, Lanes = 2 },
            new BiomeProfile { Name = "Town", TerrainAmplitude = 5, TerrainFrequency = 1.0 / 300.0,
                MinRadius = 40, MaxRadius = 120, StraightChance = 0.35, MaxGrade = 0.020, KnotMin = 60, KnotMax = 150,
                Vegetation = 0.25, Rockiness = 0.1, TintR = 0.55, TintG = 0.53, TintB = 0.50, Lanes = 2 },
        };

        public static BiomeProfile Get(BiomeKind kind) => Profiles[(int)kind];

        /// <summary>
        /// Blends all profiles with normalised weights. Lane count comes from the dominant biome
        /// (it is discrete); every other field is a weighted average (continuous).
        /// </summary>
        public static BiomeProfile Blend(double[] weights, out BiomeKind dominant)
        {
            var r = new BiomeProfile();
            double best = -1.0;
            dominant = BiomeKind.Meadow;
            for (int i = 0; i < Count; i++)
            {
                double w = weights[i];
                if (w > best)
                {
                    best = w;
                    dominant = (BiomeKind)i;
                }
                BiomeProfile p = Profiles[i];
                r.TerrainAmplitude += w * p.TerrainAmplitude;
                r.TerrainFrequency += w * p.TerrainFrequency;
                r.MinRadius += w * p.MinRadius;
                r.MaxRadius += w * p.MaxRadius;
                r.StraightChance += w * p.StraightChance;
                r.MaxGrade += w * p.MaxGrade;
                r.KnotMin += w * p.KnotMin;
                r.KnotMax += w * p.KnotMax;
                r.Vegetation += w * p.Vegetation;
                r.Rockiness += w * p.Rockiness;
                r.TintR += w * p.TintR;
                r.TintG += w * p.TintG;
                r.TintB += w * p.TintB;
            }
            r.Name = Profiles[(int)dominant].Name;
            r.Lanes = Profiles[(int)dominant].Lanes;
            return r;
        }
    }
}
