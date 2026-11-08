using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Race1.Game
{
    /// <summary>
    /// Race state the post-processing stack reacts to. Every field is expected in 0..1.
    /// </summary>
    public struct PostState
    {
        /// <summary>1 at night, 0 by day.</summary>
        public float Night01;

        /// <summary>1 during golden hour, 0 otherwise.</summary>
        public float Golden01;

        /// <summary>1 in heavy rain, 0 in dry weather.</summary>
        public float Rain01;

        /// <summary>Fog density. Reserved: the current grade does not use it yet.</summary>
        public float Fog01;

        /// <summary>Short spike on impacts that decays back to 0.</summary>
        public float Impact01;

        /// <summary>Normalised car speed, 0 at rest.</summary>
        public float Speed01;
    }

    /// <summary>
    /// Drives one global URP Volume with the race look: bloom, ACES tonemapping, colour grading,
    /// vignette and subtle film grain. Uses the Resources profile "Race1PostProfile" when it exists
    /// (Race1 > Create Visual Profile in the editor), otherwise an in-memory profile.
    /// Missing components are tolerated: nothing throws, the affected override is simply skipped.
    /// </summary>
    public sealed class PostProcessingController
    {
        private const string ProfileResourceName = "Race1PostProfile";
        private const float EnvironmentTau = 0.8f;
        private const float ImpactTau = 0.15f;
        private const float MaxStep = 0.25f;

        private const float DayBloom = 0.15f;
        private const float NightBloom = 0.35f;
        private const float ImpactBloom = 0.1f;

        private static readonly Color WarmFilter = new Color(1f, 0.95f, 0.88f, 1f);

        private readonly Volume _volume;
        private readonly VolumeProfile _profile;
        private readonly Bloom _bloom;
        private readonly Tonemapping _tonemapping;
        private readonly ColorAdjustments _colorAdjustments;
        private readonly Vignette _vignette;
        private readonly FilmGrain _filmGrain;

        private PostState _smoothed;
        private bool _hasState;

        /// <summary>
        /// Finds or creates the global Volume, assigns the profile and caches the effect components.
        /// </summary>
        public PostProcessingController()
        {
            _volume = FindOrCreateVolume();

            _profile = Resources.Load<VolumeProfile>(ProfileResourceName);
            if (_profile == null)
            {
                _profile = ScriptableObject.CreateInstance<VolumeProfile>();
                _profile.name = "Race1PostProfile (runtime)";
            }

            if (_volume != null)
            {
                _volume.isGlobal = true;
                _volume.weight = 1f;
                _volume.priority = 10f;
                _volume.sharedProfile = _profile;
            }

            _bloom = GetOrAdd<Bloom>(_profile);
            _tonemapping = GetOrAdd<Tonemapping>(_profile);
            _colorAdjustments = GetOrAdd<ColorAdjustments>(_profile);
            _vignette = GetOrAdd<Vignette>(_profile);
            _filmGrain = GetOrAdd<FilmGrain>(_profile);
        }

        /// <summary>True when a global Volume with a profile is driving the scene.</summary>
        public bool HasVolume
        {
            get { return _volume != null && _profile != null; }
        }

        /// <summary>
        /// Writes the grade for this frame. <paramref name="dt"/> is the frame time in seconds; the state is
        /// eased toward <paramref name="state"/> so golden hour and impacts do not pop.
        /// </summary>
        public void Tick(float dt, PostState state)
        {
            if (!HasVolume)
            {
                return;
            }

            Advance(dt, state);

            float night = Mathf.Clamp01(_smoothed.Night01);
            float golden = Mathf.Clamp01(_smoothed.Golden01);
            float rain = Mathf.Clamp01(_smoothed.Rain01);
            float impact = Mathf.Clamp01(_smoothed.Impact01);
            float speed = Mathf.Clamp01(_smoothed.Speed01);

            if (_bloom != null)
            {
                Override(_bloom.intensity, Mathf.Clamp01(Mathf.Lerp(DayBloom, NightBloom, night) + ImpactBloom * impact));
            }

            if (_tonemapping != null)
            {
                Override(_tonemapping.mode, TonemappingMode.ACES);
            }

            if (_colorAdjustments != null)
            {
                Override(_colorAdjustments.postExposure, Mathf.Lerp(0.2f, -0.1f, night));
                Override(_colorAdjustments.contrast, 10f);
                Override(_colorAdjustments.saturation, Mathf.Lerp(8f, -20f, rain));
                Override(_colorAdjustments.colorFilter, Color.Lerp(Color.white, WarmFilter, golden));
            }

            if (_vignette != null)
            {
                Override(_vignette.intensity, Mathf.Clamp01(0.22f + 0.2f * impact + 0.04f * speed));
                Override(_vignette.smoothness, 0.4f);
            }

            if (_filmGrain != null)
            {
                Override(_filmGrain.intensity, 0.08f);
            }
        }

        private static Volume FindOrCreateVolume()
        {
#if UNITY_6000_0_OR_NEWER
            Volume existing = UnityEngine.Object.FindFirstObjectByType<Volume>();
#else
            Volume existing = UnityEngine.Object.FindObjectOfType<Volume>();
#endif
            if (existing != null)
            {
                return existing;
            }

            return new GameObject("Race1PostVolume").AddComponent<Volume>();
        }

        private static T GetOrAdd<T>(VolumeProfile profile) where T : VolumeComponent
        {
            T component;
            if (profile.TryGet<T>(out component) && component != null)
            {
                return component;
            }

            return profile.Add<T>(true);
        }

        private static void Override<T>(VolumeParameter<T> parameter, T value)
        {
            if (parameter == null)
            {
                return;
            }

            parameter.value = value;
            parameter.overrideState = true;
        }

        private void Advance(float dt, PostState target)
        {
            if (!_hasState)
            {
                _smoothed = target;
                _hasState = true;
                return;
            }

            float step = Mathf.Clamp(dt, 0f, MaxStep);
            float environment = 1f - Mathf.Exp(-step / EnvironmentTau);
            float impact = 1f - Mathf.Exp(-step / ImpactTau);

            _smoothed.Night01 += (target.Night01 - _smoothed.Night01) * environment;
            _smoothed.Golden01 += (target.Golden01 - _smoothed.Golden01) * environment;
            _smoothed.Rain01 += (target.Rain01 - _smoothed.Rain01) * environment;
            _smoothed.Fog01 += (target.Fog01 - _smoothed.Fog01) * environment;
            _smoothed.Speed01 += (target.Speed01 - _smoothed.Speed01) * environment;
            _smoothed.Impact01 += (target.Impact01 - _smoothed.Impact01) * impact;
        }
    }
}
