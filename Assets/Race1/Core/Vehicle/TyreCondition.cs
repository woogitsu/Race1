using System;

namespace Race1.Core
{
    /// <summary>
    /// Tyre wear and temperature. Wear comes from the power the tyre dissipates while it slides; temperature comes
    /// from the same power, cooled towards ambient. Both reduce grip: worn rubber grips less, and a tyre grips best
    /// inside its operating window. Every function here is pure, so the effects can be tested directly.
    /// </summary>
    public static class TyreCondition
    {
        public const double AmbientTemperature = 20.0;          // °C
        public const double OperatingStartTemperature = 90.0;   // °C, a tyre starts a run warm

        public const double OptimalMinTemperature = 85.0;       // °C, bottom of the grip window
        public const double OptimalMaxTemperature = 105.0;      // °C, top of the grip window

        private const double WearLossAtFull = 0.22;             // grip lost at full wear
        private const double WearExponent = 1.3;
        private const double WearPerJoule = 2.4e-8;             // wear fraction per joule of sliding work
        private const double ColdHotLossAtExtreme = 0.35;       // grip lost far outside the window
        private const double ExtremeDistance = 60.0;            // °C beyond the window where the loss saturates
        private const double HeatFraction = 0.5;                // share of sliding power that becomes tyre heat
        private const double CoolingConductance = 15.0;         // W/K, tyre to air
        private const double HeatCapacity = 1500.0;             // J/K

        /// <summary>Grip multiplier from wear: 1 when new, falling monotonically to about 0.78 when fully worn.</summary>
        public static double WearFactor(double wear)
        {
            double w = Mathx.Clamp01(wear);
            return 1.0 - WearLossAtFull * Math.Pow(w, WearExponent);
        }

        /// <summary>
        /// Grip multiplier from temperature: 1 inside the operating window, falling monotonically on both sides as the
        /// tyre gets further away, and never below 1 - <see cref="ColdHotLossAtExtreme"/>.
        /// </summary>
        public static double TemperatureFactor(double temperatureC)
        {
            double distance;
            if (temperatureC < OptimalMinTemperature) distance = OptimalMinTemperature - temperatureC;
            else if (temperatureC > OptimalMaxTemperature) distance = temperatureC - OptimalMaxTemperature;
            else distance = 0.0;
            return 1.0 - ColdHotLossAtExtreme * Mathx.SmoothStep(0.0, ExtremeDistance, distance);
        }

        /// <summary>Total grip multiplier for a tyre with the given wear and temperature.</summary>
        public static double GripMultiplier(double wear, double temperatureC)
        {
            return WearFactor(wear) * TemperatureFactor(temperatureC);
        }

        /// <summary>Adds the wear from <paramref name="slidingPower"/> (W) over <paramref name="dt"/> seconds. Never decreases.</summary>
        public static double AccumulateWear(double wear, double slidingPower, double dt)
        {
            if (!(slidingPower > 0.0) || !(dt > 0.0)) return Mathx.Clamp01(wear);
            return Mathx.Clamp01(wear + WearPerJoule * slidingPower * dt);
        }

        /// <summary>One explicit step of the tyre temperature: sliding power heats it, air cools it.</summary>
        public static double Heat(double temperatureC, double slidingPower, double dt, double ambientC)
        {
            double power = Math.Max(0.0, slidingPower);
            double net = HeatFraction * power - CoolingConductance * (temperatureC - ambientC);
            return temperatureC + dt * net / HeatCapacity;
        }
    }
}
