using Race1.Core;
using UnityEngine;

namespace Race1.Game
{
    /// <summary>Procedural textures from the same seeded noise as the world. No image files are shipped.</summary>
    public static class ProceduralTextures
    {
        private const uint ChipToneSalt = 0x9E3779B9u;
        private const uint GritSalt = 0x3C6EF372u;
        private const uint WearSalt = 0x1F83D9ABu;
        private const uint PebbleSalt = 0x6A09E667u;
        private const uint PebbleToneSalt = 0x2F9BE6A1u;

        private static readonly byte[] SrgbLut = BuildSrgbLut();

        /// <summary>
        /// Asphalt albedo (sRGB, tileable): cool grey, sparse light aggregate, darker tar patches and faint wheel tracks.
        /// Kept for existing callers. New code should use <see cref="RoadMaterialFactory"/>.
        /// </summary>
        public static Texture2D Asphalt(ulong seed, int size = 256)
        {
            return AsphaltAlbedo(new AsphaltField(seed, size));
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

        /// <summary>
        /// Asphalt albedo in sRGB. Texture x runs across the lane (mesh u) and y runs along the road (mesh v),
        /// so the wheel tracks sit at fixed x and run along y.
        /// </summary>
        internal static Texture2D AsphaltAlbedo(AsphaltField f)
        {
            int size = f.Size;
            float inv = 1f / size;
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int i = y * size + x;
                    float h = f.Height[i];
                    float p = f.Patch[i];

                    float shade = 0.94f + 0.12f * h;
                    shade *= 1f - 0.30f * Smooth(0.74f, 0.92f, p);
                    shade *= 1f - 0.10f * WheelTrack(x * inv) * (0.25f + 0.75f * p);

                    // Linear base albedo around 0.115 with a slight cool tint.
                    float r = 0.106f * shade;
                    float g = 0.114f * shade;
                    float b = 0.124f * shade;

                    if (IsChip(f, x, y, h))
                    {
                        float s = 0.25f + 0.15f * Unit(Hash(x, y, f.Salt ^ ChipToneSalt));
                        r = s;
                        g = s * 0.99f;
                        b = s * 0.97f;
                    }

                    px[i] = new Color32(Encode(r), Encode(g), Encode(b), 255);
                }
            }
            return TextureUtil.FromPixels(px, size, "AsphaltAlbedo", false);
        }

