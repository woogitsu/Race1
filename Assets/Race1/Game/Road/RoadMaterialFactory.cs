using System;
using UnityEngine;

namespace Race1.Game
{
    /// <summary>
    /// The three road materials for one world seed: carriageway asphalt, lane markings and gravel shoulder.
    /// Every chunk should share these instances. <see cref="SetWetness"/> changes all of them in place.
    /// </summary>
    public sealed class RoadMaterialSet
    {
        /// <summary>Dry smoothness of the asphalt.</summary>
        internal const float AsphaltDrySmoothness = 0.25f;

        /// <summary>Dry smoothness of the lane markings.</summary>
        internal const float MarkingDrySmoothness = 0.35f;

        /// <summary>Dry smoothness of the gravel shoulder.</summary>
        internal const float ShoulderDrySmoothness = 0.15f;

        private const float WetGloss = 0.65f;
        private const float WetDarkening = 0.35f;

        private float _wetness;

        /// <summary>Carriageway asphalt. Darkens and gets glossier when wet.</summary>
        public Material Asphalt;

        /// <summary>Lane markings. Keeps its colour when wet. Only its gloss changes.</summary>
        public Material Marking;

        /// <summary>Gravel shoulder. Darkens by half the asphalt amount when wet.</summary>
        public Material Shoulder;

        internal RoadMaterialSet(Material asphalt, Material marking, Material shoulder)
        {
            Asphalt = asphalt;
            Marking = marking;
            Shoulder = shoulder;
        }

        /// <summary>Current wetness: 0 is dry and 1 is fully wet.</summary>
        public float Wetness => _wetness;

        /// <summary>
        /// Sets how wet the road looks. The value is clamped to 0..1. Asphalt darkens by up to 35 percent
        /// and gets glossier. Markings get glossier only. Shoulders darken by half the asphalt amount.
        /// </summary>
        public void SetWetness(float wet01)
        {
            float w = float.IsNaN(wet01) ? 0f : Mathf.Clamp01(wet01);
            _wetness = w;

            if (Asphalt != null)
            {
                RoadMaterialFactory.SetBaseGrey(Asphalt, 1f - WetDarkening * w);
                RoadMaterialFactory.SetSmoothness(Asphalt, AsphaltDrySmoothness + WetGloss * w);
            }
            if (Marking != null)
            {
                RoadMaterialFactory.SetSmoothness(Marking, Mathf.Clamp01(MarkingDrySmoothness + WetGloss * w));
            }
            if (Shoulder != null)
            {
                RoadMaterialFactory.SetBaseGrey(Shoulder, 1f - 0.5f * WetDarkening * w);
                RoadMaterialFactory.SetSmoothness(Shoulder, ShoulderDrySmoothness + 0.5f * WetGloss * w);
            }
        }
    }

    /// <summary>
    /// Builds the road materials from seeded procedural textures. Textures are generated at runtime, so no
    /// image files are shipped. Create one set per world seed and share it across all road chunks.
    /// </summary>
    public static class RoadMaterialFactory
    {
        private const int TextureSize = 512;

        /// <summary>
        /// Generates the road textures for <paramref name="seed"/> and returns dry materials. Call once per world.
        /// The set starts dry (wetness 0).
        /// </summary>
        public static RoadMaterialSet Create(ulong seed)
        {
            var field = new AsphaltField(seed, TextureSize);
            Shader lit = FindLitShader();

            Texture2D normal = ProceduralTextures.AsphaltNormal(field);

            Material asphalt = NewMaterial("RoadAsphalt", lit, ProceduralTextures.AsphaltAlbedo(field), normal, 1.0f, RoadMaterialSet.AsphaltDrySmoothness);
            Material marking = NewMaterial("RoadMarking", lit, ProceduralTextures.MarkingAlbedo(field), normal, 0.35f, RoadMaterialSet.MarkingDrySmoothness);
            Material shoulder = NewMaterial("RoadShoulder", lit, ProceduralTextures.ShoulderAlbedo(field), normal, 1.4f, RoadMaterialSet.ShoulderDrySmoothness);

            return new RoadMaterialSet(asphalt, marking, shoulder);
        }

        /// <summary>Sets the base colour to a grey level (URP and Standard property names).</summary>
        internal static void SetBaseGrey(Material mat, float grey)
        {
            var colour = new Color(grey, grey, grey, 1f);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", colour);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", colour);
        }

        /// <summary>Sets smoothness on URP Lit (_Smoothness) or Standard (_Glossiness).</summary>
        internal static void SetSmoothness(Material mat, float value)
        {
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", value);
            else if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", value);
        }

        private static Material NewMaterial(string materialName, Shader shader, Texture2D albedo, Texture2D normal, float bumpScale, float smoothness)
        {
            var mat = new Material(shader) { name = materialName };
            SetBaseGrey(mat, 1f);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
            SetSmoothness(mat, smoothness);

            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", albedo);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", albedo);

            if (mat.HasProperty("_BumpMap"))
            {
                mat.SetTexture("_BumpMap", normal);
                if (mat.HasProperty("_BumpScale")) mat.SetFloat("_BumpScale", bumpScale);
                mat.EnableKeyword("_NORMALMAP");
            }
            return mat;
        }

        private static Shader FindLitShader()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null)
            {
                throw new InvalidOperationException("Neither Universal Render Pipeline/Lit nor Standard is available for road materials.");
            }
            return shader;
        }
    }
}
