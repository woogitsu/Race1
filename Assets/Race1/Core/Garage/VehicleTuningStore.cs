using System;
using System.Collections.Generic;

namespace Race1.Core
{
    /// <summary>
    /// Per-car tuning records inside the save. A record is created on the first edit of a car, and each car has at
    /// most one. Reading never changes the save, so cars that were never tuned add nothing to it.
    /// </summary>
    public static class VehicleTuningStore
    {
        /// <summary>The saved record for a car, or a neutral record that is not stored. Never changes the save.</summary>
        public static VehicleTuningData Read(SaveData save, string vehicleId)
        {
            return Find(save, vehicleId) ?? Neutral(vehicleId);
        }

        /// <summary>Sets one knob from a slider position (0..1), creating the car's record if needed.</summary>
        public static void SetFromSlider(SaveData save, string vehicleId, TuningKnob knob, double position)
        {
            VehicleTuningData record = Ensure(save, vehicleId);
            TuningControls.Write(record, knob, TuningControls.ToValue(knob, position));
        }

        /// <summary>Moves the car's tyre compound to the next choice.</summary>
        public static void CycleTyres(SaveData save, string vehicleId)
        {
            VehicleTuningData record = Ensure(save, vehicleId);
            record.TireCompound = TuningControls.NextTyre((int)Math.Round(record.TireCompound));
            VehicleTuning.Normalise(record);
        }

        /// <summary>Sets every knob and the tyre back to neutral. The record stays, so the car keeps its place in the save.</summary>
        public static void Reset(SaveData save, string vehicleId)
        {
            VehicleTuningData record = Ensure(save, vehicleId);
            record.SpringScale = 1f;
            record.DampingScale = 1f;
            record.FinalDriveScale = 1f;
            record.BrakeScale = 1f;
            record.TireCompound = VehicleTuning.TireStandard;
            VehicleTuning.Normalise(record);
        }

        public static VehicleTuningData Neutral(string vehicleId)
        {
            return new VehicleTuningData { VehicleId = vehicleId ?? "", TireCompound = VehicleTuning.TireStandard };
        }

        private static VehicleTuningData Find(SaveData save, string vehicleId)
        {
            if (save == null || save.Tunings == null) return null;
            foreach (VehicleTuningData record in save.Tunings)
            {
                if (record != null && record.VehicleId == vehicleId) return record;
            }
            return null;
        }

        private static VehicleTuningData Ensure(SaveData save, string vehicleId)
        {
            if (save == null) throw new ArgumentNullException(nameof(save));
            VehicleTuningData found = Find(save, vehicleId);
            if (found != null) return found;

            VehicleTuningData record = Neutral(vehicleId);
            var list = new List<VehicleTuningData>(save.Tunings ?? new VehicleTuningData[0]) { record };
            save.Tunings = list.ToArray();
            return record;
        }
    }
}
