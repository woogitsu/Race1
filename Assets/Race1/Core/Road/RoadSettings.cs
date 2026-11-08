namespace Race1.Core
{
    /// <summary>Tunable parameters of the procedural road. Plain data: Unity wraps it in a ScriptableObject.</summary>
    public sealed class RoadSettings
    {
        public ulong Seed = 20260101UL;
        public double SampleSpacing = 2.0;          // m between centre-line samples
        public double CheckpointSpacing = 2000.0;   // m between generator snapshots (rewind cost bound)

        public double MaxCurvature = 1.0 / 28.0;    // tightest radius 28 m
        public double MaxCurvatureRate = 1.5e-3;    // 1/m^2, limits how fast curvature (and so lateral g) changes
        public double MaxGradientRate = 1.2e-3;     // 1/m, limits vertical curvature
        public double MaxWidthRate = 0.05;          // m/m, lane-count transitions
        public double MaxTurnPerKnot = 0.4;         // rad of heading change allowed in one design segment
        public double MaxHeadingDrift = 0.9;        // rad, keeps the road from curling back on itself
        public double MaxBank = 0.12;               // rad (~7 deg)
        public double ReferenceSpeed = 27.0;        // m/s used to derive superelevation

        public double Shoulder = 1.5;               // m
        public double LaneWidth = 3.5;              // m
        public double BridgeHeight = 9.0;           // road this far above ground is flagged as a bridge
        public double Clearance = 1.5;              // road sits this far above natural ground when it can

        public double AltitudeFloor = -60.0;        // m
        public double AltitudeCeiling = 1100.0;     // m

        public static RoadSettings Default => new RoadSettings();
    }
}
