using System.Collections.Generic;
using Race1.Core;
using UnityEngine;

namespace Race1.Game
{
    /// <summary>
    /// Procedural car geometry built from the car's real dimensions. The body is a lofted superellipse with a
    /// tapered tail, a low pointed nose, a tumblehome roof and recesses around each wheel. Paint details (mirrors,
    /// splitter, skirts, diffuser and, on the GT, a rear wing) are closed solids. Glass and lamp areas are chosen per
    /// quad from its position and normal. Every triangle is oriented against an interior reference, so normals face
    /// out whatever the loop order. This is a stand-in for an authored model; see docs/STATUS.md.
    /// Model space: forward +Z, right +X, up +Y, origin at the centre of gravity. The road is at y = -CgHeightM, so a
    /// wheel hub sits at y = WheelRadius - CgHeightM.
    /// </summary>
    public static class CarMeshFactory
    {
        /// <summary>Submesh for body paint and paint details.</summary>
        public const int BodyPaint = 0;

        /// <summary>Submesh for windscreen, side and rear glass.</summary>
        public const int BodyGlass = 1;

        /// <summary>Submesh for headlamps and tail lamps.</summary>
        public const int BodyLights = 2;

        private const int Stations = 44;             // rings along the length
        private const int Sides = 40;                // samples around each ring
        private const int RimSides = 40;             // segments around the rim lip
        private const int SpokeCount = 5;
        private const float TyreWidth = 0.22f;       // tread width (m)
        private const float ShoulderExponent = 2.6f; // superellipse exponent above the centre line
        private const float SillExponent = 4.5f;     // flatter, boxier underbody
        private const float Tumblehome = 0.12f;      // how far the top of the body is pulled in
        private const float ArchDepth = 0.15f;       // relative inset at the centre of a wheel arch
        private const float ArchMargin = 0.10f;      // arch radius beyond the wheel radius
        private const float GlassCeiling = 0.77f;    // greenhouse glass ends at this plan position
        private const float GlassFloor = 0.64f;      // and starts this fraction of the height above the road

        // Height profiles as fractions of HeightM, against plan position p (0 = tail, 1 = nose).
        private static readonly float[] SportTopP = { 0.00f, 0.05f, 0.16f, 0.26f, 0.36f, 0.46f, 0.56f, 0.65f, 0.74f, 0.82f, 0.91f, 0.96f, 1.00f };
        private static readonly float[] SportTopH = { 0.60f, 0.63f, 0.66f, 0.84f, 0.98f, 1.00f, 0.98f, 0.86f, 0.68f, 0.60f, 0.55f, 0.50f, 0.42f };
        private static readonly float[] HatchTopP = { 0.00f, 0.05f, 0.12f, 0.20f, 0.34f, 0.50f, 0.60f, 0.68f, 0.75f, 0.82f, 0.91f, 0.96f, 1.00f };
        private static readonly float[] HatchTopH = { 0.78f, 0.80f, 0.98f, 1.00f, 1.00f, 1.00f, 0.97f, 0.84f, 0.68f, 0.60f, 0.55f, 0.50f, 0.42f };

        // Underbody height above the road in metres, against the same p.
        private static readonly float[] SillP = { 0f, 0.1f, 0.5f, 0.9f, 1f };
        private static readonly float[] SillY = { 0.20f, 0.15f, 0.13f, 0.14f, 0.20f };

