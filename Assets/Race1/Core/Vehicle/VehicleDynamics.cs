using System;

namespace Race1.Core
{
    /// <summary>
    /// Simcade vehicle model in road-relative coordinates.
    /// Position is (s, t, eps): arc length along the road centre line, lateral offset (positive right) and
    /// heading error. The car therefore follows an endless road without world-space drift. Body dynamics
    /// (vx, vy, yaw rate) use per-wheel combined-slip tyres, per-corner suspension for load transfer, and a
    /// drivetrain with gearbox, turbo lag, ABS/TC/ESP and brake fade.
    /// The physics always steps at <see cref="PhysicsStep"/>, independent of the render frame rate.
    /// </summary>
    public sealed partial class VehicleDynamics
    {
        public const double PhysicsStep = 1.0 / 600.0;
        private const int MaxSubstepsPerFrame = 40;
        private const double AirDensity = 1.225;
        private const double HeatScale = 1.0 / 400000.0;

        private readonly IRoadProvider _road;
        private readonly VehicleTelemetry _telemetry = new VehicleTelemetry();

        private readonly double[] _omega = new double[4];
        private readonly double[] _comp = new double[4];
        private readonly double[] _compVel = new double[4];
        private readonly double[] _load = new double[4];
        private readonly double[] _absFactor = new double[4];
        private readonly double[] _heat = new double[4];
        private readonly double[] _slipRatio = new double[4];
        private readonly double[] _slipAngle = new double[4];
        private readonly double[] _brakeTorque = new double[4];
        private readonly double[] _driveTorque = new double[4];
        private readonly double[] _forceX = new double[4];
        private readonly double[] _forceY = new double[4];
        private readonly double[] _tyreWear = new double[4];
        private readonly double[] _tyreTemp = new double[4];

        // Road-relative state
        private double _s;
        private double _t;
        private double _eps;
        private double _sDot;
        private double _tDot;

        // Body state (car frame: x forward, y right)
        private double _vx;
        private double _vy;
        private double _yawRate;
        private double _ax;
        private double _ay;
        private double _steer;

        // Drivetrain and aids
        private double _accumulator;
        private double _surfaceMu = 1.0;
        private double _rollMul = 1.0;
        private double _engineRpm;
        private int _gear;
        private double _shiftTimer;
        private double _boost;
        private double _tcFactor = 1.0;
        private double _espThrottle = 1.0;
        private double _brakeInput;
        private bool _tcActive;
        private bool _absActive;
        private bool _espActive;

        // Damage
        private double _odometer;
        private double _damage;
        private int _collisions;
        private bool _touchingBarrier;

        public VehicleDynamics(VehicleSpec spec, IRoadProvider road, AssistSettings assists)
        {
            spec.Validate();
            Spec = spec;
            _road = road ?? throw new ArgumentNullException(nameof(road));
            Assists = assists;
            Reset(0.0, 0.0, 0.0);
        }

        public VehicleSpec Spec { get; }

        public AssistSettings Assists { get; set; }

        /// <summary>Global grip multiplier (1 = dry asphalt). Set from the weather system.</summary>
        public double FrictionScale { get; set; } = 1.0;

        public VehicleTelemetry Telemetry => _telemetry;

        public double S => _s;
        public double T => _t;
        public double HeadingError => _eps;
        public double Speed => _vx;
        public double LateralSpeed => _vy;
        public int Gear => _gear;
        public double EngineRpm => _engineRpm;
        public double Odometer => _odometer;

