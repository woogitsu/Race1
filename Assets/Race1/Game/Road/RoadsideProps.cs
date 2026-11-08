using System.Collections.Generic;
using Race1.Core;
using UnityEngine;

namespace Race1.Game
{
    /// <summary>
    /// Roadside dressing for one streaming chunk: a guardrail mesh, post transforms and street lamp
    /// positions. Posts and lamps are relative to the chunk origin.
    /// </summary>
    public sealed class RoadsideChunk
    {
        /// <summary>Galvanised W-beam guardrail on one or both sides, or null when the chunk has none.</summary>
        public Mesh Guardrail;

        /// <summary>
        /// Post transforms: rail posts, reflector posts (scale 0.5) and kilometre markers.
        /// Only the first <see cref="PostCount"/> entries are valid.
        /// </summary>
        public Matrix4x4[] Posts;

        /// <summary>Number of valid entries in <see cref="Posts"/>.</summary>
        public int PostCount;

        /// <summary>Street lamp head positions. Only the first <see cref="LampCount"/> entries are valid.</summary>
        public Vector3[] Lamps;

        /// <summary>Number of valid entries in <see cref="Lamps"/>.</summary>
        public int LampCount;

        /// <summary>True when the chunk has a rail, a post or a lamp.</summary>
        public bool HasContent
        {
            get { return Guardrail != null || PostCount > 0 || LampCount > 0; }
        }
    }

    /// <summary>
    /// Builds the roadside props for one road chunk. Rails stand on Alpine and Highway samples and on
    /// bridges, with posts along them. Reflector posts sit on the outer shoulder every 50 m in open
    /// biomes, kilometre markers every 1000 m, and street lamps every 40 m in Town and Highway.
    /// Output is deterministic for a given road, chunk and seed.
    /// </summary>
    public static class RoadsideProps
    {
        private const double ChunkLength = RoadMeshBuilder.ChunkLength;
        private const int ProfileCount = 4;
        private const double RailHeight = 0.55;
        private const double RailOffset = 0.6;
        private const double PostSpacing = 4.0;
        private const double ReflectorSpacing = 50.0;
        private const double KilometreSpacing = 1000.0;
        private const double LampSpacing = 40.0;
        private const double LampOffset = 0.8;
        private const double LampHeight = 9.0;

        // W-beam cross-section in the rail plane: depth towards traffic and height about the rail centre (m).
        // Three bands spanning 0.3 m, with a rounded bulge towards traffic.
        private static readonly double[] ProfileDepth = { 0.0, 0.055, 0.055, 0.0 };
        private static readonly double[] ProfileHeight = { 0.15, 0.05, -0.05, -0.15 };

        /// <summary>
        /// Builds the dressing for road distance [chunkIndex * 100, (chunkIndex + 1) * 100).
        /// Matrices and points are world minus <paramref name="chunkOrigin"/>, converted to float.
        /// </summary>
        public static RoadsideChunk Build(RoadTrack road, long chunkIndex, Vector3d chunkOrigin, ulong seed)
        {
            double ds = road.SampleSpacing;
            long i0 = (long)System.Math.Round(chunkIndex * ChunkLength / ds);
            int segments = (int)System.Math.Round(ChunkLength / ds);

            var samples = new RoadSample[segments + 1];
            for (int j = 0; j <= segments; j++)
            {
                samples[j] = road.GetSample(i0 + j);
            }

            var railHere = new bool[segments];
            bool anyRail = false;
            for (int j = 0; j < segments; j++)
            {
                railHere[j] = NeedsRail(samples[j]);
                anyRail |= railHere[j];
            }

            int postStep = StepFor(PostSpacing, ds);
            int reflectorStep = StepFor(ReflectorSpacing, ds);
            int kilometreStep = StepFor(KilometreSpacing, ds);
            int lampStep = StepFor(LampSpacing, ds);

            // Worst case: consecutive samples hit a step at most segments / step + 1 times. Each bound
            // adds two to the integer quotient for safety, and the arrays are trimmed at the end.
            var posts = new Matrix4x4[2 * (segments / postStep + 2)
                + (segments / reflectorStep + 2) + (segments / kilometreStep + 2)];
            var lamps = new Vector3[segments / lampStep + 2];
            int postCount = 0;
            int lampCount = 0;

            var rng = new DeterministicRng(DeterministicRng.Combine(seed, (ulong)chunkIndex));

            for (int j = 0; j < segments; j++)
            {
                RoadSample s = samples[j];
                long gi = i0 + j;
                double edge = s.HalfWidth + s.Shoulder;
                double outer = System.Math.Max(s.HalfWidth + 0.3, edge - 0.4);

                // Rail posts on both sides, every postStep samples, wherever the rail is built.
                if (railHere[j] && gi % postStep == 0)
                {
                    AddPost(posts, ref postCount, s.PointAt(-(edge + RailOffset)), s, chunkOrigin, 1f);
                    AddPost(posts, ref postCount, s.PointAt(edge + RailOffset), s, chunkOrigin, 1f);
                }

                // Reflector posts every 50 m on the outer shoulder, alternating sides, in open biomes.
                if (gi % reflectorStep == 0 && IsReflectorBiome(s.Biome))
                {
                    long mark = gi / reflectorStep;
                    double side = mark % 2 == 0 ? -1.0 : 1.0;
                    Vector3d p = s.PointAt(side * outer) + s.Up * rng.Range(-0.01, 0.01);
                    AddPost(posts, ref postCount, p, s, chunkOrigin, 0.5f);
                }

                // Kilometre markers on the right shoulder, skipping the start line.
                if (gi > 0 && gi % kilometreStep == 0)
                {
                    AddPost(posts, ref postCount, s.PointAt(outer), s, chunkOrigin, 1f);
                }

                // Street lamp heads every 40 m on the right side of Town and Highway.
                if (gi % lampStep == 0 && (s.Biome == BiomeKind.Town || s.Biome == BiomeKind.Highway))
                {
                    Vector3d lamp = s.PointAt(edge + LampOffset) + s.Up * LampHeight;
                    lamps[lampCount++] = ToLocal(lamp, chunkOrigin);
                }
            }

            Mesh guardrail = anyRail ? BuildRail(samples, railHere, segments, chunkOrigin) : null;

            if (postCount != posts.Length)
            {
                System.Array.Resize(ref posts, postCount);
            }
            if (lampCount != lamps.Length)
            {
                System.Array.Resize(ref lamps, lampCount);
            }

            return new RoadsideChunk
            {
                Guardrail = guardrail,
                Posts = posts,
                PostCount = postCount,
                Lamps = lamps,
                LampCount = lampCount,
            };
        }

