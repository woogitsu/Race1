using System;
using NUnit.Framework;
using Race1.Core;

namespace Race1.Tests.Core
{
    [TestFixture]
    public sealed class CarLodTests
    {
        private const double CarSize = 4.4;   // a car's length (m)

        [Test]
        public void ScreenFraction_FallsWithDistance_AndRisesWithSize()
        {
            double previous = double.PositiveInfinity;
            for (double d = 5.0; d <= 500.0; d += 5.0)
            {
                double f = CarLod.ScreenFraction(CarSize, d, CarLod.ReferenceVerticalFovDeg);
                Assert.That(f, Is.InRange(0.0, 1.0));
                Assert.That(f, Is.LessThan(previous), $"fraction did not fall at {d} m");
                previous = f;
            }
            Assert.That(CarLod.ScreenFraction(8.8, 100.0, 70.0), Is.GreaterThan(CarLod.ScreenFraction(4.4, 100.0, 70.0)));
        }

        [Test]
        public void ScreenFraction_MatchesThePinholeFormula()
        {
            // height / (2 * distance * tan(fov / 2)), with a 70 degree field: tan(35 deg) = 0.7002
            double expected = CarSize / (2.0 * 90.0 * Math.Tan(35.0 * Math.PI / 180.0));
            Assert.That(CarLod.ScreenFraction(CarSize, 90.0, 70.0), Is.EqualTo(expected).Within(1e-12));
        }

        [Test]
        public void ScreenFraction_IsLargerThroughANarrowerLens()
        {
            // The same car at the same distance covers more of the screen through a longer lens.
            Assert.That(CarLod.ScreenFraction(CarSize, 120.0, 30.0), Is.GreaterThan(CarLod.ScreenFraction(CarSize, 120.0, 70.0)));
        }

        [Test]
        public void ScreenFraction_HandlesDegenerateInput()
        {
            Assert.That(CarLod.ScreenFraction(0.0, 100.0, 70.0), Is.EqualTo(0.0));
            Assert.That(CarLod.ScreenFraction(CarSize, 100.0, 0.0), Is.EqualTo(0.0));
            Assert.That(CarLod.ScreenFraction(CarSize, 0.0, 70.0), Is.EqualTo(1.0), "at the camera the car fills the screen");
            Assert.That(CarLod.ScreenFraction(CarSize, double.NaN, 70.0), Is.EqualTo(0.0), "an unknown distance hides the car");
        }

        [Test]
        public void DistanceForScreenFraction_IsTheInverseOfScreenFraction()
        {
            for (double d = 10.0; d <= 500.0; d += 10.0)
            {
                double f = CarLod.ScreenFraction(CarSize, d, 70.0);
                double back = CarLod.DistanceForScreenFraction(CarSize, f, 70.0);
                Assert.That(back, Is.EqualTo(d).Within(1e-6), $"round trip at {d} m");
            }
            Assert.That(CarLod.DistanceForScreenFraction(CarSize, 0.0, 70.0), Is.EqualTo(double.PositiveInfinity));
        }

        [Test]
        public void Transitions_PutTheSwitchesAtTheDistancesInMetres()
        {
            CarLod.Transitions(CarSize, out double fullToSimple, out double simpleToCull);

            Assert.That(simpleToCull, Is.LessThan(fullToSimple), "a car is culled only after it has become simple");
            Assert.That(fullToSimple, Is.InRange(0.0, 1.0));
            Assert.That(simpleToCull, Is.GreaterThan(0.0));

            Assert.That(CarLod.DistanceForScreenFraction(CarSize, fullToSimple, CarLod.ReferenceVerticalFovDeg),
                Is.EqualTo(CarLod.FullDetailDistanceM).Within(1e-6));
            Assert.That(CarLod.DistanceForScreenFraction(CarSize, simpleToCull, CarLod.ReferenceVerticalFovDeg),
                Is.EqualTo(CarLod.CullDistanceM).Within(1e-6));
        }

        [Test]
        public void SwitchDistances_AreOrdered()
        {
            Assert.That(CarLod.FullDetailDistanceM, Is.LessThan(CarLod.CullDistanceM));
            Assert.That(CarLod.FullDetailDistanceM, Is.GreaterThan(0.0));
        }
    }
}
