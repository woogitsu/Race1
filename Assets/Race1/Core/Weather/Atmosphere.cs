using System;

namespace Race1.Core
{
    /// <summary>Linear-space RGB colour. Channels are not clamped by the type itself.</summary>
    public struct LinearRgb
    {
        /// <summary>Red channel.</summary>
        public double R;

        /// <summary>Green channel.</summary>
        public double G;

        /// <summary>Blue channel.</summary>
        public double B;

        /// <summary>Creates a colour from its three channels.</summary>
        public LinearRgb(double r, double g, double b)
        {
            R = r;
            G = g;
            B = b;
        }

        /// <summary>Linear interpolation from a to b. t is clamped to 0..1; NaN gives a.</summary>
        public static LinearRgb Lerp(LinearRgb a, LinearRgb b, double t)
        {
            double u = !(t > 0.0) ? 0.0 : (t >= 1.0 ? 1.0 : t);
            return new LinearRgb(
                a.R + (b.R - a.R) * u,
                a.G + (b.G - a.G) * u,
                a.B + (b.B - a.B) * u);
        }

        /// <summary>Scales every channel by s.</summary>
        public static LinearRgb operator *(LinearRgb c, double s)
        {
            return new LinearRgb(c.R * s, c.G * s, c.B * s);
        }

        /// <summary>Channel-wise sum of two colours.</summary>
        public static LinearRgb operator +(LinearRgb a, LinearRgb b)
        {
            return new LinearRgb(a.R + b.R, a.G + b.G, a.B + b.B);
        }
    }

    /// <summary>
    /// Sky, sun, fog and cloud parameters for one moment of the day and one weather state. Colours are
    /// linear RGB in 0..2; the scalar fields are in 0..1.
    /// </summary>
    public struct SkyLook
    {
        /// <summary>Colour of the sky straight overhead.</summary>
        public LinearRgb Zenith;

        /// <summary>Colour of the sky at the horizon. Brighter and paler than the zenith, warm at low sun.</summary>
        public LinearRgb Horizon;

        /// <summary>Dark earth-like colour for the ground below the horizon. Reduced at night.</summary>
        public LinearRgb Ground;

        /// <summary>Normalised sun transmittance (largest channel is 1). White at noon, orange at the horizon.</summary>
        public LinearRgb SunTint;

        /// <summary>Grey-white haze colour, warm at low sun. Fog pulls the horizon towards it.</summary>
        public LinearRgb FogTint;

        /// <summary>Sky-averaged ambient light, scaled by sun intensity, with a small night floor.</summary>
        public LinearRgb Ambient;

        /// <summary>Direct sun strength: 1 when the sun is high and clear, 0 below the horizon.</summary>
        public double SunIntensity;

        /// <summary>Star visibility: 1 below about -12 degrees, 0 above about -2 degrees.</summary>
        public double StarVisibility;

        /// <summary>Cloud coverage, including the contribution of rain.</summary>
        public double CloudCoverage;

        /// <summary>Cloud density, rising with coverage.</summary>
        public double CloudDensity;

        /// <summary>Cloud brightness: bright on a sunny day, dim at night and in storms.</summary>
        public double CloudBrightness;

        /// <summary>Sun glow strength: large at golden hour, small at noon. Apply it near the sun's direction.</summary>
        public double SunGlow;
    }

    /// <summary>
    /// Stylised, physically motivated sky model in pure C#. It turns the sun elevation and the weather into
    /// the colours that light and tint the sky, horizon, fog and sun, plus cloud and star parameters.
    /// The sun colour comes from Rayleigh-style transmittance over a relative air mass. Every output is
    /// finite and clamped to its documented range.
    /// </summary>
    public static class Atmosphere
    {
        private const double DegToRad = Math.PI / 180.0;
        private const double RadToDeg = 180.0 / Math.PI;
        private const double HalfPi = Math.PI / 2.0;

        // Rayleigh scattering relative coefficients per channel (blue scatters most).
        private const double RayleighR = 0.65;
        private const double RayleighG = 1.0;
        private const double RayleighB = 2.1;

        // Optical depth per unit air mass, per unit of turbidity.
        private const double TurbidityScale = 0.015;

