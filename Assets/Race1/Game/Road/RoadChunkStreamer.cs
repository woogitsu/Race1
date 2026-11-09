using System.Collections.Generic;
using Race1.Core;
using UnityEngine;

namespace Race1.Game
{
    /// <summary>
    /// Keeps the road chunks around the car loaded. Chunks are built a few per frame (time budget), recycled
    /// through a pool, and repositioned when the floating origin moves. Each chunk carries its road and terrain
    /// materials, a guardrail mesh, and instanced posts and lamps. Nothing waits on generation: the road is
    /// prefetched well ahead of the car.
    /// </summary>
    public sealed class RoadChunkStreamer
    {
        public sealed class Chunk
        {
            public long Index;
            public Vector3d Origin;
            public GameObject Go;
            public MeshFilter Filter;
            public MeshRenderer Renderer;
            public Material TerrainMaterial;
            public GameObject Rail;
            public MeshFilter RailFilter;
            public MeshRenderer RailRenderer;
            public Matrix4x4[] Trees;
            public int TreeCount;
            public Matrix4x4[] Rocks;
            public int RockCount;
            public int TreeVariant;
            public Matrix4x4[] Posts;
            public int PostCount;
            public Matrix4x4[] Lamps;
            public int LampCount;
            public GameObject Water;
            public MeshFilter WaterFilter;
            public MeshRenderer WaterRenderer;
            public Matrix4x4[] Pillars;
            public int PillarCount;
            public GameObject Lining;
            public MeshFilter LiningFilter;
            public MeshRenderer LiningRenderer;
        }

        private const int MaxPerDraw = 1023;

        /// <summary>Lamp keys are chunk index times this stride plus the slot, so keys stay unique for any chunk.</summary>
        public const long LampKeyStride = 4096;

        private readonly RoadTrack _road;
        private readonly TerrainField _terrain;
        private readonly BiomeField _biomes;
        private readonly Transform _parent;
        private readonly RoadMaterialSet _roads;
        private readonly TerrainMaterialSet _terrainMats;
        private readonly Material _railMaterial;
        private readonly Material _postMaterial;
        private readonly Material _lampMaterial;
        private readonly Mesh _postMesh;
        private readonly Mesh _lampMesh;
        private readonly VegetationStreamer _vegetation;
        private readonly ulong _seed;
        private readonly Dictionary<long, Chunk> _active = new Dictionary<long, Chunk>();
        private readonly Stack<Chunk> _pool = new Stack<Chunk>();
        private readonly Matrix4x4[] _scratch = new Matrix4x4[MaxPerDraw];

        public RoadChunkStreamer(RoadTrack road, TerrainField terrain, BiomeField biomes, Transform parent,
            RoadMaterialSet roads, TerrainMaterialSet terrainMats, Material railMaterial,
            Material postMaterial, Material lampMaterial, VegetationStreamer vegetation, ulong seed)
        {
            _road = road;
            _terrain = terrain;
            _biomes = biomes;
            _parent = parent;
            _roads = roads;
            _terrainMats = terrainMats;
            _railMaterial = railMaterial;
            _postMaterial = postMaterial;
            _lampMaterial = lampMaterial;
            _vegetation = vegetation;
            _seed = seed;
            _postMesh = CubeMesh();
            _lampMesh = CubeMesh();
        }

        public IEnumerable<Chunk> Active => _active.Values;
        public int ActiveCount => _active.Count;

        /// <summary>Chunks ahead of and behind the car. Quality tiers change the draw distance.</summary>
        public int ChunksAhead = 12;
        public int ChunksBehind = 2;
        public int BuildsPerFrame = 2;
        public double BuildBudgetMs = 6.0;

        /// <summary>Material for the coast sea surface. When null, no water is built.</summary>
        public Material WaterMaterial { get; set; }

        /// <summary>Material for tunnel linings. When null, tunnels are not lined.</summary>
        public Material TunnelMaterial { get; set; }

        public void Update(double carS, Vector3d origin)
        {
            long centre = RoadMeshBuilder.ChunkIndexOf(carS);
            long from = centre - ChunksBehind;
            long to = centre + ChunksAhead;

            // Build nearest chunks first, within the per-frame budget.
            int built = 0;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            for (long k = centre; k <= to && built < BuildsPerFrame; k++)
            {
                if (!_active.ContainsKey(k) && sw.Elapsed.TotalMilliseconds < BuildBudgetMs)
                {
                    Build(k, origin);
                    built++;
                }
            }
            for (long k = centre - 1; k >= from && built < BuildsPerFrame; k--)
            {
                if (!_active.ContainsKey(k) && sw.Elapsed.TotalMilliseconds < BuildBudgetMs)
                {
                    Build(k, origin);
                    built++;
                }
            }

            // Recycle chunks that fell out of range.
            var remove = new List<long>();
            foreach (KeyValuePair<long, Chunk> pair in _active)
            {
                if (pair.Key < from - 1 || pair.Key > to + 1) remove.Add(pair.Key);
            }
            foreach (long key in remove)
            {
                Recycle(_active[key]);
                _active.Remove(key);
            }

            _road.Prefetch(carS, (ChunksAhead + 1) * RoadMeshBuilder.ChunkLength);
            _road.TrimBefore(carS - (ChunksBehind + 1) * RoadMeshBuilder.ChunkLength);
        }

