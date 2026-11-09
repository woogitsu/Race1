using System;
using NUnit.Framework;
using Race1.Core;

namespace Race1.Tests.Core
{
    [TestFixture]
    public sealed class PhotoOrbitTests
    {
        private static readonly Vector3d Target = new Vector3d(120.0, 0.6, -40.0);

        [Test]
        public void Position_IsAtTheOrbitDistanceFromTheTarget_ForAnyYawAndPitch()
        {
            var orbit = new PhotoOrbit(distance: 7.5);
            for (int yaw = -180; yaw <= 180; yaw += 30)
            {
                for (int pitch = -2; pitch <= 70; pitch += 8)
                {
                    var o = new PhotoOrbit(7.5, yaw, pitch, 35.0);
                    Vector3d p = o.PositionAround(Target);
                    double d = Vector3d.Distance(p, Target);
                    Assert.That(d, Is.EqualTo(7.5).Within(1e-9), $"yaw {yaw}, pitch {pitch}");
                }
            }
            Assert.That(orbit.Distance, Is.EqualTo(7.5));
        }

        [Test]
        public void Drag_TurnsYawWithTheHorizontalDrag()
        {
            var orbit = new PhotoOrbit(yawDeg: 0.0, pitchDeg: 10.0);
            double before = orbit.YawRadians;
            orbit.Drag(100.0, 0.0, 1000.0);
            Assert.That(orbit.YawRadians, Is.GreaterThan(before), "dragging right should turn the camera right");
            Assert.That(orbit.YawRadians - before, Is.EqualTo(0.1 * Math.PI).Within(1e-9), "a tenth of the screen is a tenth of a half turn");
        }

        [Test]
        public void Drag_FingerUpRaisesTheCamera()
        {
            var orbit = new PhotoOrbit(pitchDeg: 10.0);
            double before = orbit.PitchRadians;
            orbit.Drag(0.0, -50.0, 1000.0);
            Assert.That(orbit.PitchRadians, Is.GreaterThan(before));
            Assert.That(orbit.PositionAround(Target).Y, Is.GreaterThan(Target.Y + 0.0));
        }

        [Test]
        public void Pitch_IsClampedBetweenAboveTheRoadAndOverhead()
        {
            var orbit = new PhotoOrbit(pitchDeg: 10.0);
            orbit.Drag(0.0, -1e6, 1000.0);
            Assert.That(orbit.PitchRadians * 180.0 / Math.PI, Is.EqualTo(PhotoOrbit.MaxPitchDeg).Within(1e-9));
            orbit.Drag(0.0, 1e6, 1000.0);
            Assert.That(orbit.PitchRadians * 180.0 / Math.PI, Is.EqualTo(PhotoOrbit.MinPitchDeg).Within(1e-9));
        }

        [Test]
        public void Yaw_StaysWithinOneTurn_AfterManyDrags()
        {
            var orbit = new PhotoOrbit();
            for (int i = 0; i < 1000; i++) orbit.Drag(37.0, 0.0, 500.0);
            Assert.That(orbit.YawRadians, Is.InRange(-Math.PI - 1e-9, Math.PI + 1e-9));
            Assert.That(double.IsNaN(orbit.YawRadians), Is.False);
        }

        [Test]
        public void Drag_IgnoresNonFiniteAndEmptyScreens()
        {
            var orbit = new PhotoOrbit(yawDeg: 45.0, pitchDeg: 10.0);
            double yaw = orbit.YawRadians, pitch = orbit.PitchRadians;
            orbit.Drag(double.NaN, 5.0, 1000.0);
            orbit.Drag(5.0, double.PositiveInfinity, 1000.0);
            orbit.Drag(5.0, 5.0, 0.0);
            Assert.That(orbit.YawRadians, Is.EqualTo(yaw));
            Assert.That(orbit.PitchRadians, Is.EqualTo(pitch));
        }