        /// <summary>
        /// Builds the car body: a lofted shell with wheel-arch recesses, plus paint details. Submesh 0 is paint and
        /// details, 1 is glass, 2 is lamps.
        /// </summary>
        public static Mesh BuildBody(VehicleSpec spec)
        {
            var layout = new Layout(spec);
            var mb = new MeshBuilder(3);
            var sections = new Section[Stations + 1];
            var ringStart = new int[Stations + 1];

            // Shell vertices. Stations cluster towards both ends so the nose and tail keep their shape.
            for (int r = 0; r <= Stations; r++)
            {
                float p = 0.5f - 0.5f * Mathf.Cos(Mathf.PI * r / Stations);
                Section sec = layout.SectionAt(p);
                sections[r] = sec;
                ringStart[r] = mb.Verts.Count;
                for (int s = 0; s < Sides; s++)
                {
                    float theta = s / (float)Sides * 2f * Mathf.PI;
                    float c = Mathf.Cos(theta);
                    float sn = Mathf.Sin(theta);
                    float n = sn < 0f ? SillExponent : ShoulderExponent;
                    float t = Mathf.Sign(sn) * Mathf.Pow(Mathf.Abs(sn), 2f / n);
                    float x = sec.HalfWidth * Mathf.Sign(c) * Mathf.Pow(Mathf.Abs(c), 2f / n);
                    float y = sec.Centre + sec.RadiusY * t;
                    if (t > 0f) x *= 1f - Tumblehome * t * t;
                    x *= 1f - ArchDepth * layout.ArchInset(x, y, sec.Z);
                    mb.Add(new Vector3(x, y, sec.Z), new Vector2(s / (float)Sides, p));
                }
            }

            // Closed ends: planar fans at the first and last rings.
            AddCap(mb, ringStart[0], new Vector3(0f, 0f, -1f));
            AddCap(mb, ringStart[Stations], new Vector3(0f, 0f, 1f));

            // Shell quads, each one classified by where it sits on the car.
            for (int r = 0; r < Stations; r++)
            {
                int r0 = ringStart[r];
                int r1 = ringStart[r + 1];
                for (int s = 0; s < Sides; s++)
                {
                    int a = r0 + s;
                    int b = r0 + (s + 1) % Sides;
                    int d = r1 + (s + 1) % Sides;
                    int e = r1 + s;
                    Vector3 pa = mb.Verts[a];
                    Vector3 pb = mb.Verts[b];
                    Vector3 pd = mb.Verts[d];
                    Vector3 pe = mb.Verts[e];
                    Vector3 mid = (pa + pb + pd + pe) * 0.25f;
                    Vector3 reference = new Vector3(0f, 0.5f * (sections[r].Centre + sections[r + 1].Centre), mid.z);
                    Vector3 normal = Vector3.Cross(pb - pa, pd - pa);
                    if (Vector3.Dot(normal, mid - reference) < 0f) normal = -normal;
                    int sub = Classify(layout, mid, normal.normalized, layout.P(mid.z));
                    mb.Quad(sub, a, b, d, e, reference);
                }
            }

            // Mirrors: rounded pods on the body side at the base of the A-pillar, sitting on the surface.
            Section mirrorSection = layout.SectionAt(0.66f);
            float mirrorY = layout.Ground + 0.92f;
            float mirrorX = mirrorSection.HalfWidthAt(mirrorY);
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 centre = new Vector3(side * (mirrorX + 0.03f), mirrorY, mirrorSection.Z + 0.02f);
                AddEllipsoid(mb, BodyPaint, centre, new Vector3(0.05f, 0.04f, 0.08f), 6, 10);
            }

            // Front splitter: a thin plate under the nose, leading edge about 9 cm off the road.
            AddBox(mb, BodyPaint, new Vector3(0f, layout.Ground + 0.13f, layout.Front - 0.14f),
                new Vector3(0.52f, 0.012f, 0.19f), Quaternion.Euler(12f, 0f, 0f));

            // Side skirts: thin strips along the lower sides, between the wheel arches.
            float skirtZ0 = layout.RearAxle + 0.45f;
            float skirtZ1 = layout.FrontAxle - 0.45f;
            float skirtZ = 0.5f * (skirtZ0 + skirtZ1);
            float skirtY = layout.Ground + 0.20f;
            float skirtX = layout.SectionAt(layout.P(skirtZ)).HalfWidthAt(skirtY);
            foreach (float side in new[] { -1f, 1f })
            {
                AddBox(mb, BodyPaint, new Vector3(side * (skirtX + 0.005f), skirtY, skirtZ),
                    new Vector3(0.025f, 0.035f, 0.5f * (skirtZ1 - skirtZ0)), Quaternion.identity);
            }

            // Rear diffuser: a slightly recessed lip under the tail, with three fins.
            Section rearSection = layout.SectionAt(0.02f);
            float diffuserY = rearSection.Bottom - 0.035f;
            AddBox(mb, BodyPaint, new Vector3(0f, diffuserY, layout.Rear + 0.10f),
                new Vector3(0.55f, 0.03f, 0.08f), Quaternion.identity);
            for (int i = -1; i <= 1; i++)
            {
                AddBox(mb, BodyPaint, new Vector3(i * 0.28f, diffuserY - 0.01f, layout.Rear + 0.05f),
                    new Vector3(0.008f, 0.03f, 0.06f), Quaternion.identity);
            }