        /// <summary>Moves every chunk to the new floating origin.</summary>
        public void Rebase(Vector3d origin)
        {
            foreach (Chunk c in _active.Values)
            {
                c.Go.transform.localPosition = ToFloat(c.Origin - origin);
            }
        }

        /// <summary>Draws the instanced roadside posts and lamps of every active chunk.</summary>
        public void DrawRoadside(Vector3d origin)
        {
            foreach (Chunk chunk in _active.Values)
            {
                if (!chunk.Go.activeInHierarchy) continue;
                Vector3 offset = ToFloat(chunk.Origin - origin);
                Matrix4x4 root = Matrix4x4.Translate(offset);
                DrawBatch(_postMesh, _postMaterial, root, chunk.Posts, chunk.PostCount);
                DrawBatch(_postMesh, _postMaterial, root, chunk.Pillars, chunk.PillarCount);
                DrawBatch(_lampMesh, _lampMaterial, root, chunk.Lamps, chunk.LampCount);
            }
        }

        public void Clear()
        {
            foreach (Chunk c in _active.Values) Recycle(c);
            _active.Clear();
        }

        /// <summary>
        /// Collects the world positions of every street lamp in the active chunks, relative to the floating origin,
        /// and a stable key per lamp (chunk index and slot) that survives between frames. The buffers are grown
        /// when needed and reused otherwise. Returns the number of valid entries.
        /// </summary>
        public int CollectLamps(Vector3d origin, ref Vector3d[] positions, ref long[] keys)
        {
            int total = 0;
            foreach (Chunk chunk in _active.Values) total += chunk.LampCount;
            if (positions == null || positions.Length < total)
            {
                positions = new Vector3d[total * 2];
                keys = new long[total * 2];
            }

            int n = 0;
            foreach (Chunk chunk in _active.Values)
            {
                if (!chunk.Go.activeInHierarchy) continue;
                Vector3d chunkOffset = chunk.Origin - origin;
                for (int j = 0; j < chunk.LampCount; j++)
                {
                    Vector4 column = chunk.Lamps[j].GetColumn(3);
                    positions[n] = chunkOffset + new Vector3d(column.x, column.y, column.z);
                    keys[n] = chunk.Index * LampKeyStride + j;
                    n++;
                }
            }
            return n;
        }

        private void Build(long k, Vector3d origin)
        {
            RoadMeshBuilder.ChunkData data = RoadMeshBuilder.Build(_road, _terrain, _biomes, k);
            Chunk chunk = _pool.Count > 0 ? _pool.Pop() : CreateChunk();
            chunk.Index = k;
            chunk.Origin = data.Origin;
            chunk.Go.name = $"Chunk_{k}";
            chunk.Go.SetActive(true);
            chunk.Go.transform.localPosition = ToFloat(data.Origin - origin);

            if (chunk.Filter.sharedMesh != null) Object.Destroy(chunk.Filter.sharedMesh);
            chunk.Filter.sharedMesh = data.Mesh;

            // Terrain: a per-chunk instance of the biome's ground material, tinted by the biome colour.
            if (chunk.TerrainMaterial != null) Object.Destroy(chunk.TerrainMaterial);
            chunk.TerrainMaterial = new Material(_terrainMats.ForBiome(data.Biome)) { name = "TerrainChunk" };
            TerrainMaterialFactory.ApplyBiomeTint(chunk.TerrainMaterial, data.TerrainTint);
            chunk.Renderer.sharedMaterials = new[] { _roads.Asphalt, chunk.TerrainMaterial, _roads.Marking };

            // Roadside: guardrail mesh (already relative to the chunk origin), posts and lamps.
            RoadsideChunk side = RoadsideProps.Build(_road, k, data.Origin, _seed);
            if (side.Guardrail != null)
            {
                if (chunk.Rail == null) CreateRail(chunk);
                if (chunk.RailFilter.sharedMesh != null) Object.Destroy(chunk.RailFilter.sharedMesh);
                chunk.RailFilter.sharedMesh = side.Guardrail;
                chunk.RailRenderer.sharedMaterial = _railMaterial;
                chunk.Rail.SetActive(true);
            }
            else if (chunk.Rail != null)
            {
                chunk.Rail.SetActive(false);
            }
            // Coast water: a sea surface where the coast biome dominates and the ground lies below sea level.
            Mesh water = WaterMaterial != null ? CoastWaterBuilder.Build(_road, _terrain, _biomes, k, data.Origin) : null;
            if (water != null)
            {
                if (chunk.Water == null) CreateWater(chunk);
                if (chunk.WaterFilter.sharedMesh != null) Object.Destroy(chunk.WaterFilter.sharedMesh);
                chunk.WaterFilter.sharedMesh = water;
                chunk.WaterRenderer.sharedMaterial = WaterMaterial;
                chunk.Water.SetActive(true);
            }
            else if (chunk.Water != null)
            {
                chunk.Water.SetActive(false);
            }

            // Bridge pillars under the deck, and the lining of any tunnel in this chunk.
            chunk.PillarCount = StructureProps.BuildPillars(_road, _terrain, k, data.Origin, ref chunk.Pillars);
            Mesh lining = TunnelMaterial != null ? StructureProps.BuildTunnelLining(_road, k, data.Origin) : null;
            if (lining != null)
            {
                if (chunk.Lining == null) CreateLining(chunk);
                if (chunk.LiningFilter.sharedMesh != null) Object.Destroy(chunk.LiningFilter.sharedMesh);
                chunk.LiningFilter.sharedMesh = lining;
                chunk.LiningRenderer.sharedMaterial = TunnelMaterial;
                chunk.Lining.SetActive(true);
            }
            else if (chunk.Lining != null)
            {
                chunk.Lining.SetActive(false);
            }

            chunk.Posts = side.Posts;
            chunk.PostCount = side.PostCount;
            if (chunk.Lamps == null || chunk.Lamps.Length < side.LampCount) chunk.Lamps = new Matrix4x4[side.LampCount];
            for (int i = 0; i < side.LampCount; i++)
            {
                chunk.Lamps[i] = Matrix4x4.TRS(side.Lamps[i], Quaternion.identity, new Vector3(0.35f, 0.2f, 0.35f));
            }
            chunk.LampCount = side.LampCount;

            _vegetation.Populate(chunk, _road, _terrain, _biomes);
            _active[k] = chunk;
        }

