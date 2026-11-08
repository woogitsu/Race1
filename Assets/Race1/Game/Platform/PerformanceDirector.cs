using Race1.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Race1.Game
{
    /// <summary>
    /// Connects the core QualityGovernor to the engine: render scale on the URP asset, shadow distance, MSAA,
    /// frame rate target and road draw distance. Settings change at most twice a second to avoid flicker.
    /// </summary>
    public sealed class PerformanceDirector
    {
        private readonly QualityGovernor _governor;
        private readonly FrameTiming[] _timings = new FrameTiming[1];
        private float _applyTimer;
        private float _frameMs = 16.7f;
        private float _gpuMs;
        private QualityTier _appliedTier = (QualityTier)(-1);
        private double _appliedScale = -1.0;

        public PerformanceDirector(QualityTier startTier, QualityTier maxTier)
        {
            _governor = new QualityGovernor(60.0, startTier) { MaxTier = maxTier };
        }

        public QualityTier Tier => _governor.Tier;

        /// <summary>Chunk builds allowed per frame. Lower on weak tiers so generation never causes a long frame.</summary>
        public int BuildsPerFrame => _governor.Tier <= QualityTier.Balanced ? 1 : 2;
        public double RenderScale => _governor.RenderScale;

        /// <summary>Road chunks drawn ahead of the car for the current tier.</summary>
        public int ChunksAhead
        {
            get
            {
                switch (_governor.Tier)
                {
                    case QualityTier.BatterySaver: return 7;
                    case QualityTier.Balanced: return 9;
                    case QualityTier.High: return 11;
                    default: return 14;
                }
            }
        }

        public void Tick(float dt, DevicePlatform device)
        {
            // Frame timing: CPU from unscaled delta, GPU from the engine's frame timing API when the device provides it.
            _frameMs = dt * 1000f;
            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.GetLatestTimings(1, _timings) > 0)
            {
                _gpuMs = (float)_timings[0].gpuFrameTime;
            }

            _governor.Update(device.Signals(_frameMs, _gpuMs), dt);

            _applyTimer -= dt;
            if (_applyTimer > 0f) return;
            _applyTimer = 0.5f;
            Apply(device.RefreshRateHz);
        }

        private void Apply(int refreshHz)
        {
            QualityTier tier = _governor.Tier;
            double scale = _governor.RenderScale;

            var asset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (asset != null && System.Math.Abs(scale - _appliedScale) > 0.001)
            {
                asset.renderScale = (float)scale;
                _appliedScale = scale;
            }

            if (tier != _appliedTier)
            {
                _appliedTier = tier;
                QualitySettings.shadowDistance = tier == QualityTier.BatterySaver ? 60f : tier == QualityTier.Balanced ? 120f : 220f;
                if (asset != null)
                {
                    asset.msaaSampleCount = tier >= QualityTier.High ? 4 : 1;
                }
                int target = 60;
                if (tier == QualityTier.BatterySaver) target = 30;
                else if (tier >= QualityTier.High && refreshHz >= 90) target = refreshHz >= 120 && tier == QualityTier.Ultra ? 120 : 90;
                Application.targetFrameRate = target;
                QualitySettings.vSyncCount = 0;
            }
        }
    }
}
