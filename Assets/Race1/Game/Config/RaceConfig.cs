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
        public int ChunksAheadMax = 14;
        public int BuildsPerFrame = 2;

        [Header("Traffic")]
        public int MaxTrafficCars = 14;

        [Header("Lighting")]
        [Tooltip("Street lamps that get a real light source at night. The rest only glow.")]
        public int MaxLitStreetLamps = 8;

        [Header("Vehicle")]
        public float PhysicsStepHint = 1f / 600f;

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
