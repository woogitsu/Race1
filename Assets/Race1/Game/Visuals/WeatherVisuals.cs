using Race1.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Race1.Game
{
    /// <summary>
    /// Drives the rain and snow particle emitters and the road wetness from a <see cref="WeatherState"/>.
    /// Both emitters live in a box ahead of the focus point and move with it every tick.
    /// </summary>
    public sealed class WeatherVisuals
    {
        private const float RainMaxRate = 1200f;
        private const float SnowMaxRate = 300f;
        private const float RainAlpha = 0.35f;
        private const float SnowAlpha = 0.6f;
        private const float PlayThreshold = 0.05f;
        private const float WetnessEpsilon = 0.01f;
        private const float RainBaseVelocityScale = 0.06f;
        private const float RainStretchAtFullSpeed = 0.5f;
        private const float FullStretchSpeedMs = 60f;
        private const float AheadOffset = 15f;
        private const float AboveOffset = 12f;
        private const float VolumeWidth = 40f;
        private const float VolumeThickness = 0.5f;
        private const float DirectionEpsilon = 1e-6f;
        private const int MaxParticles = 2000;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int TintColorId = Shader.PropertyToID("_Color");

        private readonly Camera _camera;
        private readonly ParticleSystem _rain;
        private readonly ParticleSystem _snow;
        private readonly Transform _rainTransform;
        private readonly Transform _snowTransform;
        private readonly ParticleSystemRenderer _rainRenderer;
        private RoadMaterialSet _roads;
        private float _lastWetness = float.NaN;

        /// <summary>
        /// Creates the rain and snow emitters under <paramref name="parent"/>. The camera is used only as a
        /// heading fallback when the car forward vector is degenerate; it may be null.
        /// </summary>
        public WeatherVisuals(Camera camera, Transform parent)
        {
            _camera = camera;

            Material rainMaterial = CreateParticleMaterial(RainAlpha);
            Material snowMaterial = CreateParticleMaterial(SnowAlpha);

            _rain = CreateEmitter("Rain", parent, rainMaterial, ParticleSystemRenderMode.Stretch);
            ConfigureRain(_rain);
            _rainTransform = _rain.transform;
            _rainRenderer = _rain.GetComponent<ParticleSystemRenderer>();
            _rainRenderer.lengthScale = 0.2f;
            _rainRenderer.velocityScale = RainBaseVelocityScale;

            _snow = CreateEmitter("Snow", parent, snowMaterial, ParticleSystemRenderMode.Billboard);
            ConfigureSnow(_snow);
            _snowTransform = _snow.transform;
        }

        /// <summary>
        /// Updates emitter rates, positions and road wetness for the current frame.
        /// </summary>
        /// <param name="dt">Frame time in seconds (currently unused; kept for the shared tick signature).</param>
        /// <param name="weather">Current weather parameters.</param>
        /// <param name="focusWorld">World position the emitters follow (typically the camera or car).</param>
        /// <param name="carForwardWorld">Car forward direction in world space; flattened to the ground plane.</param>
        /// <param name="speedMs">Car speed in m/s; lengthens rain streaks slightly at speed.</param>
        public void Tick(float dt, WeatherState weather, Vector3 focusWorld, Vector3 carForwardWorld, float speedMs)
        {
            if (weather == null)
            {
                return;
            }

            float rain = Mathf.Clamp01((float)weather.Rain);
            float snow = Mathf.Clamp01((float)weather.Snow);

            Vector3 forward = FlatForward(carForwardWorld);
            Vector3 centre = focusWorld + forward * AheadOffset + Vector3.up * AboveOffset;
            _rainTransform.position = centre;
            _snowTransform.position = centre;

            UpdateEmitter(_rain, rain * RainMaxRate, rain > PlayThreshold);
            UpdateEmitter(_snow, snow * SnowMaxRate, snow > PlayThreshold);

            float speedFactor = Mathf.Clamp01(speedMs / FullStretchSpeedMs);
            _rainRenderer.velocityScale = RainBaseVelocityScale * (1f + RainStretchAtFullSpeed * speedFactor);

            float wet = Mathf.Clamp01((float)weather.Wetness);
            if (_roads != null && (float.IsNaN(_lastWetness) || Mathf.Abs(wet - _lastWetness) > WetnessEpsilon))
            {
                _roads.SetWetness(wet);
                _lastWetness = wet;
            }
        }

        /// <summary>
        /// Sets the road material set that receives wetness updates. Pass the set once it is built;
        /// the next tick pushes the current wetness to it.
        /// </summary>
        public void BindRoads(RoadMaterialSet roads)
        {
            _roads = roads;
            _lastWetness = float.NaN;
        }

        private Vector3 FlatForward(Vector3 carForward)
        {
            Vector3 f = carForward;
            f.y = 0f;
            if (f.sqrMagnitude < DirectionEpsilon)
            {
                f = _camera != null ? _camera.transform.forward : Vector3.forward;
                f.y = 0f;
                if (f.sqrMagnitude < DirectionEpsilon)
                {
                    return Vector3.forward;
                }
            }

            return f.normalized;
        }

        private static void UpdateEmitter(ParticleSystem system, float rate, bool active)
        {
            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = new ParticleSystem.MinMaxCurve(rate);
            if (active && !system.isPlaying)
            {
                system.Play();
            }
        }

        private static ParticleSystem CreateEmitter(string name, Transform parent, Material material, ParticleSystemRenderMode renderMode)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            ParticleSystem system = go.AddComponent<ParticleSystem>();

            ParticleSystem.MainModule main = system.main;
            main.playOnAwake = false;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = MaxParticles;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = new ParticleSystem.MinMaxCurve(0f);

            ParticleSystem.ShapeModule shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(VolumeWidth, VolumeThickness, VolumeWidth);
            // Local +Z points down after this rotation, so the emitted particles travel downwards.
            shape.rotation = new Vector3(90f, 0f, 0f);

            ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = renderMode;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            if (material != null)
            {
                renderer.sharedMaterial = material;
            }

            return system;
        }

        private static void ConfigureRain(ParticleSystem system)
        {
            ParticleSystem.MainModule main = system.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 0.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(25f, 35f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.03f);
            main.gravityModifier = new ParticleSystem.MinMaxCurve(0.1f);
        }

        private static void ConfigureSnow(ParticleSystem system)
        {
            ParticleSystem.MainModule main = system.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.08f);
            main.gravityModifier = new ParticleSystem.MinMaxCurve(0f);
        }

        private static Material CreateParticleMaterial(float alpha)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Particles/Standard Unlit");
            }

            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            if (shader == null)
            {
                return null;
            }

            var material = new Material(shader);
            var colour = new Color(1f, 1f, 1f, alpha);
            if (material.HasProperty(BaseColorId))
            {
                material.SetColor(BaseColorId, colour);
            }

            if (material.HasProperty(TintColorId))
            {
                material.SetColor(TintColorId, colour);
            }

            return material;
        }
    }
}
