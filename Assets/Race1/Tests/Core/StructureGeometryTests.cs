using System;
using NUnit.Framework;
using Race1.Core;

namespace Race1.Tests.Core
{
    [TestFixture]
    public sealed class StructureGeometryTests
    {
        [Test]
        public void PillarSamples_OccurOncePerSpan()
        {
            const double ds = 2.0;                 // 12 samples per 24 m span
            int count = 0;
            for (long i = 0; i < 1200; i++)
            {
                if (StructureGeometry.IsPillarSample(i, ds)) count++;
            }
            Assert.That(count, Is.EqualTo(100), "one pillar sample per 24 m over 2400 m");
            Assert.That(StructureGeometry.IsPillarSample(0, ds), Is.True);
            Assert.That(StructureGeometry.IsPillarSample(12, ds), Is.True);
            Assert.That(StructureGeometry.IsPillarSample(13, ds), Is.False);
        }

        [Test]
        public void PillarSamples_HandleInvalidInputAndNegativeIndices()
        {
            Assert.That(StructureGeometry.IsPillarSample(0, 0.0), Is.False);
            Assert.That(StructureGeometry.IsPillarSample(0, 2.0, 0.0), Is.False);
            Assert.That(StructureGeometry.IsPillarSample(-12, 2.0), Is.True, "a negative index on the span grid is still on it");
            Assert.That(StructureGeometry.IsPillarSample(-11, 2.0), Is.False);
        }

        [Test]
        public void PillarHeight_IsTheGapMinusTheDeck_AndZeroWhenTooLow()
        {
            Assert.That(StructureGeometry.PillarHeight(deckY: 10.0, groundY: 2.0), Is.EqualTo(10.0 - StructureGeometry.DeckThicknessM - 2.0).Within(1e-9));
            Assert.That(StructureGeometry.PillarHeight(deckY: 3.0, groundY: 2.0), Is.EqualTo(0.0), "too low for a pillar");
            Assert.That(StructureGeometry.PillarHeight(deckY: 2.0, groundY: 9.0), Is.EqualTo(0.0), "ground above the deck");
            Assert.That(StructureGeometry.PillarHeight(double.NaN, 2.0), Is.EqualTo(0.0));
        }

        [Test]
        public void LiningHalfWidth_IsTheRoadHalfWidthPlusTheWall()
        {
            Assert.That(StructureGeometry.LiningHalfWidth(3.5), Is.EqualTo(3.5 + StructureGeometry.LiningWallOffsetM).Within(1e-12));
        }

        [Test]
        public void LiningProfile_RunsFromLeftBaseOverTheCrownToTheRightBase()
        {
            const double halfWidth = 3.5;
            var profile = StructureGeometry.LiningProfile(halfWidth, 12);
            double a = StructureGeometry.LiningHalfWidth(halfWidth);
            double b = StructureGeometry.LiningHeightM;

            Assert.That(profile.Length, Is.EqualTo(13));
            Assert.That(profile[0].lateral, Is.EqualTo(-a).Within(1e-9));
            Assert.That(profile[0].height, Is.EqualTo(0.0).Within(1e-9));
            Assert.That(profile[12].lateral, Is.EqualTo(a).Within(1e-9));
            Assert.That(profile[12].height, Is.EqualTo(0.0).Within(1e-9));
            Assert.That(profile[6].lateral, Is.EqualTo(0.0).Within(1e-9), "the crown is in the middle");
            Assert.That(profile[6].height, Is.EqualTo(b).Within(1e-9));
        }

        [Test]
        public void LiningProfile_LiesOnTheEllipseAndIsSymmetric()
        {
            var profile = StructureGeometry.LiningProfile(3.5, 16);
            double a = StructureGeometry.LiningHalfWidth(3.5);
            double b = StructureGeometry.LiningHeightM;
            int n = profile.Length - 1;
            for (int k = 0; k <= n; k++)
            {
                double x = profile[k].lateral / a, y = profile[k].height / b;
                Assert.That(x * x + y * y, Is.EqualTo(1.0).Within(1e-9), $"point {k} is off the ellipse");
                Assert.That(profile[k].height, Is.GreaterThanOrEqualTo(-1e-12), "the lining never goes below the road");
                Assert.That(profile[k].lateral, Is.EqualTo(-profile[n - k].lateral).Within(1e-9), $"point {k} is not mirrored");
            }
        }

        [Test]
        public void LiningProfile_ClampsTheSegmentCount()
        {
            Assert.That(StructureGeometry.LiningProfile(3.5, 0).Length, Is.EqualTo(3), "at least two segments");
            Assert.That(StructureGeometry.LiningProfile(3.5, 1).Length, Is.EqualTo(3));
        }
    }
}
