using System.Collections.Generic;
using Race1.Core;
using UnityEngine;

namespace Race1.Game
{
    /// <summary>
    /// Procedural tree and rock meshes, built once from a seed and drawn as GPU instances. Trees have submesh 0 =
    /// bark (trunk and branches) and submesh 1 = foliage: crossed quads that carry the leaf or needle texture and
    /// read from any side. Each tree is under about 600 vertices. They are stylised stand-ins until authored,
    /// licensed foliage assets replace them (see docs/STATUS.md).
    /// </summary>
    public static class ProceduralMeshes
    {
        private const int BarkSub = 0;
        private const int FoliageSub = 1;

        /// <summary>Conifer (spruce): a slim trunk, five tiers of outward-leaning cone cards and a leader card.</summary>
        public static Mesh Conifer(int seed)
        {
            var rng = SeededRng(seed, 0xC0F1UL);
            var b = new MeshBuilder();
            b.Tube(Vector3.zero, new Vector3(0f, 3.2f, 0f), 0.11f, 0.03f, 6);

            for (int tier = 0; tier < 5; tier++)
            {
                float y = 0.9f + tier * 0.72f;
                float radius = 1.45f - tier * 0.27f;
                for (int k = 0; k < 8; k++)
                {
                    float a = (k + (float)rng.Range(-0.3, 0.3)) / 8f * Mathf.PI * 2f;
                    Vector3 outward = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    Vector3 tangent = new Vector3(-outward.z, 0f, outward.x);
                    float lean = 0.35f + (float)rng.Range(-0.08, 0.08);
                    Vector3 up = (outward * lean + Vector3.up).normalized;
                    Vector3 normal = (outward * 0.8f + Vector3.up * 0.5f).normalized;
                    Vector3 centre = outward * (radius * 0.5f) + new Vector3(0f, y, 0f);
                    b.Card(centre, tangent, up, radius * 0.5f, 0.62f, normal, rng.NextDouble() < 0.5);
                }
            }

            b.Card(new Vector3(0f, 3.9f, 0f), Vector3.right, Vector3.up, 0.22f, 0.85f, Vector3.up, false);
            return Assemble("Conifer", b, 2);
        }

        /// <summary>Pine: a leaning trunk with a bare lower half and four flattened clusters of needle cards.</summary>
        public static Mesh Pine(int seed)
        {
            var rng = SeededRng(seed, 0x9171UL);
            var b = new MeshBuilder();
            b.Tube(Vector3.zero, new Vector3(0.15f, 4.6f, -0.1f), 0.13f, 0.05f, 7);
            b.Tube(new Vector3(0.05f, 2.6f, 0f), new Vector3(-0.9f, 3.6f, 0.4f), 0.05f, 0.015f, 5);
            b.Tube(new Vector3(0.1f, 3.4f, -0.05f), new Vector3(0.8f, 4.0f, -0.6f), 0.05f, 0.015f, 5);

            Vector3[] centres =
            {
                new Vector3(0f, 4.6f, 0f),
                new Vector3(-0.95f, 3.9f, 0.45f),
                new Vector3(0.9f, 3.6f, -0.6f),
                new Vector3(0.1f, 5.4f, -0.15f),
            };
            float[] radii = { 1.2f, 1.0f, 0.95f, 0.75f };
            for (int c = 0; c < centres.Length; c++)
            {
                for (int k = 0; k < 12; k++)
                {
                    float a = (float)rng.Range(0.0, Mathf.PI * 2.0);
                    float r = radii[c] * Mathf.Sqrt((float)rng.NextDouble());
                    Vector3 radial = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    Vector3 centre = centres[c] + radial * r + new Vector3(0f, (float)rng.Range(-0.18, 0.18), 0f);
                    Vector3 right = Vector3.Cross(Vector3.up, radial).normalized;
                    Vector3 up = (Vector3.up + radial * 0.45f).normalized;     // flattened: cards lean outward
                    Vector3 normal = (radial * 0.6f + Vector3.up * 0.8f).normalized;
                    b.Card(centre, right, up, 0.6f, 0.38f, normal, rng.NextDouble() < 0.5);
                }
            }
            return Assemble("Pine", b, 2);
        }

