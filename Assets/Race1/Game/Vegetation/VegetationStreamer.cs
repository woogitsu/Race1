using System.Collections.Generic;
using Race1.Core;
using UnityEngine;

namespace Race1.Game
{
    /// <summary>
    /// Scatters trees and rocks deterministically per chunk (same seed, same chunk, same result) and draws them
    /// with GPU instancing: one draw call per 1023 instances per mesh, not one GameObject per tree. Each chunk has
    /// one tree species, chosen from its centre biome, so a chunk batches into one mesh per submesh.
    /// </summary>
    public sealed class VegetationStreamer
    {
        /// <summary>
        /// Tree species. A chunk stores its value in <see cref="RoadChunkStreamer.Chunk.TreeVariant"/>.
        /// <c>None</c> (-1) means the chunk has rocks only.
        /// </summary>
        public enum TreeSpecies
        {
            /// <summary>Tall spruce, needle cards.</summary>
            Conifer = 0,
            /// <summary>Open pine, needle clusters on a leaning trunk.</summary>
            Pine = 1,
            /// <summary>Oak, leaf cards.</summary>
            Broadleaf = 2,
            /// <summary>Birch, white trunk and small leaf cards.</summary>
            Birch = 3,
            /// <summary>No trees (desert).</summary>
            None = -1,
        }

        private const int MaxPerDraw = 1023;
        private const int CandidatesPerChunk = 40;
        private const double SparseDensity = 0.35;   // highway and town: fewer trees
        private const int BarkSub = 0;
        private const int FoliageSub = 1;

        private sealed class SpeciesSet
        {
            public Mesh[] Meshes;
            public Material Bark;
            public Material Leaves;
        }

        private readonly ulong _seed;
        private readonly SpeciesSet[] _species;   // indexed by TreeSpecies, Conifer..Birch
        private readonly Mesh[] _rocks;
        private readonly Material _rockMaterial;
        private readonly Matrix4x4[] _scratch = new Matrix4x4[MaxPerDraw];

        // Per-frame working lists for the budgeted draw. Reused, so a frame does not allocate them again.
        private readonly List<RoadChunkStreamer.Chunk> _visible = new List<RoadChunkStreamer.Chunk>();
        private double[] _distanceKey = new double[0];
        private int[] _available = new int[0];
        private int[] _allocated = new int[0];

        /// <summary>
        /// Builds two meshes per species and per rock (two seeds each, so the variety repeats less) and prepares
        /// the materials: bark gets the bark texture, needles the needle cluster, foliage the leaf card.
        /// </summary>
        public VegetationStreamer(ulong seed, Material bark, Material needles, Material foliage, Material rock)
        {
            _seed = seed;

            ConfigureBark(bark);
            ConfigureCards(needles, TreeTextures.NeedleCluster);
            ConfigureCards(foliage, TreeTextures.LeafCard);
            Material birchBark = CreateBirchBark(bark);

            _species = new[]
            {
                new SpeciesSet
                {
                    Meshes = new[] { ProceduralMeshes.Conifer(11), ProceduralMeshes.Conifer(23) },
                    Bark = bark,
                    Leaves = needles,
                },
                new SpeciesSet
                {
                    Meshes = new[] { ProceduralMeshes.Pine(71), ProceduralMeshes.Pine(89) },
                    Bark = bark,
                    Leaves = needles,
                },
                new SpeciesSet
                {
                    Meshes = new[] { ProceduralMeshes.Broadleaf(31), ProceduralMeshes.Broadleaf(47) },
                    Bark = bark,
                    Leaves = foliage,
                },
                new SpeciesSet
                {
                    Meshes = new[] { ProceduralMeshes.Birch(101), ProceduralMeshes.Birch(113) },
                    Bark = birchBark,
                    Leaves = foliage,
                },
            };

            _rocks = new[] { ProceduralMeshes.Rock(59), ProceduralMeshes.Rock(67) };
            _rockMaterial = rock;
            if (rock != null) rock.enableInstancing = true;
        }

