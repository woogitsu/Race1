using Race1.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Race1.Game
{
    /// <summary>
    /// Connects the core QualityGovernor to the engine: render scale on the URP asset, shadow distance, MSAA,
    /// frame rate target and road draw distance. Settings change at most twice a second to avoid flicker.
    /// The per-tier limits (chunks, builds, vegetation, lights, shadows) come from <see cref="Budgets"/>, which the
    /// race config fills in. Each limit is sanitized before it is used.
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

        /// <summary>The budget table, one row per quality tier. The race config sets it once at start-up.</summary>
        public PerformanceLimits[] Budgets { get; set; } = PerformanceBudget.Defaults();

        /// <summary>The sanitized limits of the current tier.</summary>
        public PerformanceLimits Current => PerformanceBudget.ForTier(Budgets, _governor.Tier);

        /// <summary>Tree and rock instances the vegetation may draw in one frame.</summary>
        public int VegetationLimit => Current.MaxVegetationInstances;

        /// <summary>Street lamps that may use a real light at once.</summary>
        public int LitLightLimit => Current.MaxLitLights;

        /// <summary>Chunk builds allowed per frame. Lower on weak tiers so generation never causes a long frame.</summary>
        public int BuildsPerFrame => Current.BuildsPerFrame;
        public double RenderScale => _governor.RenderScale;

        /// <summary>Road chunks drawn ahead of the car for the current tier.</summary>
        public int ChunksAhead => Current.ChunksAhead;

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
                QualitySettings.shadowDistance = Current.ShadowDistanceM;
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
