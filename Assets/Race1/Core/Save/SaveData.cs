using System;

namespace Race1.Core
{
    // Save data uses only types that Unity's JsonUtility can serialise: no double, no dictionaries, no ulong.
    // Doubles that need full precision (route position) are stored as round-trip strings.

    [Serializable]
    public sealed class VehicleTuningData
    {
        public string VehicleId = "";
        public string PaintHex = "#C8102E";
        public string WheelStyle = "sport";
        public float SpringScale = 1f;
        public float DampingScale = 1f;
        public float FinalDriveScale = 1f;
        public float BrakeScale = 1f;
        public float TireCompound = 1f;   // 0 soft, 1 standard, 2 wet
    }

    [Serializable]
    public sealed class ProgressData
    {
        public float TotalDistanceKm;
        public float BestTimeAttackSeconds = -1f;
        public int BestTrafficScore;
        public int UnlockedVehicles = 1;
    }

    [Serializable]
    public sealed class SettingsData
    {
        public int ControlScheme;          // 0 touch wheel, 1 buttons, 2 tilt, 3 gamepad
        public int CameraMode;             // see CameraMode in the game layer
        public int QualityTier = 2;
        public float SteeringSensitivity = 1f;
        public float SteeringDeadzone = 0.05f;
        public float SteeringAssist = 0.5f;
        public int DrivingMode = 1;        // 0 arcade, 1 sport, 2 simulation
        public bool TrafficEnabled = true;
        public int Weather;
        public float TimeOfDay = 10f;
        public float ControlsScale = 1f;
        public float ControlsOffsetX;
        public float ControlsOffsetY;
    }

    [Serializable]
    public sealed class SaveData
    {
        public int Version = SaveMigrator.CurrentVersion;
        public string RouteSeed = "20260101";
        public string RouteS = "0";        // arc length, as a round-trip string
        public string RouteT = "0";
        public string SelectedVehicleId = "aurora-veloce";
        public VehicleTuningData[] Tunings = new VehicleTuningData[0];
        public ProgressData Progress = new ProgressData();
        public SettingsData Settings = new SettingsData();
    }

    /// <summary>Brings saves from older versions up to the current one. Never drops data it does not understand.</summary>
    public static class SaveMigrator
    {
        public const int CurrentVersion = 2;

        public static SaveData Migrate(SaveData data)
        {
            if (data == null) return new SaveData();
            if (data.Progress == null) data.Progress = new ProgressData();
            if (data.Settings == null) data.Settings = new SettingsData();
            if (data.Tunings == null) data.Tunings = new VehicleTuningData[0];

            if (data.Version < 1)
            {
                // Pre-release saves had no version field; keep the route and selected car.
                data.Version = 1;
            }
            if (data.Version < 2)
            {
                // Version 2 introduced settings and per-car tuning. Defaults were set by the constructors above.
                if (string.IsNullOrEmpty(data.SelectedVehicleId)) data.SelectedVehicleId = "aurora-veloce";
                data.Version = 2;
            }

            if (data.Version > CurrentVersion)
            {
                // Saved by a newer build. Keep it as-is; the game will read the fields it knows.
                return data;
            }
            return data;
        }

        public static string RoundTrip(double value) => value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);

        public static bool TryParseDouble(string text, out double value)
        {
            return double.TryParse(text, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out value) && double.IsFinite(value);
        }
    }
}
