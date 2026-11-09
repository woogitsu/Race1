using Race1.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Race1.Game
{
    /// <summary>
    /// Water drops on the windscreen, drawn over the cockpit view. The drop layout comes from the core and is painted
    /// once into a texture. The overlay's opacity follows the wetness, and the overlay is hidden when the glass is dry.
    /// </summary>
    public sealed class WindscreenOverlay
    {
        private const int TextureSize = 512;
        private const int DropCount = 260;
        private const float MaxOpacity = 0.85f;
        private const float HideBelow = 0.01f;

        private readonly Canvas _canvas;
        private readonly Image _image;
        private readonly Texture2D _texture;
        private readonly Sprite _sprite;

        public WindscreenOverlay(ulong seed)
        {
            var canvasGo = new GameObject("WindscreenCanvas", typeof(Canvas));
            _canvas = canvasGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 5;

            var dropsGo = new GameObject("Droplets", typeof(RectTransform), typeof(Image));
            dropsGo.transform.SetParent(canvasGo.transform, false);
            RectTransform rect = dropsGo.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            _image = dropsGo.GetComponent<Image>();
            _image.raycastTarget = false;
            _texture = Paint(seed);
            _sprite = Sprite.Create(_texture, new Rect(0f, 0f, TextureSize, TextureSize), new Vector2(0.5f, 0.5f));
            _image.sprite = _sprite;

            SetWetness(0.0);
        }

        /// <summary>Opacity of the drops from the wetness (0..1). Zero hides the overlay.</summary>
        public void SetWetness(double wetness01)
        {
            float wet = double.IsNaN(wetness01) ? 0f : Mathf.Clamp01((float)wetness01);
            float alpha = MaxOpacity * wet;
            _canvas.enabled = alpha > HideBelow;
            _image.color = new Color(1f, 1f, 1f, alpha);
        }

        /// <summary>Paints every drop as a soft disc with a brighter rim, into a transparent texture.</summary>
        private static Texture2D Paint(ulong seed)
        {
            var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
            {
                name = "WindscreenDroplets",
                wrapMode = TextureWrapMode.Clamp,
            };
            var pixels = new Color32[TextureSize * TextureSize];
            foreach (WindscreenDroplet drop in WindscreenDroplets.Generate(seed, DropCount))
            {
                PaintDrop(pixels, drop);
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static void PaintDrop(Color32[] pixels, WindscreenDroplet drop)
        {
            float cx = (float)drop.X * TextureSize;
            float cy = (float)drop.Y * TextureSize;
            float r = Mathf.Max(1f, (float)drop.Radius * TextureSize);

            int x0 = Mathf.Max(0, Mathf.FloorToInt(cx - r - 1f));
            int x1 = Mathf.Min(TextureSize - 1, Mathf.CeilToInt(cx + r + 1f));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(cy - r - 1f));
            int y1 = Mathf.Min(TextureSize - 1, Mathf.CeilToInt(cy + r + 1f));
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    float dist = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy)) / r;
                    if (dist >= 1f) continue;

                    // Clear centre, bright thin rim near the edge, soft fade outside it.
                    float rim = Mathf.Clamp01((dist - 0.7f) / 0.3f);
                    float alpha = (1f - dist) * 0.35f + rim * 0.4f;
                    byte a = (byte)Mathf.Clamp(Mathf.RoundToInt(alpha * 255f), 0, 255);
                    byte shade = (byte)Mathf.Clamp(Mathf.RoundToInt((0.85f + 0.15f * rim) * 255f), 0, 255);

                    int i = y * TextureSize + x;
                    if (a > pixels[i].a)
                    {
                        pixels[i] = new Color32(shade, shade, shade, a);
                    }
                }
            }
        }
    }
}
