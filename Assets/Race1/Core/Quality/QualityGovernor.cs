using System;

namespace Race1.Core
{
    public enum QualityTier
    {
        BatterySaver = 0,
        Balanced = 1,
        High = 2,
        Ultra = 3,
    }

    /// <summary>One frame of device feedback fed to the governor.</summary>
    public struct QualitySignals
    {
        public double FrameMs;        // total frame time (ms), measured on the main thread
        public double GpuMs;          // GPU frame time (ms); 0 if unavailable
        public int ThermalStatus;     // Android PowerManager thermal status: 0 none .. 6 shutdown
        public double BatteryLevel;   // 0..1, or negative if unknown
        public bool Charging;
    }

    /// <summary>
    /// Keeps frame time inside budget by first lowering render scale (dynamic resolution), then the quality tier.
    /// It raises quality again only after sustained headroom, which prevents oscillation. Thermal pressure
    /// is handled before frame time: the device cools down first.
    /// </summary>
    public sealed class QualityGovernor
    {
        public const double MinRenderScale = 0.6;
        public const double MaxRenderScale = 1.0;

        private const double ScaleStep = 0.05;
        private const double RecoverStep = 0.025;
        private const double DownTime = 0.5;
        private const double UpTime = 3.0;
        private const double TierChangeCooldown = 8.0;

        private double _frameEma = 16.0;
        private double _overBudgetTime;
        private double _headroomTime;
        private double _cooldown;

        public QualityGovernor(double targetFps = 60.0, QualityTier startTier = QualityTier.High)
        {
            TargetFrameMs = 1000.0 / Math.Max(10.0, targetFps);
            Tier = startTier;
            RenderScale = 1.0;
            MaxTier = QualityTier.Ultra;
        }

        public double TargetFrameMs { get; }
        public QualityTier Tier { get; private set; }
        public QualityTier MaxTier { get; set; }
        public double RenderScale { get; private set; }
        public double SmoothedFrameMs => _frameEma;

        /// <summary>Advances the governor by one frame. dt is real time in seconds.</summary>
        public void Update(in QualitySignals s, double dt)
        {
            _cooldown = Math.Max(0.0, _cooldown - dt);
            double frame = s.FrameMs > 0.0 ? s.FrameMs : TargetFrameMs;
            _frameEma += (frame - _frameEma) * Math.Min(1.0, dt * 2.0);

            QualityTier cap = MaxTier;
            if (s.BatteryLevel >= 0.0 && s.BatteryLevel < 0.15 && !s.Charging)
            {
                cap = QualityTier.BatterySaver;
            }

            // Thermal pressure first: step down immediately, then wait before recovering.
            if (s.ThermalStatus >= 3 && _cooldown <= 0.0)
            {
                if (s.ThermalStatus >= 4)
                {
                    RenderScale = MinRenderScale;
                }
                if (Tier > QualityTier.BatterySaver)
                {
                    SetTier(Tier - 1);
                    RenderScale = Math.Max(MinRenderScale, RenderScale - ScaleStep);
                }
                _cooldown = TierChangeCooldown;
                _overBudgetTime = 0.0;
                _headroomTime = 0.0;
            }
            else
            {
                double budget = TargetFrameMs * 0.92;
                double gpuLimited = s.GpuMs > budget ? s.GpuMs : 0.0;
                bool over = _frameEma > budget * 1.05 || gpuLimited > 0.0;
                bool headroom = _frameEma < budget * 0.75 && s.ThermalStatus <= 1;

                if (over)
                {
                    _overBudgetTime += dt;
                    _headroomTime = 0.0;
                }
                else if (headroom)
                {
                    _headroomTime += dt;
                    _overBudgetTime = 0.0;
                }
                else
                {
                    _overBudgetTime = 0.0;
                    _headroomTime = 0.0;
                }

                if (_overBudgetTime >= DownTime)
                {
                    _overBudgetTime = 0.0;
                    if (RenderScale > MinRenderScale + 1e-9)
                    {
                        RenderScale = Math.Max(MinRenderScale, RenderScale - ScaleStep);
                    }
                    else if (Tier > QualityTier.BatterySaver && _cooldown <= 0.0)
                    {
                        SetTier(Tier - 1);
                        _cooldown = TierChangeCooldown;
                    }
                }
                else if (_headroomTime >= UpTime)
                {
                    _headroomTime = 0.0;
                    if (RenderScale < MaxRenderScale - 1e-9)
                    {
                        RenderScale = Math.Min(MaxRenderScale, RenderScale + RecoverStep);
                    }
                    else if (Tier < cap && _cooldown <= 0.0)
                    {
                        SetTier(Tier + 1);
                        RenderScale = MinRenderScale + 0.1; // re-enter the higher tier with some headroom
                        _cooldown = TierChangeCooldown;
                    }
                }
            }

            if (Tier > cap)
            {
                SetTier(cap);
            }
        }

        private void SetTier(QualityTier tier)
        {
            Tier = (QualityTier)Math.Max((int)QualityTier.BatterySaver, Math.Min((int)QualityTier.Ultra, (int)tier));
        }
    }
}