        [Test]
        public void Zoom_IsClampedToTheDistanceRange()
        {
            var orbit = new PhotoOrbit(distance: 9.0);
            for (int i = 0; i < 100; i++) orbit.ZoomStepIn();
            Assert.That(orbit.Distance, Is.EqualTo(PhotoOrbit.MinDistance).Within(1e-9));
            for (int i = 0; i < 100; i++) orbit.ZoomStepOut();
            Assert.That(orbit.Distance, Is.EqualTo(PhotoOrbit.MaxDistance).Within(1e-9));

            orbit.Zoom(double.NaN);
            orbit.Zoom(-2.0);
            Assert.That(orbit.Distance, Is.EqualTo(PhotoOrbit.MaxDistance).Within(1e-9), "invalid zoom factors are ignored");
        }

        [Test]
        public void Focal_LongerLensNarrowsTheFieldOfView_AndStaysInRange()
        {
            var orbit = new PhotoOrbit(focalMm: 35.0);
            double start = orbit.VerticalFovDegrees;
            double previous = start;
            for (int i = 0; i < 20; i++)
            {
                // Steps are clamped at the longest lens, so the field of view only ever narrows or holds still.
                orbit.StepFocal(1);
                Assert.That(orbit.VerticalFovDegrees, Is.LessThanOrEqualTo(previous), $"step {i}");
                previous = orbit.VerticalFovDegrees;
            }
            Assert.That(orbit.VerticalFovDegrees, Is.LessThan(start));
            Assert.That(orbit.FocalLengthMm, Is.EqualTo(PhotoOrbit.MaxFocalMm).Within(1e-9));

            for (int i = 0; i < 40; i++)
            {
                orbit.StepFocal(-1);
                Assert.That(orbit.VerticalFovDegrees, Is.GreaterThan(0.0));
            }
            Assert.That(orbit.FocalLengthMm, Is.EqualTo(PhotoOrbit.MinFocalMm).Within(1e-9));
            Assert.That(orbit.VerticalFovDegrees, Is.LessThanOrEqualTo(180.0));
        }

        [Test]
        public void VerticalFov_MatchesTheClassicLenses()
        {
            // 24 mm sensor height: 50 mm ~ 27 degrees, 24 mm ~ 53 degrees, 14 mm ~ 81 degrees.
            Assert.That(new PhotoOrbit(focalMm: 50.0).VerticalFovDegrees, Is.EqualTo(26.99).Within(0.05));
            Assert.That(new PhotoOrbit(focalMm: 24.0).VerticalFovDegrees, Is.EqualTo(53.13).Within(0.05));
            Assert.That(new PhotoOrbit(focalMm: 14.0).VerticalFovDegrees, Is.EqualTo(81.2).Within(0.1));
        }

        [Test]
        public void Constructor_ClampsItsArguments()
        {
            var orbit = new PhotoOrbit(distance: 1000.0, pitchDeg: 500.0, focalMm: 5.0);
            Assert.That(orbit.Distance, Is.EqualTo(PhotoOrbit.MaxDistance));
            Assert.That(orbit.PitchRadians * 180.0 / Math.PI, Is.EqualTo(PhotoOrbit.MaxPitchDeg).Within(1e-9));
            Assert.That(orbit.FocalLengthMm, Is.EqualTo(PhotoOrbit.MinFocalMm));
        }

        [Test]
        public void PhotoFileName_IsFixedFormatAndSortsByTime()
        {
            var early = new DateTime(2026, 11, 9, 5, 28, 1);
            var late = new DateTime(2026, 11, 9, 5, 28, 12);
            Assert.That(PhotoShots.FileName(early, 0), Is.EqualTo("Race1_20261109_052801.png"));
            Assert.That(PhotoShots.FileName(early, 2), Is.EqualTo("Race1_20261109_052801_2.png"));
            Assert.That(string.CompareOrdinal(PhotoShots.FileName(early, 0), PhotoShots.FileName(late, 0)), Is.LessThan(0));
        }
    }
}
