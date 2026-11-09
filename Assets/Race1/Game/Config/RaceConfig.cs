using Race1.Core;
using UnityEngine;

namespace Race1.Game
{
    /// <summary>
    /// Designer-facing tuning. Lives as an asset at Assets/Race1/Resources/RaceConfig.asset (created by the
    /// Race1 setup menu). If the asset is missing, the code defaults are used, so the game always starts.
    /// </summary>
    [CreateAssetMenu(fileName = "RaceConfig", menuName = "Race1/Race Config")]
    public sealed class RaceConfig : ScriptableObject
    {
        [Header("Road")]
        public ulong Seed = 20260101;
        public double SampleSpacing = 2.0;
        public double MaxCurvature = 1.0 / 28.0;
        public double MaxTurnPerKnot = 0.4;

        [Header("Streaming")]
        [Tooltip("Not used. The chunk limits are in Budgets, one row per quality tier.")]
        public int ChunksAheadMax = 14;
        public int BuildsPerFrame = 2;

        [Header("Performance budget")]
        [Tooltip("Limits per quality tier, in order: battery saver, balanced, high, ultra. Values outside the safe " +
                 "ranges are clamped, and a missing table falls back to the defaults.")]
        public PerformanceLimits[] Budgets = PerformanceBudget.Defaults();

        [Header("Traffic")]
        public int MaxTrafficCars = 14;

        [Header("Lighting")]
        [Tooltip("Street lamps that get a real light source at night. The rest only glow.")]
        public int MaxLitStreetLamps = 8;

        [Header("Vehicle")]
        public float PhysicsStepHint = 1f / 600f;

        [Header("AI race")]
        [Tooltip("Point-to-point race length from the grid, in metres.")]
        public double RaceLengthM = 3000.0;
        [Tooltip("AI rivals in the race, 0 to 8.")]
        public int RivalCount = 5;
        [Tooltip("Cruising speed of a rival at pace 1, in m/s. The difficulty scales it.")]
        public double RivalReferenceSpeedMs = 50.0;
        [Tooltip("Distance between grid slots behind the start, in metres.")]
        public double RivalGridSpacing = 12.0;
        public AiDifficulty RivalDifficulty = AiDifficulty.Normal;

        public RoadSettings ToRoadSettings()
        {
            return new RoadSettings
            {
                Seed = Seed,
                SampleSpacing = SampleSpacing,
                MaxCurvature = MaxCurvature,
                MaxTurnPerKnot = MaxTurnPerKnot,
                MaxCurvatureRate = RoadSettings.Default.MaxCurvatureRate,
                MaxGradientRate = RoadSettings.Default.MaxGradientRate,
                MaxHeadingDrift = RoadSettings.Default.MaxHeadingDrift,
            };
        }
    }
}
