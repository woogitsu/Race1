using System;

namespace Race1.Core
{
    /// <summary>
    /// How wet the windscreen is, from 0 (clear) to 1 (covered). Rain adds water; speed clears it through the wipers and
    /// the airflow over the glass. At a standstill nothing clears it except slow evaporation, so a parked car in the
    /// rain fogs up, and a fast car in the rain stays clearer.
    /// </summary>
    /// <remarks>
    /// The wetness follows dW/dt = A·rain − (A·rain + C(speed))·W. That linear equation has an exact solution, so the
    /// step is exact for any frame time and never overshoots the equilibrium A·rain / (A·rain + C) or leaves 0..1.
    /// </remarks>
    public static class WindscreenWetness
    {
        /// <summary>Water gained per second in full rain, as a fraction of the glass.</summary>
        public const double AccumulationPerSecond = 0.3;

        /// <summary>Evaporation per second with the car standing still.</summary>
        public const double IdleClearingPerSecond = 0.02;

        /// <summary>Extra clearing per second at <see cref="FullClearingSpeedMs"/>, from the wipers and airflow.</summary>
        public const double SpeedClearingPerSecond = 0.5;

        /// <summary>Speed at which clearing reaches its full rate (m/s).</summary>
        public const double FullClearingSpeedMs = 25.0;

        /// <summary>Clearing rate per second at a speed (m/s). Never below the idle rate.</summary>
        public static double ClearingRate(double speedMs)
        {
            if (double.IsNaN(speedMs)) return IdleClearingPerSecond;
            return IdleClearingPerSecond + SpeedClearingPerSecond * Mathx.SmoothStep(0.0, FullClearingSpeedMs, Math.Max(0.0, speedMs));
        }

        /// <summary>Wetness the glass settles at under a constant rain and speed.</summary>
        public static double Equilibrium(double rain01, double speedMs)
        {
            double gain = AccumulationPerSecond * Mathx.Clamp01(double.IsNaN(rain01) ? 0.0 : rain01);
            double clear = ClearingRate(speedMs);
            return gain / (gain + clear);
        }

        /// <summary>
        /// Advances the wetness by <paramref name="dt"/> seconds under the given rain (0..1) and speed (m/s). Bad inputs
        /// leave the wetness unchanged, and the result always stays in 0..1.
        /// </summary>
        public static double Step(double wetness, double rain01, double speedMs, double dt)
        {
            double w = double.IsNaN(wetness) ? 0.0 : Mathx.Clamp01(wetness);
            if (!(dt > 0.0) || double.IsInfinity(dt)) return w;

            double gain = AccumulationPerSecond * Mathx.Clamp01(double.IsNaN(rain01) ? 0.0 : rain01);
            double clear = ClearingRate(speedMs);
            double k = gain + clear;
            double equilibrium = gain / k;
            double next = equilibrium + (w - equilibrium) * Math.Exp(-k * dt);
            return Mathx.Clamp01(next);
        }
    }
}
