using UnityEngine;
using UnityEngine.Rendering;

namespace Race1.Game
{
    /// <summary>
    /// A realtime reflection probe that follows the car and re-renders every 2.5 seconds.
    /// Rendering is scripted (ViaScripting), so the expensive render happens only on the interval.
    /// </summary>
    public sealed class ReflectionProbeRig
    {
        private const float RefreshIntervalSeconds = 2.5f;
        private const float HeightOffset = 3f;

        private readonly Transform _transform;
        private readonly ReflectionProbe _probe;
        private float _elapsed;

        /// <summary>
        /// Creates the "CarReflectionProbe" child under <paramref name="parent"/>.
        /// </summary>
        public ReflectionProbeRig(Transform parent)
        {
            var go = new GameObject("CarReflectionProbe");
            go.transform.SetParent(parent, false);
            _transform = go.transform;

            _probe = go.AddComponent<ReflectionProbe>();
            _probe.mode = ReflectionProbeMode.Realtime;
            _probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            _probe.resolution = 128;
            _probe.size = new Vector3(60f, 20f, 60f);
            _probe.boxProjection = true;
            _probe.intensity = 1f;
            _probe.importance = 1;
            _probe.clearFlags = ReflectionProbeClearFlags.Skybox;
            _probe.enabled = true;

            // Start at the interval so the first Follow call renders immediately.
            _elapsed = RefreshIntervalSeconds;
        }

        /// <summary>
        /// Moves the probe to <paramref name="worldPosition"/> (raised slightly) and re-renders it
        /// once every 2.5 seconds of accumulated <paramref name="dt"/>.
        /// </summary>
        public void Follow(Vector3 worldPosition, float dt)
        {
            _transform.position = worldPosition + new Vector3(0f, HeightOffset, 0f);

            if (!_probe.enabled)
            {
                _probe.enabled = true;
            }

            _elapsed += dt;
            if (_elapsed >= RefreshIntervalSeconds)
            {
                _elapsed = 0f;
                _probe.RenderProbe();
            }
        }
    }
}
