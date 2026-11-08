using UnityEngine;

namespace Race1.Game
{
    /// <summary>
    /// Material presets for car parts. Body paint is metallic with a procedural flake normal map; glass, tyres and
    /// rims are plain PBR surfaces. Each call returns a new material, so callers that build many cars should share
    /// the result.
    /// </summary>
    public static class CarPaint
    {
        private const int FlakeSize = 128;
        private const ulong FlakeSeed = 0xF1A6C0DEUL;
        private static Texture2D _flakeNormal;

        /// <summary>Metallic body paint in the given colour, with a fine sparkle from the flake normal map.</summary>
        public static Material CreateBodyPaint(Color colour)
        {
            Material mat = RenderMaterials.Create(colour, 0.75f, 0.88f, null);
            if (mat.HasProperty("_BumpMap"))
            {
                mat.SetTexture("_BumpMap", FlakeNormal);
                mat.SetTextureScale("_BumpMap", new Vector2(8f, 8f));
                mat.EnableKeyword("_NORMALMAP");
            }
            if (mat.HasProperty("_BumpScale")) mat.SetFloat("_BumpScale", 1f);
            return mat;
        }

        /// <summary>Dark tinted glass for windows.</summary>
        public static Material CreateGlass()
        {
            return RenderMaterials.Create(new Color(0.03f, 0.04f, 0.06f), 0.1f, 0.97f, null);
        }

        /// <summary>Very dark, matte-ish rubber for tyres.</summary>
        public static Material CreateTyre()
        {
            return RenderMaterials.Create(new Color(0.02f, 0.02f, 0.022f), 0f, 0.3f, null);
        }

        /// <summary>Brushed metal alloy for wheel rims.</summary>
        public static Material CreateRim()
        {
            return RenderMaterials.Create(new Color(0.7f, 0.72f, 0.75f), 1f, 0.6f, null);
        }

        private static Texture2D FlakeNormal
        {
            get
            {
                if (_flakeNormal == null) _flakeNormal = BuildFlakeNormal();
                return _flakeNormal;
            }
        }

        /// <summary>Soft tileable noise plus scattered two-pixel flake facets, converted to a normal map.</summary>
        private static Texture2D BuildFlakeNormal()
        {
            const int size = FlakeSize;
            float[] height = TextureUtil.TileableFbm(FlakeSeed, size, 32, 2, 0.5);
            var rng = new System.Random(2026);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    if (rng.NextDouble() >= 0.08) continue;
                    for (int dy = 0; dy < 2; dy++)
                    {
                        for (int dx = 0; dx < 2; dx++)
                        {
                            int index = ((y + dy) % size) * size + (x + dx) % size;
                            height[index] += 0.6f;
                        }
                    }
                }
            }
            return TextureUtil.NormalMap(height, size, 0.25f, "CarFlakeNormal");
        }
    }
}
