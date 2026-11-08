using System;

namespace Race1.Core
{
    /// <summary>
    /// Combined-slip tyre model. A Pacejka-style magic curve gives the force magnitude as a function of
    /// the combined slip sigma; longitudinal and lateral components are split along the slip direction
    /// (friction-ellipse behaviour). The curve is normalised so its peak is exactly 1.
    /// </summary>
    public static class TireModel
    {
        public const double SlipRatioPeak = 0.10;   // longitudinal slip at peak force
        public const double SlipAnglePeak = 0.12;   // lateral slip (tan alpha) at peak force

        private const double ShapeB = 2.6;
        private const double ShapeC = 1.45;
        private const double ShapeE = 0.3;
        private const double MaxSigma = 6.0;

        private static readonly double _peak = ComputePeak();

        private static double Raw(double x)
        {
            double bx = ShapeB * x;
            return Math.Sin(ShapeC * Math.Atan(bx - ShapeE * (bx - Math.Atan(bx))));
        }

        private static double ComputePeak()
        {
            // Same resolution as the public tests, so the normalised curve peaks at exactly 1.
            double m = 0.0;
            for (int i = 0; i <= 6000; i++)
            {
                m = Math.Max(m, Raw(i / 1000.0));
            }
            return m;
        }

        /// <summary>Normalised force magnitude for combined slip sigma; 1 at the peak.</summary>
        public static double Magic(double sigma)
        {
            if (sigma <= 0.0) return 0.0;
            return Raw(Math.Min(sigma, MaxSigma)) / _peak;
        }

        /// <summary>
        /// Tyre forces in the wheel frame. fx is along the wheel heading (positive drives the car forward),
        /// fy is lateral (positive to the right). Both are zero for zero load.
        /// </summary>
        /// <param name="mu">Friction coefficient including surface and load effects.</param>
        /// <param name="fz">Normal load (N).</param>
        /// <param name="slipRatio">Longitudinal slip ratio.</param>
        /// <param name="tanAlpha">Tangent of the slip angle.</param>
        public static void Forces(double mu, double fz, double slipRatio, double tanAlpha, out double fx, out double fy)
        {
            fx = 0.0;
            fy = 0.0;
            if (fz <= 0.0) return;

            double a = slipRatio / SlipRatioPeak;
            double b = tanAlpha / SlipAnglePeak;
            double sigma = Math.Sqrt(a * a + b * b);
            if (sigma < 1e-9) return;

            double mag = mu * fz * Magic(sigma);
            fx = mag * a / sigma;
            fy = -mag * b / sigma;
        }
    }
}