            // Rear wing on the GT only: a thin plate on two posts, slightly nose up.
            if (spec.Id == VehicleCatalog.TempestaGtx)
            {
                float wingZ = layout.Rear + 0.22f;
                Section deck = layout.SectionAt(layout.P(wingZ));
                float wingY = layout.Ground + 1.02f;
                AddBox(mb, BodyPaint, new Vector3(0f, wingY, wingZ), new Vector3(0.72f, 0.012f, 0.13f),
                    Quaternion.Euler(-3f, 0f, 0f));
                float postBottom = deck.Top - 0.03f;
                float postY = 0.5f * (postBottom + wingY);
                float postHalf = 0.5f * (wingY - postBottom);
                foreach (float side in new[] { -1f, 1f })
                {
                    AddBox(mb, BodyPaint, new Vector3(side * 0.36f, postY, wingZ - 0.02f),
                        new Vector3(0.022f, postHalf, 0.022f), Quaternion.identity);
                }
            }

            return mb.ToMesh("CarBody");
        }

        /// <summary>
        /// Tyre and rim for one corner. Axis along X, origin at the hub. Submesh 0 is the tyre, 1 is the rim.
        /// </summary>
        public static Mesh BuildWheel(VehicleSpec spec)
        {
            float radius = (float)spec.WheelRadius;
            float rimRadius = 0.64f * radius;                  // bead seat, which the rim lip sits inside
            float major = 0.5f * (radius + rimRadius);
            float sectionR = 0.5f * (radius - rimRadius);      // radial half-height of the tyre section
            float halfTread = 0.5f * TyreWidth;
            const int around = 48;
            const int section = 20;
            var mb = new MeshBuilder(2);

            // Tyre: a torus whose section is a sine profile in radius, with fuller shoulders (the sidewall bulge).
            for (int i = 0; i < around; i++)
            {
                float a = i / (float)around * 2f * Mathf.PI;
                var radial = new Vector3(0f, Mathf.Cos(a), Mathf.Sin(a));
                for (int j = 0; j < section; j++)
                {
                    float b = j / (float)section * 2f * Mathf.PI;
                    float sb = Mathf.Sin(b);
                    float x = halfTread * Mathf.Cos(b) * (1f + 0.10f * sb * sb);
                    float rr = major + sectionR * sb;
                    mb.Add(new Vector3(x, 0f, 0f) + radial * rr, new Vector2(i / (float)around, j / (float)section));
                }
            }

            for (int i = 0; i < around; i++)
            {
                float mid = (i + 0.5f) / around * 2f * Mathf.PI;
                var core = new Vector3(0f, Mathf.Cos(mid), Mathf.Sin(mid)) * major;
                int ni = (i + 1) % around;
                for (int j = 0; j < section; j++)
                {
                    int nj = (j + 1) % section;
                    int a = i * section + j;
                    int a1 = i * section + nj;
                    int b = ni * section + j;
                    int b1 = ni * section + nj;
                    mb.Quad(0, a, a1, b1, b, core);
                }
            }

            // Rim on both faces: a lip ring, a recessed hub and five thin wedge spokes.
            foreach (float side in new[] { -1f, 1f })
            {
                AddRim(mb, side);
            }

            return mb.ToMesh("CarWheel");
        }

        /// <summary>Rim for one side of the wheel. Submesh 1.</summary>
        private static void AddRim(MeshBuilder mb, float side)
        {
            const int sub = 1;
            float xFace = side * 0.045f;   // outer face of the rim
            float xLip = side * 0.012f;    // back of the rim lip
            float xSpoke = side * 0.025f;  // back face of the spokes
            float xHub = side * 0.020f;    // recessed hub face

            AddTube(mb, sub, xLip, xFace, 0.214f, RimSides, true);
            AddTube(mb, sub, xLip, xFace, 0.196f, RimSides, false);
            AddAnnulus(mb, sub, xFace, 0.196f, 0.214f, RimSides, side);
            AddAnnulus(mb, sub, xLip, 0.196f, 0.214f, RimSides, -side);

            AddTube(mb, sub, xHub, xFace, 0.085f, 24, true);
            AddDisc(mb, sub, xHub, 0.085f, 24, side);

            for (int k = 0; k < SpokeCount; k++)
            {
                float a = (k + 0.5f) / SpokeCount * 2f * Mathf.PI;
                var radial = new Vector3(0f, Mathf.Cos(a), Mathf.Sin(a));
                var tang = new Vector3(0f, -Mathf.Sin(a), Mathf.Cos(a));
                Vector3 inner = radial * 0.075f;
                Vector3 outer = radial * 0.205f;
                var corners = new[]
                {
                    inner - tang * 0.011f,
                    inner + tang * 0.011f,
                    outer + tang * 0.022f,
                    outer - tang * 0.022f,
                };
                var v = new Vector3[8];
                for (int i = 0; i < 4; i++)
                {
                    v[i] = WithX(corners[i], xFace);
                    v[i + 4] = WithX(corners[i], xSpoke);
                }
                AddHexahedron(mb, sub, v, WithX(radial * 0.14f, side * 0.035f));
            }
        }