        private static readonly LinearRgb DayZenithBlue = new LinearRgb(0.16, 0.38, 0.92);
        private static readonly LinearRgb NightZenith = new LinearRgb(0.004, 0.008, 0.02);
        private static readonly LinearRgb DayHorizon = new LinearRgb(0.72, 0.82, 0.95);
        private static readonly LinearRgb NightHorizon = new LinearRgb(0.012, 0.016, 0.03);
        private static readonly LinearRgb WarmHorizon = new LinearRgb(1.0, 0.56, 0.30);
        private static readonly LinearRgb DayFog = new LinearRgb(0.92, 0.93, 0.95);
        private static readonly LinearRgb NightFog = new LinearRgb(0.10, 0.105, 0.12);
        private static readonly LinearRgb WarmFog = new LinearRgb(1.0, 0.72, 0.50);
        private static readonly LinearRgb Earth = new LinearRgb(0.075, 0.062, 0.045);
        private static readonly LinearRgb AmbientFloor = new LinearRgb(0.02, 0.022, 0.03);

        // Sine of -12 and -2 degrees: stars are fully visible below the first and hidden above the second.
        private static readonly double StarFullSin = Math.Sin(-12.0 * DegToRad);
        private static readonly double StarNoneSin = Math.Sin(-2.0 * DegToRad);

        /// <summary>
        /// Evaluates the sky. sunElevationRad is the sun elevation in radians; it is clamped to -pi/2..pi/2
        /// (NaN is treated as 0). cloudCover01, rain01 and fog01 are clamped to 0..1 (NaN is treated as 0).
        /// turbidity is clamped to 1..10 (NaN is treated as 2.5).
        /// </summary>
        public static SkyLook Evaluate(double sunElevationRad, double cloudCover01, double rain01, double fog01, double turbidity)
        {
            double elevation = double.IsNaN(sunElevationRad) ? 0.0 : Clamp(sunElevationRad, -HalfPi, HalfPi);
            double cloud = Clamp01(cloudCover01);
            double rain = Clamp01(rain01);
            double fog = Clamp01(fog01);
            double turb = double.IsNaN(turbidity) ? 2.5 : Clamp(turbidity, 1.0, 10.0);
            double haze = (turb - 1.0) / 9.0;
            double sinE = Math.Sin(elevation);

            // Light state: day, dusk afterglow, warmth near the horizon, and total coverage.
            double daylight = SmoothStep(-0.20, 0.12, sinE);
            double warmth = Warmth(sinE);
            double twilight = Clamp01(daylight + 0.6 * warmth);
            double coverage = Clamp01(cloud + 0.35 * rain);

            // Sun colour: transmittance through the air over the relative air mass, normalised to max 1.
            double airMass = AirMass(elevation);
            double scale = TurbidityScale * turb;
            double tR = Math.Exp(-RayleighR * scale * airMass);
            double tG = Math.Exp(-RayleighG * scale * airMass);
            double tB = Math.Exp(-RayleighB * scale * airMass);
            double tMax = Math.Max(tR, Math.Max(tG, tB));
            LinearRgb sunTint = new LinearRgb(tR / tMax, tG / tMax, tB / tMax);

            // Saturation of the sky: high sun and clear air give deep blue; haze, rain, fog and cloud wash it out.
            double sunSat = 0.35 + 0.65 * SmoothStep(-0.05, 0.40, sinE);
            double sat = Clamp01(sunSat * (1.0 - 0.35 * haze) * (1.0 - 0.35 * rain) * (1.0 - 0.3 * fog) * (1.0 - 0.25 * cloud));

            // Zenith: blue by day, dark blue at night.
            double zBright = (0.45 + 0.55 * SmoothStep(0.0, 0.9, sinE)) * (1.0 - 0.35 * coverage);
            LinearRgb dayZenith = Desaturate(DayZenithBlue, sat) * zBright;
            LinearRgb zenith = LinearRgb.Lerp(NightZenith, dayZenith, daylight);

            // Horizon: brighter and paler than the zenith, warmer when the sun is low.
            double hBright = 0.95 * (1.0 - 0.25 * coverage);
            double hSat = Clamp01((0.5 + 0.4 * sat) * (1.0 - 0.3 * haze));
            LinearRgb hazeDay = Desaturate(DayHorizon, hSat) * hBright;
            LinearRgb horizonDay = LinearRgb.Lerp(hazeDay, WarmHorizon * hBright, warmth * 0.85);
            LinearRgb horizon = LinearRgb.Lerp(NightHorizon, horizonDay, twilight);

            // Fog: a grey-white haze, warm near the horizon at low sun. The horizon is pulled towards it.
            LinearRgb fogBase = LinearRgb.Lerp(NightFog, DayFog, daylight);
            LinearRgb fogTint = LinearRgb.Lerp(fogBase, WarmFog, warmth * 0.3);
            horizon = LinearRgb.Lerp(horizon, fogTint, fog * 0.6);

            // Ground: dark earth, fading at night.
            LinearRgb ground = Earth * (0.12 + 0.88 * daylight) * (1.0 - 0.2 * rain);

            // Direct sun: full when high, zero below the horizon, reduced by cloud, rain and fog.
            double sunIntensity = SmoothStep(0.0, 0.20, sinE)
                                  * (1.0 - 0.7 * cloud - 0.2 * rain)
                                  * (1.0 - 0.25 * fog);
            sunIntensity = Clamp01(sunIntensity);

            // Ambient: sky average scaled by sun intensity, plus a small floor so night is not black.
            LinearRgb skyAverage = (zenith + horizon) * 0.5;
            LinearRgb ambient = skyAverage * sunIntensity + AmbientFloor;

            double starVisibility = 1.0 - SmoothStep(StarFullSin, StarNoneSin, sinE);

            double cloudDensity = coverage * coverage * (3.0 - 2.0 * coverage);
            double cloudBrightness = (0.08 + 0.92 * daylight) * (1.0 - 0.4 * rain) * (1.0 - 0.1 * fog);

            // Sun glow: strong at golden hour (sun near the horizon, dusk afterglow present), weak at noon.
            double golden = 1.0 - SmoothStep(0.05, 0.35, sinE);
            double sunGlow = 0.05 + 0.95 * golden * twilight * (1.0 - 0.4 * coverage) * (0.85 + 0.15 * haze);

            return new SkyLook
            {
                Zenith = ClampColour(zenith),
                Horizon = ClampColour(horizon),
                Ground = ClampColour(ground),
                SunTint = ClampColour(sunTint),
                FogTint = ClampColour(fogTint),
                Ambient = ClampColour(ambient),
                SunIntensity = Clamp01(sunIntensity),
                StarVisibility = Clamp01(starVisibility),
                CloudCoverage = Clamp01(coverage),
                CloudDensity = Clamp01(cloudDensity),
                CloudBrightness = Clamp01(cloudBrightness),
                SunGlow = Clamp01(sunGlow),
            };
        }

