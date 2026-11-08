using Race1.Core;
using UnityEngine;

namespace Race1.Game
{
    /// <summary>
    /// Visual car. Position comes from road-relative physics: the car sits at (s, t) on the road, its heading is
    /// road heading plus heading error, and its up vector is the road normal. Suspension compression drives body
    /// pitch and roll; wheel speed drives spin; steering angle drives the front wheels. Lamps follow the body and
    /// respond to night (<see cref="SetNight"/>) and brake input.
    /// </summary>
    public sealed class CarView : MonoBehaviour
    {
        // Glass, tyre and rim materials carry no per-car state, so every car shares one instance of each.
        private static Material _glassMaterial;
        private static Material _tyreMaterial;
        private static Material _rimMaterial;

        private Transform _body;
        private readonly Transform[] _steer = new Transform[4];
        private readonly Transform[] _spin = new Transform[4];
        private readonly double[] _spinAngle = new double[4];
        private Material _paint;
        private Material _lensMaterial;
        private CarLights _carLights;
        private VehicleSpec _spec;
        private float _brakeGlow;
        private float _night;

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

        /// <summary>
        /// Night amount for the lamps: 0 is full day, 1 is full night. Headlights switch on with it, and the
        /// value holds until it is set again. <see cref="Place"/> has no night input, so the owner calls this.
        /// AI cars (null telemetry) keep their last value, so they need this call too to show headlights at night.
        /// </summary>
        public void SetNight(float night01)
        {
            _night = Mathf.Clamp01(night01);
            if (_carLights != null) _carLights.SetNight(_night);
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

            // Submesh contract from CarMeshFactory: 0 = paint, 1 = glass, 2 = lights.
            _paint = CarPaint.CreateBodyPaint(paint);
            _lensMaterial = RenderMaterials.Emissive(new Color(0.95f, 0.95f, 0.9f), new Color(0.5f, 0.5f, 0.45f));
            renderer.sharedMaterials = new[] { _paint, GlassMaterial, _lensMaterial };

            _carLights = new CarLights(_body, (float)spec.LengthM, (float)spec.WidthM, (float)spec.HeightM);

            Mesh wheelMesh = CarMeshFactory.BuildWheel(spec);
            float hubY = (float)(spec.WheelRadius - spec.CgHeightM);
            for (int i = 0; i < 4; i++)
            {
                bool front = i < 2;
                float track = (float)(front ? spec.TrackFrontM : spec.TrackRearM);
                float x = (i & 1) == 0 ? -track * 0.5f : track * 0.5f;
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
                wr.sharedMaterials = new[] { TyreMaterial, RimMaterial };
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
            if (_lensMaterial != null && _lensMaterial.HasProperty("_EmissionColor"))
            {
                _lensMaterial.SetColor("_EmissionColor", Color.Lerp(new Color(0.5f, 0.5f, 0.45f), new Color(2.4f, 0.2f, 0.1f), _brakeGlow));
            }
            if (_carLights != null)
            {
                _carLights.SetBrake(_brakeGlow);
            }
        }

        public static Vector3 ToFloat(Vector3d v) => new Vector3((float)v.X, (float)v.Y, (float)v.Z);

        private static Material GlassMaterial
        {
            get
            {
                if (_glassMaterial == null) _glassMaterial = CarPaint.CreateGlass();
                return _glassMaterial;
            }
        }

        private static Material TyreMaterial
        {
            get
            {
                if (_tyreMaterial == null) _tyreMaterial = CarPaint.CreateTyre();
                return _tyreMaterial;
            }
        }

        private static Material RimMaterial
        {
            get
            {
                if (_rimMaterial == null) _rimMaterial = CarPaint.CreateRim();
                return _rimMaterial;
            }
        }
    }
}
