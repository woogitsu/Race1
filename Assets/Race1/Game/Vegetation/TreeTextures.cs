using Race1.Core;
using UnityEngine;

namespace Race1.Game
{
    /// <summary>
    /// Procedural vegetation textures, generated once on first use and cached: a leaf card (alpha = leaf mask),
    /// a conifer needle cluster (alpha = needle mask) and a streaked bark. The colour in the cards and bark is
    /// near neutral, so the material colour sets the hue. Fixed seeds keep the output identical in every run.
    /// </summary>
    public static class TreeTextures
    {
        public const int LeafSize = 128;
        public const int NeedleSize = 128;
        public const int BarkSize = 256;

        private static Texture2D _leaf;
        private static Texture2D _needles;
        private static Texture2D _bark;

        /// <summary>Broadleaf card: a serrated elliptical leaf. Alpha is the leaf mask.</summary>
        public static Texture2D LeafCard
        {
            get
            {
                if (_leaf == null) _leaf = BuildLeaf();
                return _leaf;
            }
        }

        /// <summary>Conifer needle cluster: needles fanned from a central stem. Alpha is the needle mask.</summary>
        public static Texture2D NeedleCluster
        {
            get
            {
                if (_needles == null) _needles = BuildNeedles();
                return _needles;
            }
        }

        /// <summary>Bark: vertical streaks and dark cracks, tileable in x and y. Multiplied by the material colour.</summary>
        public static Texture2D Bark
        {
            get
            {
                if (_bark == null) _bark = BuildBark();
                return _bark;
            }
        }