        /// <summary>
        /// Density comes from the biome at each candidate point, so forests are dense and deserts almost bare.
        /// The species is chosen once per chunk from its centre biome, so each chunk batches cleanly.
        /// </summary>
        public void Populate(RoadChunkStreamer.Chunk chunk, RoadTrack road, TerrainField terrain, BiomeField biomes)
        {
            var rng = new DeterministicRng(DeterministicRng.Combine(_seed, (ulong)chunk.Index + 0x7E5EUL));
            const int cap = CandidatesPerChunk;
            if (chunk.Trees == null || chunk.Trees.Length < cap) chunk.Trees = new Matrix4x4[cap];
            if (chunk.Rocks == null || chunk.Rocks.Length < cap) chunk.Rocks = new Matrix4x4[cap];
            chunk.TreeCount = 0;
            chunk.RockCount = 0;

            double ds = road.SampleSpacing;
            long i0 = (long)System.Math.Round(chunk.Index * RoadMeshBuilder.ChunkLength / ds);
            int segments = (int)System.Math.Round(RoadMeshBuilder.ChunkLength / ds);
            var weights = new double[Biomes.Count];

            biomes.Weights(chunk.Origin.X + 50.0, chunk.Origin.Z, weights);
            Biomes.Blend(weights, out BiomeKind centreKind);
            TreeSpecies species = ChooseSpecies(centreKind, rng.NextDouble());
            chunk.TreeVariant = (int)species;
            double density = IsSparse(centreKind) ? SparseDensity : 1.0;

            for (int c = 0; c < CandidatesPerChunk; c++)
            {
                long index = i0 + rng.NextInt(0, segments + 1);
                RoadSample sample = road.GetSample(index);
                double side = rng.NextDouble() < 0.5 ? -1.0 : 1.0;
                double edge = sample.HalfWidth + sample.Shoulder;
                double t = side * (edge + 4.0 + rng.Range(0.0, 90.0));

                Vector3d p = sample.PointAt(t);
                biomes.Weights(p.X, p.Z, weights);
                BiomeProfile profile = Biomes.Blend(weights, out _);

                double natural = terrain.NaturalHeight(p.X, p.Z);
                double y = RoadTerrainBlend.GroundHeight(sample, t, natural);
                Vector3d local = new Vector3d(p.X - chunk.Origin.X, y - chunk.Origin.Y, p.Z - chunk.Origin.Z);
                double scale = rng.Range(0.8, 1.35);
                double yaw = rng.Range(0.0, 360.0);
                double roll = rng.NextDouble();

                if (roll < profile.Rockiness * 0.25)
                {
                    chunk.Rocks[chunk.RockCount++] = Matrix4x4.TRS(ToVec3(local), Quaternion.Euler(0f, (float)yaw, 0f),
                        Vector3.one * (float)(scale * rng.Range(0.8, 1.8)));
                }
                else if (species != TreeSpecies.None && profile.Vegetation > 0.02 &&
                         roll < profile.Rockiness * 0.25 + profile.Vegetation * 0.7 * density)
                {
                    chunk.Trees[chunk.TreeCount++] = Matrix4x4.TRS(ToVec3(local), Quaternion.Euler(0f, (float)yaw, 0f),
                        Vector3.one * (float)scale);
                }
            }
        }

        /// <summary>Draws the chunks in view with no instance limit.</summary>
        public void Draw(IEnumerable<RoadChunkStreamer.Chunk> chunks, Vector3d origin)
        {
            Draw(chunks, origin, int.MaxValue);
        }

        /// <summary>
        /// Draws the chunks in view, with at most <paramref name="maxInstances"/> tree and rock instances in total. The
        /// nearest chunks are served first, so the budget cuts the far ones. Matrices are chunk-relative; the chunk offset
        /// is applied here.
        /// </summary>
        public void Draw(IEnumerable<RoadChunkStreamer.Chunk> chunks, Vector3d origin, int maxInstances)
        {
            _visible.Clear();
            foreach (RoadChunkStreamer.Chunk chunk in chunks)
            {
                if (chunk.Go.activeInHierarchy) _visible.Add(chunk);
            }

            int n = _visible.Count;
            if (_distanceKey.Length < n)
            {
                _distanceKey = new double[n];
                _available = new int[n];
                _allocated = new int[n];
            }
            for (int i = 0; i < n; i++)
            {
                Vector3d d = _visible[i].Origin - origin;
                _distanceKey[i] = d.X * d.X + d.Y * d.Y + d.Z * d.Z;
            }
            SortNearestFirst(n);
            for (int i = 0; i < n; i++)
            {
                _available[i] = _visible[i].TreeCount + _visible[i].RockCount;
            }

            DrawBudget.Allocate(_available, n, maxInstances, _allocated);
            for (int i = 0; i < n; i++)
            {
                DrawChunk(_visible[i], origin, _allocated[i]);
            }
            _visible.Clear();
        }

        /// <summary>Insertion sort of the visible chunks by squared distance. The lists are short, so this is cheap.</summary>
        private void SortNearestFirst(int n)
        {
            for (int i = 1; i < n; i++)
            {
                double key = _distanceKey[i];
                RoadChunkStreamer.Chunk chunk = _visible[i];
                int j = i - 1;
                while (j >= 0 && _distanceKey[j] > key)
                {
                    _distanceKey[j + 1] = _distanceKey[j];
                    _visible[j + 1] = _visible[j];
                    j--;
                }
                _distanceKey[j + 1] = key;
                _visible[j + 1] = chunk;
            }
        }

