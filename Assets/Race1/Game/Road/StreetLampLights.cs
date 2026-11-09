using System.Collections.Generic;
using Race1.Core;
using UnityEngine;

namespace Race1.Game
{
    /// <summary>
    /// Real light sources for the nearest street lamps at night. Lamp heads are instanced meshes, so this pool owns
    /// a fixed number of spot lights and places them on the lamps that <see cref="LampBudget"/> chooses. A light
    /// keeps its lamp while that lamp stays lit, so lights do not jump from lamp to lamp. Outside the night the
    /// lights are disabled and the lamp heads only glow, as before.
    /// </summary>
    public sealed class StreetLampLights
    {
        private const float PeakIntensity = 3.0f;
        private const float Range = 26f;
        private const float SpotAngle = 120f;
        private const float DarkThreshold = 0.02f;   // below this night amount no light is used
        private static readonly Color LampColour = new Color(1.0f, 0.82f, 0.56f);

        private readonly LampBudget _budget = new LampBudget();
        private readonly Light[] _lights;
        private readonly long[] _keyOfSlot;          // lamp key held by each light, or -1 when free
        private readonly bool[] _heldThisFrame;
        private readonly Dictionary<long, int> _slotOfKey = new Dictionary<long, int>();
        private readonly Stack<int> _freeSlots = new Stack<int>();
        private Vector3d[] _positions = new Vector3d[0];
        private long[] _keys = new long[0];

        /// <summary>Creates the pool under <paramref name="parent"/>, which should be the streamed world root.</summary>
        public StreetLampLights(Transform parent, int maxLights)
        {
            int n = Mathf.Max(0, maxLights);
            _lights = new Light[n];
            _keyOfSlot = new long[n];
            _heldThisFrame = new bool[n];
            for (int slot = 0; slot < n; slot++)
            {
                var go = new GameObject("StreetLampLight_" + slot);
                go.transform.SetParent(parent, false);
                go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);   // spot forward is down
                var light = go.AddComponent<Light>();
                light.type = LightType.Spot;
                light.color = LampColour;
                light.range = Range;
                light.spotAngle = SpotAngle;
                light.shadows = LightShadows.None;
                light.enabled = false;
                _lights[slot] = light;
                _keyOfSlot[slot] = -1;
                _freeSlots.Push(slot);
            }
        }

        /// <summary>
        /// Places the lights for this frame. <paramref name="viewer"/> and the chunks are both relative to the
        /// floating origin. <paramref name="night01"/> is 0 in full day and 1 at full night.
        /// </summary>
        public void Update(RoadChunkStreamer chunks, Vector3d origin, Vector3d viewer, float night01)
        {
            bool dark = night01 > DarkThreshold;
            int n = chunks.CollectLamps(origin, ref _positions, ref _keys);
            _budget.Update(_positions, n, viewer, dark ? _lights.Length : 0);

            // 1. Lamps that stay lit keep their light.
            for (int slot = 0; slot < _heldThisFrame.Length; slot++) _heldThisFrame[slot] = false;
            for (int i = 0; i < n; i++)
            {
                int held;
                if (_budget.IsLit(i) && _slotOfKey.TryGetValue(_keys[i], out held)) _heldThisFrame[held] = true;
            }

            // 2. Lights whose lamp went dark are freed first, so new lamps can take them in the same frame.
            for (int slot = 0; slot < _lights.Length; slot++)
            {
                if (_keyOfSlot[slot] >= 0 && !_heldThisFrame[slot])
                {
                    _slotOfKey.Remove(_keyOfSlot[slot]);
                    _keyOfSlot[slot] = -1;
                    _freeSlots.Push(slot);
                }
            }

            // 3. Newly lit lamps take a free light. The budget never lights more lamps than there are lights.
            for (int i = 0; i < n; i++)
            {
                if (!_budget.IsLit(i) || _slotOfKey.ContainsKey(_keys[i]) || _freeSlots.Count == 0) continue;
                int slot = _freeSlots.Pop();
                _slotOfKey[_keys[i]] = slot;
                _keyOfSlot[slot] = _keys[i];
            }

            // 4. Place the held lights on their lamps. A light is on only while it holds a lamp, and only in the dark.
            for (int i = 0; i < n; i++)
            {
                int slot;
                if (!_budget.IsLit(i) || !_slotOfKey.TryGetValue(_keys[i], out slot)) continue;
                _lights[slot].transform.localPosition = ToFloat(_positions[i]);
            }
            for (int slot = 0; slot < _lights.Length; slot++)
            {
                Light light = _lights[slot];
                bool on = dark && _keyOfSlot[slot] >= 0;
                if (light.enabled != on) light.enabled = on;
                if (on) light.intensity = PeakIntensity * night01;
            }
        }

        /// <summary>Releases every light. Call when the road is cleared for a new run.</summary>
        public void Clear()
        {
            for (int slot = 0; slot < _lights.Length; slot++)
            {
                _keyOfSlot[slot] = -1;
                _lights[slot].enabled = false;
            }
            _slotOfKey.Clear();
            _freeSlots.Clear();
            for (int slot = _lights.Length - 1; slot >= 0; slot--) _freeSlots.Push(slot);
        }

        private static Vector3 ToFloat(Vector3d v) => new Vector3((float)v.X, (float)v.Y, (float)v.Z);
    }
}
