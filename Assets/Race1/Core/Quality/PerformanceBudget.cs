using System;

namespace Race1.Core
{
    /// <summary>
    /// The limits one quality tier runs with. Public fields and [Serializable] so the designer can edit them in the
    /// <c>RaceConfig</c> asset; the values are checked with <see cref="PerformanceBudget.Sanitize"/> before use.
    /// </summary>
    [Serializable]
    public struct PerformanceLimits
    {
        /// <summary>Road chunks drawn ahead of the car.</summary>
        public int ChunksAhead;

        /// <summary>Chunk builds allowed in one frame, so generation never causes a long frame.</summary>
        public int BuildsPerFrame;

        /// <summary>Tree and rock instances drawn in one frame. The nearest chunks get them first.</summary>
        public int MaxVegetationInstances;

        /// <summary>Street lamps that get a real light source at night.</summary>
        public int MaxLitLights;

        /// <summary>Shadow distance in metres.</summary>
        public float ShadowDistanceM;
    }

    /// <summary>
    /// Budget table for the quality tiers, its safe ranges, and the checks the engine code relies on. Index order is the
    /// <see cref="QualityTier"/> order: battery saver, balanced, high, ultra.
    /// </summary>
    public static class PerformanceBudget
    {
        public const int TierCount = 4;

        // Safe ranges. Values outside these are clamped, so a bad asset cannot break the streaming or the renderer.
        public const int MinChunksAhead = 4, MaxChunksAhead = 20;
        public const int MinBuildsPerFrame = 1, MaxBuildsPerFrame = 4;
        public const int MinVegetation = 200, MaxVegetation = 50000;
        public const int MinLitLights = 0, MaxLitLights = 16;
        public const float MinShadowDistance = 20f, MaxShadowDistance = 300f;

        /// <summary>The table the game ships with. It matches the limits the director used before the budget existed.</summary>
        public static PerformanceLimits[] Defaults()
        {
            return new[]
            {
                new PerformanceLimits { ChunksAhead = 7, BuildsPerFrame = 1, MaxVegetationInstances = 2000, MaxLitLights = 0, ShadowDistanceM = 60f },
                new PerformanceLimits { ChunksAhead = 9, BuildsPerFrame = 1, MaxVegetationInstances = 4000, MaxLitLights = 4, ShadowDistanceM = 120f },
                new PerformanceLimits { ChunksAhead = 11, BuildsPerFrame = 2, MaxVegetationInstances = 7000, MaxLitLights = 6, ShadowDistanceM = 220f },
                new PerformanceLimits { ChunksAhead = 14, BuildsPerFrame = 2, MaxVegetationInstances = 10000, MaxLitLights = 8, ShadowDistanceM = 220f },
            };
        }

        /// <summary>Clamps every field to its safe range. NaN shadow distances become the minimum.</summary>
        public static PerformanceLimits Sanitize(PerformanceLimits l)
        {
            return new PerformanceLimits
            {
                ChunksAhead = Clamp(l.ChunksAhead, MinChunksAhead, MaxChunksAhead),
                BuildsPerFrame = Clamp(l.BuildsPerFrame, MinBuildsPerFrame, MaxBuildsPerFrame),
                MaxVegetationInstances = Clamp(l.MaxVegetationInstances, MinVegetation, MaxVegetation),
                MaxLitLights = Clamp(l.MaxLitLights, MinLitLights, MaxLitLights),
                ShadowDistanceM = float.IsNaN(l.ShadowDistanceM)
                    ? MinShadowDistance
                    : Math.Max(MinShadowDistance, Math.Min(MaxShadowDistance, l.ShadowDistanceM)),
            };
        }

        /// <summary>
        /// The sanitized limits for a tier. A missing or short table falls back to the defaults, so the game always has
        /// a budget to run with.
        /// </summary>
        public static PerformanceLimits ForTier(PerformanceLimits[] table, QualityTier tier)
        {
            int index = (int)tier;
            if (index < 0 || index >= TierCount) index = TierCount - 1;
            if (table == null || table.Length < TierCount) return Defaults()[index];
            return Sanitize(table[index]);
        }

        /// <summary>
        /// True when every field is at least as large on a higher tier as on the one below it. A budget that shrinks as
        /// the tier rises would work against the governor.
        /// </summary>
        public static bool IsMonotone(PerformanceLimits[] table)
        {
            if (table == null || table.Length < TierCount) return false;
            for (int i = 1; i < TierCount; i++)
            {
                PerformanceLimits lo = table[i - 1], hi = table[i];
                if (hi.ChunksAhead < lo.ChunksAhead) return false;
                if (hi.BuildsPerFrame < lo.BuildsPerFrame) return false;
                if (hi.MaxVegetationInstances < lo.MaxVegetationInstances) return false;
                if (hi.MaxLitLights < lo.MaxLitLights) return false;
                if (hi.ShadowDistanceM < lo.ShadowDistanceM) return false;
            }
            return true;
        }

        private static int Clamp(int v, int lo, int hi) => Math.Max(lo, Math.Min(hi, v));
    }

    /// <summary>
    /// Shares a per-frame budget over groups of instances. Earlier groups get their full count first, so the groups
    /// should be ordered nearest first: the far ones are the ones that go unrendered when the budget runs out.
    /// </summary>
    public static class DrawBudget
    {
        /// <summary>
        /// Writes how many instances each of the first <paramref name="count"/> groups may draw into
        /// <paramref name="allocated"/>. The total never exceeds <paramref name="limit"/>. Negative counts draw nothing.
        /// </summary>
        public static void Allocate(int[] available, int count, int limit, int[] allocated)
        {
            if (available == null) throw new ArgumentNullException(nameof(available));
            if (allocated == null) throw new ArgumentNullException(nameof(allocated));
            if (count > available.Length || count > allocated.Length) throw new ArgumentOutOfRangeException(nameof(count));

            int remaining = Math.Max(0, limit);
            for (int i = 0; i < count; i++)
            {
                int take = Math.Min(Math.Max(0, available[i]), remaining);
                allocated[i] = take;
                remaining -= take;
            }
        }
    }
}
