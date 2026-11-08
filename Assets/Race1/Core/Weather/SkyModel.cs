using System;

namespace Race1.Core
{
    /// <summary>Sun position and sky brightness from latitude, day of year and local time.</summary>
    public static class SkyModel
    {
        /// <summary>
        /// Solar elevation and azimuth (radians). Standard declination and hour-angle formulas, accurate
        /// enough for lighting and sky colour. Azimuth is measured clockwise from north.
        /// </summary>
        public static void SolarPosition(double latitudeDeg, int dayOfYear, double localHour,
            out double elevation, out double azimuth)
        {
            double lat = latitudeDeg * Mathx.Deg2Rad;
            double declination = -23.44 * Mathx.Deg2Rad * Math.Cos(2.0 * Math.PI / 365.0 * (dayOfYear + 10));
            double hourAngle = (localHour - 12.0) * 15.0 * Mathx.Deg2Rad;

            double sinElev = Math.Sin(lat) * Math.Sin(declination) + Math.Cos(lat) * Math.Cos(declination) * Math.Cos(hourAngle);
            elevation = Math.Asin(Mathx.Clamp(sinElev, -1.0, 1.0));

            double cosAz = (Math.Sin(declination) - Math.Sin(elevation) * Math.Sin(lat))
                           / Math.Max(1e-6, Math.Cos(elevation) * Math.Cos(lat));
            double az = Math.Acos(Mathx.Clamp(cosAz, -1.0, 1.0));
            azimuth = hourAngle > 0 ? 2.0 * Math.PI - az : az;
        }

        /// <summary>Direct sun intensity 0..1 from elevation. Soft start below the horizon so dusk fades smoothly.</summary>
        public static double SunIntensity(double elevation)
        {
            return Mathx.Clamp01(Math.Sin(elevation) * 1.4 + 0.05);
        }

        /// <summary>Night factor 0 (day) .. 1 (full night). Stars appear below about -6 degrees.</summary>
        public static double NightFactor(double elevation)
        {
            return 1.0 - Mathx.SmoothStep(-0.10, 0.05, elevation);
        }

        /// <summary>Warm horizon factor for sunrise and sunset, peaking at the horizon.</summary>
        public static double GoldenHourFactor(double elevation)
        {
            double near = 1.0 - Mathx.Clamp01(Math.Abs(elevation) / 0.30);
            return near * near;
        }
    }

    public enum WeatherKind
    {
        Clear = 0,
        Cloudy = 1,
        Rain = 2,
        Storm = 3,
        Fog = 4,
        Snow = 5,
    }

    /// <summary>
    /// Weather as smooth parameters. Each kind has a target preset; parameters move towards it with a
    /// time constant, so changes are gradual. Road wetness accumulates in rain and dries slowly, and it
    /// feeds back into tyre grip.
    /// </summary>
    public sealed class WeatherState
    {
        public WeatherKind Kind { get; private set; } = WeatherKind.Clear;
        public double Cloud { get; private set; }
        public double Rain { get; private set; }
        public double Fog { get; private set; }
        public double Snow { get; private set; }
        public double Wetness { get; private set; }

        public double TimeConstantSeconds { get; set; } = 12.0;

        public void SetKind(WeatherKind kind) => Kind = kind;

        /// <summary>Preset (cloud, rain, fog, snow) for each weather kind.</summary>
        public static void Preset(WeatherKind kind, out double cloud, out double rain, out double fog, out double snow)
        {
            switch (kind)
            {
                case WeatherKind.Cloudy: cloud = 0.7; rain = 0.0; fog = 0.0; snow = 0.0; break;
                case WeatherKind.Rain: cloud = 0.9; rain = 0.6; fog = 0.05; snow = 0.0; break;
                case WeatherKind.Storm: cloud = 1.0; rain = 1.0; fog = 0.1; snow = 0.0; break;
                case WeatherKind.Fog: cloud = 0.6; rain = 0.0; fog = 0.9; snow = 0.0; break;
                case WeatherKind.Snow: cloud = 0.9; rain = 0.0; fog = 0.2; snow = 0.8; break;
                default: cloud = 0.1; rain = 0.0; fog = 0.0; snow = 0.0; break;
            }
        }

        public void Update(double dt)
        {
            Preset(Kind, out double cTarget, out double rTarget, out double fTarget, out double sTarget);
            double k = 1.0 - Math.Exp(-dt / Math.Max(0.1, TimeConstantSeconds));
            Cloud += (cTarget - Cloud) * k;
            Rain += (rTarget - Rain) * k;
            Fog += (fTarget - Fog) * k;
            Snow += (sTarget - Snow) * k;

            // Wet asphalt builds up quickly in rain, drains slowly afterwards, and snow covers it instead.
            double wetGain = Rain * 0.25 + Math.Max(0.0, Rain - 0.5) * 0.25;
            double dry = 0.02 * (1.0 - Rain);
            Wetness = Mathx.Clamp01(Wetness + (wetGain - dry) * dt);
        }

        /// <summary>Global tyre grip multiplier: 1 on dry asphalt, down to about 0.6 on wet snow.</summary>
        public double FrictionScale => Mathx.Clamp(1.0 - 0.3 * Wetness - 0.35 * Snow, 0.4, 1.0);
    }
}