        /// <summary>Broadleaf (oak): a short trunk, three branch stubs and a crown of seventy leaf cards at random angles.</summary>
        public static Mesh Broadleaf(int seed)
        {
            var rng = SeededRng(seed, 0xB0AD1EUL);
            var b = new MeshBuilder();
            b.Tube(Vector3.zero, new Vector3(0f, 2.5f, 0f), 0.17f, 0.11f, 7);
            for (int k = 0; k < 3; k++)
            {
                float a = k * 2.1f + (float)rng.Range(-0.4, 0.4);
                Vector3 start = new Vector3(0f, 1.7f + 0.35f * k, 0f);
                Vector3 end = start + new Vector3(Mathf.Cos(a) * 0.9f, 0.9f, Mathf.Sin(a) * 0.9f);
                b.Tube(start, end, 0.1f, 0.035f, 5);
            }
            AddCrown(b, ref rng, new Vector3(0f, 3.6f, 0f), new Vector3(1.9f, 1.6f, 1.9f), 70, 0.5f, 0.42f);
            return Assemble("Broadleaf", b, 2);
        }

        /// <summary>Birch: a slim, leaning white trunk with thin branches and three airy clusters of small leaf cards.</summary>
        public static Mesh Birch(int seed)
        {
            var rng = SeededRng(seed, 0xB1C4UL);
            var b = new MeshBuilder();
            b.Tube(Vector3.zero, new Vector3(0.12f, 4.8f, 0.05f), 0.08f, 0.04f, 6);
            b.Tube(new Vector3(0.09f, 3.0f, 0.03f), new Vector3(0.7f, 3.9f, 0.3f), 0.03f, 0.012f, 4);
            b.Tube(new Vector3(0.1f, 3.7f, 0.04f), new Vector3(-0.5f, 4.4f, -0.2f), 0.03f, 0.012f, 4);

            Vector3[] centres =
            {
                new Vector3(0.2f, 3.5f, 0.05f),
                new Vector3(-0.3f, 4.25f, -0.15f),
                new Vector3(0.25f, 4.95f, -0.1f),
            };
            float[] radii = { 0.85f, 0.7f, 0.55f };
            for (int c = 0; c < centres.Length; c++)
            {
                AddCrown(b, ref rng, centres[c], new Vector3(radii[c], radii[c] * 0.9f, radii[c]), 16, 0.3f, 0.26f);
            }
            return Assemble("Birch", b, 2);
        }

        /// <summary>Rock: a lumpy, faceted boulder with flat cut planes and a flat, partly buried base. Flat-shaded, uv set.</summary>
        public static Mesh Rock(int seed)
        {
            var rng = SeededRng(seed, 0x20C4UL);
            const int rings = 6;
            const int sides = 9;

            // Index 0 and 1 are the shared poles; ring r (1..rings-1) uses 2 + (r - 1) * sides + s.
            int Index(int r, int s) => r == 0 ? 0 : r == rings ? 1 : 2 + (r - 1) * sides + (s % sides);

            var grid = new Vector3[2 + (rings - 1) * sides];
            float lumpA = (float)rng.Range(0.0, 6.28);
            float lumpB = (float)rng.Range(0.0, 6.28);
            for (int r = 0; r <= rings; r++)
            {
                float phi = r / (float)rings * Mathf.PI;
                for (int s = 0; s < sides; s++)
                {
                    if ((r == 0 || r == rings) && s > 0) continue;
                    float theta = s / (float)sides * Mathf.PI * 2f;
                    Vector3 dir = new Vector3(Mathf.Sin(phi) * Mathf.Cos(theta), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(theta));
                    float lump = 1f + 0.12f * Mathf.Sin(2f * theta + lumpA) * Mathf.Sin(phi + lumpB)
                                 + 0.12f * (float)rng.Range(-1.0, 1.0);
                    grid[Index(r, s)] = dir * (0.9f * lump);
                }
            }

            for (int i = 0; i < grid.Length; i++)
            {
                grid[i].x *= 1.1f;
                grid[i].y *= 0.72f;
            }

            // Three random cut planes give the flat facets; a horizontal plane flattens the base.
            for (int k = 0; k < 3; k++)
            {
                Vector3 pn = RandomDirection(ref rng);
                float offset = 0.6f + 0.15f * (float)rng.NextDouble();
                for (int i = 0; i < grid.Length; i++)
                {
                    float d = Vector3.Dot(grid[i], pn) - offset;
                    if (d > 0f) grid[i] -= pn * (d * 0.9f);
                }
            }
            for (int i = 0; i < grid.Length; i++)
            {
                if (grid[i].y < -0.38f) grid[i].y = -0.38f;
            }

            var mb = new MeshBuilder();
            for (int r = 0; r < rings; r++)
            {
                for (int s = 0; s < sides; s++)
                {
                    int a = Index(r, s);
                    int b = Index(r, s + 1);
                    int c = Index(r + 1, s);
                    int d = Index(r + 1, s + 1);
                    Face(mb, grid[a], grid[c], grid[b]);
                    Face(mb, grid[b], grid[c], grid[d]);
                }
            }
            return Assemble("Rock", mb, 1);
        }

