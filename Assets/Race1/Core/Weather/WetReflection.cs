using System;

namespace Race1.Core
{
    /// <summary>
    /// How strongly wet asphalt reflects the sky, the sun and the lamps, from 0 (dry, no reflection) to 1 (a glossy film
    /// of water). A film forms only after some wetness. A low sun reflects more, as wet roads do at grazing angles, and
    /// the night reflects a little less because there is no sun in the sky.
    /// </summary>
    public static class WetReflection
    {
        /// <summary>Wetness at which the reflection starts to show.</summary>
        public const double FilmFrom = 0.1;

        /// <summary>Wetness at which the film is complete.</summary>
        public const double FilmFull = 0.6;

        /// <summary>Sun elevation (deg) above which reflection is at its lowest.</summary>
        public const double HighSunDeg = 50.0;

        /// <summary>Sun elevation (deg) below which reflection is at its highest.</summary>
        public const double LowSunDeg = 5.0;

        /// <summary>How much less the reflection is at high sun, relative to low sun.</summary>
        public const double HighSunReduction = 0.6;

        /// <summary>How much the night reduces the reflection, at full night.</summary>
        public const double NightReduction = 0.2;

        /// <summary>
        /// Reflection strength (0..1). <paramref name="sunElevationRad"/> is the solar elevation, so a sun below the
        /// horizon counts as the lowest sun. <paramref name="night01"/> is 0 in full day and 1 at full night.
        /// </summary>
        public static double Strength(double wetness01, double sunElevationRad, double night01)
        {
            double wet = double.IsNaN(wetness01) ? 0.0 : Mathx.Clamp01(wetness01);
            double film = Mathx.SmoothStep(FilmFrom, FilmFull, wet);
            if (film <= 0.0) return 0.0;

            double elevationDeg = double.IsNaN(sunElevationRad) ? 0.0 : sunElevationRad * 180.0 / Math.PI;
            double highness = Mathx.SmoothStep(LowSunDeg, HighSunDeg, elevationDeg);
            double sunFactor = 1.0 - HighSunReduction * highness;

            double dark = double.IsNaN(night01) ? 0.0 : Mathx.Clamp01(night01);
            double nightFactor = 1.0 - NightReduction * dark;

            return Mathx.Clamp01(film * sunFactor * nightFactor);
        }
    }
}