        private static void AddCap(MeshBuilder mb, int ring, Vector3 facing)
        {
            Vector3 apex = Vector3.zero;
            for (int s = 0; s < Sides; s++) apex += mb.Verts[ring + s];
            apex /= Sides;
            int centre = mb.Add(apex);
            for (int s = 0; s < Sides; s++)
            {
                mb.TriFacing(BodyPaint, centre, ring + s, ring + (s + 1) % Sides, facing);
            }
        }

        /// <summary>
        /// Chooses the submesh for a shell quad from its centre, its outward normal and its plan position p.
        /// Lamps are checked first, then the greenhouse glass band; everything else is paint.
        /// </summary>
        private static int Classify(Layout layout, Vector3 c, Vector3 normal, float p)
        {
            float ax = Mathf.Abs(c.x);
            float h = (c.y - layout.Ground) / layout.Height;

            // Headlamp clusters at the outer corners of the nose.
            if (p > 0.955f && ax > 0.40f && ax < 0.74f && h > 0.30f && h < 0.46f) return BodyLights;

            // Tail lamps at the upper rear corners.
            if (p < 0.06f && ax > 0.42f && ax < 0.74f && h > 0.44f && h < 0.60f) return BodyLights;

            // Greenhouse: windscreen, side windows and rear window. Roof and deck are almost flat, so they stay paint.
            if (p >= layout.GlassStart && p <= GlassCeiling && h > GlassFloor && normal.y < 0.975f) return BodyGlass;

            return BodyPaint;
        }

        private static void AddBox(MeshBuilder mb, int sub, Vector3 centre, Vector3 half, Quaternion rot)
        {
            Vector3 ex = rot * new Vector3(half.x, 0f, 0f);
            Vector3 ey = rot * new Vector3(0f, half.y, 0f);
            Vector3 ez = rot * new Vector3(0f, 0f, half.z);
            var v = new Vector3[8];
            v[0] = centre - ex - ey + ez;
            v[1] = centre + ex - ey + ez;
            v[2] = centre + ex + ey + ez;
            v[3] = centre - ex + ey + ez;
            v[4] = centre - ex - ey - ez;
            v[5] = centre + ex - ey - ez;
            v[6] = centre + ex + ey - ez;
            v[7] = centre - ex + ey - ez;
            AddHexahedron(mb, sub, v, centre);
        }

        /// <summary>
        /// Adds a closed hexahedron. v[0..3] is one cyclic ring of corners and v[4..7] the matching ring at the other end.
        /// Assumes convex faces, with the reference point inside.
        /// </summary>
        private static void AddHexahedron(MeshBuilder mb, int sub, Vector3[] v, Vector3 reference)
        {
            int a = mb.Add(v[0]);
            int b = mb.Add(v[1]);
            int c = mb.Add(v[2]);
            int d = mb.Add(v[3]);
            int e = mb.Add(v[4]);
            int f = mb.Add(v[5]);
            int g = mb.Add(v[6]);
            int h = mb.Add(v[7]);
            mb.Quad(sub, a, b, c, d, reference);
            mb.Quad(sub, e, h, g, f, reference);
            mb.Quad(sub, a, e, f, b, reference);
            mb.Quad(sub, b, f, g, c, reference);
            mb.Quad(sub, c, g, h, d, reference);
            mb.Quad(sub, d, h, e, a, reference);
        }

