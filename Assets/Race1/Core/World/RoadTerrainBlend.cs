using System;

namespace Race1.Core
{
    /// <summary>
    /// Ground height across the road. On the carriageway and shoulder the surface is exactly the road,
    /// so the car never floats or sinks. Beyond the shoulder the ground blends into the natural terrain
    /// over <see cref="FalloffMeters"/>. The blend is continuous at both ends, so neighbouring chunks join
    /// without steps.
    /// </summary>
    public static class RoadTerrainBlend
    {
        public const double FalloffMeters = 22.0;

        /// <param name="road">Road sample at the point along the road.</param>
        /// <param name="t">Lateral offset from the centre line (positive right, metres).</param>
        /// <param name="naturalHeight">Natural terrain height at the horizontal position of that offset.</param>
        public static double GroundHeight(in RoadSample road, double t, double naturalHeight)
        {
            double edge = road.HalfWidth + road.Shoulder;
            double a = Math.Abs(t);
            if (a <= edge)
            {
                return road.PointAt(t).Y;
            }

            double edgeY = road.PointAt(Math.Sign(t) * edge).Y;
            double w = Mathx.SmoothStep(0.0, FalloffMeters, a - edge);
            return Mathx.Lerp(edgeY, naturalHeight, w);
        }

        /// <summary>Lateral distance beyond which the ground is purely natural (no road influence).</summary>
        public static double InfluenceEdge(in RoadSample road) => road.HalfWidth + road.Shoulder + FalloffMeters;
    }
}
