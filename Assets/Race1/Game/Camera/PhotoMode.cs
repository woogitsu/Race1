using Race1.Core;
using UnityEngine;

namespace Race1.Game
{
    /// <summary>
    /// Drives the game camera for photo mode. The orbit maths lives in the core (<see cref="PhotoOrbit"/>); this
    /// class puts the result on the camera: position, aim at the car, and the field of view from the focal length.
    /// </summary>
    public sealed class PhotoMode
    {
        /// <summary>Aim point above the car's origin, so the camera frames the body rather than the road.</summary>
        public const double AimHeightM = 0.6;

        private readonly Camera _camera;

        public PhotoMode(Camera camera)
        {
            _camera = camera;
            Orbit = new PhotoOrbit();
        }

        public PhotoOrbit Orbit { get; }

        /// <summary>
        /// Places the camera for this frame. <paramref name="carPosition"/> is the car relative to the floating
        /// origin, the same frame the camera uses.
        /// </summary>
        public void Apply(Vector3d carPosition)
        {
            Vector3d aim = carPosition + new Vector3d(0.0, AimHeightM, 0.0);
            Vector3d eye = Orbit.PositionAround(aim);
            Transform t = _camera.transform;
            t.position = ToFloat(eye);
            t.LookAt(ToFloat(aim));
            _camera.fieldOfView = (float)Orbit.VerticalFovDegrees;
        }

        private static Vector3 ToFloat(Vector3d v) => new Vector3((float)v.X, (float)v.Y, (float)v.Z);
    }
}
