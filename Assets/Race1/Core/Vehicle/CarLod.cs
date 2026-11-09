using System;

namespace Race1.Core
{
    /// <summary>
    /// Level-of-detail switch points for car models. A <c>LODGroup</c> switches on the fraction of the screen height a
    /// car covers, not on distance. These functions convert between the two, so the switch points can be stated in
    /// metres and still be correct for the field of view in use.
    /// </summary>
    public static class CarLod
    {
        /// <summary>Beyond this distance a car is drawn with its simplified body (m).</summary>
        public const double FullDetailDistanceM = 90.0;

        /// <summary>Beyond this distance a car is not drawn at all (m).</summary>
        public const double CullDistanceM = 300.0;

        /// <summary>Vertical field of view the switch points are stated for: the chase camera's base field of view.</summary>
        public const double ReferenceVerticalFovDeg = 70.0;

        /// <summary>
        /// Fraction of the screen height covered by an object of <paramref name="sizeM"/> seen from
        /// <paramref name="distanceM"/> through a camera with the given vertical field of view. Clamped to 0..1.
        /// </summary>
        public static double ScreenFraction(double sizeM, double distanceM, double verticalFovDeg)
        {
            if (!(sizeM > 0.0) || !(verticalFovDeg > 0.0 && verticalFovDeg < 180.0)) return 0.0;
            if (double.IsNaN(distanceM)) return 0.0;
            if (distanceM <= 0.0) return 1.0;
            double tanHalf = Math.Tan(verticalFovDeg * Math.PI / 360.0);
            return Math.Max(0.0, Math.Min(1.0, sizeM / (2.0 * distanceM * tanHalf)));
        }

        /// <summary>
        /// Distance at which an object of <paramref name="sizeM"/> covers <paramref name="fraction"/> of the screen
        /// height. The inverse of <see cref="ScreenFraction"/>. Returns infinity for a fraction of zero or less.
        /// </summary>
        public static double DistanceForScreenFraction(double sizeM, double fraction, double verticalFovDeg)
        {
            if (!(sizeM > 0.0) || !(verticalFovDeg > 0.0 && verticalFovDeg < 180.0)) return 0.0;
            if (!(fraction > 0.0)) return double.PositiveInfinity;
            double tanHalf = Math.Tan(verticalFovDeg * Math.PI / 360.0);
            return sizeM / (2.0 * fraction * tanHalf);
        }

        /// <summary>
        /// Screen fractions for a car of <paramref name="sizeM"/>: the fraction below which it switches to the simplified
        /// body, and the fraction below which it is culled. Both are stated for the reference field of view.
        /// </summary>
        public static void Transitions(double sizeM, out double fullToSimple, out double simpleToCull)
        {
            fullToSimple = ScreenFraction(sizeM, FullDetailDistanceM, ReferenceVerticalFovDeg);
            simpleToCull = ScreenFraction(sizeM, CullDistanceM, ReferenceVerticalFovDeg);
        }
    }
}
