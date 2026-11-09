using System;

namespace Race1.Core
{
    /// <summary>
    /// Sea level and shoreline rules. Water is drawn only where the coast biome dominates and the ground lies
    /// below sea level, so low inland ground never fills with water. Foam is strongest at the waterline and fades
    /// out over <see cref="FoamBandM"/> metres of depth.
    /// </summary>
    public static class Shoreline
    {
        /// <summary>Height of the sea surface in world space (m). Matches the world origin plane.</summary>
        public const double SeaLevelM = 0.0;

        /// <summary>Coast biome weight from which a point counts as coast. Weights sum to one, so this is the dominant biome.</summary>
        public const double CoastThreshold = 0.5;

        /// <summary>Ground must be at least this far below sea level to hold water. Avoids a sliver at the waterline.</summary>
        public const double MinDepthM = 0.2;

        /// <summary>Depth over which foam fades from full to none (m).</summary>
        public const double FoamBandM = 3.0;

        /// <summary>How far the ground lies below sea level (m). Zero above the sea.</summary>
        public static double Depth(double groundY)
        {
            if (double.IsNaN(groundY)) return 0.0;
            return Math.Max(0.0, SeaLevelM - groundY);
        }

        /// <summary>True when water should cover a point: coast dominates and the ground is clearly below the sea.</summary>
        public static bool IsWaterAt(double groundY, double coastWeight)
        {
            if (double.IsNaN(groundY) || double.IsNaN(coastWeight)) return false;
            return coastWeight >= CoastThreshold && groundY < SeaLevelM - MinDepthM;
        }

        /// <summary>Foam from 1 at the waterline down to 0 at <see cref="FoamBandM"/> of depth, clamped to 0..1.</summary>
        public static double FoamStrength(double depth)
        {
            if (double.IsNaN(depth)) return 0.0;
            return Math.Max(0.0, Math.Min(1.0, 1.0 - depth / FoamBandM));
        }
    }
}
