using System;

namespace Race1.Core
{
    /// <summary>
    /// Orbit camera for photo mode. The camera circles a target at a chosen distance, yaw and pitch. The focal
    /// length sets the vertical field of view as seen through a full-frame sensor (24 mm high), so a 50 mm lens
    /// gives about 27 degrees. Pure maths: the game turns the result into a camera transform.
    /// </summary>
    public sealed class PhotoOrbit
    {
        public const double MinDistance = 3.0;              // m
        public const double MaxDistance = 40.0;             // m
        public const double MinPitchDeg = -2.0;             // just above the road beside the car
        public const double MaxPitchDeg = 70.0;
        public const double MinFocalMm = 14.0;
        public const double MaxFocalMm = 200.0;
        public const double SensorHeightMm = 24.0;          // full-frame sensor height

        private const double Deg = Math.PI / 180.0;
        private const double TwoPi = 2.0 * Math.PI;
        private const double DragRadiansPerScreen = Math.PI; // dragging across the whole screen turns half a circle
        private const double ZoomStep = 1.2;
        private const double FocalStep = 1.25;

        private double _yaw;      // radians, kept in (-pi, pi]
        private double _pitch;    // radians
        private double _distance;
        private double _focal;

        /// <summary>Starts from a side view of the car at a slight downward angle, with a standard lens.</summary>
        public PhotoOrbit(double distance = 9.0, double yawDeg = 90.0, double pitchDeg = 12.0, double focalMm = 35.0)
        {
            _distance = Clamp(distance, MinDistance, MaxDistance);
            _yaw = WrapAngle(yawDeg * Deg);
            _pitch = Clamp(pitchDeg, MinPitchDeg, MaxPitchDeg) * Deg;
            _focal = Clamp(focalMm, MinFocalMm, MaxFocalMm);
        }

        public double YawRadians => _yaw;
        public double PitchRadians => _pitch;
        public double Distance => _distance;
        public double FocalLengthMm => _focal;

        /// <summary>Vertical field of view in degrees for the current focal length.</summary>
        public double VerticalFovDegrees => 2.0 * Math.Atan(SensorHeightMm / (2.0 * _focal)) / Deg;

        /// <summary>
        /// Turns the camera by a drag in screen pixels. Dragging across the screen turns half a circle. Dragging
        /// finger-up (negative <paramref name="dyPixels"/>) raises the camera.
        /// </summary>
        public void Drag(double dxPixels, double dyPixels, double screenHeightPixels)
        {
            if (!(screenHeightPixels > 0.0) || !IsFinite(dxPixels) || !IsFinite(dyPixels)) return;
            double k = DragRadiansPerScreen / screenHeightPixels;
            _yaw = WrapAngle(_yaw + dxPixels * k);
            _pitch = Clamp(_pitch - dyPixels * k, MinPitchDeg * Deg, MaxPitchDeg * Deg);
        }

        /// <summary>Moves the camera towards (factor below 1) or away from (factor above 1) the target.</summary>
        public void Zoom(double factor)
        {
            if (!(factor > 0.0) || double.IsInfinity(factor)) return;
            _distance = Clamp(_distance * factor, MinDistance, MaxDistance);
        }

        /// <summary>One focal step: positive for a longer lens (narrower view), negative for a wider one.</summary>
        public void StepFocal(int direction)
        {
            if (direction == 0) return;
            double factor = direction > 0 ? FocalStep : 1.0 / FocalStep;
            _focal = Clamp(_focal * factor, MinFocalMm, MaxFocalMm);
        }

        /// <summary>Convenience for button zoom: one step in or out.</summary>
        public void ZoomStepIn() => Zoom(1.0 / ZoomStep);

        public void ZoomStepOut() => Zoom(ZoomStep);

        /// <summary>Camera position for a target point, in the same frame as the target. The camera looks at the target.</summary>
        public Vector3d PositionAround(Vector3d target)
        {
            double cp = Math.Cos(_pitch);
            return new Vector3d(
                target.X + _distance * Math.Sin(_yaw) * cp,
                target.Y + _distance * Math.Sin(_pitch),
                target.Z + _distance * Math.Cos(_yaw) * cp);
        }

        private static double WrapAngle(double a)
        {
            if (!IsFinite(a)) return 0.0;
            return a - TwoPi * Math.Round(a / TwoPi);
        }

        private static double Clamp(double v, double lo, double hi) => Math.Max(lo, Math.Min(hi, v));

        private static bool IsFinite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);
    }
}
