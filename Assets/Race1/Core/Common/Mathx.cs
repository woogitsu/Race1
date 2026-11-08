using System;

namespace Race1.Core
{
    /// <summary>Small numeric helpers. Core code must not depend on UnityEngine.Mathf.</summary>
    public static class Mathx
    {
        public const double Deg2Rad = Math.PI / 180.0;
        public const double Rad2Deg = 180.0 / Math.PI;
        public const double Gravity = 9.80665;

        public static double Clamp(double v, double lo, double hi) => v < lo ? lo : (v > hi ? hi : v);

        public static double Clamp01(double v) => Clamp(v, 0.0, 1.0);

        public static double Lerp(double a, double b, double t) => a + (b - a) * t;

        public static double InverseLerp(double a, double b, double v) =>
            Math.Abs(b - a) < 1e-12 ? 0.0 : Clamp01((v - a) / (b - a));

        public static double SmoothStep(double edge0, double edge1, double x)
        {
            double t = Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3.0 - 2.0 * t);
        }

        public static double MoveTowards(double current, double target, double maxDelta)
        {
            double d = target - current;
            if (Math.Abs(d) <= maxDelta) return target;
            return current + Math.Sign(d) * maxDelta;
        }

        /// <summary>Wraps an angle to [-pi, pi].</summary>
        public static double WrapAngle(double a) => a - 2.0 * Math.PI * Math.Round(a / (2.0 * Math.PI));

        /// <summary>Piecewise-linear interpolation over ascending xs. Clamps outside the range.</summary>
        public static double Interp(double[] xs, double[] ys, double x)
        {
            int n = xs.Length;
            if (x <= xs[0]) return ys[0];
            if (x >= xs[n - 1]) return ys[n - 1];
            for (int i = 1; i < n; i++)
            {
                if (x <= xs[i])
                {
                    double t = (x - xs[i - 1]) / (xs[i] - xs[i - 1]);
                    return ys[i - 1] + (ys[i] - ys[i - 1]) * t;
                }
            }
            return ys[n - 1];
        }

        /// <summary>
        /// Critically damped spring (same family as Unity's SmoothDamp). Never overshoots the target.
        /// </summary>
        public static void SmoothDamp(ref double value, ref double velocity, double target, double smoothTime, double dt)
        {
            smoothTime = Math.Max(1e-4, smoothTime);
            double omega = 2.0 / smoothTime;
            double x = omega * dt;
            double exp = 1.0 / (1.0 + x + 0.48 * x * x + 0.235 * x * x * x);
            double change = value - target;
            double temp = (velocity + omega * change) * dt;
            velocity = (velocity - omega * temp) * exp;
            double result = target + (change + temp) * exp;
            // Prevent overshoot past the target.
            if ((target - value > 0.0) == (result > target))
            {
                result = target;
                velocity = (result - target) / Math.Max(dt, 1e-9);
            }
            value = result;
        }
    }
}