        /// <summary>Places the car on the road. Speed is along the road tangent.</summary>
        public void Reset(double s, double t, double speedMs)
        {
            _s = s;
            _t = t;
            _eps = 0.0;
            _sDot = speedMs;
            _tDot = 0.0;
            _vx = speedMs;
            _vy = 0.0;
            _yawRate = 0.0;
            _ax = 0.0;
            _ay = 0.0;
            _steer = 0.0;
            _accumulator = 0.0;
            _engineRpm = Spec.IdleRpm;
            _gear = 0;
            _shiftTimer = 0.0;
            _boost = 0.0;
            _tcFactor = 1.0;
            _espThrottle = 1.0;
            _brakeInput = 0.0;
            _tcActive = _absActive = _espActive = false;
            _odometer = 0.0;
            _damage = 0.0;
            _collisions = 0;
            _touchingBarrier = false;

            double weight = Spec.Mass * Mathx.Gravity;
            double frontShare = 1.0 - Spec.RearWeightShare;
            for (int i = 0; i < 4; i++)
            {
                bool front = i < 2;
                double staticLoad = weight * (front ? frontShare : Spec.RearWeightShare) * 0.5;
                double k = front ? Spec.SpringFront : Spec.SpringRear;
                _comp[i] = staticLoad / k;
                _compVel[i] = 0.0;
                _load[i] = staticLoad;
                _omega[i] = speedMs / Spec.WheelRadius;
                _absFactor[i] = 1.0;
                _heat[i] = 0.0;
                _brakeTorque[i] = 0.0;
                _driveTorque[i] = 0.0;
                _slipRatio[i] = 0.0;
                _slipAngle[i] = 0.0;
                _forceX[i] = 0.0;
                _forceY[i] = 0.0;
                _tyreWear[i] = 0.0;
                _tyreTemp[i] = TyreCondition.OperatingStartTemperature;
            }
            UpdateTelemetry();
        }

        /// <summary>Sets tyre wear (0 new .. 1 finished) and temperature (°C) on all four corners, for a fresh or used set.</summary>
        public void ResetTyres(double wear, double temperatureC)
        {
            for (int i = 0; i < 4; i++)
            {
                _tyreWear[i] = Mathx.Clamp01(wear);
                _tyreTemp[i] = temperatureC;
            }
        }

        /// <summary>
        /// Advances the simulation by a frame of real time. Input is held for the whole frame.
        /// Internally the model runs fixed substeps, so results do not depend on the frame rate.
        /// </summary>
        public void Advance(double frameDt, VehicleInput input)
        {
            if (!(frameDt > 0.0))
            {
                UpdateTelemetry();
                return;
            }

            _accumulator += Math.Min(frameDt, 0.1);
            ApplyManualGearRequest(input);

            // The small tolerance stops floating-point rounding from losing a substep per frame.
            const double stepTolerance = PhysicsStep * 1e-9;
            int steps = 0;
            while (_accumulator >= PhysicsStep - stepTolerance)
            {
                if (steps >= MaxSubstepsPerFrame)
                {
                    _accumulator = 0.0;
                    break;
                }
                Substep(PhysicsStep, input);
                _accumulator -= PhysicsStep;
                steps++;
            }
            UpdateTelemetry();
        }

        private void Substep(double dt, VehicleInput input)
        {
            RoadSample road = _road.Sample(_s);

            double absT = Math.Abs(_t);
            double edge = road.HalfWidth;
            double shoulderEdge = edge + road.Shoulder;
            _surfaceMu = absT <= edge ? 1.0 : (absT <= shoulderEdge ? 0.85 : 0.6);
            _rollMul = absT <= shoulderEdge ? 1.0 : 2.5;

            if (_shiftTimer > 0.0)
            {
                _shiftTimer -= dt;
            }
            if (input.AutoGearbox)
            {
                UpdateAutomaticGearbox(input);
            }

            UpdateSuspension(dt, road);
            UpdateSteering(dt, input);
            double stabilityMoment = ComputeStabilityMoment();
            UpdateTractionAids(dt, input);
            ComputeDriveTorques(dt, input);
            ComputeBrakeTorques(input);
            UpdateTyresAndWheels(dt);
            IntegrateBody(dt, road, stabilityMoment);
            IntegrateRoadRelative(dt, road);
            ApplyBarrier(road);
        }

