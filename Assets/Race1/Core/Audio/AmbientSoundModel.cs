using System;

namespace Race1.Core
{
    /// <summary>
    /// Maps the car and the weather onto the levels of the ambient sounds, each in 0..1: wind, tyre squeal and rain.
    /// Pure functions, so the mapping can be tested. The synth in <see cref="AmbientSynth"/> turns the levels into sound.
    /// </summary>
    public static class AmbientSoundModel
    {
        /// <summary>Wind is silent below this speed (m/s).</summary>
        public const double WindStartSpeedMs = 15.0;

        /// <summary>Wind reaches full level at this speed (m/s).</summary>
        public const double WindFullSpeedMs = 90.0;

        /// <summary>Combined slip (rad of slip angle plus unitless slip ratio) where tyres start to squeal.</summary>
        public const double SquealStartSlip = 0.10;

        /// <summary>Combined slip where the squeal is at full level.</summary>
        public const double SquealFullSlip = 0.30;

        /// <summary>Wind level from the car's speed.</summary>
        public static double WindLevel(double speedMs)
        {
            if (double.IsNaN(speedMs)) return 0.0;
            return Mathx.SmoothStep(WindStartSpeedMs, WindFullSpeedMs, speedMs);
        }

        /// <summary>Squeal level of one tyre from its slip angle (rad) and slip ratio. Signs do not matter.</summary>
        public static double SquealLevel(double slipAngleRad, double slipRatio)
        {
            if (double.IsNaN(slipAngleRad) || double.IsNaN(slipRatio)) return 0.0;
            double slip = Math.Abs(slipAngleRad) + Math.Abs(slipRatio);
            return Mathx.SmoothStep(SquealStartSlip, SquealFullSlip, slip);
        }

        /// <summary>Rain level from the weather's rain amount, clamped to 0..1.</summary>
        public static double RainLevel(double rain01)
        {
            if (double.IsNaN(rain01)) return 0.0;
            return Math.Max(0.0, Math.Min(1.0, rain01));
        }
    }
}
