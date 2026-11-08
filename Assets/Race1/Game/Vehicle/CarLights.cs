using UnityEngine;

namespace Race1.Game
{
    /// <summary>
    /// Lamps for one car: headlight and taillight lens quads, two headlight spot lights and a brake glow point light
    /// behind the car. Everything is parented under the given transform, in model space (forward +Z, right +X,
    /// up +Y). Headlights fade in with <see cref="SetNight"/>. Taillights stay lit at night, and brake input
    /// brightens them and the point light through <see cref="SetBrake"/>.
    /// </summary>
    public sealed class CarLights
    {
        private static readonly Color HeadEmission = new Color(3.2f, 3.0f, 2.6f);
        private static readonly Color TailEmission = new Color(1.0f, 0.06f, 0.03f);
        private static readonly Color BrakeColour = new Color(1.0f, 0.1f, 0.05f);
        private static Mesh _quad;

        private readonly Material _headLens;
        private readonly Material _tailLens;
        private readonly Light _headLeft;
        private readonly Light _headRight;
        private readonly Light _brakeLight;
        private float _night;
        private float _brake;

        /// <summary>
        /// Builds the lamps. <paramref name="length"/>, <paramref name="width"/> and <paramref name="height"/> are the
        /// car's overall dimensions in metres. The lamps sit on the front and rear end faces, low in the body, where
        /// the CarMeshFactory body ends.
        /// </summary>
        public CarLights(Transform parent, float length, float width, float height)
        {
            float lampY = 0.08f * height;
            float lampX = 0.12f * width;
            float lampZ = 0.5f * length + 0.01f;
            float quadW = 0.085f * width;
            const float quadH = 0.07f;

            _headLens = RenderMaterials.Emissive(new Color(0.9f, 0.92f, 0.95f), Color.black);
            _tailLens = RenderMaterials.Emissive(new Color(0.5f, 0.02f, 0.02f), TailEmission * 0.5f);

            CreateQuad(parent, "HeadLensL", new Vector3(-lampX, lampY, lampZ), quadW, quadH, _headLens);
            CreateQuad(parent, "HeadLensR", new Vector3(lampX, lampY, lampZ), quadW, quadH, _headLens);
            CreateQuad(parent, "TailLensL", new Vector3(-lampX, lampY, -lampZ), quadW, quadH, _tailLens);
            CreateQuad(parent, "TailLensR", new Vector3(lampX, lampY, -lampZ), quadW, quadH, _tailLens);

            _headLeft = CreateHeadSpot(parent, "HeadlightL", new Vector3(-lampX, lampY, lampZ + 0.04f));
            _headRight = CreateHeadSpot(parent, "HeadlightR", new Vector3(lampX, lampY, lampZ + 0.04f));

            var brakeGo = new GameObject("BrakeGlow");
            brakeGo.transform.SetParent(parent, false);
            brakeGo.transform.localPosition = new Vector3(0f, lampY + 0.1f, -(0.5f * length + 0.5f));
            _brakeLight = brakeGo.AddComponent<Light>();
            _brakeLight.type = LightType.Point;
            _brakeLight.color = BrakeColour;
            _brakeLight.range = 8f;
            _brakeLight.shadows = LightShadows.None;
            SetLight(_brakeLight, 0f);

            Apply();
        }

        /// <summary>Night amount: 0 is full day, 1 is full night. Headlights and their glow fade in with it.</summary>
        public void SetNight(float night01)
        {
            _night = Mathf.Clamp01(night01);
            Apply();
        }

        /// <summary>Brake pedal amount, 0..1. Brightens the taillights and drives the rear glow light.</summary>
        public void SetBrake(float brake01)
        {
            _brake = Mathf.Clamp01(brake01);
            Apply();
        }

        private void Apply()
        {
            SetEmission(_headLens, HeadEmission * _night);
            float tailGlow = 0.5f + 0.9f * _night + 2.2f * _brake;
            SetEmission(_tailLens, TailEmission * tailGlow);

            SetLight(_headLeft, 4f * _night);
            SetLight(_headRight, 4f * _night);
            SetLight(_brakeLight, 2.5f * _brake);
        }

        private static void SetEmission(Material mat, Color emission)
        {
            if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", emission);
        }

        /// <summary>Disables the light component when it is off, so URP does not spend light slots on it.</summary>
        private static void SetLight(Light light, float intensity)
        {
            light.intensity = intensity;
            light.enabled = intensity > 0.001f;
        }

        private static void CreateQuad(Transform parent, string name, Vector3 position, float w, float h, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = new Vector3(w, h, 1f);
            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = Quad;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private static Light CreateHeadSpot(Transform parent, string name, Vector3 position)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(3f, 0f, 0f);
            var light = go.AddComponent<Light>();
            light.type = LightType.Spot;
            light.color = new Color(1.0f, 0.95f, 0.85f);
            light.range = 60f;
            light.spotAngle = 40f;
            light.shadows = LightShadows.None;
            SetLight(light, 0f);
            return light;
        }

        /// <summary>Unit quad in the XY plane, drawn from both sides so the lamp is visible from any angle.</summary>
        private static Mesh Quad
        {
            get
            {
                if (_quad != null) return _quad;
                _quad = new Mesh { name = "CarLampQuad" };
                _quad.vertices = new[]
                {
                    new Vector3(-0.5f, -0.5f, 0f),
                    new Vector3(0.5f, -0.5f, 0f),
                    new Vector3(-0.5f, 0.5f, 0f),
                    new Vector3(0.5f, 0.5f, 0f),
                };
                _quad.normals = new[]
                {
                    new Vector3(0f, 0f, -1f),
                    new Vector3(0f, 0f, -1f),
                    new Vector3(0f, 0f, -1f),
                    new Vector3(0f, 0f, -1f),
                };
                _quad.triangles = new[] { 0, 2, 3, 0, 3, 1, 0, 3, 2, 0, 1, 3 };
                _quad.RecalculateBounds();
                return _quad;
            }
        }
    }
}
