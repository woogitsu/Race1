using System;

namespace Race1.Core
{
    /// <summary>Suspension, steering, drivetrain, brakes and driver aids.</summary>
    public sealed partial class VehicleDynamics
    {
        /// <summary>Per-corner spring-damper. Tyre load is the spring-damper force, so weight transfer has realistic lag.</summary>
        private void UpdateSuspension(double dt, RoadSample road)
        {
            double m = Spec.Mass;
            double weight = m * Mathx.Gravity * Math.Cos(road.Pitch) * Math.Cos(road.Bank);
            double wheelbase = Spec.WheelbaseM;
            double h = Spec.CgHeightM;
            double longTransfer = m * _ax * h / wheelbase;                 // positive: weight moves rearwards
            double avgTrack = 0.5 * (Spec.TrackFrontM + Spec.TrackRearM);
            double latTotal = m * _ay * h / avgTrack;                      // positive: weight moves to the left
            double frontShare = 1.0 - Spec.RearWeightShare;
            double aero = 0.5 * AirDensity * Spec.DownforceCoefficient * Spec.FrontalArea * _vx * _vx;
            double sprungPerCorner = m * 0.25;

            for (int i = 0; i < 4; i++)
            {
                bool front = i < 2;
                bool left = (i & 1) == 0;
                double staticW = weight * (front ? frontShare : Spec.RearWeightShare) * 0.5;
                double longW = (front ? -longTransfer : longTransfer) * 0.5;
                double rollShare = front ? Spec.RollShareFront : 1.0 - Spec.RollShareFront;
                double latW = (left ? 1.0 : -1.0) * latTotal * rollShare * 0.5;
                double aeroW = aero * (front ? Spec.AeroFrontShare : 1.0 - Spec.AeroFrontShare) * 0.5;
                double target = staticW + longW + latW + aeroW;

                double k = front ? Spec.SpringFront : Spec.SpringRear;
                double d = 2.0 * Spec.DampingRatio * Math.Sqrt(k * sprungPerCorner);
                double accel = (target - k * _comp[i] - d * _compVel[i]) / sprungPerCorner;
                _compVel[i] += accel * dt;
                _comp[i] += _compVel[i] * dt;

                if (_comp[i] < -0.02)
                {
                    _comp[i] = -0.02; // fully extended
                    if (_compVel[i] < 0.0) _compVel[i] = 0.0;
                }
                else if (_comp[i] > 0.12)
                {
                    _comp[i] = 0.12; // bump stop
                    if (_compVel[i] > 0.0) _compVel[i] = 0.0;
                }
                _load[i] = Math.Max(0.0, k * _comp[i] + d * _compVel[i]);
            }
        }

        private void UpdateSteering(double dt, VehicleInput input)
        {
            double speed = Math.Abs(_vx);
            // Wheel lock shrinks with speed, as on a real steering rack.
            double maxSteer = Spec.MaxSteerDeg * Mathx.Deg2Rad * (0.35 + 0.65 * Math.Exp(-speed / 25.0));
            double target = Mathx.Clamp(input.Steer, -1.0, 1.0) * maxSteer;
            _steer = Mathx.MoveTowards(_steer, target, Spec.SteerRateRadPerSecond * dt);
        }

        /// <summary>Yaw moment from steering assist and ESP. Models stability control as a corrective yaw moment.</summary>
        private double ComputeStabilityMoment()
        {
            _espActive = false;
            _espThrottle = 1.0;
            double speed = Math.Abs(_vx);
            double idealYaw = _vx * _steer / (Spec.WheelbaseM + Spec.UndersteerCoefficient * _vx * _vx);
            double error = _yawRate - idealYaw;
            double moment = 0.0;

            if (Assists.SteeringAssist > 0.0 && speed > 1.0)
            {
                double yawAccel = Mathx.Clamp(-Assists.SteeringAssist * 3.0 * error, -6.0, 6.0);
                moment += yawAccel * Spec.YawInertia;
            }

            if (Assists.Esp && speed > 3.0)
            {
                double excess = Math.Abs(error) - 0.12;
                if (excess > 0.0)
                {
                    _espActive = true;
                    _espThrottle = 0.5;
                    moment += -Math.Sign(error) * Math.Min(4000.0, 3000.0 * excess);
                }
            }
            return moment;
        }

        /// <summary>ABS modulates per-wheel brake torque; TC scales engine torque. Both use the previous substep's slip.</summary>
        private void UpdateTractionAids(double dt, VehicleInput input)
        {
            bool braking = input.Brake > 0.05;
            _absActive = false;
            for (int i = 0; i < 4; i++)
            {
                if (Assists.Abs && braking)
                {
                    if (_slipRatio[i] < -0.15)
                    {
                        _absFactor[i] = Math.Max(0.2, _absFactor[i] - 6.0 * dt);
                    }
                    else if (_slipRatio[i] > -0.10)
                    {
                        _absFactor[i] = Math.Min(1.0, _absFactor[i] + 4.0 * dt);
                    }
                    if (_absFactor[i] < 0.95) _absActive = true;
                }
                else
                {
                    _absFactor[i] = 1.0;
                }
            }

            _tcActive = false;
            if (Assists.TractionControl && input.Throttle > 0.05)
            {
                double driven = DrivenSlipRatio();
                if (driven > 0.12)
                {
                    _tcFactor = Math.Max(0.1, _tcFactor - 5.0 * dt);
                    _tcActive = true;
                }
                else
                {
                    _tcFactor = Math.Min(1.0, _tcFactor + 2.5 * dt);
                }
            }
            else
            {
                _tcFactor = 1.0;
            }
        }

