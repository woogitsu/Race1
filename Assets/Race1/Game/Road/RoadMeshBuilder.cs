using System.Collections.Generic;
using Race1.Core;
using UnityEngine;

namespace Race1.Game
{
    /// <summary>
    /// Builds one streaming chunk: the carriageway, the shoulders and the blended terrain as a strip of
    /// lateral rings sampled along the road, plus lane markings. Vertices are relative to the chunk origin
    /// (the road point at the chunk start), so float precision stays good however far the car has driven.
    /// </summary>
    public static class RoadMeshBuilder
    {
        public const double ChunkLength = 100.0;
        public const int RingCount = 21;

        /// <summary>Lateral ring offsets for one sample. Road rings: index 7..13 (from -E to +E).</summary>
        private static void RingOffsets(double halfWidth, double edge, double[] t)
        {
            double h = halfWidth;
            t[0] = -(edge + 250.0);
            t[1] = -(edge + 120.0);
            t[2] = -(edge + 60.0);
            t[3] = -(edge + 30.0);
            t[4] = -(edge + 14.0);
            t[5] = -(edge + 6.0);
            t[6] = -(edge + 2.0);
            t[7] = -edge;
            t[8] = -h;
            t[9] = -h * 0.5;
            t[10] = 0.0;
            t[11] = h * 0.5;
            t[12] = h;
            t[13] = edge;
            t[14] = edge + 2.0;
            t[15] = edge + 6.0;
            t[16] = edge + 14.0;
            t[17] = edge + 30.0;
            t[18] = edge + 60.0;
            t[19] = edge + 120.0;
            t[20] = edge + 250.0;
        }

        public static long ChunkIndexOf(double s) => (long)System.Math.Floor(s / ChunkLength);

        /// <summary>World-space origin of a chunk: the road centre point at its start.</summary>
        public static Vector3d OriginOf(RoadTrack road, long chunk)
        {
            long i0 = IndexStart(road, chunk);
            return road.GetSample(i0).Position;
        }

        private static long IndexStart(RoadTrack road, long chunk)
        {
            return (long)System.Math.Round(chunk * ChunkLength / road.SampleSpacing);
        }

        public sealed class ChunkData
        {
            public Mesh Mesh;
            public Color TerrainTint;
            public Vector3d Origin;
        }

        public static ChunkData Build(RoadTrack road, TerrainField terrain, BiomeField biomes, long chunk)
        {
            double ds = road.SampleSpacing;
            long i0 = IndexStart(road, chunk);
            int segments = (int)System.Math.Round(ChunkLength / ds);
            int sampleCount = segments + 1;

            var samples = new RoadSample[sampleCount];
            for (int j = 0; j < sampleCount; j++)
            {
                samples[j] = road.GetSample(i0 + j);
            }

            Vector3d origin = samples[0].Position;
            var verts = new List<Vector3>(sampleCount * RingCount);
            var uvs = new List<Vector2>(sampleCount * RingCount);
            var roadTris = new List<int>();
            var terrainTris = new List<int>();
            var markVerts = new List<Vector3>();
            var markUvs = new List<Vector2>();
            var markTris = new List<int>();

            var t = new double[RingCount];
            for (int j = 0; j < sampleCount; j++)
            {
                RoadSample sample = samples[j];
                double edge = sample.HalfWidth + sample.Shoulder;
                RingOffsets(sample.HalfWidth, edge, t);

                for (int r = 0; r < RingCount; r++)
                {
                    Vector3d p = sample.PointAt(t[r]);
                    double y = p.Y;
                    if (System.Math.Abs(t[r]) > edge)
                    {
                        double natural = terrain.NaturalHeight(p.X, p.Z);
                        y = RoadTerrainBlend.GroundHeight(sample, t[r], natural);
                    }
                    Vector3d local = new Vector3d(p.X - origin.X, y - origin.Y, p.Z - origin.Z);
                    verts.Add(new Vector3((float)local.X, (float)local.Y, (float)local.Z));
                    uvs.Add(new Vector2((float)(t[r] / 3.5), (float)(sample.S / 3.5)));
                }

                // Lane markings: dividers and edge lines, lifted slightly off the asphalt.
                if (j < sampleCount - 1)
                {
                    AddMarkings(sample, samples[j + 1], origin, markVerts, markUvs, markTris);
                }
            }

            for (int j = 0; j < sampleCount - 1; j++)
            {
                for (int r = 0; r < RingCount - 1; r++)
                {
                    int a = j * RingCount + r;
                    int b = (j + 1) * RingCount + r;
                    bool isRoad = r >= 7 && r <= 12;
                    List<int> tris = isRoad ? roadTris : terrainTris;
                    // Two triangles per quad, counter-clockwise seen from above.
                    tris.Add(a); tris.Add(a + 1); tris.Add(b + 1);
                    tris.Add(a); tris.Add(b + 1); tris.Add(b);
                }
            }

            var mesh = new Mesh
            {
                name = $"RoadChunk_{chunk}",
                indexFormat = UnityEngine.Rendering.IndexFormat.UInt32,
            };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = 3;
            mesh.SetTriangles(roadTris, 0);
            mesh.SetTriangles(terrainTris, 1);

            // Markings are a separate vertex set so they can use their own material.
            var all = new List<Vector3>(verts.Count + markVerts.Count);
            all.AddRange(verts);
            all.AddRange(markVerts);
            var allUvs = new List<Vector2>(uvs.Count + markUvs.Count);
            allUvs.AddRange(uvs);
            allUvs.AddRange(markUvs);
            mesh.SetVertices(all);
            mesh.SetUVs(0, allUvs);
            var markIdx = new List<int>(markTris.Count);
            foreach (int idx in markTris) markIdx.Add(idx + verts.Count);
            mesh.SetTriangles(markIdx, 2);

            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            BiomeProfile profile = SampleBiome(biomes, origin.X + 50, origin.Z);
            return new ChunkData
            {
                Mesh = mesh,
                TerrainTint = new Color((float)profile.TintR, (float)profile.TintG, (float)profile.TintB),
                Origin = origin,
            };
        }

