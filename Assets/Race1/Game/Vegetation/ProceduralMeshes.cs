using System.Collections.Generic;
using UnityEngine;

namespace Race1.Game
{
    /// <summary>
    /// Procedural tree and rock meshes. Both are built once and drawn as GPU instances. They are stylised
    /// stand-ins until authored, licensed foliage assets replace them (see docs/STATUS.md).
    /// </summary>
    public static class ProceduralMeshes
    {
        /// <summary>Conifer: trunk plus three stacked, jittered cones. Submesh 0 = bark, 1 = needles.</summary>
        public static Mesh Conifer(int seed)
        {
            var rng = new System.Random(seed);
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var trunkTris = new List<int>();
            var leafTris = new List<int>();

            AddCylinder(verts, uvs, trunkTris, Vector3.zero, 0.09f, 0.09f, 2.4f, 6);
            float[] heights = { 1.6f, 2.5f, 3.4f };
            float[] radii = { 1.25f, 0.95f, 0.6f };
            for (int i = 0; i < 3; i++)
            {
                float jitter = 0.9f + 0.2f * (float)rng.NextDouble();
                AddCylinder(verts, uvs, leafTris, new Vector3(0, heights[i], 0),
                    radii[i] * jitter, 0.0f, 1.6f, 7);
            }
            return Assemble("Conifer", verts, uvs, new[] { trunkTris, leafTris });
        }

        /// <summary>Broadleaf: trunk and a noise-displaced crown. Submesh 0 = bark, 1 = foliage.</summary>
        public static Mesh Broadleaf(int seed)
        {
            var rng = new System.Random(seed);
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var trunkTris = new List<int>();
            var leafTris = new List<int>();
            AddCylinder(verts, uvs, trunkTris, Vector3.zero, 0.14f, 0.1f, 2.6f, 6);
            AddBlob(verts, uvs, leafTris, new Vector3(0, 3.6f, 0), 1.9f, rng);
            return Assemble("Broadleaf", verts, uvs, new[] { trunkTris, leafTris });
        }

        /// <summary>Rock: an irregular blob, flattened a little.</summary>
        public static Mesh Rock(int seed)
        {
            var rng = new System.Random(seed);
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            AddBlob(verts, uvs, tris, new Vector3(0, 0.35f, 0), 0.8f, rng);
            return Assemble("Rock", verts, uvs, new[] { tris });
        }

        private static Mesh Assemble(string name, List<Vector3> verts, List<Vector2> uvs, List<int>[] subs)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = subs.Length;
            for (int i = 0; i < subs.Length; i++)
            {
                mesh.SetTriangles(subs[i], i);
            }
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddCylinder(List<Vector3> verts, List<Vector2> uvs, List<int> tris, Vector3 baseCentre,
            float bottomRadius, float topRadius, float height, int sides)
        {
            int start = verts.Count;
            for (int i = 0; i <= sides; i++)
            {
                float a = i / (float)sides * Mathf.PI * 2f;
                float c = Mathf.Cos(a), s = Mathf.Sin(a);
                verts.Add(baseCentre + new Vector3(c * bottomRadius, 0f, s * bottomRadius));
                uvs.Add(new Vector2(i / (float)sides, 0f));
                verts.Add(baseCentre + new Vector3(c * Mathf.Max(0.01f, topRadius), height, s * Mathf.Max(0.01f, topRadius)));
                uvs.Add(new Vector2(i / (float)sides, 1f));
            }
            for (int i = 0; i < sides; i++)
            {
                int a = start + i * 2;
                tris.Add(a); tris.Add(a + 2); tris.Add(a + 1);
                tris.Add(a + 1); tris.Add(a + 2); tris.Add(a + 3);
            }
        }

        private static void AddBlob(List<Vector3> verts, List<Vector2> uvs, List<int> tris, Vector3 centre, float radius, System.Random rng)
        {
            const int rings = 6;
            const int sides = 9;
            int start = verts.Count;
            for (int r = 0; r <= rings; r++)
            {
                float phi = r / (float)rings * Mathf.PI;
                for (int s = 0; s < sides; s++)
                {
                    float theta = s / (float)sides * Mathf.PI * 2f;
                    float k = 0.75f + 0.5f * (float)rng.NextDouble();
                    Vector3 dir = new Vector3(Mathf.Sin(phi) * Mathf.Cos(theta), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(theta));
                    verts.Add(centre + dir * radius * k);
                    uvs.Add(new Vector2(s / (float)sides, r / (float)rings));
                }
            }
            for (int r = 0; r < rings; r++)
            {
                for (int s = 0; s < sides; s++)
                {
                    int a = start + r * sides + s;
                    int b = start + r * sides + (s + 1) % sides;
                    int c = a + sides;
                    int d = b + sides;
                    tris.Add(a); tris.Add(c); tris.Add(b);
                    tris.Add(b); tris.Add(c); tris.Add(d);
                }
            }
        }
    }
}
