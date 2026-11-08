using Race1.Core;
using UnityEngine;

namespace Race1.Game
{
    /// <summary>Procedural textures from the same seeded noise as the world. No image files are shipped.</summary>
    public static class ProceduralTextures
    {
        /// <summary>Asphalt: fine grain plus larger patches. Tileable because the noise is sampled on a torus.</summary>
        public static Texture2D Asphalt(ulong seed, int size = 256)
        {
            var noise = new PerlinNoise2D(seed);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    double u = x / (double)size * 4.0;
                    double v = y / (double)size * 4.0;
                    double grain = noise.Fbm(u * 16.0, v * 16.0, 3, 2.0, 0.5);
                    double patch = noise.Fbm(u * 2.0 + 7.1, v * 2.0 + 3.7, 2, 2.0, 0.5);
                    float g = Mathf.Clamp01(0.36f + 0.05f * (float)grain + 0.04f * (float)patch);
                    byte c = (byte)(g * 255f);
                    pixels[y * size + x] = new Color32(c, c, (byte)(c + 2), 255);
                }
            }
            return Build(pixels, size, "Asphalt");
        }

        /// <summary>Terrain detail: green and brown variation used as a tiled albedo.</summary>
        public static Texture2D Ground(ulong seed, int size = 256)
        {
            var noise = new PerlinNoise2D(seed ^ 0xA5A5UL);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    double n = noise.Fbm(x / (double)size * 8.0, y / (double)size * 8.0, 4, 2.0, 0.5);
                    float k = Mathf.Clamp01(0.85f + 0.15f * (float)n);
                    pixels[y * size + x] = new Color32((byte)(k * 255f), (byte)(k * 250f), (byte)(k * 240f), 255);
                }
            }
            return Build(pixels, size, "Ground");
        }

        private static Texture2D Build(Color32[] pixels, int size, string name)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true, false)
            {
                name = name,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 8,
            };
            tex.SetPixels32(pixels);
            tex.Apply(true, true);
            return tex;
        }
    }
}
