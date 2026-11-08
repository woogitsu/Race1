using System.Collections.Generic;
using Race1.Core;
using UnityEngine;

namespace Race1.Game
{
    /// <summary>
    /// Scatters trees and rocks deterministically per chunk (same seed, same chunk, same result) and draws them
    /// with GPU instancing: one draw call per 1023 instances per mesh, not one GameObject per tree.
    /// </summary>
    public sealed class VegetationStreamer
    {
        private const int MaxPerDraw = 1023;
        private const int CandidatesPerChunk = 40;

        private readonly ulong _seed;
        private readonly Mesh[] _conifer;
        private readonly Mesh[] _broadleaf;
        private readonly Mesh _rock;
        private readonly Material _barkMaterial;
        private readonly Material[] _leafMaterials;
        private readonly Material _rockMaterial;
        private readonly Matrix4x4[] _scratch = new Matrix4x4[MaxPerDraw];

        public VegetationStreamer(ulong seed, Material bark, Material needles, Material foliage, Material rock)
        {
            _seed = seed;
            _conifer = new[] { ProceduralMeshes.Conifer(11), ProceduralMeshes.Conifer(23) };
            _broadleaf = new[] { ProceduralMeshes.Broadleaf(31), ProceduralMeshes.Broadleaf(47) };
            _rock = ProceduralMeshes.Rock(59);
            _barkMaterial = bark;
            _leafMaterials = new[] { needles, foliage };
            _rockMaterial = rock;
        }

        /// <summary>
        /// Density comes from the biome at each candidate point, so forests are dense and deserts almost bare.
        /// The tree species is chosen once per chunk from its centre biome, so each chunk batches cleanly.
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
            BiomeProfile centre = Biomes.Blend(weights, out BiomeKind centreKind);
            chunk.TreeVariant = (centreKind == BiomeKind.Forest || centreKind == BiomeKind.Alpine) ? 0 : 1;

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
                else if (roll < profile.Rockiness * 0.25 + profile.Vegetation * 0.7 && profile.Vegetation > 0.02)
                {
                    chunk.Trees[chunk.TreeCount++] = Matrix4x4.TRS(ToVec3(local), Quaternion.Euler(0f, (float)yaw, 0f),
                        Vector3.one * (float)scale);
                }
            }
        }

        /// <summary>Draws the chunks in view. Matrices are chunk-relative; the chunk offset is applied here.</summary>
        public void Draw(IEnumerable<RoadChunkStreamer.Chunk> chunks, Vector3d origin)
        {
            foreach (RoadChunkStreamer.Chunk chunk in chunks)
            {
                if (!chunk.Go.activeInHierarchy) continue;
                Vector3 offset = new Vector3((float)(chunk.Origin.X - origin.X), (float)(chunk.Origin.Y - origin.Y),
                    (float)(chunk.Origin.Z - origin.Z));
                Matrix4x4 root = Matrix4x4.Translate(offset);
                int pick = (int)(chunk.Index & 1);
                Mesh treeMesh = chunk.TreeVariant == 0 ? _conifer[pick] : _broadleaf[pick];
                DrawBatch(treeMesh, _barkMaterial, root, chunk.Trees, chunk.TreeCount, 0);
                DrawBatch(treeMesh, _leafMaterials[chunk.TreeVariant], root, chunk.Trees, chunk.TreeCount, 1);
                DrawBatch(_rock, _rockMaterial, root, chunk.Rocks, chunk.RockCount, 0);
            }
        }

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

        private static Vector3 ToVec3(Vector3d v) => new Vector3((float)v.X, (float)v.Y, (float)v.Z);
    }
}