        /// <summary>Relative optical air mass (Kasten-Young style), clamped to 1..40. Below the horizon it uses the horizon value.</summary>
        private static double AirMass(double elevation)
        {
            double e = Math.Max(elevation, 1e-3);
            double eDeg = e * RadToDeg;
            double denom = Math.Sin(e) + 0.15 * Math.Pow(eDeg + 3.885, -1.253);
            return Clamp(1.0 / denom, 1.0, 40.0);
        }

        /// <summary>Warmth 0..1: 1 at the horizon, fading to 0 at about 15 degrees either side.</summary>
        private static double Warmth(double sinE)
        {
            return Clamp01(1.0 - Math.Abs(sinE) / 0.25);
        }

        /// <summary>Mixes a colour towards its own luminance. saturation 1 keeps it, 0 gives grey.</summary>
        private static LinearRgb Desaturate(LinearRgb c, double saturation)
        {
            double luma = 0.2126 * c.R + 0.7152 * c.G + 0.0722 * c.B;
            return LinearRgb.Lerp(new LinearRgb(luma, luma, luma), c, saturation);
        }

        private static LinearRgb ClampColour(LinearRgb c)
        {
            return new LinearRgb(Clamp(c.R, 0.0, 2.0), Clamp(c.G, 0.0, 2.0), Clamp(c.B, 0.0, 2.0));
        }

        /// <summary>Clamps v to lo..hi. NaN gives lo, so the result is always finite.</summary>
        private static double Clamp(double v, double lo, double hi)
        {
            if (!(v > lo))
            {
                return lo;
            }

            return v > hi ? hi : v;
        }

        private static double Clamp01(double v)
        {
            return Clamp(v, 0.0, 1.0);
        }

        /// <summary>Smooth Hermite step from 0 at edge0 to 1 at edge1.</summary>
        private static double SmoothStep(double edge0, double edge1, double x)
        {
            double t = Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3.0 - 2.0 * t);
        }
    }
}