        /// <summary>Tangent-space normal map: fine height field plus raised aggregate chips and a little grit.</summary>
        internal static Texture2D AsphaltNormal(AsphaltField f)
        {
            int size = f.Size;
            var bump = new float[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int i = y * size + x;
                    float h = f.Height[i];
                    float grit = 0.03f * (Unit(Hash(x, y, f.Salt ^ GritSalt)) - 0.5f);
                    bump[i] = h + grit + (IsChip(f, x, y, h) ? 0.25f : 0f);
                }
            }
            return TextureUtil.NormalMap(bump, size, 2.0f, "AsphaltNormal");
        }

        /// <summary>Near-white paint in sRGB with a warm tint. A few worn spots show the road beneath.</summary>
        internal static Texture2D MarkingAlbedo(AsphaltField f)
        {
            int size = f.Size;
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int i = y * size + x;
                    float shade = 0.97f + 0.06f * f.Height[i];
                    float r = 0.88f * shade;
                    float g = 0.865f * shade;
                    float b = 0.82f * shade;
                    if (Unit(Hash(x, y, f.Salt ^ WearSalt)) > 0.985f)
                    {
                        r = 0.2f;
                        g = 0.2f;
                        b = 0.2f;
                    }
                    px[i] = new Color32(Encode(r), Encode(g), Encode(b), 255);
                }
            }
            return TextureUtil.FromPixels(px, size, "MarkingAlbedo", false);
        }

        /// <summary>Gravel-grey shoulder in sRGB: albedo about 0.28 with lighter and darker pebbles.</summary>
        internal static Texture2D ShoulderAlbedo(AsphaltField f)
        {
            int size = f.Size;
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int i = y * size + x;
                    float shade = 0.85f + 0.3f * f.Height[i];
                    float r = 0.285f * shade;
                    float g = 0.28f * shade;
                    float b = 0.27f * shade;
                    if (Unit(Hash(x, y, f.Salt ^ PebbleSalt)) > 0.9f)
                    {
                        float s = 0.14f + 0.28f * Unit(Hash(x, y, f.Salt ^ PebbleToneSalt));
                        r = s;
                        g = s * 0.98f;
                        b = s * 0.95f;
                    }
                    px[i] = new Color32(Encode(r), Encode(g), Encode(b), 255);
                }
            }
            return TextureUtil.FromPixels(px, size, "ShoulderAlbedo", false);
        }

        /// <summary>
        /// Remaps a field so its 0.5th and 99.5th percentiles become 0 and 1. The raw fractal output clusters
        /// near 0.5, so this gives the thresholds and normal map real contrast.
        /// </summary>
        internal static float[] Stretch(float[] src)
        {
            int n = src.Length;
            var dst = new float[n];
            float min = float.MaxValue;
            float max = float.MinValue;
            for (int i = 0; i < n; i++)
            {
                float v = src[i];
                if (v < min) min = v;
                if (v > max) max = v;
            }

            float range = max - min;
            if (range < 1e-6f)
            {
                for (int i = 0; i < n; i++) dst[i] = 0.5f;
                return dst;
            }

            const int bins = 1024;
            var hist = new int[bins];
            float scale = (bins - 1) / range;
            for (int i = 0; i < n; i++)
            {
                int b = (int)((src[i] - min) * scale);
                if (b < 0) b = 0;
                if (b >= bins) b = bins - 1;
                hist[b]++;
            }

            float lo = Percentile(hist, n, 0.005, min, range);
            float hi = Percentile(hist, n, 0.995, min, range);
            float inv = 1f / Mathf.Max(hi - lo, 1e-6f);
            for (int i = 0; i < n; i++)
            {
                dst[i] = Mathf.Clamp01((src[i] - lo) * inv);
            }
            return dst;
        }

        private static float Percentile(int[] hist, int total, double fraction, float min, float range)
        {
            long target = (long)(total * fraction);
            long acc = 0;
            for (int b = 0; b < hist.Length; b++)
            {
                acc += hist[b];
                if (acc > target)
                {
                    return min + range * (b / (float)(hist.Length - 1));
                }
            }
            return min + range;
        }

        /// <summary>A chip is a sparse texel (about 3 percent) on the higher ground. Albedo and normal use the same test.</summary>
        private static bool IsChip(AsphaltField f, int x, int y, float height)
        {
            return height > 0.3f && Unit(Hash(x, y, f.Salt)) > 0.965f;
        }

        /// <summary>Darkening weight near the two wheel tracks at u = 0.27 and 0.73 of the lane.</summary>
        private static float WheelTrack(float u)
        {
            float a = Smooth(0.09f, 0f, WrapDistance(u, 0.27f));
            float b = Smooth(0.09f, 0f, WrapDistance(u, 0.73f));
            return Mathf.Max(a, b);
        }

        private static float WrapDistance(float u, float centre)
        {
            float d = Mathf.Abs(u - centre);
            return d > 0.5f ? 1f - d : d;
        }

        private static float Smooth(float e0, float e1, float x)
        {
            float t = Mathf.Clamp01((x - e0) / (e1 - e0));
            return t * t * (3f - 2f * t);
        }

        /// <summary>Per-texel integer hash. Texels are independent, so the pattern tiles by construction.</summary>
        private static uint Hash(int x, int y, uint salt)
        {
            unchecked
            {
                uint h = salt ^ ((uint)x * 0x9E3779B1u) ^ ((uint)y * 0x85EBCA77u);
                h ^= h >> 16;
                h *= 0x7FEB352Du;
                h ^= h >> 15;
                h *= 0x846CA68Bu;
                h ^= h >> 16;
                return h;
            }
        }

        /// <summary>Maps a hash to [0, 1).</summary>
        private static float Unit(uint h)
        {
            return (h >> 8) * (1f / 16777216f);
        }

        private static byte Encode(float linear)
        {
            int idx = (int)(Mathf.Clamp01(linear) * 4095f + 0.5f);
            return SrgbLut[idx];
        }

        private static byte[] BuildSrgbLut()
        {
            var lut = new byte[4096];
            for (int i = 0; i < lut.Length; i++)
            {
                float lin = i / 4095f;
                float s = lin <= 0.0031308f ? lin * 12.92f : 1.055f * Mathf.Pow(lin, 1f / 2.4f) - 0.055f;
                lut[i] = (byte)Mathf.Clamp(Mathf.RoundToInt(s * 255f), 0, 255);
            }
            return lut;
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

    /// <summary>
    /// Seeded height and patch fields shared by every road texture, so the albedo, normal and marking maps
    /// line up with each other.
    /// </summary>
    internal sealed class AsphaltField
    {
        /// <summary>Texture width and height in texels.</summary>
        public readonly int Size;

        /// <summary>Seed-derived salt for the per-texel hashes.</summary>
        public readonly uint Salt;

        /// <summary>Fine surface height, 0..1 (fractal noise, 16 cells per tile, 4 octaves).</summary>
        public readonly float[] Height;

        /// <summary>Low-frequency field, 0..1 (8 cells per tile, 3 octaves). Drives tar patches and tone.</summary>
        public readonly float[] Patch;

        public AsphaltField(ulong seed, int size)
        {
            Size = size;
            Salt = (uint)(seed ^ (seed >> 32)) ^ 0x51ED270Bu;
            Height = ProceduralTextures.Stretch(TextureUtil.TileableFbm(DeterministicRng.Combine(seed, 0xA5F1UL), size, 16, 4, 0.5));
            Patch = ProceduralTextures.Stretch(TextureUtil.TileableFbm(DeterministicRng.Combine(seed, 0x7A4BUL), size, 8, 3, 0.5));
        }
    }
}
