using System.Collections.Generic;
using Race1.Core;
using UnityEngine;

namespace Race1.Game
{
    /// <summary>
    /// Procedural car geometry, generated from the car's real dimensions. The body is a lofted superellipse:
    /// rounded ends, a cabin bump and a sloping rear. Triangles are oriented against an outward reference,
    /// so normals face out whatever the loop order. This is a stand-in for an authored model; see docs/STATUS.md.
    /// Model space: forward +Z, right +X, up +Y, origin at the centre of gravity projected to the road.
    /// </summary>
    public static class CarMeshFactory
    {
        public const int BodyPaint = 0;
        public const int BodyGlass = 1;
        public const int BodyLights = 2;

        public static Mesh BuildBody(VehicleSpec spec)
        {
            float length = (float)spec.LengthM;
            float width = (float)spec.WidthM;
            float cg = (float)spec.CgHeightM;
            float wheelR = (float)spec.WheelRadius;
            float bottom = wheelR - cg + 0.14f;
            float roof = wheelR - cg + (float)spec.HeightM;

            const int rings = 28;
            const int sides = 30;
            const float n = 3.2f;
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var paint = new List<int>();
            var glass = new List<int>();
            var lights = new List<int>();

            for (int r = 0; r <= rings; r++)
            {
                float u = r / (float)rings;
                float z = (u - 0.5f) * length;
                float endTaper = Mathf.Pow(Mathf.Sin(Mathf.PI * Mathf.Clamp(0.03f + 0.94f * u, 0f, 1f)), 0.45f);
                float halfWidth = Mathf.Max(0.22f, 0.5f * width * endTaper);
                float cabin = Mathf.Exp(-Mathf.Pow((u - 0.5f) / 0.23f, 2f));
                float frontRise = Mathf.SmoothStep(0.0f, 0.32f, u);
                float rearRise = Mathf.SmoothStep(1.0f, 0.72f, u);
                float top = bottom + (roof - bottom) * (0.34f + 0.66f * cabin) * (0.52f + 0.48f * Mathf.Min(frontRise, rearRise));

                for (int s = 0; s < sides; s++)
                {
                    float theta = s / (float)sides * Mathf.PI * 2f;
                    float c = Mathf.Cos(theta);
                    float sn = Mathf.Sin(theta);
                    float x = halfWidth * Mathf.Sign(c) * Mathf.Pow(Mathf.Abs(c), 2f / n);
                    float yNorm = Mathf.Sign(sn) * Mathf.Pow(Mathf.Abs(sn), 2f / n);
                    float y = bottom + (top - bottom) * (0.5f + 0.5f * yNorm);
                    verts.Add(new Vector3(x, y, z));
                    uvs.Add(new Vector2(s / (float)sides, u));
                }
            }

            Vector3 centre = new Vector3(0f, (bottom + roof) * 0.5f, 0f);
            for (int r = 0; r < rings; r++)
            {
                for (int s = 0; s < sides; s++)
                {
                    int a = r * sides + s;
                    int b = r * sides + (s + 1) % sides;
                    int c = a + sides;
                    int d = b + sides;
                    ClassifyQuad(verts, a, b, c, d, centre, length, paint, glass, lights);
                }
            }

            // End caps: fans to a centre point, so the body is closed.
            AddCap(verts, 0, sides, centre, paint);
            AddCap(verts, rings * sides, sides, centre, paint);

            var mesh = new Mesh { name = "CarBody" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = 3;
            mesh.SetTriangles(paint, BodyPaint);
            mesh.SetTriangles(glass, BodyGlass);
            mesh.SetTriangles(lights, BodyLights);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void ClassifyQuad(List<Vector3> verts, int a, int b, int c, int d, Vector3 centre, float length,
            List<int> paint, List<int> glass, List<int> lights)
        {
            // Pick the material from the quad centroid, then emit both triangles with that material.
            Vector3 mid = (verts[a] + verts[b] + verts[c] + verts[d]) * 0.25f;
            float zNorm = Mathf.InverseLerp(-0.5f * length, 0.5f * length, mid.z);
            float halfWidth = Mathf.Abs(mid.x);

            List<int> target = paint;
            bool endZone = zNorm < 0.05f || zNorm > 0.95f;
            bool glassZone = zNorm > 0.36f && zNorm < 0.72f && mid.y > centre.y + 0.05f;
            if (endZone && halfWidth > 0.35f && mid.y > centre.y - 0.2f && mid.y < centre.y + 0.1f)
            {
                target = lights;
            }
            else if (glassZone && halfWidth < 0.75f)
            {
                target = glass;
            }

            AddTri(verts, target, a, c, b, centre);
            AddTri(verts, target, b, c, d, centre);
        }

        private static void AddCap(List<Vector3> verts, int ringStart, int sides, Vector3 centre, List<int> paint)
        {
            int apex = verts.Count;
            Vector3 mid = Vector3.zero;
            for (int s = 0; s < sides; s++) mid += verts[ringStart + s];
            verts.Add(mid / sides);
            for (int s = 0; s < sides; s++)
            {
                int a = ringStart + s;
                int b = ringStart + (s + 1) % sides;
                AddTri(verts, paint, a, b, apex, centre);
            }
        }

        /// <summary>Adds a triangle, flipping it if its normal points towards the reference centre.</summary>
        private static void AddTri(List<Vector3> verts, List<int> list, int a, int b, int c, Vector3 reference)
        {
            Vector3 n = Vector3.Cross(verts[b] - verts[a], verts[c] - verts[a]);
            Vector3 centroid = (verts[a] + verts[b] + verts[c]) / 3f;
            if (Vector3.Dot(n, centroid - reference) < 0f)
            {
                int t = b; b = c; c = t;
            }
            list.Add(a);
            list.Add(b);
            list.Add(c);
        }

        /// <summary>Tyre and rim for one corner. Axis along X, origin at the hub. Submesh 0 = tyre, 1 = rim.</summary>
        public static Mesh BuildWheel(VehicleSpec spec)
        {
            float radius = (float)spec.WheelRadius;
            float tyreSection = radius * 0.32f;
            float major = radius - tyreSection;
            float tyreWidth = 0.22f;
            const int around = 36;
            const int section = 14;

            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tyre = new List<int>();
            var rim = new List<int>();

            for (int i = 0; i <= around; i++)
            {
                float a = i / (float)around * Mathf.PI * 2f;
                Vector3 radial = new Vector3(0f, Mathf.Cos(a), Mathf.Sin(a));
                for (int j = 0; j <= section; j++)
                {
                    float b = j / (float)section * Mathf.PI * 2f;
                    float x = tyreWidth * 0.5f * Mathf.Cos(b);
                    float rr = major + tyreSection * Mathf.Sin(b);
                    verts.Add(new Vector3(x, 0f, 0f) + radial * rr);
                    uvs.Add(new Vector2(i / (float)around, j / (float)section));
                }
            }
            int stride = section + 1;
            for (int i = 0; i < around; i++)
            {
                for (int j = 0; j < section; j++)
                {
                    int a = i * stride + j;
                    int b = a + stride;
                    Vector3 core = new Vector3(0f, Mathf.Cos((i + 0.5f) / around * Mathf.PI * 2f), Mathf.Sin((i + 0.5f) / around * Mathf.PI * 2f)) * major;
                    AddTri(verts, tyre, a, a + 1, b + 1, core);
                    AddTri(verts, tyre, a, b + 1, b, core);
                }
            }

            // Rim faces on both sides, plus a hub, with outward reference directions.
            float rimRadius = radius * 0.62f;
            foreach (float side in new[] { -1f, 1f })
            {
                float x = side * tyreWidth * 0.36f;
                int centre = verts.Count;
                verts.Add(new Vector3(x, 0f, 0f));
                uvs.Add(new Vector2(0.5f, 0.5f));
                int first = verts.Count;
                const int spokes = 24;
                for (int i = 0; i < spokes; i++)
                {
                    float a = i / (float)spokes * Mathf.PI * 2f;
                    verts.Add(new Vector3(x, Mathf.Cos(a) * rimRadius, Mathf.Sin(a) * rimRadius));
                    uvs.Add(new Vector2(0f, 0f));
                }
                for (int i = 0; i < spokes; i++)
                {
                    int a = first + i;
                    int b = first + (i + 1) % spokes;
                    AddTriOutward(verts, rim, centre, a, b, new Vector3(side, 0f, 0f));
                }
            }

            var mesh = new Mesh { name = "CarWheel" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(tyre, 0);
            mesh.SetTriangles(rim, 1);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddTriOutward(List<Vector3> verts, List<int> list, int a, int b, int c, Vector3 outward)
        {
            Vector3 n = Vector3.Cross(verts[b] - verts[a], verts[c] - verts[a]);
            if (Vector3.Dot(n, outward) < 0f)
            {
                int t = b; b = c; c = t;
            }
            list.Add(a);
            list.Add(b);
            list.Add(c);
        }
    }
}