        private static Texture2D BuildLeaf()
        {
            const int size = LeafSize;
            float[] tone = TextureUtil.TileableFbm(0x1EAF0AUL, size, 4, 2);
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                float v = (y + 0.5f) / size * 2f - 1f;          // -1 at the stem, +1 at the tip
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2f - 1f;      // -1..+1 across the leaf
                    float au = Mathf.Abs(u);

                    // Elliptical blade, a little narrower toward the stem.
                    float ex = u / 0.84f;
                    float ey = (v - 0.04f) / 0.96f;
                    float radius = Mathf.Sqrt(ex * ex + ey * ey);

                    // Serrated margin: sawtooth teeth along the length, pointing toward the tip.
                    float limit = 0.93f + 0.10f * Frac(v * 5.5f + 0.5f);
                    float alpha = Mathf.Clamp01((limit - radius) / 0.03f);

                    // Midrib (lighter) and fine lateral veins (darker).
                    float rib = Mathf.Clamp01(1f - au / 0.045f);
                    float phase = Frac((v + au * 0.9f) * 4.5f);
                    float vein = 1f - Mathf.Clamp01(Mathf.Min(phase, 1f - phase) / 0.06f);

                    float lum = Mathf.Lerp(0.62f, 1f, Mathf.Clamp01(0.1f + 0.9f * tone[y * size + x]));
                    float shade = 1f + 0.10f * rib - 0.14f * vein;
                    px[y * size + x] = Px(lum * 0.93f * shade, lum * shade, lum * 0.85f * shade, alpha);
                }
            }
            return TextureUtil.FromPixels(px, size, "Race1 leaf card", false);
        }

        private static Texture2D BuildNeedles()
        {
            const int size = NeedleSize;
            const int pairs = 15;
            const int count = pairs * 2 + 1;               // needle pairs, then the stem
            var rng = new DeterministicRng(0x4EED1EUL);
            var seg = new Vector4[count];                  // start.xy, end.xy
            var width = new float[count];
            var tone = new float[count];

            for (int i = 0; i < pairs; i++)
            {
                float t = 0.06f + 0.74f * i / (pairs - 1);
                for (int side = 0; side < 2; side++)
                {
                    int k = i * 2 + side;
                    float sign = side == 0 ? -1f : 1f;
                    float angle = (float)rng.Range(0.5, 0.9);
                    float length = 0.36f * (1f - 0.5f * t) * (float)rng.Range(0.85, 1.1);
                    seg[k] = new Vector4(0.5f, t, 0.5f + sign * Mathf.Sin(angle) * length, t + Mathf.Cos(angle) * length);
                    width[k] = 0.02f + 0.004f * (float)rng.NextDouble();
                    tone[k] = (float)rng.NextDouble();
                }
            }
            seg[count - 1] = new Vector4(0.5f, 0f, 0.5f, 0.9f);
            width[count - 1] = 0.012f;
            tone[count - 1] = 0f;

            const float aa = 0.9f / size;
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                float v = (y + 0.5f) / size;
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size;
                    float best = 0f;
                    int bestK = 0;
                    for (int k = 0; k < count; k++)
                    {
                        Vector4 s = seg[k];
                        float d = SegmentDistance(u, v, s.x, s.y, s.z, s.w);
                        float cov = Mathf.Clamp01((width[k] - d) / aa);
                        if (cov > best)
                        {
                            best = cov;
                            bestK = k;
                        }
                    }
                    float lum = Mathf.Lerp(0.6f, 1f, tone[bestK]);
                    px[y * size + x] = Px(lum * 0.95f, lum, lum * 0.9f, best);
                }
            }
            return TextureUtil.FromPixels(px, size, "Race1 needle cluster", false);
        }

        private static Texture2D BuildBark()
        {
            const int size = BarkSize;
            float[] streak = AnisoFbm(0xBA2C1UL, size, 24, 3, 2);   // thin streaks: many cells across, few along
            float[] crack = AnisoFbm(0xC2AC4UL, size, 14, 2, 1);    // zero lines of this field become cracks
            var px = new Color32[size * size];
            for (int i = 0; i < px.Length; i++)
            {
                float groove = Mathf.Clamp01(1f - Mathf.Abs(2f * crack[i] - 1f) / 0.03f);
                float t = Mathf.Clamp01(0.62f + 0.45f * (streak[i] - 0.5f) - 0.45f * groove);
                float lum = Mathf.Lerp(0.5f, 0.95f, t);
                px[i] = Px(lum, lum * 0.92f, lum * 0.8f, 1f);
            }
            return TextureUtil.FromPixels(px, size, "Race1 bark", false);
        }

        /// <summary>
        /// Fractal noise in 0..1 that tiles in x and y with separate cell counts per axis, so features can be
        /// stretched (many cells across, few along for vertical streaks). Same octave structure as TextureUtil.TileableFbm.
        /// </summary>
        private static float[] AnisoFbm(ulong seed, int size, int cellsX, int cellsY, int octaves)
        {
            var noise = new PerlinNoise2D(seed);
            var h = new float[size * size];
            for (int y = 0; y < size; y++)
            {
                double v = y / (double)size;
                for (int x = 0; x < size; x++)
                {
                    double u = x / (double)size;
                    double acc = 0.0;
                    double amp = 1.0;
                    double norm = 0.0;
                    int cx = cellsX;
                    int cy = cellsY;
                    for (int o = 0; o < octaves; o++)
                    {
                        acc += amp * TileNoiseAniso(noise, u * cx, v * cy, cx, cy);
                        norm += amp;
                        amp *= 0.5;
                        cx *= 2;
                        cy *= 2;
                    }
                    double n = 0.5 + 0.5 * acc / norm;
                    h[y * size + x] = (float)System.Math.Max(0.0, System.Math.Min(1.0, n));
                }
            }
            return h;
        }

        /// <summary>Crossfade of four Perlin samples with a separate period per axis (the TextureUtil.TileNoise idea).</summary>
        private static double TileNoiseAniso(PerlinNoise2D noise, double x, double y, double px, double py)
        {
            double fx = x / px;
            double fy = y / py;
            double a = noise.Sample(x, y);
            double b = noise.Sample(x - px, y);
            double c = noise.Sample(x, y - py);
            double d = noise.Sample(x - px, y - py);
            double top = a + (b - a) * fx;
            double bottom = c + (d - c) * fx;
            return top + (bottom - top) * fy;
        }

        private static float SegmentDistance(float px, float py, float ax, float ay, float bx, float by)
        {
            float dx = bx - ax;
            float dy = by - ay;
            float t = Mathf.Clamp01(((px - ax) * dx + (py - ay) * dy) / (dx * dx + dy * dy));
            float cx = ax + dx * t - px;
            float cy = ay + dy * t - py;
            return Mathf.Sqrt(cx * cx + cy * cy);
        }

        private static float Frac(float x) => x - Mathf.Floor(x);

        private static Color32 Px(float r, float g, float b, float a) =>
            new Color32(ToByte(r), ToByte(g), ToByte(b), ToByte(a));

        private static byte ToByte(float v) => (byte)Mathf.RoundToInt(Mathf.Clamp01(v) * 255f);
    }
}
