using System.Collections.Generic;
using Race1.Core;
using UnityEngine;

namespace Race1.Game
{
    /// <summary>
    /// Keeps the road chunks around the car loaded. Chunks are built a few per frame (time budget), recycled
    /// through a pool, and repositioned when the floating origin moves. Nothing waits on generation: the
    /// road is prefetched well ahead of the car.
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
            public Matrix4x4[] Trees;
            public int TreeCount;
            public Matrix4x4[] Rocks;
            public int RockCount;
            public int TreeVariant;
        }

        private readonly RoadTrack _road;
        private readonly TerrainField _terrain;
        private readonly BiomeField _biomes;
        private readonly Transform _parent;
        private readonly Material _roadMaterial;
        private readonly Material _markingMaterial;
        private readonly VegetationStreamer _vegetation;
        private readonly Dictionary<long, Chunk> _active = new Dictionary<long, Chunk>();
        private readonly Stack<Chunk> _pool = new Stack<Chunk>();

        public RoadChunkStreamer(RoadTrack road, TerrainField terrain, BiomeField biomes, Transform parent,
            Material roadMaterial, Material markingMaterial, VegetationStreamer vegetation)
        {
            _road = road;
            _terrain = terrain;
            _biomes = biomes;
            _parent = parent;
            _roadMaterial = roadMaterial;
            _markingMaterial = markingMaterial;
            _vegetation = vegetation;
        }

        public IEnumerable<Chunk> Active => _active.Values;
        public int ActiveCount => _active.Count;

        /// <summary>Chunks ahead of and behind the car. Quality tiers change the draw distance.</summary>
        public int ChunksAhead = 12;
        public int ChunksBehind = 2;
        public int BuildsPerFrame = 2;   // 1 on battery-saver tiers, see PerformanceDirector
        public double BuildBudgetMs = 6.0;

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

        public void Clear()
        {
            foreach (Chunk c in _active.Values) Recycle(c);
            _active.Clear();
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

            if (chunk.TerrainMaterial == null)
            {
                chunk.TerrainMaterial = RenderMaterials.Create(data.TerrainTint, 0f, 0.15f, null);
            }
            else
            {
                RenderMaterials.SetBaseColor(chunk.TerrainMaterial, data.TerrainTint);
            }
            chunk.Renderer.sharedMaterials = new[] { _roadMaterial, chunk.TerrainMaterial, _markingMaterial };

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

        private void Recycle(Chunk c)
        {
            c.Go.SetActive(false);
            c.TreeCount = 0;
            c.RockCount = 0;
            _pool.Push(c);
        }

        private static Vector3 ToFloat(Vector3d v) => new Vector3((float)v.X, (float)v.Y, (float)v.Z);
    }
}
