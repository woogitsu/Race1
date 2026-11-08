using Race1.Core;
using UnityEngine;

namespace Race1.Game
{
    public enum CameraMode
    {
        Chase = 0,      // behind the car, Gran Turismo style
        Bumper = 1,     // closer and lower, for speed
        Hood = 2,       // above the bonnet, looking down the road
        Cockpit = 3,    // driver's seat, with a small sway
        Cinematic = 4,  // slow orbit for replays and showing off the road
    }

    /// <summary>Owns the game camera and switches between view modes without cuts in the chase views.</summary>
    public sealed class CameraDirector
    {
        private readonly Camera _camera;
        private readonly ChaseCameraState _chaseState = new ChaseCameraState();
        private readonly ChaseCameraSettings _chase = new ChaseCameraSettings();
        private readonly ChaseCameraSettings _bumper = new ChaseCameraSettings { Distance = 4.2, Height = 1.0, LookAhead = 14.0, BaseFov = 70.0, SpeedFovGain = 14.0 };
        private double _orbit;
        private Vector3d _cinematicPos;

        public CameraDirector(Camera camera)
        {
            _camera = camera;
            _camera.nearClipPlane = 0.08f;
            _camera.farClipPlane = 2500f;
            Mode = CameraMode.Chase;
        }

        public CameraMode Mode { get; private set; }

        public void Cycle()
        {
            Mode = (CameraMode)(((int)Mode + 1) % 5);
        }

        public void Tick(float dt, CarView car, VehicleTelemetry tm, Vector3d origin, CameraEffectsOutput fx)
        {
            Vector3d carPos = car.WorldPosition;
            Vector3d fwd = car.WorldForward;
            Vector3d up = car.WorldUp;
            Vector3d right = Vector3d.Cross(up, fwd).Normalized;
            double lateralG = tm != null ? tm.LateralG : 0.0;
            double longG = tm != null ? tm.LongitudinalG : 0.0;
            double speed = tm != null ? tm.SpeedMs : 0.0;

            Vector3d position;
            Vector3d lookAt;
            double fov;

            switch (Mode)
            {
                case CameraMode.Bumper:
                    _chaseState.Step(carPos, fwd, up, speed, lateralG, longG, dt, _bumper);
                    position = _chaseState.Position;
                    lookAt = _chaseState.LookAt;
                    fov = _chaseState.Fov;
                    break;

                case CameraMode.Hood:
                    position = carPos + fwd * 1.15 + up * 1.02;
                    lookAt = carPos + fwd * 40.0 + up * 0.8;
                    fov = 72.0;
                    break;

                case CameraMode.Cockpit:
                    double sway = Mathx.Clamp(lateralG, -1.0, 1.0) * 0.04;
                    position = carPos + fwd * 0.2 + up * 1.02 - right * (0.36 - sway);
                    lookAt = carPos + fwd * 60.0 + up * 0.9;
                    fov = 68.0;
                    break;

                case CameraMode.Cinematic:
                    _orbit += dt * 0.12;
                    double radius = 9.0;
                    Vector3d target = carPos + up * 0.8;
                    Vector3d offset = right * (System.Math.Cos(_orbit) * radius) + fwd * (System.Math.Sin(_orbit) * radius);
                    position = target + offset + up * 2.6;
                    lookAt = target;
                    fov = 46.0;
                    break;

                default:
                    _chaseState.Step(carPos, fwd, up, speed, lateralG, longG, dt, _chase);
                    position = _chaseState.Position;
                    lookAt = _chaseState.LookAt;
                    fov = _chaseState.Fov;
                    break;
            }

            // Camera effects: impact shake and cockpit bob move the view, roll leans it, FOV kicks with speed.
            _camera.fieldOfView = (float)(fov + fx.FovOffsetDeg);
            Vector3 shake = CarView.ToFloat(right * fx.ShakeX + up * (fx.ShakeY + fx.HeadBobY));
            Vector3 p = CarView.ToFloat(position - origin) + shake;
            Vector3 l = CarView.ToFloat(lookAt - origin) + shake;
            _camera.transform.position = p;
            Vector3 dir = l - p;
            if (dir.sqrMagnitude > 1e-6f)
            {
                Quaternion look = Quaternion.LookRotation(dir.normalized, Vector3.up);
                float rollDeg = (float)(fx.RollRad * Mathx.Rad2Deg);
                _camera.transform.rotation = look * Quaternion.Euler(0f, 0f, rollDeg);
            }
        }

        /// <summary>Immediately places the chase camera behind a freshly spawned car (no sweep from the origin).</summary>
        public void Snap(CarView car)
        {
            _chaseState.Snap(car.WorldPosition, car.WorldForward, 0.0, _chase);
        }
    }
}