        private double DrivenSlipRatio()
        {
            switch (Spec.Drive)
            {
                case DriveLayout.FrontWheel: return 0.5 * (_slipRatio[0] + _slipRatio[1]);
                case DriveLayout.RearWheel: return 0.5 * (_slipRatio[2] + _slipRatio[3]);
                default: return 0.25 * (_slipRatio[0] + _slipRatio[1] + _slipRatio[2] + _slipRatio[3]);
            }
        }

        private double DrivenShaftSpeed()
        {
            switch (Spec.Drive)
            {
                case DriveLayout.FrontWheel: return 0.5 * (_omega[0] + _omega[1]);
                case DriveLayout.RearWheel: return 0.5 * (_omega[2] + _omega[3]);
                default: return 0.25 * (_omega[0] + _omega[1] + _omega[2] + _omega[3]);
            }
        }

        /// <summary>
        /// Engine torque at the wheels. The clutch is modelled as locked above idle, so engine speed follows
        /// the driven wheels through the current gear. Shifts interrupt torque; the limiter cuts fuel.
        /// </summary>
        private void ComputeDriveTorques(double dt, VehicleInput input)
        {
            double throttleIn = Mathx.Clamp01(input.Throttle) * _tcFactor * _espThrottle;
            if (Spec.Turbo)
            {
                _boost += (throttleIn - _boost) * Math.Min(1.0, dt / Spec.TurboLagSeconds);
            }
            else
            {
                _boost = throttleIn;
            }

            double gearRatio = Spec.GearRatios[_gear] * Spec.FinalDrive;
            double rpmFromWheels = Math.Max(0.0, DrivenShaftSpeed()) * gearRatio * 60.0 / (2.0 * Math.PI);
            _engineRpm = Math.Max(Spec.IdleRpm, rpmFromWheels);

            double torque;
            if (_shiftTimer > 0.0)
            {
                torque = 0.0;
            }
            else
            {
                double curve = _engineRpm >= Spec.LimiterRpm
                    ? 0.0
                    : Mathx.Interp(Spec.RpmPoints, Spec.TorquePoints, _engineRpm);
                double engineBrake = Math.Abs(_vx) < 0.5
                    ? 0.0
                    : Spec.EngineBrakeNm * Mathx.Clamp(_engineRpm / 3000.0, 0.3, 1.2);
                torque = _boost * curve - (1.0 - _boost) * engineBrake;
            }

            double wheelTorque = torque * gearRatio * Spec.DrivetrainEfficiency;
            double frontShare;
            switch (Spec.Drive)
            {
                case DriveLayout.FrontWheel: frontShare = 1.0; break;
                case DriveLayout.RearWheel: frontShare = 0.0; break;
                default: frontShare = Spec.AwdFrontShare; break;
            }
            _driveTorque[0] = wheelTorque * frontShare * 0.5;
            _driveTorque[1] = _driveTorque[0];
            _driveTorque[2] = wheelTorque * (1.0 - frontShare) * 0.5;
            _driveTorque[3] = _driveTorque[2];
        }

        private void ComputeBrakeTorques(VehicleInput input)
        {
            double brake = Mathx.Clamp01(input.Brake);
            _brakeInput = brake;
            for (int i = 0; i < 4; i++)
            {
                bool rear = i >= 2;
                double maxBrake = rear ? Spec.MaxBrakeRear : Spec.MaxBrakeFront;
                double fade = 1.0 - 0.35 * Mathx.Clamp01((_heat[i] - 0.5) / 0.5);
                double torque = brake * maxBrake * fade * _absFactor[i];
                if (rear && input.Handbrake)
                {
                    torque += Spec.HandbrakeTorque;
                }
                _brakeTorque[i] = torque;
            }
        }

        /// <summary>
        /// Shift points use the rpm implied by road speed, not by wheel speed. A car whose wheels are
        /// spinning off the line must not upshift early, just as a real automatic does not.
        /// </summary>
        private void UpdateAutomaticGearbox(VehicleInput input)
        {
            if (_shiftTimer > 0.0) return;
            int last = Spec.GearRatios.Length - 1;
            double ratio = Spec.GearRatios[_gear] * Spec.FinalDrive;
            double rpmRoad = Math.Abs(_vx) * ratio * 60.0 / (2.0 * Math.PI * Spec.WheelRadius);

            if (rpmRoad > Spec.AutoUpshiftRpm && _gear < last)
            {
                _gear++;
                _shiftTimer = Spec.ShiftTime;
                return;
            }

            if (_gear > 0 && rpmRoad < Spec.AutoDownshiftRpm && input.Throttle > 0.1)
            {
                // Only downshift if the engine will not overrev in the lower gear.
                double predicted = rpmRoad * Spec.GearRatios[_gear - 1] / Spec.GearRatios[_gear];
                if (predicted < Spec.AutoUpshiftRpm - 400.0)
                {
                    _gear--;
                    _shiftTimer = Spec.ShiftTime;
                }
            }
        }

        private void ApplyManualGearRequest(VehicleInput input)
        {
            if (input.AutoGearbox || input.GearRequest == 0) return;
            int last = Spec.GearRatios.Length - 1;
            int next = Math.Max(0, Math.Min(last, _gear + input.GearRequest));
            if (next != _gear)
            {
                _gear = next;
                _shiftTimer = Spec.ShiftTime;
            }
        }
    }
}
