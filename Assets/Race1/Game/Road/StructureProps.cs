using System.Collections.Generic;
using Race1.Core;
using UnityEngine;

namespace Race1.Game
{
    /// <summary>
    /// Bridge pillars and tunnel linings for one chunk. Pillars are instanced boxes under the deck, from the natural
    /// ground up. The lining is an arch of quads over the carriageway whose faces point inwards, so the player sees
    /// it from inside the tunnel.
    /// </summary>
    public static class StructureProps
    {
        /// <summary>Pillar width across and along the road (m).</summary>
        private const double PillarWidthM = 0.9;

        /// <summary>Segments across the lining profile.</summary>
        private const int LiningSegments = 12;

        /// <summary>
        /// Pillar transforms for one chunk, relative to the chunk origin. Returns how many entries of
        /// <paramref name="buffer"/> are valid. The buffer grows when needed and is reused otherwise.
        /// </summary>
        public static int BuildPillars(RoadTrack road, TerrainField terrain, long chunk, Vector3d origin, ref Matrix4x4[] buffer)
        {
            double ds = road.SampleSpacing;
            long i0 = RoadMeshBuilder.IndexStart(road, chunk);
            int segments = (int)System.Math.Round(RoadMeshBuilder.ChunkLength / ds);
            int count = 0;

            for (int j = 0; j <= segments; j++)
            {
                long index = i0 + j;
                if (!StructureGeometry.IsPillarSample(index, ds)) continue;
                RoadSample sample = road.GetSample(index);
                if (!sample.Bridge) continue;

                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3d p = sample.PointAt(side * sample.HalfWidth * StructureGeometry.PillarLateralFactor);
                    double ground = terrain.NaturalHeight(p.X, p.Z);
                    double height = StructureGeometry.PillarHeight(p.Y, ground);
                    if (height <= 0.0) continue;

                    if (buffer == null || buffer.Length <= count)
                    {
                        var grown = new Matrix4x4[System.Math.Max(16, count * 2)];
                        if (buffer != null) System.Array.Copy(buffer, grown, count);
                        buffer = grown;
                    }

                    Vector3d centre = new Vector3d(p.X, ground + height * 0.5, p.Z) - origin;
                    buffer[count++] = Matrix4x4.TRS(
                        new Vector3((float)centre.X, (float)centre.Y, (float)centre.Z),
                        Quaternion.identity,
                        new Vector3((float)PillarWidthM, (float)height, (float)PillarWidthM));
                }
            }
            return count;
        }

        /// <summary>
        /// Tunnel lining for one chunk, or null when the chunk has no tunnel. Quads run along the road between tunnel
        /// samples, around the lining profile. Normals point into the tunnel, and winding matches them.
        /// </summary>
        public static Mesh BuildTunnelLining(RoadTrack road, long chunk, Vector3d origin)
        {
            double ds = road.SampleSpacing;
            long i0 = RoadMeshBuilder.IndexStart(road, chunk);
            int segments = (int)System.Math.Round(RoadMeshBuilder.ChunkLength / ds);

            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var tris = new List<int>();

            Vector3[] prevPos = null;
            Vector3[] prevNormal = null;
            for (int j = 0; j <= segments; j++)
            {
                RoadSample sample = road.GetSample(i0 + j);
                if (!sample.Tunnel)
                {
                    prevPos = null;
                    continue;
                }

                var profile = StructureGeometry.LiningProfile(sample.HalfWidth, LiningSegments);
                var pos = new Vector3[profile.Length];
                var nrm = new Vector3[profile.Length];
                double a = StructureGeometry.LiningHalfWidth(sample.HalfWidth);
                double b = StructureGeometry.LiningHeightM;
                for (int k = 0; k < profile.Length; k++)
                {
                    Vector3d world = sample.PointAt(profile[k].lateral) + sample.Up * profile[k].height;
                    Vector3d local = world - origin;
                    pos[k] = new Vector3((float)local.X, (float)local.Y, (float)local.Z);

                    // Inward normal of the ellipse, in road axes (lateral along Right, height along Up).
                    double x = profile[k].lateral, y = profile[k].height;
                    Vector3d inward = -(sample.Right * (x / (a * a)) + sample.Up * (y / (b * b)));
                    double len = inward.Length;
                    if (len > 0.0) inward = inward / len;
                    nrm[k] = new Vector3((float)inward.X, (float)inward.Y, (float)inward.Z);
                }

                if (prevPos != null)
                {
                    for (int k = 0; k < profile.Length - 1; k++)
                    {
                        AddLiningQuad(prevPos[k], prevPos[k + 1], pos[k + 1], pos[k],
                            prevNormal[k], prevNormal[k + 1], nrm[k + 1], nrm[k], verts, normals, tris);
                    }
                }
                prevPos = pos;
                prevNormal = nrm;
            }

            if (tris.Count == 0) return null;
            var mesh = new Mesh { name = "TunnelLining" };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddLiningQuad(Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3,
            Vector3 n0, Vector3 n1, Vector3 n2, Vector3 n3,
            List<Vector3> verts, List<Vector3> normals, List<int> tris)
        {
            int i = verts.Count;
            verts.Add(v0); verts.Add(v1); verts.Add(v2); verts.Add(v3);
            normals.Add(n0); normals.Add(n1); normals.Add(n2); normals.Add(n3);

            // Wind each triangle so its front faces the inward normal of its first corner.
            Vector3 face = Vector3.Cross(v1 - v0, v2 - v0);
            Vector3 inward = n0 + n2;
            bool flip = Vector3.Dot(face, inward) < 0f;
            if (!flip)
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
}
