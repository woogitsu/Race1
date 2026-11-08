using System;

namespace Race1.Core
{
    /// <summary>
    /// Maps raw controls (touch pads, sticks, tilt, triggers) to driving inputs. Pure functions, so every
    /// feel setting can be tested without a device.
    /// </summary>
    public static class InputShaping
    {
        /// <summary>Radial-free symmetric dead zone that rescales the remaining range back to 0..1.</summary>
        public static double Deadzone(double value, double zone)
        {
            double a = Math.Abs(value);
            if (a <= zone) return 0.0;
            double scaled = (a - zone) / Math.Max(1e-6, 1.0 - zone);
            return Math.Sign(value) * Math.Min(1.0, scaled);
        }

        /// <summary>Power curve: exponent above 1 gives fine control near centre, below 1 gives quick response.</summary>
        public static double Curve(double value, double exponent)
        {
            return Math.Sign(value) * Math.Pow(Math.Min(1.0, Math.Abs(value)), Math.Max(0.1, exponent));
        }

        /// <summary>Steering from a raw -1..1 value: dead zone, curve and sensitivity, clamped.</summary>
        public static double Steering(double raw, double deadzone, double exponent, double sensitivity)
        {
            return Mathx.Clamp(Curve(Deadzone(raw, deadzone), exponent) * sensitivity, -1.0, 1.0);
        }

        /// <summary>
        /// Tilt steering from the accelerometer X axis, in units of g. The phone is held near landscape,
        /// so a sideways tilt of maxTiltG gives full lock.
        /// </summary>
        public static double TiltSteering(double accelerationX, double maxTiltG, double deadzone, double sensitivity)
        {
            double raw = Mathx.Clamp(accelerationX / Math.Max(0.05, maxTiltG), -1.0, 1.0);
            return Steering(raw, deadzone, 1.4, sensitivity);
        }

        /// <summary>
        /// Analogue pedal: rises at riseRate and falls at fallRate per second. The pedal is held
        /// smoothly, so a tap gives a short, soft pulse rather than a switch.
        /// </summary>
        public static double Pedal(double current, double target, double riseRate, double fallRate, double dt)
        {
            double rate = target > current ? riseRate : fallRate;
            return Mathx.MoveTowards(current, Mathx.Clamp01(target), rate * dt);
        }

        /// <summary>Limits how fast the steering can change, so fast finger movements do not produce jerky wheel input.</summary>
        public static double SteerRate(double current, double target, double maxRatePerSecond, double dt)
        {
            return Mathx.MoveTowards(current, Mathx.Clamp(target, -1.0, 1.0), maxRatePerSecond * dt);
        }
    }
}