        /// <summary>Closed ellipsoid with radii r, built from rings with pole fans.</summary>
        private static void AddEllipsoid(MeshBuilder mb, int sub, Vector3 c, Vector3 r, int rings, int sectors)
        {
            int top = mb.Add(c + new Vector3(0f, r.y, 0f));
            int first = mb.Verts.Count;
            for (int i = 1; i < rings; i++)
            {
                float phi = Mathf.PI * i / rings;
                for (int s = 0; s < sectors; s++)
                {
                    float theta = 2f * Mathf.PI * s / sectors;
                    mb.Add(c + new Vector3(r.x * Mathf.Sin(phi) * Mathf.Cos(theta),
                        r.y * Mathf.Cos(phi),
                        r.z * Mathf.Sin(phi) * Mathf.Sin(theta)));
                }
            }
            int bottom = mb.Add(c - new Vector3(0f, r.y, 0f));

            for (int s = 0; s < sectors; s++)
            {
                mb.TriAway(sub, top, first + s, first + (s + 1) % sectors, c);
            }
            for (int i = 0; i < rings - 2; i++)
            {
                for (int s = 0; s < sectors; s++)
                {
                    int a = first + i * sectors + s;
                    int b = first + i * sectors + (s + 1) % sectors;
                    int d = a + sectors;
                    int e = b + sectors;
                    mb.Quad(sub, a, b, e, d, c);
                }
            }
            int last = first + (rings - 2) * sectors;
            for (int s = 0; s < sectors; s++)
            {
                mb.TriAway(sub, bottom, last + (s + 1) % sectors, last + s, c);
            }
        }

        /// <summary>Cylindrical band about the X axis. outward = true faces away from the axis.</summary>
        private static void AddTube(MeshBuilder mb, int sub, float x0, float x1, float radius, int sides, bool outward)
        {
            float sign = outward ? 1f : -1f;
            for (int i = 0; i < sides; i++)
            {
                float a0 = i / (float)sides * 2f * Mathf.PI;
                float a1 = (i + 1) / (float)sides * 2f * Mathf.PI;
                float c0 = Mathf.Cos(a0);
                float s0 = Mathf.Sin(a0);
                float c1 = Mathf.Cos(a1);
                float s1 = Mathf.Sin(a1);
                int p0 = mb.Add(new Vector3(x0, radius * c0, radius * s0));
                int p1 = mb.Add(new Vector3(x1, radius * c0, radius * s0));
                int p2 = mb.Add(new Vector3(x1, radius * c1, radius * s1));
                int p3 = mb.Add(new Vector3(x0, radius * c1, radius * s1));
                var facing = new Vector3(0f, c0 + c1, s0 + s1) * sign;
                mb.TriFacing(sub, p0, p1, p2, facing);
                mb.TriFacing(sub, p0, p2, p3, facing);
            }
        }

        /// <summary>Flat ring between radii r0 and r1 at a fixed X. side gives the facing direction along X.</summary>
        private static void AddAnnulus(MeshBuilder mb, int sub, float x, float r0, float r1, int sides, float side)
        {
            var facing = new Vector3(side, 0f, 0f);
            for (int i = 0; i < sides; i++)
            {
                float a0 = i / (float)sides * 2f * Mathf.PI;
                float a1 = (i + 1) / (float)sides * 2f * Mathf.PI;
                float c0 = Mathf.Cos(a0);
                float s0 = Mathf.Sin(a0);
                float c1 = Mathf.Cos(a1);
                float s1 = Mathf.Sin(a1);
                int p0 = mb.Add(new Vector3(x, r0 * c0, r0 * s0));
                int p1 = mb.Add(new Vector3(x, r1 * c0, r1 * s0));
                int p2 = mb.Add(new Vector3(x, r1 * c1, r1 * s1));
                int p3 = mb.Add(new Vector3(x, r0 * c1, r0 * s1));
                mb.TriFacing(sub, p0, p1, p2, facing);
                mb.TriFacing(sub, p0, p2, p3, facing);
            }
        }

        /// <summary>Flat disc at a fixed X, centred on the axis. side gives the facing direction along X.</summary>
        private static void AddDisc(MeshBuilder mb, int sub, float x, float radius, int sides, float side)
        {
            int centre = mb.Add(new Vector3(x, 0f, 0f));
            int first = mb.Verts.Count;
            for (int i = 0; i < sides; i++)
            {
                float a = i / (float)sides * 2f * Mathf.PI;
                mb.Add(new Vector3(x, radius * Mathf.Cos(a), radius * Mathf.Sin(a)));
            }
            var facing = new Vector3(side, 0f, 0f);
            for (int i = 0; i < sides; i++)
            {
                mb.TriFacing(sub, centre, first + i, first + (i + 1) % sides, facing);
            }
        }

