namespace Race1.Core
{
    /// <summary>Driver controls for one frame. Values are sampled once per frame and held over its substeps.</summary>
    public struct VehicleInput
    {
        public double Throttle;       // 0..1
        public double Brake;          // 0..1
        public double Steer;          // -1 (full left) .. +1 (full right)
        public bool Handbrake;
        public int GearRequest;       // manual shift edge this frame: +1 up, -1 down, 0 none
        public bool AutoGearbox;

        public static VehicleInput Idle => new VehicleInput { AutoGearbox = true };
    }

    /// <summary>Electronic driver aids. Presets map to the Arcade, Sport and Simulation driving modes.</summary>
    public struct AssistSettings
    {
        public bool Abs;
        public bool TractionControl;
        public bool Esp;
        public double SteeringAssist;  // 0..1, yaw-rate correction towards the ideal rate

        public static AssistSettings Arcade => new AssistSettings { Abs = true, TractionControl = true, Esp = true, SteeringAssist = 0.8 };
        public static AssistSettings Sport => new AssistSettings { Abs = true, TractionControl = true, Esp = false, SteeringAssist = 0.0 };
        public static AssistSettings Simulation => new AssistSettings { Abs = false, TractionControl = false, Esp = false, SteeringAssist = 0.0 };
    }

    /// <summary>Read-only snapshot for HUD, camera, audio and visuals. Arrays are reused, not reallocated.</summary>
    public sealed class VehicleTelemetry
    {
        public double SpeedMs;
        public double SpeedKmh;
        public double EngineRpm;
        public int Gear;              // 1-based; 0 is never reported
        public double Throttle;       // effective throttle after aids
        public double Brake;
        public double SteerAngleRad;
        public double LongitudinalG;
        public double LateralG;
        public double YawRate;
        public double S;
        public double T;
        public double HeadingError;
        public double Boost;
        public double Odometer;
        public double Damage;
        public int CollisionCount;
        public bool TractionControlActive;
        public bool AbsActive;
        public bool EspActive;
        public bool ShiftInProgress;
        public readonly double[] Compression = new double[4];
        public readonly double[] Load = new double[4];
        public readonly double[] WheelSpeed = new double[4];
        public readonly double[] SlipRatio = new double[4];
        public readonly double[] SlipAngle = new double[4];
        public readonly double[] TyreWear = new double[4];         // 0 new .. 1 finished
        public readonly double[] TyreTemperature = new double[4];  // °C
    }
}
