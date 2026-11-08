using System;

namespace Race1.Core
{
    /// <summary>Per-frame inputs for the camera motion effects. SI units throughout.</summary>
    public struct CameraEffectsInput
    {
        /// <summary>Car speed (m/s).</summary>
        public double SpeedMs;

        /// <summary>Lateral acceleration in g (positive = to the right).</summary>
        public double LateralG;

        /// <summary>Longitudinal acceleration in g (positive = forward).</summary>
        public double LongitudinalG;

        /// <summary>RMS of wheel compression velocity (m/s).</summary>
        public double SuspensionRoughness;

        /// <summary>True when no wheel is touching the road.</summary>
        public bool Airborne;
    }

    /// <summary>Camera motion outputs. Offsets are in metres, angles in radians or degrees as named.</summary>
    public struct CameraEffectsOutput
    {
        public double ShakeX;
        public double ShakeY;
        public double RollRad;

        /// <summary>Degrees, added to the base field of view.</summary>
        public double FovOffsetDeg;

        public double WindLines01;
        public double Vibration01;
        public double HeadBobY;
        public double ImpactFlash01;
    }

    /// <summary>
    /// Deterministic camera motion: impact shake and flash, speed wind lines and FOV kick,
    /// body roll from lateral g, and cockpit vibration. Pure maths, no engine or random-number use.
    /// </summary>
    public sealed class CameraEffects
    {
        private const double TwoPi = 2.0 * Math.PI;
        private const double KmhToMs = 1.0 / 3.6;

        private const double WindStartMs = 60.0 * KmhToMs;
        private const double WindFullMs = 250.0 * KmhToMs;
        private const double FovMaxOffsetDeg = 4.0;

        private const double ShakeTimeConstant = 0.35;
        private const double ShakeMaxAmplitude = 0.06;
        private const double ShakeFrequencyHz = 18.0;
        private const double FlashDuration = 0.3;

        private const double RollGainRad = 0.012;
        private const double RollLimitRad = 0.03;
        private const double RollTimeConstant = 0.25;

        private const double VibrationTimeConstant = 0.1;
        private const double HeadBobAmplitude = 0.01;
        private const double HeadBobRadPerMetre = 0.6;
        private const double PitchBobPerG = 0.004;
        private const double PitchTimeConstant = 0.15;

        private const double GroundTimeConstant = 0.1;

        private const double GLimit = 5.0;
        private const double RoughnessLimit = 10.0;

        private double _shakeLevel;     // 0..1, decays with ShakeTimeConstant
        private double _shakePhase;     // radians
        private double _flashPeak;      // 0..1
        private double _flashAge;       // seconds since the last impact
        private double _groundBlend;    // 1 on the road, 0 airborne
        private double _roll;           // radians
        private double _vibration;      // 0..1
        private double _pitchG;         // filtered longitudinal g
        private double _bobPhase;       // radians
        private CameraEffectsOutput _last;

        public CameraEffects()
        {
            Reset();
        }

        /// <summary>Adds an impact (for example a collision). Magnitude is clamped to 0..1.</summary>
        public void TriggerImpact(double magnitude01)
        {
            double m = Mathx.Clamp01(Sane(magnitude01));
            if (m <= 0.0) return;

            _shakeLevel = Mathx.Clamp01(_shakeLevel + m);
            _flashPeak = Math.Max(CurrentFlash(), m);
            _flashAge = 0.0;
        }

        /// <summary>Clears impact, roll, vibration and phase state. The next Step starts from rest.</summary>
        public void Reset()
        {
            _shakeLevel = 0.0;
            _shakePhase = 0.0;
            _flashPeak = 0.0;
            _flashAge = 0.0;
            _groundBlend = 1.0;
            _roll = 0.0;
            _vibration = 0.0;
            _pitchG = 0.0;
            _bobPhase = 0.0;
            _last = default(CameraEffectsOutput);
        }

        /// <summary>
        /// Advances the effects by dt seconds. A non-positive or non-finite dt returns the previous output
        /// unchanged. All outputs are finite.
        /// </summary>
        public CameraEffectsOutput Step(CameraEffectsInput input, double dt)
        {
            if (!(dt > 0.0) || double.IsInfinity(dt)) return _last;

            double speed = Math.Max(0.0, Sane(input.SpeedMs));
            double lateralG = Mathx.Clamp(Sane(input.LateralG), -GLimit, GLimit);
            double longitudinalG = Mathx.Clamp(Sane(input.LongitudinalG), -GLimit, GLimit);
            double roughness = Mathx.Clamp(Sane(input.SuspensionRoughness), 0.0, RoughnessLimit);

            // Impact shake and flash decay.
            _shakeLevel *= Math.Exp(-dt / ShakeTimeConstant);
            _shakePhase = Sane((_shakePhase + TwoPi * ShakeFrequencyHz * dt) % TwoPi);

            _flashAge += dt;
            if (_flashAge >= FlashDuration)
            {
                _flashPeak = 0.0;
                _flashAge = FlashDuration;
            }

            // Airborne fades the shake out smoothly.
            _groundBlend = Approach(_groundBlend, input.Airborne ? 0.0 : 1.0, dt, GroundTimeConstant);
            double amplitude = ShakeMaxAmplitude * _shakeLevel * _groundBlend;
            double shakeX = amplitude * Math.Sin(_shakePhase);
            double shakeY = amplitude * Math.Cos(_shakePhase);

            // Body roll from lateral g, smoothed.
            double rollTarget = Mathx.Clamp(-lateralG * RollGainRad, -RollLimitRad, RollLimitRad);
            _roll = Approach(_roll, rollTarget, dt, RollTimeConstant);

            // Cockpit vibration and head bob.
            double vibrationTarget = Mathx.Clamp01(roughness * 2.0);
            _vibration = Approach(_vibration, vibrationTarget, dt, VibrationTimeConstant);
            _bobPhase = Sane((_bobPhase + speed * dt * HeadBobRadPerMetre) % TwoPi);
            _pitchG = Approach(_pitchG, Mathx.Clamp(longitudinalG, -1.5, 1.5), dt, PitchTimeConstant);
            double headBobY = -_vibration * HeadBobAmplitude * Math.Sin(_bobPhase)
                + _pitchG * PitchBobPerG;

            // Speed: wind lines and FOV kick share one smoothstep over 60 to 250 km/h.
            double wind = Mathx.SmoothStep(WindStartMs, WindFullMs, speed);

            var output = new CameraEffectsOutput
            {
                ShakeX = Sane(shakeX),
                ShakeY = Sane(shakeY),
                RollRad = Sane(_roll),
                FovOffsetDeg = Sane(FovMaxOffsetDeg * wind),
                WindLines01 = Mathx.Clamp01(wind),
                Vibration01 = Mathx.Clamp01(_vibration),
                HeadBobY = Sane(headBobY),
                ImpactFlash01 = Mathx.Clamp01(CurrentFlash()),
            };

            _last = output;
            return output;
        }

        private double CurrentFlash()
        {
            return _flashPeak * Mathx.Clamp01(1.0 - _flashAge / FlashDuration);
        }

        /// <summary>Frame-rate independent first-order approach to a target.</summary>
        private static double Approach(double current, double target, double dt, double timeConstant)
        {
            double k = 1.0 - Math.Exp(-dt / timeConstant);
            return current + (target - current) * k;
        }

        /// <summary>Maps NaN and infinities to zero.</summary>
        private static double Sane(double v)
        {
            return double.IsNaN(v) || double.IsInfinity(v) ? 0.0 : v;
        }
    }
}