        /// <summary>Draws up to <paramref name="allowed"/> instances of one chunk: trees first, then rocks.</summary>
        private void DrawChunk(RoadChunkStreamer.Chunk chunk, Vector3d origin, int allowed)
        {
            if (allowed <= 0) return;
            Vector3 offset = new Vector3((float)(chunk.Origin.X - origin.X), (float)(chunk.Origin.Y - origin.Y),
                (float)(chunk.Origin.Z - origin.Z));
            Matrix4x4 root = Matrix4x4.Translate(offset);
            int pick = MeshPick(chunk.Index);

            int trees = System.Math.Min(allowed, chunk.TreeCount);
            if (trees > 0 && chunk.TreeVariant >= 0 && chunk.TreeVariant < _species.Length)
            {
                SpeciesSet set = _species[chunk.TreeVariant];
                Mesh treeMesh = set.Meshes[pick];
                DrawBatch(treeMesh, set.Bark, root, chunk.Trees, trees, BarkSub);
                DrawBatch(treeMesh, set.Leaves, root, chunk.Trees, trees, FoliageSub);
            }
            int rocks = System.Math.Min(allowed - trees, chunk.RockCount);
            DrawBatch(_rocks[pick], _rockMaterial, root, chunk.Rocks, rocks, BarkSub);
        }

        /// <summary>Picks the species for a chunk from its centre biome and a per-chunk roll.</summary>
        private static TreeSpecies ChooseSpecies(BiomeKind kind, double roll)
        {
            switch (kind)
            {
                case BiomeKind.Forest:
                    return roll < 0.6 ? TreeSpecies.Conifer : TreeSpecies.Pine;
                case BiomeKind.Alpine:
                    return roll < 0.75 ? TreeSpecies.Conifer : TreeSpecies.Pine;
                case BiomeKind.Meadow:
                    return roll < 0.6 ? TreeSpecies.Broadleaf : TreeSpecies.Birch;
                case BiomeKind.Coast:
                    return roll < 0.5 ? TreeSpecies.Broadleaf : TreeSpecies.Birch;
                case BiomeKind.Highway:
                case BiomeKind.Town:
                    return TreeSpecies.Broadleaf;
                default:
                    return TreeSpecies.None;
            }
        }

        private static bool IsSparse(BiomeKind kind) => kind == BiomeKind.Highway || kind == BiomeKind.Town;

        /// <summary>Which of the two meshes per species a chunk uses. A hash of the chunk index, so it is stable.</summary>
        private int MeshPick(long index) => (int)(DeterministicRng.Finalize((ulong)index ^ _seed) & 1UL);

        private void DrawBatch(Mesh mesh, Material material, Matrix4x4 root, Matrix4x4[] source, int count, int submesh)
        {
            if (count <= 0 || mesh == null || material == null) return;
            for (int start = 0; start < count; start += MaxPerDraw)
            {
                int n = System.Math.Min(MaxPerDraw, count - start);
                for (int i = 0; i < n; i++)
                {
                    _scratch[i] = root * source[start + i];
                }
                Graphics.DrawMeshInstanced(mesh, submesh, material, _scratch, n, null,
                    UnityEngine.Rendering.ShadowCastingMode.On, true);
            }
        }

        private static void ConfigureBark(Material bark)
        {
            if (bark == null) return;
            bark.enableInstancing = true;
            SetMainTexture(bark, TreeTextures.Bark);
        }

        /// <summary>Alpha-tested, double-sided card material. The texture's alpha is the leaf or needle mask.</summary>
        private static void ConfigureCards(Material m, Texture2D alpha)
        {
            if (m == null) return;
            m.enableInstancing = true;
            SetMainTexture(m, alpha);
            m.EnableKeyword("_ALPHATEST_ON");
            if (m.HasProperty("_Cutoff")) m.SetFloat("_Cutoff", 0.4f);
            if (m.HasProperty("_AlphaClip")) m.SetFloat("_AlphaClip", 1f);
            if (m.HasProperty("_Mode")) m.SetFloat("_Mode", 1f);
            if (m.HasProperty("_Cull")) m.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
        }

        /// <summary>A copy of the bark material with a near-white colour, so the birch trunk reads as white.</summary>
        private static Material CreateBirchBark(Material bark)
        {
            if (bark == null) return null;
            var m = new Material(bark) { name = "Race1 birch bark", enableInstancing = true };
            RenderMaterials.SetBaseColor(m, new Color(1f, 0.98f, 0.94f));
            return m;
        }

        private static void SetMainTexture(Material m, Texture texture)
        {
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", texture);
            if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", texture);
        }

        private static Vector3 ToVec3(Vector3d v) => new Vector3((float)v.X, (float)v.Y, (float)v.Z);
    }
}
