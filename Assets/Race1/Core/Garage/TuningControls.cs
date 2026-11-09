using System;

namespace Race1.Core
{
    /// <summary>The continuous garage knobs. Each one has a slider; the tyre compound is a separate choice.</summary>
    public enum TuningKnob
    {
        Spring = 0,
        Damping = 1,
        FinalDrive = 2,
        Brake = 3,
    }

    /// <summary>
    /// Slider mapping for the garage. A slider position runs from 0 to 1 across the range that
    /// <see cref="VehicleTuning"/> allows, so every position is a legal setting. Also reads and writes one knob of
    /// a saved record, and names the choices for the menu.
    /// </summary>
    public static class TuningControls
    {
        public const int KnobCount = 4;

        public static float Min(TuningKnob knob)
        {
            switch (knob)
            {
                case TuningKnob.Spring: return VehicleTuning.MinSpringScale;
                case TuningKnob.Damping: return VehicleTuning.MinDampingScale;
                case TuningKnob.FinalDrive: return VehicleTuning.MinFinalDriveScale;
                default: return VehicleTuning.MinBrakeScale;
            }
        }

        public static float Max(TuningKnob knob)
        {
            switch (knob)
            {
                case TuningKnob.Spring: return VehicleTuning.MaxSpringScale;
                case TuningKnob.Damping: return VehicleTuning.MaxDampingScale;
                case TuningKnob.FinalDrive: return VehicleTuning.MaxFinalDriveScale;
                default: return VehicleTuning.MaxBrakeScale;
            }
        }

        public static string Label(TuningKnob knob)
        {
            switch (knob)
            {
                case TuningKnob.Spring: return "Springs";
                case TuningKnob.Damping: return "Damping";
                case TuningKnob.FinalDrive: return "Final drive";
                default: return "Brakes";
            }
        }

        /// <summary>Tuning value at a slider position. The position is clamped to 0..1; NaN means the middle.</summary>
        public static float ToValue(TuningKnob knob, double position)
        {
            double p = ClampUnit(position);
            double lo = Min(knob), hi = Max(knob);
            return (float)(lo + (hi - lo) * p);
        }

        /// <summary>Slider position of a tuning value. The value is clamped to the knob's range; NaN means the middle.</summary>
        public static double ToPosition(TuningKnob knob, float value)
        {
            double lo = Min(knob), hi = Max(knob);
            if (float.IsNaN(value)) return 0.5;
            return ClampUnit((value - lo) / (hi - lo));
        }

        /// <summary>Signed percent change from neutral (1.0): "+10%", "0%", "-30%".</summary>
        public static string Readout(float value)
        {
            if (float.IsNaN(value)) return "n/a";
            int percent = (int)Math.Round((value - 1.0) * 100.0);
            if (percent == 0) return "0%";
            return percent > 0 ? "+" + percent + "%" : percent + "%";
        }

        /// <summary>Reads one knob of a record. A missing record is neutral.</summary>
        public static float Read(VehicleTuningData data, TuningKnob knob)
        {
            if (data == null) return 1f;
            switch (knob)
            {
                case TuningKnob.Spring: return data.SpringScale;
                case TuningKnob.Damping: return data.DampingScale;
                case TuningKnob.FinalDrive: return data.FinalDriveScale;
                default: return data.BrakeScale;
            }
        }

        /// <summary>Writes one knob of a record, clamped to its range. The record is normalised afterwards.</summary>
        public static void Write(VehicleTuningData data, TuningKnob knob, float value)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            float clamped = VehicleTuning.Clamp(value, Min(knob), Max(knob));
            switch (knob)
            {
                case TuningKnob.Spring: data.SpringScale = clamped; break;
                case TuningKnob.Damping: data.DampingScale = clamped; break;
                case TuningKnob.FinalDrive: data.FinalDriveScale = clamped; break;
                default: data.BrakeScale = clamped; break;
            }
            VehicleTuning.Normalise(data);
        }

        public static string TyreName(int compound)
        {
            switch (compound)
            {
                case VehicleTuning.TireSoft: return "soft";
                case VehicleTuning.TireWet: return "wet";
                default: return "standard";
            }
        }

        /// <summary>The next compound in the order soft, standard, wet, then back to soft.</summary>
        public static int NextTyre(int compound)
        {
            int c = Math.Max(VehicleTuning.TireSoft, Math.Min(VehicleTuning.TireWet, compound));
            return c == VehicleTuning.TireSoft ? VehicleTuning.TireStandard
                : c == VehicleTuning.TireStandard ? VehicleTuning.TireWet
                : VehicleTuning.TireSoft;
        }

        private static double ClampUnit(double v)
        {
            if (double.IsNaN(v)) return 0.5;
            return Math.Max(0.0, Math.Min(1.0, v));
        }
    }
}
