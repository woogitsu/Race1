using System;

namespace Race1.Core
{
    /// <summary>
    /// Geometry rules for bridges and tunnels, kept free of engine types so they can be tested. A bridge
    /// (<see cref="RoadSample.Bridge"/>) is carried on pillars; a tunnel (<see cref="RoadSample.Tunnel"/>) is lined
    /// with an arch over the carriageway.
    /// </summary>
    public static class StructureGeometry
    {
        /// <summary>Distance between pillar spans along a bridge (m).</summary>
        public const double PillarSpacingM = 24.0;

        /// <summary>Thickness of the bridge deck above the pillars' top (m).</summary>
        public const double DeckThicknessM = 0.9;

        /// <summary>Below this height a pillar is not worth drawing: the deck is simply low.</summary>
        public const double MinPillarHeightM = 2.0;

        /// <summary>Pillars stand under the deck at this fraction of the road half width, on each side.</summary>
        public const double PillarLateralFactor = 0.6;

        /// <summary>Lining sits this far outside the road edge (m), leaving room for the walls.</summary>
        public const double LiningWallOffsetM = 1.2;

        /// <summary>Crown height of the tunnel lining above the carriageway (m).</summary>
        public const double LiningHeightM = 5.0;

        /// <summary>
        /// True for a sample index that starts a pillar span. Spans are <paramref name="spacingM"/> metres apart along
        /// the road, so each span has exactly one such index.
        /// </summary>
        public static bool IsPillarSample(long sampleIndex, double sampleSpacing, double spacingM = PillarSpacingM)
        {
            if (!(sampleSpacing > 0.0) || !(spacingM > 0.0)) return false;
            long every = Math.Max(1L, (long)Math.Round(spacingM / sampleSpacing));
            long r = sampleIndex % every;
            if (r < 0) r += every;
            return r == 0;
        }

        /// <summary>
        /// Height of a pillar from the ground up to the underside of the deck (m). Returns zero when the deck is too low
        /// to need a pillar, or when either height is not a number.
        /// </summary>
        public static double PillarHeight(double deckY, double groundY)
        {
            if (double.IsNaN(deckY) || double.IsNaN(groundY)) return 0.0;
            double height = deckY - DeckThicknessM - groundY;
            return height >= MinPillarHeightM ? height : 0.0;
        }

        /// <summary>Lateral distance from the road centre to the outside of the tunnel lining (m).</summary>
        public static double LiningHalfWidth(double roadHalfWidth) => roadHalfWidth + LiningWallOffsetM;

        /// <summary>
        /// Profile of the tunnel lining across the road, as (lateral, height) points from the left base over the crown to
        /// the right base. The lining is half an ellipse with semi-axes <see cref="LiningHalfWidth"/> and
        /// <see cref="LiningHeightM"/>. At least two segments are returned.
        /// </summary>
        public static (double lateral, double height)[] LiningProfile(double roadHalfWidth, int segments)
        {
            int n = Math.Max(2, segments);
            double a = LiningHalfWidth(roadHalfWidth);
            double b = LiningHeightM;
            var points = new (double lateral, double height)[n + 1];
            for (int k = 0; k <= n; k++)
            {
                double theta = Math.PI * k / n;
                points[k] = (-a * Math.Cos(theta), b * Math.Sin(theta));
            }
            return points;
        }
    }
}
