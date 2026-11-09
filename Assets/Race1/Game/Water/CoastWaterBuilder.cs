using System.Collections.Generic;
using Race1.Core;
using UnityEngine;

namespace Race1.Game
{
    /// <summary>
    /// Sea surface for one chunk. Water covers only the terrain rings outside the road edge, where the coast biome
    /// dominates and the ground lies below sea level, so the carriageway never gets water. Each vertex carries foam in
    /// its alpha channel, strongest at the waterline. Returns null for a chunk without water, which is most chunks.
    /// </summary>
    public static class CoastWaterBuilder
    {
        // Terrain rings on each side of the road, outside the edge. Ring 7 and ring 13 sit on the road edge.
        private static readonly int[] LeftRings = { 0, 1, 2, 3, 4, 5, 6, 7 };
        private static readonly int[] RightRings = { 13, 14, 15, 16, 17, 18, 19, 20 };

        private struct RingPoint
        {
            public Vector3d Position;
            public double Ground;
            public double Coast;
        }

        /// <param name="origin">Chunk origin in world space, the same point the terrain mesh is built around.</param>
        public static Mesh Build(RoadTrack road, TerrainField terrain, BiomeField biomes, long chunk, Vector3d origin)
        {
            double ds = road.SampleSpacing;
            long i0 = RoadMeshBuilder.IndexStart(road, chunk);
            int segments = (int)System.Math.Round(RoadMeshBuilder.ChunkLength / ds);
            int sampleCount = segments + 1;

            var samples = new RoadSample[sampleCount];
            for (int j = 0; j < sampleCount; j++)
            {
                samples[j] = road.GetSample(i0 + j);
            }

            // Cheap gate first: most chunks have no coast even at the outermost rings, and then nothing else is sampled.
            if (!HasCoastAtOuterRings(samples, biomes)) return null;

            var verts = new List<Vector3>();
            var colours = new List<Color>();
            var tris = new List<int>();
            var offsets = new double[RoadMeshBuilder.RingCount];
            var weights = new double[Biomes.Count];
            var prev = new RingPoint[RoadMeshBuilder.RingCount];
            var cur = new RingPoint[RoadMeshBuilder.RingCount];

            Fill(prev, samples[0], terrain, biomes, offsets, weights);
            for (int j = 0; j < sampleCount - 1; j++)
            {
                Fill(cur, samples[j + 1], terrain, biomes, offsets, weights);
                AddStrip(prev, cur, LeftRings, origin, verts, colours, tris);
                AddStrip(prev, cur, RightRings, origin, verts, colours, tris);
                RingPoint[] swap = prev;
                prev = cur;
                cur = swap;
            }

            if (tris.Count == 0) return null;
            var mesh = new Mesh { name = "CoastWater" };
            mesh.SetVertices(verts);
            mesh.SetColors(colours);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static bool HasCoastAtOuterRings(RoadSample[] samples, BiomeField biomes)
        {
            var offsets = new double[RoadMeshBuilder.RingCount];
            var weights = new double[Biomes.Count];
            foreach (RoadSample sample in samples)
            {
                double edge = sample.HalfWidth + sample.Shoulder;
                RoadMeshBuilder.RingOffsets(sample.HalfWidth, edge, offsets);
                for (int side = 0; side < 2; side++)
                {
                    int ring = side == 0 ? 0 : RoadMeshBuilder.RingCount - 1;
                    Vector3d p = sample.PointAt(offsets[ring]);
                    biomes.Weights(p.X, p.Z, weights);
                    if (weights[(int)BiomeKind.Coast] >= Shoreline.CoastThreshold) return true;
                }
            }
            return false;
        }

        /// <summary>Fills the rings this builder uses. The ground height matches the terrain mesh: blended to the road beyond the edge.</summary>
        private static void Fill(RingPoint[] ring, RoadSample sample, TerrainField terrain, BiomeField biomes,
            double[] offsets, double[] weights)
        {
            double edge = sample.HalfWidth + sample.Shoulder;
            RoadMeshBuilder.RingOffsets(sample.HalfWidth, edge, offsets);
            for (int r = 0; r < RoadMeshBuilder.RingCount; r++)
            {
                if (r > 7 && r < 13) continue;   // the carriageway itself

                Vector3d p = sample.PointAt(offsets[r]);
                double ground = p.Y;
                if (System.Math.Abs(offsets[r]) > edge)
                {
                    double natural = terrain.NaturalHeight(p.X, p.Z);
                    ground = RoadTerrainBlend.GroundHeight(sample, offsets[r], natural);
                }
                biomes.Weights(p.X, p.Z, weights);
                ring[r] = new RingPoint { Position = p, Ground = ground, Coast = weights[(int)BiomeKind.Coast] };
            }
        }

        /// <summary>
        /// Adds one quad per pair of neighbouring rings, between two road samples. A quad is kept only when all four of
        /// its corners are water, so the shoreline cuts cleanly along ring edges.
        /// </summary>
        private static void AddStrip(RingPoint[] prev, RingPoint[] cur, int[] rings, Vector3d origin,
            List<Vector3> verts, List<Color> colours, List<int> tris)
        {
            for (int k = 0; k < rings.Length - 1; k++)
            {
                RingPoint pa = prev[rings[k]];
                RingPoint pb = prev[rings[k + 1]];
                RingPoint ca = cur[rings[k]];
                RingPoint cb = cur[rings[k + 1]];
                if (!IsWater(pa) || !IsWater(pb) || !IsWater(ca) || !IsWater(cb)) continue;

                Vector3 v0 = Local(pa.Position, origin);
                Vector3 v1 = Local(pb.Position, origin);
                Vector3 v2 = Local(cb.Position, origin);
                Vector3 v3 = Local(ca.Position, origin);

                int i = verts.Count;
                verts.Add(v0);
                verts.Add(v1);
                verts.Add(v2);
                verts.Add(v3);
                colours.Add(Foam(pa));
                colours.Add(Foam(pb));
                colours.Add(Foam(cb));
                colours.Add(Foam(ca));

                // Wind the quad so that it faces up. The surface is flat, so the sign of one triangle decides it.
                Vector3 normal = Vector3.Cross(v1 - v0, v2 - v0);
                if (normal.y >= 0f)
                {
                    tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
                    tris.Add(i); tris.Add(i + 2); tris.Add(i + 3);
                }
                else
                {
                    tris.Add(i); tris.Add(i + 2); tris.Add(i + 1);
                    tris.Add(i); tris.Add(i + 3); tris.Add(i + 2);
                }
            }
        }

        private static bool IsWater(RingPoint p) => Shoreline.IsWaterAt(p.Ground, p.Coast);

        private static Color Foam(RingPoint p)
        {
            float foam = (float)Shoreline.FoamStrength(Shoreline.Depth(p.Ground));
            return new Color(0f, 0f, 0f, foam);
        }

        /// <summary>The surface sits at sea level, in chunk-local space.</summary>
        private static Vector3 Local(Vector3d p, Vector3d origin)
        {
            return new Vector3((float)(p.X - origin.X), (float)(Shoreline.SeaLevelM - origin.Y), (float)(p.Z - origin.Z));
        }
    }
}
