using System;
using System.Globalization;

namespace Race1.Core
{
    /// <summary>
    /// Double-precision vector used by road and vehicle maths. Double precision keeps
    /// arc lengths of hundreds of kilometres exact enough for seamless chunk joins.
    /// Unity receives floats relative to a floating origin.
    /// </summary>
    public struct Vector3d : IEquatable<Vector3d>
    {
        public double X;
        public double Y;
        public double Z;

        public static readonly Vector3d Zero = new Vector3d(0.0, 0.0, 0.0);
        public static readonly Vector3d Up = new Vector3d(0.0, 1.0, 0.0);

        public Vector3d(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public double Length => Math.Sqrt(X * X + Y * Y + Z * Z);

        /// <summary>Unit vector, or zero when the input is (near) zero.</summary>
        public Vector3d Normalized
        {
            get
            {
                double l = Length;
                return l > 1e-12 ? this / l : Zero;
            }
        }

        public static Vector3d operator +(Vector3d a, Vector3d b) => new Vector3d(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Vector3d operator -(Vector3d a, Vector3d b) => new Vector3d(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Vector3d operator -(Vector3d a) => new Vector3d(-a.X, -a.Y, -a.Z);
        public static Vector3d operator *(Vector3d a, double s) => new Vector3d(a.X * s, a.Y * s, a.Z * s);
        public static Vector3d operator *(double s, Vector3d a) => new Vector3d(a.X * s, a.Y * s, a.Z * s);
        public static Vector3d operator /(Vector3d a, double s) => new Vector3d(a.X / s, a.Y / s, a.Z / s);

        public static double Dot(Vector3d a, Vector3d b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

        public static Vector3d Cross(Vector3d a, Vector3d b) => new Vector3d(
            a.Y * b.Z - a.Z * b.Y,
            a.Z * b.X - a.X * b.Z,
            a.X * b.Y - a.Y * b.X);

        public static Vector3d Lerp(Vector3d a, Vector3d b, double t) => a + (b - a) * t;

        public static double Distance(Vector3d a, Vector3d b) => (a - b).Length;

        public bool Equals(Vector3d other) => X == other.X && Y == other.Y && Z == other.Z;

        public override bool Equals(object obj) => obj is Vector3d v && Equals(v);

        public override int GetHashCode() => X.GetHashCode() ^ (Y.GetHashCode() * 397) ^ Z.GetHashCode();

        public override string ToString() =>
            string.Format(CultureInfo.InvariantCulture, "({0:F3}, {1:F3}, {2:F3})", X, Y, Z);
    }
}