        /// <summary>Emits one flat-shaded triangle with its own vertices (faceted look). Degenerate faces are skipped.</summary>
        private static void Face(MeshBuilder mb, Vector3 p0, Vector3 p1, Vector3 p2)
        {
            Vector3 n = Vector3.Cross(p1 - p0, p2 - p0);
            if (n.sqrMagnitude < 1e-10f) return;
            n.Normalize();
            if (Vector3.Dot(n, (p0 + p1 + p2) / 3f) < 0f) n = -n;
            int i0 = mb.Vertex(p0, n, Project(p0, n));
            int i1 = mb.Vertex(p1, n, Project(p1, n));
            int i2 = mb.Vertex(p2, n, Project(p2, n));
            mb.Triangle(BarkSub, i0, i1, i2);
        }

        /// <summary>Planar uv from the dominant axis of the face normal, so stone texture does not stretch.</summary>
        private static Vector2 Project(Vector3 p, Vector3 n)
        {
            Vector3 a = new Vector3(Mathf.Abs(n.x), Mathf.Abs(n.y), Mathf.Abs(n.z));
            Vector2 uv;
            if (a.x >= a.y && a.x >= a.z) uv = new Vector2(p.z, p.y);
            else if (a.y >= a.z) uv = new Vector2(p.x, p.z);
            else uv = new Vector2(p.x, p.y);
            return uv * 0.6f + new Vector2(0.5f, 0.5f);
        }

        /// <summary>Scatters leaf cards over an ellipsoid. Each card has its own facing, so the crown reads as volume.</summary>
        private static void AddCrown(MeshBuilder b, ref DeterministicRng rng, Vector3 centre, Vector3 radii,
            int count, float halfWidth, float halfHeight)
        {
            for (int k = 0; k < count; k++)
            {
                Vector3 d = RandomDirection(ref rng);
                float shell = 0.55f + 0.45f * (float)rng.NextDouble();
                Vector3 pos = centre + new Vector3(d.x * radii.x, d.y * radii.y, d.z * radii.z) * shell;
                Vector3 n = (d + RandomDirection(ref rng) * 0.35f).normalized;
                Basis(n, ref rng, out Vector3 right, out Vector3 up);
                float s = 0.8f + 0.35f * (float)rng.NextDouble();
                b.Card(pos, right, up, halfWidth * s, halfHeight * s, n, rng.NextDouble() < 0.5);
            }
        }

        /// <summary>An orthonormal (right, up) pair in the plane of <paramref name="n"/>, turned by a random angle.</summary>
        private static void Basis(Vector3 n, ref DeterministicRng rng, out Vector3 right, out Vector3 up)
        {
            Vector3 helper = Mathf.Abs(n.y) < 0.9f ? Vector3.up : Vector3.right;
            Vector3 r0 = Vector3.Cross(helper, n).normalized;
            Vector3 u0 = Vector3.Cross(n, r0);
            float t = (float)rng.Range(0.0, Mathf.PI * 2.0);
            right = r0 * Mathf.Cos(t) + u0 * Mathf.Sin(t);
            up = u0 * Mathf.Cos(t) - r0 * Mathf.Sin(t);
        }

        private static Vector3 RandomDirection(ref DeterministicRng rng)
        {
            double z = rng.Range(-1.0, 1.0);
            double a = rng.Range(0.0, System.Math.PI * 2.0);
            double r = System.Math.Sqrt(1.0 - z * z);
            return new Vector3((float)(r * System.Math.Cos(a)), (float)z, (float)(r * System.Math.Sin(a)));
        }

        private static DeterministicRng SeededRng(int seed, ulong salt) =>
            new DeterministicRng(DeterministicRng.Combine((ulong)(uint)seed, salt));

