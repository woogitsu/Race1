using System;

namespace Race1.Core
{
    /// <summary>
    /// Intelligent Driver Model (Treiber et al.): smooth, collision-free car following.
    /// Used for AI traffic and for the lane-change gap checks.
    /// </summary>
    public struct IdmParameters
    {
        public double DesiredSpeed;   // m/s
        public double MinGap;         // s0, m, standstill gap
        public double TimeHeadway;    // T, s
        public double MaxAccel;       // a, m/s^2
        public double ComfortDecel;   // b, m/s^2
        public double Exponent;       // delta

        public static IdmParameters Default(double desiredSpeed) => new IdmParameters
        {
            DesiredSpeed = desiredSpeed,
            MinGap = 2.5,
            TimeHeadway = 1.4,
            MaxAccel = 1.6,
            ComfortDecel = 2.0,
            Exponent = 4.0,
        };
    }

    public static class IntelligentDriverModel
    {
        /// <summary>
        /// Acceleration of a follower. gap is bumper-to-bumper distance to the leader (m),
        /// approachRate is follower speed minus leader speed (m/s). Use double.PositiveInfinity for no leader.
        /// </summary>
        public static double Acceleration(in IdmParameters p, double speed, double gap, double approachRate)
        {
            double v0 = Math.Max(0.1, p.DesiredSpeed);
            double free = 1.0 - Math.Pow(Math.Max(0.0, speed) / v0, p.Exponent);
            if (double.IsPositiveInfinity(gap))
            {
                return p.MaxAccel * free;
            }

            double gapSafe = Math.Max(0.01, gap);
            double sStar = p.MinGap + Math.Max(0.0, speed * p.TimeHeadway
                                                    + speed * approachRate / (2.0 * Math.Sqrt(p.MaxAccel * p.ComfortDecel)));
            double interaction = sStar / gapSafe;
            return p.MaxAccel * (free - interaction * interaction);
        }
    }
}
