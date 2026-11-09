using System;

namespace Race1.Core
{
    /// <summary>
    /// One sample of the road centre line, taken every <see cref="RoadSettings.SampleSpacing"/> metres.
    /// Conventions: Unity-style left-handed frame (X right, Y up, Z forward).
    /// Heading 0 points along +Z and increases when turning right. Curvature is positive for right turns.
    /// </summary>
    public struct RoadSample
    {
        public double S;            // arc length from the start of the road (m)
        public Vector3d Position;   // centre line position (m)
        public double Heading;      // yaw (rad)
        public double Pitch;        // slope angle (rad), positive = climbing
        public double Bank;         // superelevation (rad), positive = right side lower
        public double Curvature;    // 1/m, positive = right
        public double HalfWidth;    // carriageway half width (m)
        public double Shoulder;     // shoulder width on each side (m)
        public int Lanes;           // total lanes (both directions)
        public BiomeKind Biome;     // dominant biome at this point
        public bool Bridge;         // road is elevated above natural ground
        public bool Tunnel;         // road runs under a hill: natural ground is well above the road

        /// <summary>Direction of travel.</summary>
        public Vector3d Forward => new Vector3d(
            Math.Sin(Heading) * Math.Cos(Pitch),
            Math.Sin(Pitch),
            Math.Cos(Heading) * Math.Cos(Pitch));

        /// <summary>Horizontal right vector, rolled by the bank angle.</summary>
        public Vector3d Right
        {
            get
            {
                Vector3d h = HorizontalRight;
                Vector3d up0 = Vector3d.Cross(Forward, h);
                return h * Math.Cos(Bank) - up0 * Math.Sin(Bank);
            }
        }

        /// <summary>Road surface normal (points up from the carriageway).</summary>
        public Vector3d Up
        {
            get
            {
                Vector3d h = HorizontalRight;
                Vector3d up0 = Vector3d.Cross(Forward, h);
                return up0 * Math.Cos(Bank) + h * Math.Sin(Bank);
            }
        }

        private Vector3d HorizontalRight => new Vector3d(Math.Cos(Heading), 0.0, -Math.Sin(Heading));

        /// <summary>World point at lateral offset t (positive = right) on the road surface.</summary>
        public Vector3d PointAt(double t) => Position + Right * t;

        public static RoadSample Lerp(in RoadSample a, in RoadSample b, double f)
        {
            return new RoadSample
            {
                S = a.S + (b.S - a.S) * f,
                Position = Vector3d.Lerp(a.Position, b.Position, f),
                Heading = a.Heading + (b.Heading - a.Heading) * f,
                Pitch = a.Pitch + (b.Pitch - a.Pitch) * f,
                Bank = a.Bank + (b.Bank - a.Bank) * f,
                Curvature = a.Curvature + (b.Curvature - a.Curvature) * f,
                HalfWidth = a.HalfWidth + (b.HalfWidth - a.HalfWidth) * f,
                Shoulder = a.Shoulder + (b.Shoulder - a.Shoulder) * f,
                Lanes = f < 0.5 ? a.Lanes : b.Lanes,
                Biome = f < 0.5 ? a.Biome : b.Biome,
                Bridge = f < 0.5 ? a.Bridge : b.Bridge,
                Tunnel = f < 0.5 ? a.Tunnel : b.Tunnel,
            };
        }
    }
}