        private Chunk CreateChunk()
        {
            var go = new GameObject("Chunk");
            go.transform.SetParent(_parent, false);
            var chunk = new Chunk
            {
                Go = go,
                Filter = go.AddComponent<MeshFilter>(),
                Renderer = go.AddComponent<MeshRenderer>(),
            };
            chunk.Renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            chunk.Renderer.receiveShadows = true;
            return chunk;
        }

        private static void CreateRail(Chunk chunk)
        {
            var rail = new GameObject("Guardrail");
            rail.transform.SetParent(chunk.Go.transform, false);
            chunk.Rail = rail;
            chunk.RailFilter = rail.AddComponent<MeshFilter>();
            chunk.RailRenderer = rail.AddComponent<MeshRenderer>();
            chunk.RailRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        }

        private static void CreateLining(Chunk chunk)
        {
            var lining = new GameObject("TunnelLining");
            lining.transform.SetParent(chunk.Go.transform, false);
            chunk.Lining = lining;
            chunk.LiningFilter = lining.AddComponent<MeshFilter>();
            chunk.LiningRenderer = lining.AddComponent<MeshRenderer>();
            chunk.LiningRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            chunk.LiningRenderer.receiveShadows = true;
        }

        private static void CreateWater(Chunk chunk)
        {
            var water = new GameObject("CoastWater");
            water.transform.SetParent(chunk.Go.transform, false);
            chunk.Water = water;
            chunk.WaterFilter = water.AddComponent<MeshFilter>();
            chunk.WaterRenderer = water.AddComponent<MeshRenderer>();
            chunk.WaterRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            chunk.WaterRenderer.receiveShadows = false;
        }

        private void Recycle(Chunk c)
        {
            c.Go.SetActive(false);
            c.TreeCount = 0;
            c.RockCount = 0;
            c.PostCount = 0;
            c.PillarCount = 0;
            c.LampCount = 0;
            _pool.Push(c);
        }

        private void DrawBatch(Mesh mesh, Material material, Matrix4x4 root, Matrix4x4[] source, int count)
        {
            if (count <= 0 || mesh == null || material == null || source == null) return;
            for (int start = 0; start < count; start += MaxPerDraw)
            {
                int n = System.Math.Min(MaxPerDraw, count - start);
                for (int i = 0; i < n; i++)
                {
                    _scratch[i] = root * source[start + i];
                }
                Graphics.DrawMeshInstanced(mesh, 0, material, _scratch, n, null,
                    UnityEngine.Rendering.ShadowCastingMode.On, true);
            }
        }

        private static Mesh CubeMesh()
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Mesh mesh = go.GetComponent<MeshFilter>().sharedMesh;
            Object.Destroy(go);
            return mesh;
        }

        private static Vector3 ToFloat(Vector3d v) => new Vector3((float)v.X, (float)v.Y, (float)v.Z);
    }
}
