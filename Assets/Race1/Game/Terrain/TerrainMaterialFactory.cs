using Race1.Core;
using UnityEngine;

namespace Race1.Game
{
    /// <summary>
    /// The ground materials used by the terrain strips beside the road. Each albedo is greyscale
    /// and near white, so a biome tint (see <see cref="TerrainMaterialFactory.ApplyBiomeTint"/>) gives the colour.
    /// </summary>
    public sealed class TerrainMaterialSet
    {
        /// <summary>Meadow, highway and town ground.</summary>
        public Material Grass;

        /// <summary>Forest floor with needle and leaf litter.</summary>
        public Material Forest;

        /// <summary>Striated cliff rock, used by alpine terrain.</summary>
        public Material Rock;

        /// <summary>Rippled sand, used by coast and desert terrain.</summary>
        public Material Sand;

        /// <summary>Soft snow cover.</summary>
        public Material Snow;

        /// <summary>Returns the ground material for a biome. Unknown biomes use <see cref="Grass"/>.</summary>
        public Material ForBiome(BiomeKind kind)
        {
            switch (kind)
            {
                case BiomeKind.Forest:
                    return Forest;
                case BiomeKind.Alpine:
                    return Rock;
                case BiomeKind.Coast:
                case BiomeKind.Desert:
                    return Sand;
                case BiomeKind.Meadow:
                case BiomeKind.Highway:
                case BiomeKind.Town:
                default:
                    return Grass;
            }
        }
    }

    /// <summary>
    /// Builds the terrain ground materials at runtime from a world seed. No image files are used.
    /// </summary>
    public static class TerrainMaterialFactory
    {
        private const int GrassSize = 512;
        private const int ForestSize = 256;
        private const int RockSize = 512;
        private const int SandSize = 256;
        private const int SnowSize = 256;

        // Low-frequency fields are computed at this size and upsampled, which saves most of the noise cost.
        private const int LowResSize = 128;

        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BumpMapId = Shader.PropertyToID("_BumpMap");
        private static readonly int BumpScaleId = Shader.PropertyToID("_BumpScale");
        private static readonly int MetallicId = Shader.PropertyToID("_Metallic");
        private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
        private static readonly int GlossinessId = Shader.PropertyToID("_Glossiness");

        /// <summary>
        /// Generates all ground textures and materials for a seed. Call once per world; the result is reusable.
        /// </summary>
        public static TerrainMaterialSet Create(ulong seed)
        {
            Shader shader = FindLitShader();
            return new TerrainMaterialSet
            {
                Grass = BuildGrass(seed, shader),
                Forest = BuildForest(seed, shader),
                Rock = BuildRock(seed, shader),
                Sand = BuildSand(seed, shader),
                Snow = BuildSnow(seed, shader),
            };
        }

        /// <summary>
        /// Sets the biome tint on a terrain material through _BaseColor (and _Color on the built-in shader).
        /// The tint multiplies the greyscale base texture. This changes the material it is given, so pass a
        /// per-chunk instance if chunks need different tints.
        /// </summary>
        public static void ApplyBiomeTint(Material material, Color tint)
        {
            if (material == null)
            {
                return;
            }

            TrySetColor(material, BaseColorId, tint);
            TrySetColor(material, ColorId, tint);
        }

        private static Material BuildGrass(ulong seed, Shader shader)
        {
            const int n = GrassSize;

            // Blade streaks: 64-cell noise, blurred twice along x only, so the streaks run horizontally
            // (roughly 4x longer in x than in y).
            float[] streak = Normalize(BlurX(BlurX(TextureUtil.TileableFbm(Derive(seed, 1), n, 64, 1), n, 6), n, 6));

            // Low-frequency dirt patches.
            float[] dirt = LowRes(Derive(seed, 2), n, 4, 2);

            var albedo = new float[n * n];
            for (int i = 0; i < albedo.Length; i++)
            {
                float patch = Smooth(0.55f, 0.8f, dirt[i]);
                float blade = 0.72f + 0.26f * streak[i];
                albedo[i] = Mathf.Lerp(blade, 0.6f + 0.12f * streak[i], patch);
            }

            return CreateMaterial(
                shader,
                "TerrainGrass",
                TextureUtil.Grey(albedo, n, "TerrainGrass_Albedo", false),
                TextureUtil.NormalMap(streak, n, 1.5f, "TerrainGrass_Normal"),
                0.2f);
        }