        private static bool NeedsRail(RoadSample s)
        {
            return s.Bridge || s.Biome == BiomeKind.Alpine || s.Biome == BiomeKind.Highway;
        }

        private static bool IsReflectorBiome(BiomeKind biome)
        {
            return biome == BiomeKind.Forest || biome == BiomeKind.Meadow
                || biome == BiomeKind.Coast || biome == BiomeKind.Desert;
        }

        /// <summary>Sample step for a spacing in metres, at least one sample.</summary>
        private static int StepFor(double spacing, double ds)
        {
            int step = (int)System.Math.Round(spacing / ds);
            return step < 1 ? 1 : step;
        }

        private static void AddPost(Matrix4x4[] posts, ref int count, Vector3d p, RoadSample s,
            Vector3d origin, float scale)
        {
            Quaternion rotation = Quaternion.LookRotation(ToVector3(s.Forward), ToVector3(s.Up));
            posts[count++] = Matrix4x4.TRS(ToLocal(p, origin), rotation, new Vector3(scale, scale, scale));
        }

        /// <summary>Builds the rail strip for every flagged segment, on both sides, into one submesh.</summary>
        private static Mesh BuildRail(RoadSample[] samples, bool[] railHere, int segments, Vector3d origin)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            for (int side = -1; side <= 1; side += 2)
            {
                var map = new int[(segments + 1) * ProfileCount];
                for (int i = 0; i < map.Length; i++)
                {
                    map[i] = -1;
                }

                for (int j = 0; j < segments; j++)
                {
                    if (!railHere[j])
                    {
                        continue;
                    }

                    for (int b = 0; b < ProfileCount - 1; b++)
                    {
                        int a0 = RailVertex(verts, samples, origin, side, j, b, map);
                        int a1 = RailVertex(verts, samples, origin, side, j, b + 1, map);
                        int c0 = RailVertex(verts, samples, origin, side, j + 1, b, map);
                        int c1 = RailVertex(verts, samples, origin, side, j + 1, b + 1, map);
                        Vector3 outward = RailNormal(samples[j], side, b);
                        AddTriangle(verts, tris, a0, a1, c1, outward);
                        AddTriangle(verts, tris, a0, c1, c0, outward);
                    }
                }
            }

            var mesh = new Mesh
            {
                name = "Guardrail",
                indexFormat = UnityEngine.Rendering.IndexFormat.UInt32,
            };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Returns the vertex for profile point k at sample j on one side, creating it on first use.</summary>
        private static int RailVertex(List<Vector3> verts, RoadSample[] samples, Vector3d origin,
            int side, int j, int k, int[] map)
        {
            int key = j * ProfileCount + k;
            if (map[key] >= 0)
            {
                return map[key];
            }

            RoadSample s = samples[j];
            double t = side * (s.HalfWidth + s.Shoulder + RailOffset);
            Vector3d p = s.PointAt(t)
                + s.Up * (RailHeight + ProfileHeight[k])
                - s.Right * (side * ProfileDepth[k]);
            map[key] = verts.Count;
            verts.Add(ToLocal(p, origin));
            return map[key];
        }

        /// <summary>Outward normal of one profile band, facing traffic and up, in world space.</summary>
        private static Vector3 RailNormal(RoadSample s, int side, int band)
        {
            double td = ProfileDepth[band + 1] - ProfileDepth[band];
            double tv = ProfileHeight[band + 1] - ProfileHeight[band];
            // Perpendicular to the band tangent in the (depth, height) plane.
            double nDepth = -tv;
            double nHeight = td;
            Vector3d dir = s.Right * (-side * nDepth) + s.Up * nHeight;
            return ToVector3(dir);
        }

        /// <summary>Adds a triangle, swapping two corners if needed so its face points along <paramref name="outward"/>.</summary>
        private static void AddTriangle(List<Vector3> verts, List<int> tris, int a, int b, int c, Vector3 outward)
        {
            Vector3 n = Vector3.Cross(verts[b] - verts[a], verts[c] - verts[a]);
            if (Vector3.Dot(n, outward) < 0f)
            {
                int swap = b;
                b = c;
                c = swap;
            }
            tris.Add(a);
            tris.Add(b);
            tris.Add(c);
        }

        private static Vector3 ToLocal(Vector3d p, Vector3d origin)
        {
            return new Vector3((float)(p.X - origin.X), (float)(p.Y - origin.Y), (float)(p.Z - origin.Z));
        }

        private static Vector3 ToVector3(Vector3d v)
        {
            return new Vector3((float)v.X, (float)v.Y, (float)v.Z);
        }
    }
}
