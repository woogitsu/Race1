// Minimal stand-ins for Unity packages that are not on NuGet. Only the members the game code uses are declared.
// They exist purely so the semantic check can run; they are not part of the shipped game.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace UnityEngine.UI
{
    public class Text : MonoBehaviour
    {
        public Font font;
        public string text;
        public TextAnchor alignment;
        public Color color;
        public int fontSize;
        public bool raycastTarget;
    }

    public class Image : MonoBehaviour
    {
        public enum Type { Simple, Filled }
        public enum FillMethod { Horizontal }
        public Color color;
        public bool raycastTarget;
        public Type type;
        public FillMethod fillMethod;
        public float fillAmount;
    }

    public class Button : MonoBehaviour
    {
        public UnityEvent onClick = new UnityEvent();
    }

    public class CanvasScaler : MonoBehaviour
    {
        public enum ScaleMode { ConstantPixelSize, ScaleWithScreenSize, ConstantPhysicalSize }
        public ScaleMode uiScaleMode;
        public Vector2 referenceResolution;
        public float matchWidthOrHeight;
    }

    public class GraphicRaycaster : MonoBehaviour { }
}

namespace UnityEngine.EventSystems
{
    public class PointerEventData
    {
        public Vector2 position;
        public Camera pressEventCamera;
    }

    public interface IPointerDownHandler { void OnPointerDown(PointerEventData eventData); }
    public interface IPointerUpHandler { void OnPointerUp(PointerEventData eventData); }
    public interface IPointerExitHandler { void OnPointerExit(PointerEventData eventData); }
    public interface IDragHandler { void OnDrag(PointerEventData eventData); }

}

// URP 17 Volume framework (lives in the Core RP package, namespace UnityEngine.Rendering).
namespace UnityEngine.Rendering
{
    public abstract class VolumeComponent : ScriptableObject
    {
        public bool active;
    }

    public class VolumeParameter<T>
    {
        public T value;
        public bool overrideState;
    }

    public class VolumeProfile : ScriptableObject
    {
        public bool TryGet<T>(out T component) where T : VolumeComponent
        {
            component = default(T);
            return false;
        }

        public T Add<T>(bool overrides = false) where T : VolumeComponent
        {
            return null;
        }
    }

    public class Volume : MonoBehaviour
    {
        public VolumeProfile sharedProfile;
        public VolumeProfile profile;
        public bool isGlobal;
        public float weight;
        public float priority;
    }
}

namespace UnityEngine.Rendering.Universal
{
    public class UniversalRenderPipelineAsset : RenderPipelineAsset
    {
        public float renderScale;
        public int msaaSampleCount;
        public float shadowDistance;
        public override UnityEngine.Rendering.RenderPipeline CreatePipeline() { return null; }
    }

    public enum TonemappingMode { None, Neutral, ACES, Custom }

    public sealed class Bloom : VolumeComponent
    {
        public VolumeParameter<float> intensity;
    }

    public sealed class Tonemapping : VolumeComponent
    {
        public VolumeParameter<TonemappingMode> mode;
    }

    public sealed class ColorAdjustments : VolumeComponent
    {
        public VolumeParameter<float> postExposure;
        public VolumeParameter<float> contrast;
        public VolumeParameter<float> saturation;
        public VolumeParameter<Color> colorFilter;
    }

    public sealed class Vignette : VolumeComponent
    {
        public VolumeParameter<float> intensity;
        public VolumeParameter<float> smoothness;
    }

    public sealed class FilmGrain : VolumeComponent
    {
        public VolumeParameter<float> intensity;
    }
}

// Input System surface used by PlayerInputReader (names as in Input System 1.x).
namespace UnityEngine.InputSystem
{
    public class InputDevice { }

    public class InputControl<T>
    {
        public T ReadValue() { return default(T); }
    }

    public class ButtonControl : InputControl<float>
    {
        public bool isPressed;
        public bool wasPressedThisFrame;
    }

    public class StickControl : InputControl<Vector2> { }
    public class AxisControl : InputControl<float> { }
    public class Vector3Control : InputControl<Vector3> { }

    public class Gamepad : InputDevice
    {
        public static Gamepad current;
        public StickControl leftStick;
        public AxisControl leftTrigger;
        public AxisControl rightTrigger;
        public ButtonControl buttonSouth;
        public ButtonControl buttonNorth;
        public ButtonControl startButton;
        public ButtonControl leftShoulder;
        public ButtonControl rightShoulder;
    }

    public class Keyboard : InputDevice
    {
        public static Keyboard current;
        public ButtonControl aKey, dKey, wKey, sKey, eKey, qKey, cKey, spaceKey, escapeKey;
        public ButtonControl leftArrowKey, rightArrowKey, upArrowKey, downArrowKey;
    }

    public class Accelerometer : InputDevice
    {
        public static Accelerometer current;
        public Vector3Control acceleration;
    }

    public static class InputSystem
    {
        public static void EnableDevice(InputDevice device) { }
    }
}
