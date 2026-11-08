using Race1.Core;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Race1.Game
{
    /// <summary>
    /// Merges touch, gamepad, keyboard and tilt into one driving input. Every source is shaped by the core
    /// InputShaping functions, so dead zones and feel are the same on all devices.
    /// </summary>
    public sealed class PlayerInputReader
    {
        public struct Frame
        {
            public VehicleInput Vehicle;
            public bool PausePressed;
            public bool CameraPressed;
        }

        public sealed class Settings
        {
            public double SteeringDeadzone = 0.05;
            public double SteeringSensitivity = 1.0;
            public double SteeringExponent = 1.3;
            public bool TiltEnabled;
            public bool TiltInvert;
            public double TiltMaxG = 0.45;
            public bool ManualGearbox;
        }

        private readonly OnScreenControls _touch;
        private readonly Settings _settings;
        private double _steer;
        private double _throttle;
        private double _brake;
        private int _gearRequest;
#if ENABLE_INPUT_SYSTEM
        private Accelerometer _accelerometer;
        private bool _accelEnabled;
#endif

        public PlayerInputReader(OnScreenControls touch, Settings settings)
        {
            _touch = touch;
            _settings = settings;
        }

        public Frame Read(float dt, bool drivingAllowed)
        {
            var frame = new Frame();
            if (!drivingAllowed)
            {
                frame.Vehicle = VehicleInput.Idle;
                frame.PausePressed = false;
                return frame;
            }

            double rawSteer = 0.0;
            double rawThrottle = 0.0;
            double rawBrake = 0.0;
            bool handbrake = false;
            int gear = 0;
            frame.CameraPressed = false;

            if (_touch != null)
            {
                rawSteer += _touch.Steering.Value;
                rawThrottle = System.Math.Max(rawThrottle, _touch.Throttle.Held ? 1.0 : 0.0);
                rawBrake = System.Math.Max(rawBrake, _touch.Brake.Held ? 1.0 : 0.0);
                handbrake |= _touch.Handbrake.Held;
                if (_touch.GearUp.ConsumeEdge()) gear += 1;
                if (_touch.GearDown.ConsumeEdge()) gear -= 1;
                if (_touch.Camera.ConsumeEdge()) frame.CameraPressed = true;
                if (_touch.Pause.ConsumeEdge()) frame.PausePressed = true;
            }

            ReadGamepad(ref rawSteer, ref rawThrottle, ref rawBrake, ref handbrake, ref gear, ref frame);
            ReadKeyboard(ref rawSteer, ref rawThrottle, ref rawBrake, ref handbrake, ref gear, ref frame);
            if (_settings.TiltEnabled) rawSteer += ReadTilt();

            double steer = InputShaping.Steering(Mathx.Clamp(rawSteer, -1.0, 1.0), _settings.SteeringDeadzone,
                _settings.SteeringExponent, _settings.SteeringSensitivity);
            _steer = InputShaping.SteerRate(_steer, steer, 5.0, dt);
            _throttle = InputShaping.Pedal(_throttle, Mathx.Clamp01(rawThrottle), 3.0, 6.0, dt);
            _brake = InputShaping.Pedal(_brake, Mathx.Clamp01(rawBrake), 4.0, 6.0, dt);
            _gearRequest = gear;

            frame.Vehicle = new VehicleInput
            {
                Steer = _steer,
                Throttle = _throttle,
                Brake = _brake,
                Handbrake = handbrake,
                GearRequest = _settings.ManualGearbox ? _gearRequest : 0,
                AutoGearbox = !_settings.ManualGearbox,
            };
            return frame;
        }

        private void ReadGamepad(ref double steer, ref double throttle, ref double brake, ref bool handbrake, ref int gear, ref Frame frame)
        {
#if ENABLE_INPUT_SYSTEM
            Gamepad gp = Gamepad.current;
            if (gp == null) return;
            steer += gp.leftStick.ReadValue().x;
            throttle = System.Math.Max(throttle, gp.rightTrigger.ReadValue());
            brake = System.Math.Max(brake, gp.leftTrigger.ReadValue());
            handbrake |= gp.buttonSouth.isPressed;
            if (gp.rightShoulder.wasPressedThisFrame) gear += 1;
            if (gp.leftShoulder.wasPressedThisFrame) gear -= 1;
            if (gp.buttonNorth.wasPressedThisFrame) frame.CameraPressed = true;
            if (gp.startButton.wasPressedThisFrame) frame.PausePressed = true;
#endif
        }

        private void ReadKeyboard(ref double steer, ref double throttle, ref double brake, ref bool handbrake, ref int gear, ref Frame frame)
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard kb = Keyboard.current;
            if (kb == null) return;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) steer -= 1.0;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) steer += 1.0;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) throttle = 1.0;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) brake = 1.0;
            handbrake |= kb.spaceKey.isPressed;
            if (kb.eKey.wasPressedThisFrame) gear += 1;
            if (kb.qKey.wasPressedThisFrame) gear -= 1;
            if (kb.cKey.wasPressedThisFrame) frame.CameraPressed = true;
            if (kb.escapeKey.wasPressedThisFrame) frame.PausePressed = true;
#endif
        }

        /// <summary>Sideways tilt in g from the accelerometer. Landscape uses the device's Y axis.</summary>
        private double ReadTilt()
        {
#if ENABLE_INPUT_SYSTEM
            if (_accelerometer == null)
            {
                _accelerometer = Accelerometer.current;
            }
            if (_accelerometer == null) return 0.0;
            if (!_accelEnabled)
            {
                InputSystem.EnableDevice(_accelerometer);
                _accelEnabled = true;
            }
            double g = _accelerometer.acceleration.ReadValue().y;
            double steer = InputShaping.TiltSteering(_settings.TiltInvert ? -g : g, _settings.TiltMaxG, 0.03, 1.0);
            return steer;
#else
            return 0.0;
#endif
        }
    }
}
