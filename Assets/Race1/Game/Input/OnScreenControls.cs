using Race1.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Race1.Game
{
    /// <summary>
    /// Touch controls built in code: a steering pad, throttle and brake, handbrake, gear buttons, camera and pause.
    /// Each control reports through a simple value or an edge, which the input reader turns into driving input.
    /// </summary>
    public sealed class OnScreenControls
    {
        public sealed class SteeringPad : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler, IPointerExitHandler
        {
            public float Value;
            private RectTransform _rect;
            private bool _active;

            public void Init(RectTransform rect) => _rect = rect;

            public void OnPointerDown(PointerEventData e) { _active = true; Update(e); }
            public void OnDrag(PointerEventData e) { if (_active) Update(e); }
            public void OnPointerUp(PointerEventData e) { Release(); }
            public void OnPointerExit(PointerEventData e) { Release(); }

            private void Update(PointerEventData e)
            {
                if (_rect == null) return;
                Vector2 local;
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_rect, e.position, e.pressEventCamera, out local))
                {
                    float half = Mathf.Max(1f, _rect.rect.width * 0.5f);
                    Value = Mathf.Clamp(local.x / half, -1f, 1f);
                }
            }

            private void Release()
            {
                _active = false;
                Value = 0f;
            }
        }

        public sealed class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
        {
            public bool Held;
            public bool EdgePending;

            public void OnPointerDown(PointerEventData e)
            {
                Held = true;
                EdgePending = true;
            }

            public void OnPointerUp(PointerEventData e) { Held = false; }
            public void OnPointerExit(PointerEventData e) { Held = false; }

            /// <summary>Returns true once per press.</summary>
            public bool ConsumeEdge()
            {
                bool e = EdgePending;
                EdgePending = false;
                return e;
            }
        }

        public SteeringPad Steering { get; private set; }
        public HoldButton Throttle { get; private set; }
        public HoldButton Brake { get; private set; }
        public HoldButton Handbrake { get; private set; }
        public HoldButton GearUp { get; private set; }
        public HoldButton GearDown { get; private set; }
        public HoldButton Camera { get; private set; }
        public HoldButton Pause { get; private set; }

        public void Build(RectTransform canvasRect, DisplayLayoutResult layout)
        {
            Roots.Clear();
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var steerGo = Panel(canvasRect, layout.SteeringArea, new Color(1f, 1f, 1f, 0.06f), "SteeringPad");
            Steering = steerGo.AddComponent<SteeringPad>();
            Steering.Init(steerGo.GetComponent<RectTransform>());
            Label(steerGo.transform, "steer", font, 0.4f);

            RectN p = layout.PedalsArea;
            Throttle = MakeButton(canvasRect, new RectN(p.X + p.W * 0.52, p.Y, p.W * 0.46, p.H * 0.62), "GAS", new Color(0.1f, 0.55f, 0.2f, 0.45f), font);
            Brake = MakeButton(canvasRect, new RectN(p.X, p.Y + p.H * 0.45, p.W * 0.46, p.H * 0.55), "BRAKE", new Color(0.6f, 0.1f, 0.1f, 0.45f), font);
            Handbrake = MakeButton(canvasRect, new RectN(p.X, p.Y, p.W * 0.46, p.H * 0.4), "HB", new Color(0.3f, 0.3f, 0.3f, 0.45f), font);

            Handbrake.transform.SetAsLastSibling();
            GearUp = MakeButton(canvasRect, new RectN(layout.HudTopRight.X, layout.HudTopRight.Bottom + 0.01, 0.07, 0.08), "+", new Color(0.2f, 0.2f, 0.2f, 0.5f), font);
            GearDown = MakeButton(canvasRect, new RectN(layout.HudTopRight.X + 0.08, layout.HudTopRight.Bottom + 0.01, 0.07, 0.08), "-", new Color(0.2f, 0.2f, 0.2f, 0.5f), font);
            Camera = MakeButton(canvasRect, new RectN(layout.HudTopRight.X, layout.HudTopRight.Bottom + 0.1, 0.15, 0.07), "VIEW", new Color(0.2f, 0.2f, 0.2f, 0.5f), font);
            Pause = MakeButton(canvasRect, new RectN(layout.HudTopRight.X + layout.HudTopRight.W - 0.1, layout.HudTopRight.Y, 0.1, 0.07), "II", new Color(0.2f, 0.2f, 0.2f, 0.5f), font);
        }

        public void SetVisible(bool visible)
        {
            foreach (Transform t in Roots) t.gameObject.SetActive(visible);
        }

        private readonly System.Collections.Generic.List<Transform> Roots = new System.Collections.Generic.List<Transform>();

        private GameObject Panel(RectTransform parent, RectN r, Color colour, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = colour;
            go.GetComponent<Image>().raycastTarget = true;
            Place(go.GetComponent<RectTransform>(), r);
            Roots.Add(go.transform);
            return go;
        }

        private HoldButton MakeButton(RectTransform parent, RectN r, string text, Color colour, Font font)
        {
            GameObject go = Panel(parent, r, colour, "Button_" + text);
            var hb = go.AddComponent<HoldButton>();
            Label(go.transform, text, font, 0.5f);
            return hb;
        }

        private static void Label(Transform parent, string text, Font font, float size)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = font;
            t.text = text;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = new Color(1f, 1f, 1f, 0.7f);
            t.fontSize = Mathf.RoundToInt(28 * size / 0.5f);
            t.raycastTarget = false;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        /// <summary>RectN uses a top-left origin in 0..1; uGUI anchors use bottom-left. Convert here.</summary>
        public static void Place(RectTransform rt, RectN r)
        {
            rt.anchorMin = new Vector2((float)r.X, (float)(1.0 - r.Bottom));
            rt.anchorMax = new Vector2((float)r.Right, (float)(1.0 - r.Y));
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