        private static Mesh Assemble(string name, MeshBuilder b, int subCount)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(b.Vertices);
            mesh.SetNormals(b.Normals);
            mesh.SetUVs(0, b.Uvs);
            mesh.subMeshCount = subCount;
            for (int i = 0; i < subCount; i++)
            {
                mesh.SetTriangles(b.Triangles(i), i);
            }
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Accumulates vertices with explicit normals and two submeshes. Triangles face their normals.</summary>
        private sealed class MeshBuilder
        {
            private readonly List<Vector3> _verts = new List<Vector3>();
            private readonly List<Vector3> _normals = new List<Vector3>();
            private readonly List<Vector2> _uvs = new List<Vector2>();
            private readonly List<int>[] _tris = { new List<int>(), new List<int>() };

            public List<Vector3> Vertices => _verts;
            public List<Vector3> Normals => _normals;
            public List<Vector2> Uvs => _uvs;
            public List<int> Triangles(int sub) => _tris[sub];

            public int Vertex(Vector3 position, Vector3 normal, Vector2 uv)
            {
                _verts.Add(position);
                _normals.Add(normal);
                _uvs.Add(uv);
                return _verts.Count - 1;
            }

            /// <summary>Adds a triangle, flipping its winding if needed so the front face agrees with the vertex normals.</summary>
            public void Triangle(int sub, int a, int b, int c)
            {
                Vector3 face = Vector3.Cross(_verts[b] - _verts[a], _verts[c] - _verts[a]);
                Vector3 n = _normals[a] + _normals[b] + _normals[c];
                List<int> t = _tris[sub];
                if (Vector3.Dot(face, n) >= 0f)
                {
                    t.Add(a); t.Add(b); t.Add(c);
                }
                else
                {
                    t.Add(a); t.Add(c); t.Add(b);
                }
            }

            private void Quad(int sub, int a, int b, int c, int d)
            {
                Triangle(sub, a, b, c);
                Triangle(sub, a, c, d);
            }

            /// <summary>A tapered tube from one point to another, with radial normals. Used for trunks and branches.</summary>
            public void Tube(Vector3 p0, Vector3 p1, float fromRadius, float toRadius, int sides)
            {
                Vector3 dir = (p1 - p0).normalized;
                Vector3 ortho = Mathf.Abs(dir.y) < 0.99f
                    ? Vector3.Cross(dir, Vector3.up).normalized
                    : Vector3.Cross(dir, Vector3.right).normalized;
                Vector3 side = Vector3.Cross(dir, ortho);
                float vEnd = Vector3.Distance(p0, p1) * 0.5f;   // bark tiles every two metres along the tube
                int start = _verts.Count;
                for (int ring = 0; ring < 2; ring++)
                {
                    Vector3 centre = ring == 0 ? p0 : p1;
                    float radius = ring == 0 ? fromRadius : toRadius;
                    float v = ring == 0 ? 0f : vEnd;
                    for (int i = 0; i <= sides; i++)
                    {
                        float t = i / (float)sides * Mathf.PI * 2f;
                        Vector3 n = ortho * Mathf.Cos(t) + side * Mathf.Sin(t);
                        Vertex(centre + n * radius, n, new Vector2(i / (float)sides, v));
                    }
                }
                int row = sides + 1;
                for (int i = 0; i < sides; i++)
                {
                    Quad(BarkSub, start + i, start + row + i, start + row + i + 1, start + i + 1);
                }
            }

            /// <summary>
            /// Two perpendicular quads that share a centre and an up axis. Seen from any side, one of them
            /// reads as a leaf card. Both quads use the same normal, so the card shades as one surface.
            /// </summary>
            public void Card(Vector3 centre, Vector3 right, Vector3 up, float halfWidth, float halfHeight, Vector3 normal, bool mirror)
            {
                Vector3 forward = Vector3.Cross(up, right).normalized;
                CardQuad(centre, right * halfWidth, up * halfHeight, normal, mirror);
                CardQuad(centre, forward * halfWidth, up * halfHeight, normal, mirror);
            }

            private void CardQuad(Vector3 c, Vector3 a, Vector3 b, Vector3 n, bool mirror)
            {
                float u0 = mirror ? 1f : 0f;
                float u1 = 1f - u0;
                int i0 = Vertex(c - a - b, n, new Vector2(u0, 0f));
                int i1 = Vertex(c + a - b, n, new Vector2(u1, 0f));
                int i2 = Vertex(c + a + b, n, new Vector2(u1, 1f));
                int i3 = Vertex(c - a + b, n, new Vector2(u0, 1f));
                Quad(FoliageSub, i0, i1, i2, i3);
            }
        }
    }
}
