using System;

namespace Race1.Core
{
    /// <summary>
    /// Chase camera maths. The camera is a critically damped follower: it never overshoots, so a
    /// hard stop or a fast corner cannot make the view whip around. Lateral and longitudinal reactions
    /// are small and proportional to the car's g-forces.
    /// </summary>
    public sealed class ChaseCameraState
    {
        public Vector3d Position;
        public Vector3d LookAt;
        public double Fov = 65.0;

        private Vector3d _posVel;
        private Vector3d _lookVel;
        private double _fovVel;

        /// <param name="carPosition">Car body position (world, metres).</param>
        /// <param name="carForward">Car forward direction (unit).</param>
        /// <param name="carUp">Car up direction (unit).</param>
        /// <param name="speedMs">Car speed (m/s).</param>
        /// <param name="lateralG">Lateral acceleration in g (positive = to the right).</param>
        /// <param name="longitudinalG">Longitudinal acceleration in g (positive = forward).</param>
        /// <param name="dt">Frame time (s).</param>
        public void Step(Vector3d carPosition, Vector3d carForward, Vector3d carUp, double speedMs,
            double lateralG, double longitudinalG, double dt, ChaseCameraSettings settings)
        {
            Vector3d right = Vector3d.Cross(carUp, carForward).Normalized;
            Vector3d flatForward = new Vector3d(carForward.X, 0.0, carForward.Z).Normalized;
            if (flatForward.Length < 0.5) flatForward = carForward;

            double speedFactor = Mathx.Clamp01(speedMs / 80.0);
            double distance = settings.Distance + speedFactor * settings.SpeedDistanceGain;
            double height = settings.Height + speedFactor * 0.3;

            // Reactions: the camera leans slightly against the force, but stays within a small limit.
            double lean = Mathx.Clamp(lateralG, -1.5, 1.5) * settings.LateralReaction;
            double pitch = Mathx.Clamp(longitudinalG, -1.5, 1.5) * settings.LongitudinalReaction;

            Vector3d target = carPosition
                - flatForward * (distance + pitch)
                + Vector3d.Up * height
                - right * lean;

            Vector3d look = carPosition + carForward * settings.LookAhead + Vector3d.Up * 0.6;

            SmoothVector(ref Position, ref _posVel, target, settings.PositionSmoothTime, dt);
            SmoothVector(ref LookAt, ref _lookVel, look, settings.LookSmoothTime, dt);

            double targetFov = settings.BaseFov + speedFactor * settings.SpeedFovGain;
            Mathx.SmoothDamp(ref Fov, ref _fovVel, targetFov, settings.FovSmoothTime, dt);
        }

        /// <summary>Snaps the camera to the car, for cuts and mode changes.</summary>
        public void Snap(Vector3d carPosition, Vector3d carForward, double speedMs, ChaseCameraSettings settings)
        {
            Vector3d flatForward = new Vector3d(carForward.X, 0.0, carForward.Z).Normalized;
            Position = carPosition - flatForward * settings.Distance + Vector3d.Up * settings.Height;
            LookAt = carPosition + carForward * settings.LookAhead;
            _posVel = Vector3d.Zero;
            _lookVel = Vector3d.Zero;
            Fov = settings.BaseFov;
            _fovVel = 0.0;
        }

        private static void SmoothVector(ref Vector3d value, ref Vector3d velocity, Vector3d target, double smoothTime, double dt)
        {
            double x = value.X, y = value.Y, z = value.Z;
            double vx = velocity.X, vy = velocity.Y, vz = velocity.Z;
            Mathx.SmoothDamp(ref x, ref vx, target.X, smoothTime, dt);
            Mathx.SmoothDamp(ref y, ref vy, target.Y, smoothTime, dt);
            Mathx.SmoothDamp(ref z, ref vz, target.Z, smoothTime, dt);
            value = new Vector3d(x, y, z);
            velocity = new Vector3d(vx, vy, vz);
        }
    }

    public sealed class ChaseCameraSettings
    {
        public double Distance = 7.0;
        public double Height = 2.4;
        public double SpeedDistanceGain = 1.5;
        public double LookAhead = 12.0;
        public double BaseFov = 62.0;
        public double SpeedFovGain = 12.0;
        public double LateralReaction = 0.25;
        public double LongitudinalReaction = 0.35;
        public double PositionSmoothTime = 0.18;
        public double LookSmoothTime = 0.12;
        public double FovSmoothTime = 0.6;
    }
}
