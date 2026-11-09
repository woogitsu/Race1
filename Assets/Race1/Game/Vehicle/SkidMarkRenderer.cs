using System.Collections.Generic;
using Race1.Core;
using UnityEngine;

namespace Race1.Game
{
    /// <summary>
    /// Draws the tyre marks of the player's car on the road. The core keeps the marks; this class turns the ones near the
    /// car into a single mesh of dark quads, faded by age through the vertex alpha, rebuilt a few times a second.
    /// </summary>
    public sealed class SkidMarkRenderer
    {
        private const int Capacity = 400;
        private const double LifetimeSeconds = 6.0;
        private const double SpacingM = 0.5;
        private const float RebuildInterval = 0.1f;
        private const float HalfWidth = 0.08f;     // a tyre tread is about 16 cm wide
        private const float HalfLength = 0.225f;   // each mark is a short piece of tread along the road
        private const float LiftM = 0.03f;         // just above the asphalt, so it does not flicker
        private const double ViewBehindM = 150.0;  // the road is sampled only near the car
        private const double ViewAheadM = 150.0;
        private const float MinVisibility = 0.01f;

        private readonly SkidMarks _marks = new SkidMarks(Capacity, LifetimeSeconds, SpacingM, 4);
        private readonly Mesh _mesh;
        private readonly double[] _s = new double[4];
        private readonly double[] _t = new double[4];
        private readonly double[] _squeal = new double[4];
        private readonly List<Vector3> _verts = new List<Vector3>();
        private readonly List<Color> _colours = new List<Color>();
        private readonly List<int> _tris = new List<int>();
        private float _timer;
        private bool _hadMarks;

        public SkidMarkRenderer(Transform parent, Material material)
        {
            var go = new GameObject("SkidMarks");
            go.transform.SetParent(parent, false);
            var filter = go.AddComponent<MeshFilter>();
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            _mesh = new Mesh { name = "SkidMarks" };
            _mesh.MarkDynamic();
            filter.sharedMesh = _mesh;
        }

        public void Clear()
        {
            _marks.Clear();
            _hadMarks = false;
            _mesh.Clear();
        }

        /// <summary>
        /// Feeds the four tyres of the car to the marks and rebuilds the mesh when it is due. The corners follow the
        /// telemetry order: front left, front right, rear left, rear right. Each tyre uses the car's arc length, so the
        /// longitudinal offset between the axles is not modelled.
        /// </summary>
        public void Tick(float dt, RoadTrack road, double carS, double carT, VehicleTelemetry tm, VehicleSpec spec, Vector3d origin)
        {
            double halfFront = spec.TrackFrontM * 0.5;
            double halfRear = spec.TrackRearM * 0.5;
            _s[0] = _s[1] = _s[2] = _s[3] = carS;
            _t[0] = carT - halfFront;
            _t[1] = carT + halfFront;
            _t[2] = carT - halfRear;
            _t[3] = carT + halfRear;
            for (int i = 0; i < 4; i++)
            {
                _squeal[i] = AmbientSoundModel.SquealLevel(tm.SlipAngle[i], tm.SlipRatio[i]);
            }
            _marks.Update(dt, _s, _t, _squeal);

            _timer -= dt;
            if (_timer > 0f) return;
            _timer = RebuildInterval;

            if (_marks.Count == 0 && !_hadMarks) return;
            Rebuild(road, carS, origin);
            _hadMarks = _marks.Count > 0;
        }

        private void Rebuild(RoadTrack road, double carS, Vector3d origin)
        {
            _verts.Clear();
            _colours.Clear();
            _tris.Clear();

            for (int i = 0; i < _marks.Count; i++)
            {
                SkidMark mark = _marks[i];
                if (mark.S < carS - ViewBehindM || mark.S > carS + ViewAheadM) continue;

                float visibility = (float)SkidMarkModel.Visibility(mark, LifetimeSeconds);
                if (visibility < MinVisibility) continue;

                RoadSample sample = road.Sample(mark.S);
                Vector3d centre = sample.PointAt(mark.T) + sample.Up * LiftM;
                Vector3d along = sample.Forward * HalfLength;
                Vector3d across = sample.Right * HalfWidth;

                int b = _verts.Count;
                Vector3 v0 = Local(centre - along - across, origin);
                Vector3 v1 = Local(centre - along + across, origin);
                Vector3 v2 = Local(centre + along + across, origin);
                Vector3 v3 = Local(centre + along - across, origin);
                _verts.Add(v0);
                _verts.Add(v1);
                _verts.Add(v2);
                _verts.Add(v3);

                var dark = new Color(0.02f, 0.02f, 0.02f, visibility);
                _colours.Add(dark);
                _colours.Add(dark);
                _colours.Add(dark);
                _colours.Add(dark);

                // Wind the quad so that it faces up. The road is flat across one mark, so one triangle decides it.
                bool up = Vector3.Cross(v1 - v0, v2 - v0).y >= 0f;
                if (up)
                {
                    _tris.Add(b); _tris.Add(b + 1); _tris.Add(b + 2);
                    _tris.Add(b); _tris.Add(b + 2); _tris.Add(b + 3);
                }
                else
                {
                    _tris.Add(b); _tris.Add(b + 2); _tris.Add(b + 1);
                    _tris.Add(b); _tris.Add(b + 3); _tris.Add(b + 2);
                }
            }

            _mesh.Clear();
            if (_verts.Count == 0) return;
            _mesh.SetVertices(_verts);
            _mesh.SetColors(_colours);
            _mesh.SetTriangles(_tris, 0);
            _mesh.RecalculateBounds();
        }

        private static Vector3 Local(Vector3d p, Vector3d origin)
        {
            return new Vector3((float)(p.X - origin.X), (float)(p.Y - origin.Y), (float)(p.Z - origin.Z));
        }
    }
}
