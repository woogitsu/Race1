using System;

namespace Race1.Core
{
    public enum DriveLayout
    {
        FrontWheel = 0,
        RearWheel = 1,
        AllWheel = 2,
    }

    /// <summary>
    /// Physical description of a car. Pure data, validated before use. Units: SI (kg, m, N, Nm, rad/s).
    /// Corner index convention everywhere in the core: 0 = front left, 1 = front right, 2 = rear left, 3 = rear right.
    /// </summary>
    public sealed class VehicleSpec
    {
        public string Id = "car";
        public string DisplayName = "Car";

        // Chassis
        public double Mass = 1250.0;
        public double WheelbaseM = 2.7;
        public double RearWeightShare = 0.47;   // static share of weight on the rear axle
        public double CgHeightM = 0.48;
        public double TrackFrontM = 1.6;
        public double TrackRearM = 1.6;
        public double YawInertia = 2400.0;
        public double UndersteerCoefficient = 0.0015; // s^2/m; reduces the ideal yaw rate at speed
        public double GuardRailOffset = 2.0;          // m beyond the shoulder before a barrier hit
        public double LengthM = 4.4;
        public double WidthM = 1.9;
        public double HeightM = 1.25;

        // Tyres
        public double WheelRadius = 0.34;
        public double WheelInertia = 1.6;
        public double GripDry = 1.12;           // peak friction coefficient on dry asphalt
        public double LoadSensitivity = 0.12;   // friction falls as load rises above the reference
        public double ReferenceTireLoad = 3800.0;
        public double RollingResistance = 0.011;
        public double RollShareFront = 0.55;    // share of lateral load transfer carried by the front axle

        // Suspension (per corner spring-damper)
        public double SpringFront = 52000.0;    // N/m
        public double SpringRear = 56000.0;     // N/m
        public double DampingRatio = 0.45;
        public double AeroFrontShare = 0.42;

        // Aerodynamics
        public double DragCoefficient = 0.31;
        public double FrontalArea = 2.05;
        public double DownforceCoefficient = 0.15;

        // Steering
        public double MaxSteerDeg = 32.0;
        public double SteerRateRadPerSecond = 3.5;

        // Engine
        public double[] RpmPoints = { 900, 1500, 2500, 3500, 4500, 5500, 6500, 7200 };
        public double[] TorquePoints = { 180, 300, 380, 430, 460, 455, 420, 380 };
        public double IdleRpm = 900.0;
        public double LimiterRpm = 7300.0;
        public bool Turbo = false;
        public double TurboLagSeconds = 0.3;
        public double EngineBrakeNm = 90.0;

        // Drivetrain
        public double[] GearRatios = { 3.4, 2.2, 1.6, 1.25, 1.02, 0.85 };
        public double FinalDrive = 3.5;
        public DriveLayout Drive = DriveLayout.RearWheel;
        public double AwdFrontShare = 0.4;
        public double DrivetrainEfficiency = 0.92;
        public double ShiftTime = 0.18;
        public double AutoUpshiftRpm = 6800.0;
        public double AutoDownshiftRpm = 3400.0;

        // Brakes (torque per wheel at full pedal)
        public double MaxBrakeFront = 1900.0;
        public double MaxBrakeRear = 1300.0;
        public double HandbrakeTorque = 1500.0;

        /// <summary>Longitudinal position of the front axle from the centre of gravity (m, positive forward).</summary>
        public double FrontAxleX => WheelbaseM * RearWeightShare;

        /// <summary>Longitudinal position of the rear axle from the centre of gravity (m).</summary>
        public double RearAxleX => -WheelbaseM * (1.0 - RearWeightShare);

        public void Validate()
        {
            if (Mass <= 0 || WheelbaseM <= 0 || YawInertia <= 0 || WheelRadius <= 0 || WheelInertia <= 0)
                throw new ArgumentException($"{Id}: non-positive physical parameter");
            if (RearWeightShare <= 0.05 || RearWeightShare >= 0.95)
                throw new ArgumentException($"{Id}: rear weight share out of range");
            if (RpmPoints.Length != TorquePoints.Length || RpmPoints.Length < 2)
                throw new ArgumentException($"{Id}: torque curve arrays must match and have 2+ points");
            for (int i = 1; i < RpmPoints.Length; i++)
                if (RpmPoints[i] <= RpmPoints[i - 1])
                    throw new ArgumentException($"{Id}: RpmPoints must be ascending");
            if (GearRatios.Length == 0)
                throw new ArgumentException($"{Id}: no gears");
            if (LimiterRpm <= IdleRpm || AutoUpshiftRpm > LimiterRpm || AutoDownshiftRpm >= AutoUpshiftRpm)
                throw new ArgumentException($"{Id}: inconsistent rpm thresholds");
            if (GripDry <= 0 || GripDry > 2.0)
                throw new ArgumentException($"{Id}: grip out of range");
        }

        public VehicleSpec Clone()
        {
            var c = (VehicleSpec)MemberwiseClone();
            c.RpmPoints = (double[])RpmPoints.Clone();
            c.TorquePoints = (double[])TorquePoints.Clone();
            c.GearRatios = (double[])GearRatios.Clone();
            return c;
        }
    }
}
