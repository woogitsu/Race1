using System;
using System.Collections.Generic;

namespace Race1.Core
{
    /// <summary>
    /// Original car designs. Parameters are chosen to give three clearly different driving characters:
    /// a light rear-drive sports car, a heavy all-wheel-drive turbo GT, and a front-drive hot hatch.
    /// Names and figures are original and not based on any manufacturer's model.
    /// </summary>
    public static class VehicleCatalog
    {
        public const string AuroraVeloce = "aurora-veloce";
        public const string TempestaGtx = "tempesta-gtx";
        public const string KestrelSr = "kestrel-sr";

        public static IReadOnlyList<string> Ids { get; } = new[] { AuroraVeloce, TempestaGtx, KestrelSr };

        public static VehicleSpec Create(string id)
        {
            VehicleSpec spec;
            switch (id)
            {
                case TempestaGtx:
                    spec = new VehicleSpec
                    {
                        Id = id,
                        DisplayName = "Tempesta GTX",
                        Mass = 1620.0,
                        WheelbaseM = 2.85,
                        RearWeightShare = 0.52,
                        CgHeightM = 0.50,
                        YawInertia = 3100.0,
                        UndersteerCoefficient = 0.0022,
                        GripDry = 1.18,
                        Drive = DriveLayout.AllWheel,
                        AwdFrontShare = 0.42,
                        Turbo = true,
                        TurboLagSeconds = 0.25,
                        RpmPoints = new double[] { 900, 1800, 2800, 3800, 4800, 5800, 6800, 7300 },
                        TorquePoints = new double[] { 260, 480, 590, 620, 620, 600, 560, 500 },
                        LimiterRpm = 7300,
                        GearRatios = new double[] { 3.6, 2.4, 1.8, 1.45, 1.18, 0.98, 0.82 },
                        FinalDrive = 3.2,
                        MaxBrakeFront = 2300.0,
                        MaxBrakeRear = 1700.0,
                        MaxSteerDeg = 30.0,
                        SpringFront = 60000.0,
                        SpringRear = 64000.0,
                        DownforceCoefficient = 0.22,
                        DragCoefficient = 0.33,
                    };
                    break;

                case KestrelSr:
                    spec = new VehicleSpec
                    {
                        Id = id,
                        DisplayName = "Kestrel SR",
                        Mass = 1290.0,
                        WheelbaseM = 2.58,
                        RearWeightShare = 0.40,
                        CgHeightM = 0.46,
                        YawInertia = 2050.0,
                        UndersteerCoefficient = 0.0019,
                        GripDry = 1.06,
                        Drive = DriveLayout.FrontWheel,
                        TrackFrontM = 1.55,
                        TrackRearM = 1.54,
                        RpmPoints = new double[] { 1000, 2000, 3000, 4000, 5000, 6000, 6800, 7400 },
                        TorquePoints = new double[] { 220, 340, 400, 420, 410, 390, 360, 330 },
                        LimiterRpm = 7400,
                        GearRatios = new double[] { 3.7, 2.3, 1.7, 1.3, 1.05, 0.88 },
                        FinalDrive = 3.8,
                        MaxBrakeFront = 1850.0,
                        MaxBrakeRear = 1150.0,
                        MaxSteerDeg = 34.0,
                        SpringFront = 50000.0,
                        SpringRear = 47000.0,
                    };
                    break;

                default:
                    spec = new VehicleSpec
                    {
                        Id = AuroraVeloce,
                        DisplayName = "Aurora Veloce",
                        Mass = 1180.0,
                        WheelbaseM = 2.55,
                        RearWeightShare = 0.46,
                        CgHeightM = 0.44,
                        YawInertia = 1950.0,
                        UndersteerCoefficient = 0.0012,
                        GripDry = 1.15,
                        Drive = DriveLayout.RearWheel,
                        TrackFrontM = 1.58,
                        TrackRearM = 1.56,
                        RpmPoints = new double[] { 900, 1500, 2500, 3500, 4500, 5500, 6500, 7200 },
                        TorquePoints = new double[] { 180, 300, 380, 430, 460, 455, 420, 380 },
                        LimiterRpm = 7200,
                        GearRatios = new double[] { 3.4, 2.2, 1.6, 1.25, 1.02, 0.85 },
                        FinalDrive = 3.5,
                        MaxBrakeFront = 1900.0,
                        MaxBrakeRear = 1300.0,
                        MaxSteerDeg = 31.0,
                    };
                    break;
            }
            spec.Validate();
            return spec;
        }
    }
}