        private static BiomeProfile SampleBiome(BiomeField biomes, double x, double z)
        {
            var w = new double[Biomes.Count];
            biomes.Weights(x, z, w);
            return Biomes.Blend(w, out _);
        }

        private static void AddMarkings(RoadSample a, RoadSample b, Vector3d origin,
            List<Vector3> verts, List<Vector2> uvs, List<int> tris)
        {
            int lanes = System.Math.Max(2, a.Lanes);
            double h = a.HalfWidth;
            double lift = 0.03;
            const double lineWidth = 0.15;
            double sa = a.S;
            bool dashOn = (sa % 12.0) < 4.0;

            // Edge lines are solid; dividers are dashed. Dividers sit between lanes.
            AddStrip(a, b, -(h - 0.25), lineWidth, lift, origin, verts, uvs, tris, true);
            AddStrip(a, b, (h - 0.25), lineWidth, lift, origin, verts, uvs, tris, true);
            for (int k = 1; k < lanes; k++)
            {
                double t = -h + k * (2.0 * h / lanes);
                if (System.Math.Abs(t) < 0.05) t = 0.0;
                if (dashOn) AddStrip(a, b, t, lineWidth, lift, origin, verts, uvs, tris, false);
            }
        }

        private static void AddStrip(RoadSample a, RoadSample b, double t, double width, double lift, Vector3d origin,
            List<Vector3> verts, List<Vector2> uvs, List<int> tris, bool solid)
        {
            Vector3d pa = a.PointAt(t);
            Vector3d pb = b.PointAt(t);
            Vector3d ra = a.Right * (width * 0.5);
            Vector3d rb = b.Right * (width * 0.5);
            Vector3d upA = a.Up * lift;
            Vector3d upB = b.Up * lift;

            int i0 = verts.Count;
            verts.Add(ToLocal(pa - ra + upA, origin));
            verts.Add(ToLocal(pa + ra + upA, origin));
            verts.Add(ToLocal(pb + rb + upB, origin));
            verts.Add(ToLocal(pb - rb + upB, origin));
            uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(1, 0));
            uvs.Add(new Vector2(1, 1)); uvs.Add(new Vector2(0, 1));
            tris.Add(i0); tris.Add(i0 + 1); tris.Add(i0 + 2);
            tris.Add(i0); tris.Add(i0 + 2); tris.Add(i0 + 3);
        }

        private static Vector3 ToLocal(Vector3d p, Vector3d origin)
        {
            return new Vector3((float)(p.X - origin.X), (float)(p.Y - origin.Y), (float)(p.Z - origin.Z));
        }
    }
}
