using System;
using System.Collections.Generic;
using Race1.Core;

namespace Race1.Tests.Core
{
    /// <summary>Builds the same world objects the game uses, for a given seed.</summary>
    internal static class TestWorld
    {
        public static RoadTrack CreateTrack(ulong seed)
        {
            var settings = new RoadSettings { Seed = seed };
            var biomes = new BiomeField(settings.Seed);
            var terrain = new TerrainField(settings.Seed, biomes);
            return new RoadTrack(settings, terrain, biomes);
        }

        /// <summary>
        /// Simple road-following driver used as a test "autopilot": feed-forward steering from curvature,
        /// feedback on lateral offset and heading error, and a speed target from upcoming curvature.
        /// </summary>
        public static VehicleInput Autopilot(VehicleDynamics car, IRoadProvider road, double speedCap = 60.0)
        {
            VehicleSpec spec = car.Spec;
            RoadSample here = road.Sample(car.S);
            double v = car.Speed;

            double lookMax = 0.0;
            for (int i = 0; i <= 12; i++)
            {
                double k = Math.Abs(road.Sample(car.S + i * 5.0).Curvature);
                if (k > lookMax) lookMax = k;
            }
            double vLimit = Math.Min(speedCap, Math.Sqrt(0.85 * Mathx.Gravity / Math.Max(lookMax, 1e-4)));

            double feedForward = Math.Atan(spec.WheelbaseM * here.Curvature);
            double steerRad = feedForward - 0.12 * car.T - 0.9 * car.HeadingError;
            double maxSteer = spec.MaxSteerDeg * Mathx.Deg2Rad * (0.35 + 0.65 * Math.Exp(-Math.Abs(v) / 25.0));
            double steer = Mathx.Clamp(steerRad / maxSteer, -1.0, 1.0);

            double speedError = vLimit - v;
            double throttle = Mathx.Clamp(speedError * 0.8, 0.0, 1.0);
            double brake = Mathx.Clamp(-speedError * 0.6, 0.0, 1.0);
            return new VehicleInput { Throttle = throttle, Brake = brake, Steer = steer, AutoGearbox = true };
        }

        public static VehicleSpec SportRwd() => new VehicleSpec { Id = "test-rwd" };

        public static VehicleSpec AllWheel() => new VehicleSpec
        {
            Id = "test-awd",
            Drive = DriveLayout.AllWheel,
            Mass = 1550.0,
            MaxBrakeFront = 2100.0,
            MaxBrakeRear = 1600.0,
        };

        public static List<double> Range(double from, double to, double step)
        {
            var list = new List<double>();
            for (double x = from; x <= to + 1e-9; x += step) list.Add(x);
            return list;
        }
    }
}
