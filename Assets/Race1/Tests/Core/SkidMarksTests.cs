using System;
using NUnit.Framework;
using Race1.Core;

namespace Race1.Tests.Core
{
    [TestFixture]
    public sealed class SkidMarksTests
    {
        private const double Dt = 1.0 / 60.0;

        [Test]
        public void Intensity_IsZeroBelowTheThreshold_FullAboveTheTop_AndRisesBetween()
        {
            Assert.That(SkidMarkModel.Intensity(0.0), Is.EqualTo(0.0));
            Assert.That(SkidMarkModel.Intensity(SkidMarkModel.MarkFrom), Is.EqualTo(0.0).Within(1e-12));
            Assert.That(SkidMarkModel.Intensity(SkidMarkModel.MarkFull), Is.EqualTo(1.0).Within(1e-12));
            Assert.That(SkidMarkModel.Intensity(5.0), Is.EqualTo(1.0));
            Assert.That(SkidMarkModel.Intensity(double.NaN), Is.EqualTo(0.0));

            double previous = -1.0;
            for (double level = 0.0; level <= 1.0; level += 0.02)
            {
                double i = SkidMarkModel.Intensity(level);
                Assert.That(i, Is.InRange(0.0, 1.0));
                Assert.That(i, Is.GreaterThanOrEqualTo(previous - 1e-12), $"darkness fell at {level:F2}");
                previous = i;
            }
        }

        [Test]
        public void Visibility_FadesFromFullToNothingOverTheLifetime()
        {
            var fresh = new SkidMark { Intensity = 1.0, Age = 0.0 };
            var half = new SkidMark { Intensity = 1.0, Age = 3.0 };
            var old = new SkidMark { Intensity = 1.0, Age = 6.0 };
            Assert.That(SkidMarkModel.Visibility(fresh, 6.0), Is.EqualTo(1.0).Within(1e-12));
            Assert.That(SkidMarkModel.Visibility(half, 6.0), Is.EqualTo(0.5).Within(1e-12));
            Assert.That(SkidMarkModel.Visibility(old, 6.0), Is.EqualTo(0.0).Within(1e-12));
            Assert.That(SkidMarkModel.Visibility(new SkidMark { Intensity = 0.3, Age = 0.0 }, 6.0), Is.EqualTo(0.3).Within(1e-12));
        }

        [Test]
        public void NoSqueal_NoMarks()
        {
            var marks = new SkidMarks(50, 6.0, 0.5);
            for (int i = 0; i < 600; i++)
            {
                marks.Update(Dt, new double[] { i * 0.4, i * 0.4, i * 0.4, i * 0.4 }, new double[4], new double[4]);
            }
            Assert.That(marks.Count, Is.EqualTo(0));
        }

        [Test]
        public void SquealingTyre_LaysMarksAtTheSpacing()
        {
            // Fine steps: at 60 Hz a car moves 0.17 m a frame, and the 0.5 m spacing would round up to whole frames.
            const double fine = 1.0 / 600.0;
            var marks = new SkidMarks(500, 1000.0, 0.5, tyres: 1);
            double s = 0.0;
            for (int i = 0; i < 600 * 10; i++)
            {
                s += 10.0 * fine;    // 10 m/s for 10 s: 100 m
                marks.Update(fine, new[] { s }, new[] { 0.0 }, new[] { 1.0 });
            }
            // One mark at the start, then one at least every 0.5 m: at most about 200 marks over 100 m. A frame step of
            // 1/600 s rounds the gaps up a little, so the count is a little under that bound.
            Assert.That(marks.Count, Is.InRange(190, 202));
            for (int i = 1; i < marks.Count; i++)
            {
                Assert.That(marks[i].S - marks[i - 1].S, Is.GreaterThanOrEqualTo(0.5 - 1e-9), "marks are at least the spacing apart");
            }
        }

        [Test]
        public void Buffer_IsBounded_AndTheOldestMarksGoFirst()
        {
            var marks = new SkidMarks(10, 1000.0, 0.5, tyres: 1);
            for (int i = 0; i < 100; i++)
            {
                marks.Update(0.0, new[] { i * 1.0 }, new[] { 0.0 }, new[] { 1.0 });
            }
            Assert.That(marks.Count, Is.EqualTo(10));
            Assert.That(marks[0].S, Is.EqualTo(90.0).Within(1e-9), "the oldest surviving mark");
            Assert.That(marks[9].S, Is.EqualTo(99.0).Within(1e-9), "the newest mark");
        }

        [Test]
        public void Marks_ExpireAfterTheirLifetime()
        {
            var marks = new SkidMarks(50, 2.0, 0.5, tyres: 1);
            marks.Update(0.0, new[] { 0.0 }, new[] { 0.0 }, new[] { 1.0 });
            Assert.That(marks.Count, Is.EqualTo(1));

            marks.Update(1.0, new[] { 0.0 }, new[] { 0.0 }, new[] { 0.0 });
            Assert.That(marks.Count, Is.EqualTo(1), "still alive after one second");

            marks.Update(1.5, new[] { 0.0 }, new[] { 0.0 }, new[] { 0.0 });
            Assert.That(marks.Count, Is.EqualTo(0), "gone after its two-second lifetime");
        }

        [Test]
        public void Tyres_AreIndependent()
        {
            var marks = new SkidMarks(50, 1000.0, 0.5, tyres: 2);
            marks.Update(0.0, new[] { 0.0, 0.0 }, new[] { -0.8, 0.8 }, new[] { 1.0, 0.0 });
            Assert.That(marks.Count, Is.EqualTo(1));
            Assert.That(marks[0].T, Is.EqualTo(-0.8).Within(1e-12), "only the squealing tyre lays a mark");
        }

        [Test]
        public void A_NewSkid_LaysItsFirstMarkAtOnce()
        {
            var marks = new SkidMarks(50, 1000.0, 5.0, tyres: 1);
            marks.Update(0.0, new[] { 0.0 }, new[] { 0.0 }, new[] { 1.0 });
            marks.Update(0.0, new[] { 0.1 }, new[] { 0.0 }, new[] { 0.0 });   // grip returns
            marks.Update(0.0, new[] { 0.2 }, new[] { 0.0 }, new[] { 1.0 });   // a new skid, short of the spacing
            Assert.That(marks.Count, Is.EqualTo(2), "a new skid starts with a mark even when it is close to the last one");
        }

        [Test]
        public void Clear_RemovesEverything()
        {
            var marks = new SkidMarks(50, 1000.0, 0.5, tyres: 1);
            marks.Update(0.0, new[] { 0.0 }, new[] { 0.0 }, new[] { 1.0 });
            marks.Clear();
            Assert.That(marks.Count, Is.EqualTo(0));
        }

        [Test]
        public void Constructor_And_Index_ValidateTheirInput()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SkidMarks(0, 1.0, 0.5));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SkidMarks(10, 0.0, 0.5));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SkidMarks(10, 1.0, 0.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SkidMarks(10, 1.0, 0.5, tyres: 0));

            var marks = new SkidMarks(10, 1.0, 0.5, tyres: 1);
            Assert.Throws<ArgumentOutOfRangeException>(() => { SkidMark m = marks[0]; });
            Assert.Throws<ArgumentException>(() => marks.Update(0.0, new double[0], new double[0], new double[0]));
        }
    }
}