        private void IntegrateBody(double dt, RoadSample road, double stabilityMoment)
        {
            double m = Spec.Mass;
            double g = Mathx.Gravity;

            double sumFx = 0.0;
            double sumFy = 0.0;
            double mz = 0.0;
            for (int i = 0; i < 4; i++)
            {
                bool front = i < 2;
                double x = front ? Spec.FrontAxleX : Spec.RearAxleX;
                double y = ((i & 1) == 0 ? -0.5 : 0.5) * (front ? Spec.TrackFrontM : Spec.TrackRearM);
                sumFx += _forceX[i];
                sumFy += _forceY[i];
                mz += x * _forceY[i] - y * _forceX[i];
            }

            double speed = Math.Abs(_vx);
            double drag = -0.5 * AirDensity * Spec.DragCoefficient * Spec.FrontalArea * _vx * speed;
            double normal = m * g * Math.Cos(road.Pitch) * Math.Cos(road.Bank);
            double rolling = speed > 0.02 ? -Math.Sign(_vx) * Spec.RollingResistance * normal * _rollMul : 0.0;

            // Gravity projected onto the road-aligned axes: slope pulls backwards, bank pulls towards the low side.
            double gravLong = -m * g * Math.Sin(road.Pitch);
            double gravLat = m * g * Math.Cos(road.Pitch) * Math.Sin(road.Bank);

            double fx = sumFx + drag + rolling + gravLong;
            double fy = sumFy + gravLat;
            mz += stabilityMoment;

            double dvx = fx / m + _vy * _yawRate;
            double dvy = fy / m - _vx * _yawRate;
            double dr = mz / Spec.YawInertia;

            _vx += dvx * dt;
            _vy += dvy * dt;
            _yawRate += dr * dt;
            _ax = dvx;
            _ay = dvy + _vx * _yawRate;
        }

        private void IntegrateRoadRelative(double dt, RoadSample road)
        {
            double kappa = road.Curvature;
            double sinE = Math.Sin(_eps);
            double cosE = Math.Cos(_eps);
            double denom = Math.Max(0.2, 1.0 - kappa * _t);

            double sDot = (_vx * cosE - _vy * sinE) / denom;
            double tDot = _vx * sinE + _vy * cosE;
            double epsDot = _yawRate - kappa * sDot;

            _s += sDot * dt;
            _t += tDot * dt;
            _eps = Mathx.WrapAngle(_eps + epsDot * dt);
            _sDot = sDot;
            _tDot = tDot;
            _odometer += Math.Abs(sDot) * dt;
        }

        /// <summary>Guard rail beyond the shoulder: reflects lateral motion and bleeds speed.</summary>
        private void ApplyBarrier(RoadSample road)
        {
            double limit = road.HalfWidth + road.Shoulder + Spec.GuardRailOffset;
            if (Math.Abs(_t) <= limit)
            {
                _touchingBarrier = false;
                return;
            }

            if (!_touchingBarrier)
            {
                _collisions++;
                _touchingBarrier = true;
            }

            double impact = Math.Abs(_tDot);
            _damage += 0.5 * Spec.Mass * impact * impact * 0.001;
            _t = Math.Sign(_t) * limit;
            _vy *= -0.3;
            _vx *= 0.97;
            _yawRate *= 0.5;
            _tDot = 0.0;
        }

