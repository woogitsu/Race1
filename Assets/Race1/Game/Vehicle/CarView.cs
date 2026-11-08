using Race1.Core;
using UnityEngine;

namespace Race1.Game
{
    /// <summary>
    /// Visual car. Position comes from road-relative physics: the car sits at (s, t) on the road, its heading is
    /// road heading plus heading error, and its up vector is the road normal. Suspension compression drives body
    /// pitch and roll; wheel speed drives spin; steering angle drives the front wheels.
    /// </summary>
    public sealed class CarView : MonoBehaviour
    {
        private Transform _body;
        private readonly Transform[] _steer = new Transform[4];
        private readonly Transform[] _spin = new Transform[4];
        private readonly double[] _spinAngle = new double[4];
        private Material _paint;
        private Material _lights;
        private VehicleSpec _spec;
        private float _brakeGlow;

        public VehicleSpec Spec => _spec;
        public Vector3d WorldPosition { get; private set; }
        public Vector3d WorldForward { get; private set; }
        public Vector3d WorldUp { get; private set; }

        public static CarView Create(Transform parent, VehicleSpec spec, Color paint, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<CarView>();
            view.Build(spec, paint);
            return view;
        }

        private void Build(VehicleSpec spec, Color paint)
        {
            _spec = spec;
            var bodyGo = new GameObject("Body");
            _body = bodyGo.transform;
            _body.SetParent(transform, false);

            var shell = new GameObject("Shell");
            shell.transform.SetParent(_body, false);
            var filter = shell.AddComponent<MeshFilter>();
            filter.sharedMesh = CarMeshFactory.BuildBody(spec);
            var renderer = shell.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

            _paint = RenderMaterials.Create(paint, 0.7f, 0.85f, null);
            Material glass = RenderMaterials.Create(new Color(0.04f, 0.06f, 0.08f), 0f, 0.95f, null);
            _lights = RenderMaterials.Emissive(new Color(0.95f, 0.95f, 0.9f), new Color(0.9f, 0.9f, 0.85f));
            renderer.sharedMaterials = new[] { _paint, glass, _lights };

            Mesh wheelMesh = CarMeshFactory.BuildWheel(spec);
            Material tyre = RenderMaterials.Create(new Color(0.03f, 0.03f, 0.035f), 0f, 0.35f, null);
            Material rim = RenderMaterials.Create(new Color(0.72f, 0.74f, 0.77f), 0.9f, 0.7f, null);

            float hubY = (float)(spec.WheelRadius - spec.CgHeightM);
            for (int i = 0; i < 4; i++)
            {
                bool front = i < 2;
                float x = (i & 1) == 0 ? -(float)spec.TrackFrontM * 0.5f : (float)spec.TrackFrontM * 0.5f;
                if (!front) x = (i & 1) == 0 ? -(float)spec.TrackRearM * 0.5f : (float)spec.TrackRearM * 0.5f;
                float z = front ? (float)spec.FrontAxleX : (float)spec.RearAxleX;

                var steerGo = new GameObject($"Steer{i}");
                _steer[i] = steerGo.transform;
                _steer[i].SetParent(transform, false);
                _steer[i].localPosition = new Vector3(x, hubY, z);

                var spinGo = new GameObject($"Wheel{i}");
                _spin[i] = spinGo.transform;
                _spin[i].SetParent(_steer[i], false);
                var wf = spinGo.AddComponent<MeshFilter>();
                wf.sharedMesh = wheelMesh;
                var wr = spinGo.AddComponent<MeshRenderer>();
                wr.sharedMaterials = new[] { tyre, rim };
                wr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
        }

        /// <summary>Places the car on the road and updates its parts. Telemetry may be null for AI cars.</summary>
        public void Place(RoadSample sample, double t, double headingError, Vector3d origin, VehicleTelemetry telemetry, float dt)
        {
            Vector3d forward = sample.Forward * Mathx.Clamp(System.Math.Cos(headingError), -1, 1)
                               + sample.Right * System.Math.Sin(headingError);
            Vector3d up = sample.Up;
            Vector3d position = sample.PointAt(t);
            WorldPosition = position;
            WorldForward = forward;
            WorldUp = up;

            transform.localPosition = ToFloat(position - origin);
            Vector3 fwd = ToFloat(forward);
            Vector3 upF = ToFloat(up);
            if (fwd.sqrMagnitude > 1e-6f && upF.sqrMagnitude > 1e-6f)
            {
                transform.localRotation = Quaternion.LookRotation(fwd.normalized, upF.normalized);
            }

            if (telemetry != null)
            {
                ApplyTelemetry(telemetry, dt);
            }
        }

        private void ApplyTelemetry(VehicleTelemetry tm, float dt)
        {
            double c0 = tm.Compression[0], c1 = tm.Compression[1], c2 = tm.Compression[2], c3 = tm.Compression[3];
            double front = 0.5 * (c0 + c1);
            double rear = 0.5 * (c2 + c3);
            double left = 0.5 * (c0 + c2);
            double right = 0.5 * (c1 + c3);
            float pitch = (float)(System.Math.Atan2(front - rear, _spec.WheelbaseM) * Mathx.Rad2Deg);
            float roll = (float)(System.Math.Atan2(left - right, _spec.TrackFrontM) * Mathx.Rad2Deg);
            pitch = Mathf.Clamp(pitch * 2.0f, -6f, 6f);
            roll = Mathf.Clamp(roll * 2.0f, -6f, 6f);
            float drop = (float)(-0.5 * (front + rear));
            _body.localPosition = new Vector3(0f, drop, 0f);
            _body.localRotation = Quaternion.Euler(pitch, 0f, roll);

            float steerDeg = (float)(tm.SteerAngleRad * Mathx.Rad2Deg);
            for (int i = 0; i < 4; i++)
            {
                _spinAngle[i] = System.Math.IEEERemainder(_spinAngle[i] + tm.WheelSpeed[i] * dt, 2.0 * System.Math.PI);
                float spinDeg = (float)(_spinAngle[i] * Mathx.Rad2Deg);
                _spin[i].localRotation = Quaternion.Euler(spinDeg, 0f, 0f);
                _steer[i].localRotation = i < 2 ? Quaternion.Euler(0f, steerDeg, 0f) : Quaternion.identity;
            }

            // Brake lights glow with the pedal: a fast attack, slower release.
            float target = (float)Mathf.Clamp01((float)tm.Brake);
            _brakeGlow = Mathf.MoveTowards(_brakeGlow, target, dt * (target > _brakeGlow ? 8f : 2f));
            if (_lights != null && _lights.HasProperty("_EmissionColor"))
            {
                _lights.SetColor("_EmissionColor", Color.Lerp(new Color(0.5f, 0.5f, 0.45f), new Color(2.4f, 0.2f, 0.1f), _brakeGlow));
            }
        }

        public static Vector3 ToFloat(Vector3d v) => new Vector3((float)v.X, (float)v.Y, (float)v.Z);
    }
}