        private static Material BuildForest(ulong seed, Shader shader)
        {
            const int n = ForestSize;

            float[] patch = LowRes(Derive(seed, 10), n, 4, 2);
            float[] litter = Normalize(TextureUtil.TileableFbm(Derive(seed, 11), n, 64, 2));
            float[] speck = Normalize(TextureUtil.TileableFbm(Derive(seed, 12), n, 128, 1));

            var albedo = new float[n * n];
            var height = new float[n * n];
            for (int i = 0; i < albedo.Length; i++)
            {
                float dark = Smooth(0.6f, 0.85f, speck[i]);
                float body = Mathf.Lerp(0.6f, 0.9f, 0.6f * patch[i] + 0.4f * litter[i]);
                albedo[i] = Mathf.Lerp(body, 0.6f, 0.6f * dark);
                height[i] = 0.6f * litter[i] + 0.4f * speck[i];
            }

            return CreateMaterial(
                shader,
                "TerrainForest",
                TextureUtil.Grey(albedo, n, "TerrainForest_Albedo", false),
                TextureUtil.NormalMap(Normalize(height), n, 2.0f, "TerrainForest_Normal"),
                0.15f);
        }

        private static Material BuildRock(ulong seed, Shader shader)
        {
            const int n = RockSize;

            float[] warp = LowRes(Derive(seed, 20), n, 4, 2);
            float[] ridgeSource = LowRes(Derive(seed, 21), n, 8, 2);
            float[] crackSource = Normalize(TextureUtil.TileableFbm(Derive(seed, 22), n, 48, 1));

            // 16 stripes per tile (period 32 px), so the pattern wraps seamlessly at the tile edge.
            float k = 2f * Mathf.PI * 16f / n;

            var albedo = new float[n * n];
            var height = new float[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    int i = y * n + x;
                    float strata = 0.5f + 0.5f * Mathf.Sin(k * (y + 8f * warp[i]));
                    float ridged = 1f - Mathf.Abs(2f * ridgeSource[i] - 1f);
                    // Thresholded band around the mid-level of the noise gives thin, soft-edged cracks.
                    float crack = 1f - Smooth(0f, 0.08f, Mathf.Abs(crackSource[i] - 0.5f));

                    float v = 0.6f + 0.2f * strata + 0.2f * ridged;
                    albedo[i] = Mathf.Lerp(v, 0.6f, 0.5f * crack);
                    height[i] = 0.55f * strata + 0.45f * ridged - 0.4f * crack;
                }
            }

            return CreateMaterial(
                shader,
                "TerrainRock",
                TextureUtil.Grey(albedo, n, "TerrainRock_Albedo", false),
                TextureUtil.NormalMap(Normalize(height), n, 3.0f, "TerrainRock_Normal"),
                0.3f);
        }

        private static Material BuildSand(ulong seed, Shader shader)
        {
            const int n = SandSize;

            float[] warp = LowRes(Derive(seed, 30), n, 4, 2);
            float[] grain = Normalize(TextureUtil.TileableFbm(Derive(seed, 31), n, 128, 1));

            var albedo = new float[n * n];
            var height = new float[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    int i = y * n + x;

                    // Integer wave numbers (10 and 3 per tile) keep the ripples seamless across the tile.
                    float phase = 2f * Mathf.PI * (10f * x / n + 3f * y / n) + 2.5f * warp[i];
                    float ripple = 0.5f + 0.5f * Mathf.Sin(phase);

                    albedo[i] = Mathf.Lerp(0.6f, 1f, 0.6f * ripple + 0.4f * grain[i]);
                    height[i] = 0.7f * ripple + 0.3f * grain[i];
                }
            }