        private void UpdateTyresAndWheels(double dt)
        {
            double r = Spec.WheelRadius;
            double inertia = Spec.WheelInertia;

            for (int i = 0; i < 4; i++)
            {
                bool front = i < 2;
                bool left = (i & 1) == 0;
                double x = front ? Spec.FrontAxleX : Spec.RearAxleX;
                double y = (left ? -0.5 : 0.5) * (front ? Spec.TrackFrontM : Spec.TrackRearM);

                // Contact-patch velocity in the car frame, then in the wheel frame (front wheels steer).
                double vxp = _vx - _yawRate * y;
                double vyp = _vy + _yawRate * x;
                double delta = front ? _steer : 0.0;
                double cd = Math.Cos(delta);
                double sd = Math.Sin(delta);
                double vwx = vxp * cd + vyp * sd;
                double vwy = -vxp * sd + vyp * cd;

                double denom = Math.Max(Math.Abs(vwx), 1.0);
                double slip = (_omega[i] * r - vwx) / denom;
                double tanAlpha = Mathx.Clamp(vwy / Math.Max(Math.Abs(vwx), 0.5), -1.5, 1.5);

                double fz = _load[i];
                double loadFactor = Mathx.Clamp(1.0 - Spec.LoadSensitivity * (fz / Spec.ReferenceTireLoad - 1.0), 0.55, 1.15);
                double condition = TyreCondition.GripMultiplier(_tyreWear[i], _tyreTemp[i]);
                double mu = Spec.GripDry * FrictionScale * _surfaceMu * loadFactor * condition;

                TireModel.Forces(mu, fz, slip, tanAlpha, out double fxw, out double fyw);

                // Sliding power heats and wears the tyre: longitudinal slip (wheel speed vs ground) and lateral slip.
                double slidingPower = Math.Abs(fxw * (r * _omega[i] - vwx)) + Math.Abs(fyw * vwy);
                _tyreWear[i] = TyreCondition.AccumulateWear(_tyreWear[i], slidingPower, dt);
                _tyreTemp[i] = TyreCondition.Heat(_tyreTemp[i], slidingPower, dt, TyreCondition.AmbientTemperature);

                // Slope of the longitudinal force w.r.t. slip ratio, for an implicit (stable) wheel update.
                const double eps = 1e-3;
                TireModel.Forces(mu, fz, slip + eps, tanAlpha, out double fxPlus, out _);
                double kSlope = Math.Max(0.0, (fxPlus - fxw) / eps);

                _forceX[i] = fxw * cd - fyw * sd;
                _forceY[i] = fxw * sd + fyw * cd;
                _slipRatio[i] = slip;
                _slipAngle[i] = Math.Atan(tanAlpha);

                double omega = _omega[i];
                double drive = _driveTorque[i];
                double brake = _brakeTorque[i];
                double netNoBrake = drive - r * fxw;

                if (Math.Abs(omega) < 1e-3 && Math.Abs(netNoBrake) <= brake)
                {
                    _omega[i] = 0.0; // held by the brake
                }
                else
                {
                    double sgn = Math.Abs(omega) > 1e-3 ? Math.Sign(omega) : Math.Sign(netNoBrake);
                    double net = netNoBrake - sgn * brake;
                    double dOmega = net * dt / (inertia + dt * r * r * kSlope / denom);
                    double next = omega + dOmega;
                    if (omega != 0.0 && Math.Sign(next) != Math.Sign(omega))
                    {
                        next = 0.0; // brakes can stop a wheel but cannot reverse it
                    }
                    _omega[i] = next;
                }

                _heat[i] = (_heat[i] + Math.Abs(brake * omega) * dt * HeatScale) * (1.0 - 0.02 * dt);
            }
        }

        private void UpdateTelemetry()
        {
            VehicleTelemetry t = _telemetry;
            double g = Mathx.Gravity;
            t.SpeedMs = Math.Abs(_vx);
            t.SpeedKmh = t.SpeedMs * 3.6;
            t.EngineRpm = _engineRpm;
            t.Gear = _gear + 1;
            t.Throttle = _boost;
            t.Brake = _brakeInput;
            t.SteerAngleRad = _steer;
            t.LongitudinalG = _ax / g;
            t.LateralG = _ay / g;
            t.YawRate = _yawRate;
            t.S = _s;
            t.T = _t;
            t.HeadingError = _eps;
            t.Boost = _boost;
            t.Odometer = _odometer;
            t.Damage = _damage;
            t.CollisionCount = _collisions;
            t.TractionControlActive = _tcActive;
            t.AbsActive = _absActive;
            t.EspActive = _espActive;
            t.ShiftInProgress = _shiftTimer > 0.0;
            for (int i = 0; i < 4; i++)
            {
                t.Compression[i] = _comp[i];
                t.Load[i] = _load[i];
                t.WheelSpeed[i] = _omega[i];
                t.SlipRatio[i] = _slipRatio[i];
                t.SlipAngle[i] = _slipAngle[i];
                t.TyreWear[i] = _tyreWear[i];
                t.TyreTemperature[i] = _tyreTemp[i];
            }
        }
    }
}
