using Race1.Core;
using UnityEngine;

namespace Race1.Game
{
    /// <summary>
    /// Seeded, seamless procedural texture helpers shared by the material factories.
    /// Every texture is generated at runtime from the world seed, so no image files are needed.
    /// </summary>
    public static class TextureUtil
    {
        /// <summary>
        /// Tileable fractal noise in 0..1. <paramref name="cells"/> is the number of noise cells per tile
        /// (use an integer). The result wraps seamlessly at the tile edges.
        /// </summary>
        public static float[] TileableFbm(ulong seed, int size, int cells, int octaves, double gain = 0.5)
        {
            var noise = new PerlinNoise2D(seed);
            var h = new float[size * size];
            int c0 = System.Math.Max(1, cells);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    double u = x / (double)size;
                    double v = y / (double)size;
                    double acc = 0.0;
                    double amp = 1.0;
                    double norm = 0.0;
                    int c = c0;
                    for (int o = 0; o < octaves; o++)
                    {
                        acc += amp * TileNoise(noise, u * c, v * c, c);
                        norm += amp;
                        amp *= gain;
                        c *= 2;
                    }
                    double n = norm > 0.0 ? acc / norm : 0.0;
                    h[y * size + x] = (float)System.Math.Max(0.0, System.Math.Min(1.0, 0.5 + 0.5 * n));
                }
            }
            return h;
        }

        /// <summary>Crossfade of four Perlin samples. The result is periodic with <paramref name="period"/> in x and y.</summary>
        public static double TileNoise(PerlinNoise2D noise, double x, double y, double period)
        {
            double fx = x / period;
            double fy = y / period;
            double a = noise.Sample(x, y);
            double b = noise.Sample(x - period, y);
            double c = noise.Sample(x, y - period);
            double d = noise.Sample(x - period, y - period);
            double top = a + (b - a) * fx;
            double bottom = c + (d - c) * fx;
            return top + (bottom - top) * fy;
        }

        /// <summary>Tangent-space normal map from a height field (RGB encoded, linear). Wraps at the edges.</summary>
        public static Texture2D NormalMap(float[] height, int size, float strength, string name)
        {
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                int yU = (y + 1) % size;
                int yD = (y - 1 + size) % size;
                for (int x = 0; x < size; x++)
                {
                    int xR = (x + 1) % size;
                    int xL = (x - 1 + size) % size;
                    float dx = (height[y * size + xR] - height[y * size + xL]) * strength;
                    float dy = (height[yU * size + x] - height[yD * size + x]) * strength;
                    Vector3 n = new Vector3(-dx, -dy, 1f).normalized;
                    px[y * size + x] = new Color32(
                        (byte)Mathf.Clamp(Mathf.RoundToInt((n.x * 0.5f + 0.5f) * 255f), 0, 255),
                        (byte)Mathf.Clamp(Mathf.RoundToInt((n.y * 0.5f + 0.5f) * 255f), 0, 255),
                        (byte)Mathf.Clamp(Mathf.RoundToInt((n.z * 0.5f + 0.5f) * 255f), 0, 255),
                        255);
                }
            }
            return FromPixels(px, size, name, true);
        }

        /// <summary>Greyscale texture from a height field in 0..1.</summary>
        public static Texture2D Grey(float[] height, int size, string name, bool linear)
        {
            var px = new Color32[size * size];
            for (int i = 0; i < px.Length; i++)
            {
                byte b = (byte)Mathf.Clamp(Mathf.RoundToInt(height[i] * 255f), 0, 255);
                px[i] = new Color32(b, b, b, 255);
            }
            return FromPixels(px, size, name, linear);
        }

        /// <summary>Creates a repeating texture with mipmaps and trilinear filtering.</summary>
        public static Texture2D FromPixels(Color32[] px, int size, string name, bool linear)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true, linear)
            {
                name = name,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 8,
            };
            tex.SetPixels32(px);
            tex.Apply(true, true);
            return tex;
        }

        /// <summary>Linear blend of two colours into a Color32 (alpha 255).</summary>
        public static Color32 Mix(Color a, Color b, float t)
        {
            Color c = Color.Lerp(a, b, Mathf.Clamp01(t));
            return new Color32((byte)(c.r * 255f), (byte)(c.g * 255f), (byte)(c.b * 255f), 255);
        }
    }
}