            return CreateMaterial(
                shader,
                "TerrainSand",
                TextureUtil.Grey(albedo, n, "TerrainSand_Albedo", false),
                TextureUtil.NormalMap(Normalize(height), n, 1.2f, "TerrainSand_Normal"),
                0.35f);
        }

        private static Material BuildSnow(ulong seed, Shader shader)
        {
            const int n = SnowSize;

            float[] drift = LowRes(Derive(seed, 40), n, 4, 2);
            float[] crust = Normalize(TextureUtil.TileableFbm(Derive(seed, 41), n, 32, 2));

            var albedo = new float[n * n];
            var height = new float[n * n];
            for (int i = 0; i < albedo.Length; i++)
            {
                float m = 0.5f * drift[i] + 0.5f * crust[i];
                albedo[i] = Mathf.Lerp(0.9f, 1f, m);
                height[i] = m;
            }

            return CreateMaterial(
                shader,
                "TerrainSnow",
                TextureUtil.Grey(albedo, n, "TerrainSnow_Albedo", false),
                TextureUtil.NormalMap(Normalize(height), n, 0.4f, "TerrainSnow_Normal"),
                0.55f);
        }

        private static Material CreateMaterial(Shader shader, string name, Texture2D albedo, Texture2D normal, float smoothness)
        {
            var material = new Material(shader);
            material.name = name;

            TrySetTexture(material, BaseMapId, albedo);
            TrySetTexture(material, MainTexId, albedo);
            TrySetColor(material, BaseColorId, Color.white);
            TrySetColor(material, ColorId, Color.white);

            if (material.HasProperty(BumpMapId))
            {
                material.SetTexture(BumpMapId, normal);
                material.EnableKeyword("_NORMALMAP");
            }

            TrySetFloat(material, BumpScaleId, 1f);
            TrySetFloat(material, MetallicId, 0f);
            TrySetFloat(material, SmoothnessId, smoothness);
            TrySetFloat(material, GlossinessId, smoothness);
            return material;
        }

        private static Shader FindLitShader()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            if (shader == null)
            {
                throw new System.InvalidOperationException("No Lit shader is available for the terrain materials.");
            }

            return shader;
        }

        private static void TrySetTexture(Material material, int id, Texture value)
        {
            if (material.HasProperty(id))
            {
                material.SetTexture(id, value);
            }
        }

        private static void TrySetColor(Material material, int id, Color value)
        {
            if (material.HasProperty(id))
            {
                material.SetColor(id, value);
            }
        }

        private static void TrySetFloat(Material material, int id, float value)
        {
            if (material.HasProperty(id))
            {
                material.SetFloat(id, value);
            }
        }

        /// <summary>Derives an independent seed for one noise layer from the world seed.</summary>
        private static ulong Derive(ulong seed, ulong salt)
        {
            unchecked
            {
                ulong z = seed + salt * 0x9E3779B97F4A7C15UL;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }

        /// <summary>Low-frequency noise computed at <see cref="LowResSize"/>, upsampled to <paramref name="size"/>, normalised to 0..1.</summary>
        private static float[] LowRes(ulong seed, int size, int cells, int octaves)
        {
            float[] small = TextureUtil.TileableFbm(seed, LowResSize, cells, octaves);
            return Normalize(Upsample(small, LowResSize, size));
        }

        /// <summary>Bilinear upsample with wrap-around, so a periodic field stays periodic.</summary>
        private static float[] Upsample(float[] src, int srcSize, int dstSize)
        {
            var dst = new float[dstSize * dstSize];
            float scale = srcSize / (float)dstSize;
            for (int y = 0; y < dstSize; y++)
            {
                float sy = (y + 0.5f) * scale - 0.5f;
                int y0 = Mathf.FloorToInt(sy);
                float fy = sy - y0;
                int row0 = Wrap(y0, srcSize) * srcSize;
                int row1 = Wrap(y0 + 1, srcSize) * srcSize;
                for (int x = 0; x < dstSize; x++)
                {
                    float sx = (x + 0.5f) * scale - 0.5f;
                    int x0 = Mathf.FloorToInt(sx);
                    float fx = sx - x0;
                    int c0 = Wrap(x0, srcSize);
                    int c1 = Wrap(x0 + 1, srcSize);
                    float top = Mathf.Lerp(src[row0 + c0], src[row0 + c1], fx);
                    float bottom = Mathf.Lerp(src[row1 + c0], src[row1 + c1], fx);
                    dst[y * dstSize + x] = Mathf.Lerp(top, bottom, fy);
                }
            }

            return dst;
        }

        /// <summary>Periodic horizontal box blur. Stretches blobs into horizontal streaks and keeps the tile seamless.</summary>
        private static float[] BlurX(float[] src, int size, int radius)
        {
            var dst = new float[src.Length];
            float inv = 1f / (2 * radius + 1);
            for (int y = 0; y < size; y++)
            {
                int row = y * size;
                float sum = 0f;
                for (int k = -radius; k <= radius; k++)
                {
                    sum += src[row + Wrap(k, size)];
                }

                for (int x = 0; x < size; x++)
                {
                    dst[row + x] = sum * inv;
                    sum += src[row + Wrap(x + radius + 1, size)] - src[row + Wrap(x - radius, size)];
                }
            }

            return dst;
        }

        /// <summary>Rescales values to 0..1. A constant field becomes 0.5.</summary>
        private static float[] Normalize(float[] values)
        {
            float min = float.MaxValue;
            float max = float.MinValue;
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] < min)
                {
                    min = values[i];
                }

                if (values[i] > max)
                {
                    max = values[i];
                }
            }

            float range = max - min;
            var result = new float[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                result[i] = range > 1e-6f ? (values[i] - min) / range : 0.5f;
            }

            return result;
        }

        private static float Smooth(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        private static int Wrap(int index, int size)
        {
            int m = index % size;
            return m < 0 ? m + size : m;
        }
    }
}
