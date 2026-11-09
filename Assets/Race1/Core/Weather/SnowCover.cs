using System;

namespace Race1.Core
{
    /// <summary>
    /// How much snow lies on the ground. The target follows the falling snow through a smooth ramp, so light snow
    /// leaves the ground bare. The cover itself moves towards the target at a limited rate: snow builds up over
    /// tens of seconds and melts more slowly than it builds, so the ground never changes in a single frame.
    /// </summary>
    public static class SnowCover
    {
        /// <summary>Cover gained per second while snow falls. Full cover takes about 25 s.</summary>
        public const double RiseRatePerSecond = 0.04;

        /// <summary>Cover lost per second once the snow stops. Melting is slower than building.</summary>
        public const double MeltRatePerSecond = 0.02;

        /// <summary>Snow amount below which the ground stays bare.</summary>
        public const double BareBelow = 0.15;

        /// <summary>Snow amount above which the ground is fully covered.</summary>
        public const double FullAbove = 0.6;

        /// <summary>Target cover for a falling-snow amount (0..1). Zero for no snow, one for heavy snow.</summary>
        public static double Target(double snow01)
        {
            if (double.IsNaN(snow01)) return 0.0;
            return Mathx.SmoothStep(BareBelow, FullAbove, Mathx.Clamp01(snow01));
        }

        /// <summary>
        /// Moves the cover one step towards the target, at most at the rise or melt rate for <paramref name="dt"/>
        /// seconds. It never overshoots the target and always stays within 0..1. Bad inputs leave the cover unchanged.
        /// </summary>
        public static double Step(double current, double target, double dt)
        {
            double cover = double.IsNaN(current) ? 0.0 : Mathx.Clamp01(current);
            if (double.IsNaN(target) || !(dt > 0.0) || double.IsInfinity(dt)) return cover;

            double goal = Mathx.Clamp01(target);
            double rate = goal > cover ? RiseRatePerSecond : MeltRatePerSecond;
            double limit = rate * dt;
            double change = Math.Max(-limit, Math.Min(limit, goal - cover));
            return Mathx.Clamp01(cover + change);
        }
    }
}
