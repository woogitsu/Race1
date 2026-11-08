using System;

namespace Race1.Core
{
    /// <summary>Screen rectangle in normalised 0..1 coordinates of the safe area (origin top-left).</summary>
    public struct RectN
    {
        public double X;
        public double Y;
        public double W;
        public double H;

        public RectN(double x, double y, double w, double h)
        {
            X = x;
            Y = y;
            W = w;
            H = h;
        }

        public double Right => X + W;
        public double Bottom => Y + H;
    }

    public enum ScreenClass
    {
        Phone = 0,       // ordinary phone or small display
        Cover = 1,       // narrow outer display, very wide aspect in landscape
        Unfolded = 2,    // large inner display, near square in landscape
        Tablet = 3,      // large display with a conventional aspect
    }

    public struct DisplayMetrics
    {
        public int WidthPx;
        public int HeightPx;
        public double Dpi;
        public RectN SafeArea;   // normalised safe area inside the window (cutouts, rounded corners, folds)
    }

    public struct DisplayLayoutResult
    {
        public ScreenClass Class;
        public double UiScale;           // multiplier for HUD and controls; 1 = 1080 px short side
        public RectN GameViewport;       // where the 3D view is rendered
        public RectN SteeringArea;       // on-screen steering control
        public RectN PedalsArea;         // throttle and brake controls
        public RectN HudTopLeft;         // speed and gear
        public RectN HudTopRight;        // position, time, weather
        public bool HudOnSides;          // true when the centre of the view must stay clear
        public double RenderResolutionScale;  // multiplier applied to native pixels before the render scale
    }

    /// <summary>
    /// Chooses HUD and control placement for the screen the game is running on. Nothing here assumes a
    /// particular device: the classes are based on size and aspect ratio, so Fold cover and inner screens,
    /// phones and tablets all work from the same code.
    /// </summary>
    public static class DisplayLayout
    {
        public static ScreenClass Classify(int widthPx, int heightPx)
        {
            int shortSide = Math.Min(widthPx, heightPx);
            int longSide = Math.Max(widthPx, heightPx);
            double aspect = shortSide <= 0 ? 1.0 : (double)longSide / shortSide;

            if (aspect >= 2.0 && shortSide <= 1200) return ScreenClass.Cover;
            if (shortSide >= 1500) return aspect < 1.5 ? ScreenClass.Unfolded : ScreenClass.Tablet;
            return ScreenClass.Phone;
        }

        public static DisplayLayoutResult Compute(DisplayMetrics info)
        {
            int w = Math.Max(1, info.WidthPx);
            int h = Math.Max(1, info.HeightPx);
            bool landscape = w >= h;
            ScreenClass cls = Classify(w, h);
            double shortSide = Math.Min(w, h);

            var result = new DisplayLayoutResult
            {
                Class = cls,
                UiScale = Mathx.Clamp(shortSide / 1080.0, 0.6, 2.0),
                RenderResolutionScale = 1.0,
            };

            RectN safe = info.SafeArea;
            if (safe.W <= 0 || safe.H <= 0) safe = new RectN(0, 0, 1, 1);
            result.GameViewport = safe;

            // Controls: thumb reach matters more than screen size. Keep them inside the safe area.
            double controlSize = 0.22 * Math.Min(1.0, result.UiScale);
            switch (cls)
            {
                case ScreenClass.Unfolded:
                case ScreenClass.Tablet:
                    // Wide, tall enough: steering wheel bottom-left, pedals bottom-right, HUD in top corners.
                    result.HudOnSides = true;
                    result.SteeringArea = new RectN(safe.X + 0.03 * safe.W, safe.Bottom - controlSize * 1.05 * safe.H * 1.4, controlSize * safe.W * 1.3, controlSize * safe.H * 1.4);
                    result.PedalsArea = new RectN(safe.Right - 0.26 * safe.W, safe.Bottom - 0.42 * safe.H, 0.23 * safe.W, 0.38 * safe.H);
                    result.HudTopLeft = new RectN(safe.X + 0.03 * safe.W, safe.Y + 0.04 * safe.H, 0.24 * safe.W, 0.2 * safe.H);
                    result.HudTopRight = new RectN(safe.Right - 0.27 * safe.W, safe.Y + 0.04 * safe.H, 0.24 * safe.W, 0.18 * safe.H);
                    break;

                case ScreenClass.Cover:
                    // Short and very wide: controls get the bottom edge, HUD is compact at the top.
                    result.HudOnSides = false;
                    result.SteeringArea = new RectN(safe.X + 0.02 * safe.W, safe.Bottom - 0.5 * safe.H, 0.3 * safe.W, 0.46 * safe.H);
                    result.PedalsArea = new RectN(safe.Right - 0.3 * safe.W, safe.Bottom - 0.5 * safe.H, 0.28 * safe.W, 0.46 * safe.H);
                    result.HudTopLeft = new RectN(safe.X + 0.02 * safe.W, safe.Y + 0.03 * safe.H, 0.2 * safe.W, 0.22 * safe.H);
                    result.HudTopRight = new RectN(safe.Right - 0.22 * safe.W, safe.Y + 0.03 * safe.H, 0.2 * safe.W, 0.2 * safe.H);
                    result.RenderResolutionScale = 0.75; // cover screen is small in pixels; render less
                    break;

                default:
                    result.HudOnSides = false;
                    result.SteeringArea = new RectN(safe.X + 0.03 * safe.W, safe.Bottom - 0.5 * safe.H, 0.34 * safe.W, 0.46 * safe.H);
                    result.PedalsArea = new RectN(safe.Right - 0.34 * safe.W, safe.Bottom - 0.5 * safe.H, 0.31 * safe.W, 0.46 * safe.H);
                    result.HudTopLeft = new RectN(safe.X + 0.03 * safe.W, safe.Y + 0.04 * safe.H, 0.3 * safe.W, 0.18 * safe.H);
                    result.HudTopRight = new RectN(safe.Right - 0.33 * safe.W, safe.Y + 0.04 * safe.H, 0.3 * safe.W, 0.16 * safe.H);
                    break;
            }

            if (!landscape)
            {
                // Racing is landscape-only. In portrait the caller shows a rotate hint instead of a HUD.
                result.HudOnSides = false;
            }
            return result;
        }
    }
}
