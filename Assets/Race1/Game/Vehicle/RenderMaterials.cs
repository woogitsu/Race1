using UnityEngine;

namespace Race1.Game
{
    /// <summary>
    /// Material factory. Prefers the URP Lit shader and falls back to the built-in Standard shader, setting
    /// both sets of property names so either pipeline shows the intended colour.
    /// </summary>
    public static class RenderMaterials
    {
        private static Shader _lit;

        public static Shader LitShader
        {
            get
            {
                if (_lit == null)
                {
                    _lit = Shader.Find("Universal Render Pipeline/Lit");
                    if (_lit == null) _lit = Shader.Find("Standard");
                }
                return _lit;
            }
        }

        public static Material Create(Color colour, float metallic, float smoothness, Texture albedo, bool instanced = false)
        {
            var mat = new Material(LitShader) { name = "Race1Lit", enableInstancing = instanced };
            SetBaseColor(mat, colour);
            SetFloatBoth(mat, "_Metallic", "_Metallic", metallic);
            SetFloatBoth(mat, "_Smoothness", "_Glossiness", smoothness);
            if (albedo != null)
            {
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", albedo);
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", albedo);
            }
            return mat;
        }

        public static Material Emissive(Color colour, Color emission)
        {
            Material mat = Create(colour, 0f, 0.4f, null);
            if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", emission);
            mat.EnableKeyword("_EMISSION");
            return mat;
        }

        public static void SetBaseColor(Material mat, Color colour)
        {
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", colour);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", colour);
        }

        private static void SetFloatBoth(Material mat, string urpName, string builtinName, float value)
        {
            if (mat.HasProperty(urpName)) mat.SetFloat(urpName, value);
            else if (mat.HasProperty(builtinName)) mat.SetFloat(builtinName, value);
        }
    }
}