        private static Vector3 WithX(Vector3 v, float x) => new Vector3(x, v.y, v.z);

        private static float Smooth01(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        /// <summary>Cubic Hermite interpolation through the key points, with finite-difference tangents.</summary>
        private static float Spline(float x, float[] xs, float[] ys)
        {
            int last = xs.Length - 1;
            if (x <= xs[0]) return ys[0];
            if (x >= xs[last]) return ys[last];
            int i = 0;
            while (x > xs[i + 1]) i++;
            float h = xs[i + 1] - xs[i];
            float t = (x - xs[i]) / h;
            float t2 = t * t;
            float t3 = t2 * t;
            float m0 = Tangent(i, xs, ys) * h;
            float m1 = Tangent(i + 1, xs, ys) * h;
            return (2f * t3 - 3f * t2 + 1f) * ys[i]
                 + (t3 - 2f * t2 + t) * m0
                 + (-2f * t3 + 3f * t2) * ys[i + 1]
                 + (t3 - t2) * m1;
        }

        private static float Tangent(int k, float[] xs, float[] ys)
        {
            int last = xs.Length - 1;
            if (k == 0) return (ys[1] - ys[0]) / (xs[1] - xs[0]);
            if (k == last) return (ys[last] - ys[last - 1]) / (xs[last] - xs[last - 1]);
            return (ys[k + 1] - ys[k - 1]) / (xs[k + 1] - xs[k - 1]);
        }

        /// <summary>One transverse cross-section of the body.</summary>
        private struct Section
        {
            public float Z;
            public float HalfWidth;
            public float Bottom;
            public float Top;

            public float Centre => 0.5f * (Bottom + Top);

            public float RadiusY => 0.5f * (Top - Bottom);

            /// <summary>Half-width of the body surface at height y on this section, in metres.</summary>
            public float HalfWidthAt(float y)
            {
                float t = (y - Centre) / RadiusY;
                if (Mathf.Abs(t) >= 1f) return 0f;
                float n = t < 0f ? SillExponent : ShoulderExponent;
                float x = HalfWidth * Mathf.Pow(1f - Mathf.Pow(Mathf.Abs(t), n), 1f / n);
                if (t > 0f) x *= 1f - Tumblehome * t * t;
                return x;
            }
        }

        /// <summary>Wheel arch centre on one side. Only vertices on the matching side are recessed.</summary>
        private struct Arch
        {
            public float Side;
            public float Y;
            public float Z;
        }

        /// <summary>Body dimensions in model space, shared by the loft, the details and the classifier.</summary>
        private sealed class Layout
        {
            public readonly float HalfWidth;
            public readonly float Ground;
            public readonly float Height;
            public readonly float Rear;
            public readonly float Front;
            public readonly float FrontAxle;
            public readonly float RearAxle;
            public readonly float GlassStart;
            public readonly float ArchRadius;
            public readonly Arch[] Arches;
            public readonly float[] TopP;
            public readonly float[] TopH;

            public Layout(VehicleSpec spec)
            {
                float wheelRadius = (float)spec.WheelRadius;
                FrontAxle = (float)spec.FrontAxleX;
                RearAxle = (float)spec.RearAxleX;

                // The body spans the wheelbase plus the overhangs, split evenly, so the wheels sit inside it.
                float overhang = Mathf.Max(1f, (float)spec.LengthM - (float)spec.WheelbaseM);
                Front = FrontAxle + 0.5f * overhang;
                Rear = RearAxle - 0.5f * overhang;

                HalfWidth = 0.5f * (float)spec.WidthM;
                Ground = -(float)spec.CgHeightM;
                Height = (float)spec.HeightM;

                bool hatch = spec.Id == VehicleCatalog.KestrelSr;
                TopP = hatch ? HatchTopP : SportTopP;
                TopH = hatch ? HatchTopH : SportTopH;
                GlassStart = hatch ? 0.03f : 0.20f;

                ArchRadius = wheelRadius + ArchMargin;
                float hubY = Ground + wheelRadius;
                Arches = new[]
                {
                    new Arch { Side = -1f, Y = hubY, Z = FrontAxle },
                    new Arch { Side = 1f, Y = hubY, Z = FrontAxle },
                    new Arch { Side = -1f, Y = hubY, Z = RearAxle },
                    new Arch { Side = 1f, Y = hubY, Z = RearAxle },
                };
            }

            /// <summary>Plan position (0 at the tail, 1 at the nose) of a longitudinal coordinate.</summary>
            public float P(float z) => (z - Rear) / (Front - Rear);

            /// <summary>Half-width factor along the length: a narrow tail, full width through the cabin, a tapered nose.</summary>
            public float Plan(float p)
            {
                float tail = Mathf.Lerp(0.82f, 1f, Smooth01(p / 0.24f));
                float nose = 1f - 0.34f * Mathf.Pow(Mathf.Clamp01((p - 0.84f) / 0.16f), 1.5f);
                float tip = 1f;
                if (p > 0.955f)
                {
                    float t = (p - 0.955f) / 0.045f;
                    tip = Mathf.Max(0.12f, Mathf.Sqrt(Mathf.Max(0f, 1f - t * t)));
                }
                return tail * nose * tip;
            }

            public Section SectionAt(float p)
            {
                float bottom = Ground + Spline(p, SillP, SillY);
                float top = Ground + Height * Spline(p, TopP, TopH);
                top = Mathf.Max(top, bottom + 0.08f);
                return new Section
                {
                    Z = Rear + p * (Front - Rear),
                    HalfWidth = HalfWidth * Plan(p),
                    Bottom = bottom,
                    Top = top,
                };
            }

            /// <summary>Recess factor from 0 to 1 for a shell vertex, near a wheel arch on its own side.</summary>
            public float ArchInset(float x, float y, float z)
            {
                float ax = Mathf.Abs(x);
                float best = 0f;
                for (int i = 0; i < Arches.Length; i++)
                {
                    Arch a = Arches[i];
                    if (x * a.Side <= 0f) continue;
                    float dy = y - a.Y;
                    float dz = z - a.Z;
                    float d = Mathf.Sqrt(dy * dy + dz * dz);
                    if (d >= ArchRadius) continue;
                    float f = Smooth01((ArchRadius - d) / (0.65f * ArchRadius));
                    float g = Smooth01((ax - 0.45f) / 0.2f);
                    best = Mathf.Max(best, f * g);
                }
                return best;
            }
        }

        /// <summary>Accumulates vertices and triangles for up to several submeshes.</summary>
        private sealed class MeshBuilder
        {
            public readonly List<Vector3> Verts = new List<Vector3>();
            private readonly List<Vector2> _uvs = new List<Vector2>();
            private readonly List<int>[] _tris;

            public MeshBuilder(int submeshes)
            {
                _tris = new List<int>[submeshes];
                for (int i = 0; i < submeshes; i++) _tris[i] = new List<int>();
            }

            public int Add(Vector3 v) => Add(v, Vector2.zero);

            public int Add(Vector3 v, Vector2 uv)
            {
                Verts.Add(v);
                _uvs.Add(uv);
                return Verts.Count - 1;
            }

            /// <summary>Adds a triangle whose normal points away from an interior reference point.</summary>
            public void TriAway(int sub, int a, int b, int c, Vector3 reference)
            {
                Vector3 centroid = (Verts[a] + Verts[b] + Verts[c]) / 3f;
                TriFacing(sub, a, b, c, centroid - reference);
            }

            /// <summary>Adds a triangle whose normal points along the given facing direction.</summary>
            public void TriFacing(int sub, int a, int b, int c, Vector3 facing)
            {
                Vector3 n = Vector3.Cross(Verts[b] - Verts[a], Verts[c] - Verts[a]);
                if (Vector3.Dot(n, facing) < 0f)
                {
                    int t = b;
                    b = c;
                    c = t;
                }
                List<int> list = _tris[sub];
                list.Add(a);
                list.Add(b);
                list.Add(c);
            }

            /// <summary>Quad with corners in cyclic order, split along a-c.</summary>
            public void Quad(int sub, int a, int b, int c, int d, Vector3 reference)
            {
                TriAway(sub, a, b, c, reference);
                TriAway(sub, a, c, d, reference);
            }

            public Mesh ToMesh(string name)
            {
                var mesh = new Mesh { name = name };
                if (Verts.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.SetVertices(Verts);
                mesh.SetUVs(0, _uvs);
                mesh.subMeshCount = _tris.Length;
                for (int i = 0; i < _tris.Length; i++) mesh.SetTriangles(_tris[i], i);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
