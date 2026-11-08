using System;

namespace Race1.Core
{
    /// <summary>
    /// Applies the player's garage tuning to a base car. Every value is clamped to a range that keeps the car
    /// drivable, so a saved or hand-edited file can never produce an invalid or absurd car. The base spec is never
    /// modified; a tuned copy is returned.
    /// </summary>
    public static class VehicleTuning
    {
        public const float MinSpringScale = 0.7f;
        public const float MaxSpringScale = 1.4f;
        public const float MinDampingScale = 0.6f;
        public const float MaxDampingScale = 1.5f;
        public const float MinFinalDriveScale = 0.85f;
        public const float MaxFinalDriveScale = 1.15f;
        public const float MinBrakeScale = 0.8f;
        public const float MaxBrakeScale = 1.2f;

        public const int TireSoft = 0;
        public const int TireStandard = 1;
        public const int TireWet = 2;

        /// <summary>Grip multiplier per tyre compound. Soft is grippier on dry asphalt, wet compound is weaker when dry.</summary>
        public static double TireGripMultiplier(int compound)
        {
            switch (compound)
            {
                case TireSoft: return 1.06;
                case TireWet: return 0.93;
                default: return 1.0;
            }
        }

        public static float Clamp(float value, float min, float max)
        {
            if (float.IsNaN(value)) return (min + max) * 0.5f;
            return value < min ? min : (value > max ? max : value);
        }

        /// <summary>
        /// Returns a tuned copy of <paramref name="baseSpec"/>. Springs scale both axles, damping is a ratio
        /// (so the damping ratio itself is scaled, not the raw coefficient), the final drive scales the gear ratio
        /// (a higher ratio means shorter gearing: more torque, lower top speed), brakes scale both axles, and the tyre compound scales grip.
        /// </summary>
        public static VehicleSpec Apply(VehicleSpec baseSpec, float springScale, float dampingScale,
            float finalDriveScale, float brakeScale, int tireCompound)
        {
            if (baseSpec == null) throw new ArgumentNullException(nameof(baseSpec));

            VehicleSpec tuned = baseSpec.Clone();
            double spring = Clamp(springScale, MinSpringScale, MaxSpringScale);
            double damping = Clamp(dampingScale, MinDampingScale, MaxDampingScale);
            double drive = Clamp(finalDriveScale, MinFinalDriveScale, MaxFinalDriveScale);
            double brake = Clamp(brakeScale, MinBrakeScale, MaxBrakeScale);

            tuned.SpringFront = baseSpec.SpringFront * spring;
            tuned.SpringRear = baseSpec.SpringRear * spring;
            tuned.DampingRatio = baseSpec.DampingRatio * damping;
            tuned.FinalDrive = baseSpec.FinalDrive * drive;
            tuned.MaxBrakeFront = baseSpec.MaxBrakeFront * brake;
            tuned.MaxBrakeRear = baseSpec.MaxBrakeRear * brake;
            tuned.GripDry = baseSpec.GripDry * TireGripMultiplier(tireCompound);

            tuned.Validate();
            return tuned;
        }

        /// <summary>Applies a saved tuning record. Unknown or missing values fall back to neutral.</summary>
        public static VehicleSpec Apply(VehicleSpec baseSpec, VehicleTuningData data)
        {
            if (data == null) return Apply(baseSpec, 1f, 1f, 1f, 1f, TireStandard);
            int compound = (int)Math.Round(Clamp(data.TireCompound, 0f, 2f));
            return Apply(baseSpec, data.SpringScale, data.DampingScale, data.FinalDriveScale, data.BrakeScale, compound);
        }

        /// <summary>Writes the clamped values back into a record, so saves never store out-of-range numbers.</summary>
        public static void Normalise(VehicleTuningData data)
        {
            if (data == null) return;
            data.SpringScale = Clamp(data.SpringScale, MinSpringScale, MaxSpringScale);
            data.DampingScale = Clamp(data.DampingScale, MinDampingScale, MaxDampingScale);
            data.FinalDriveScale = Clamp(data.FinalDriveScale, MinFinalDriveScale, MaxFinalDriveScale);
            data.BrakeScale = Clamp(data.BrakeScale, MinBrakeScale, MaxBrakeScale);
            data.TireCompound = Clamp(data.TireCompound, TireSoft, TireWet);
        }
    }
}
