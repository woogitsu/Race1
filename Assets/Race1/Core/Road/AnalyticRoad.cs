using System;

namespace Race1.Core
{
    /// <summary>
    /// Road with constant curvature, grade and bank. Used by physics tests and as a
    /// reference for the generated road (its geometry is closed-form).
    /// </summary>
    public sealed class AnalyticRoad : IRoadProvider
    {
        public double Curvature;
        public double Grade;
        public double Bank;
        public double HalfWidth = 3.5;
        public double Shoulder = 1.5;
        public int Lanes = 2;

        public RoadSample Sample(double s)
        {
            double kappa = Curvature;
            double pitch = Math.Atan(Grade);
            double cosP = Math.Cos(pitch);
            double x;
            double z;
            if (Math.Abs(kappa) < 1e-12)
            {
                x = 0.0;
                z = s;
            }
            else
            {
                x = (1.0 - Math.Cos(kappa * s)) / kappa;
                z = Math.Sin(kappa * s) / kappa;
            }
            return new RoadSample
            {
                S = s,
                Position = new Vector3d(x * cosP, s * Math.Sin(pitch), z * cosP),
                Heading = kappa * s,
                Pitch = pitch,
                Bank = Bank,
                Curvature = kappa,
                HalfWidth = HalfWidth,
                Shoulder = Shoulder,
                Lanes = Lanes,
                Biome = BiomeKind.Highway,
                Bridge = false,
            };
        }
    }
}
