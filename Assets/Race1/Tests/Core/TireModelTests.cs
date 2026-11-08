using NUnit.Framework;
using Race1.Core;

namespace Race1.Tests.Core
{
    [TestFixture]
    public sealed class TireModelTests
    {
        [Test]
        public void MagicCurve_PeaksAtOne()
        {
            double max = 0.0;
            for (double s = 0.0; s <= 6.0; s += 0.001)
            {
                max = System.Math.Max(max, TireModel.Magic(s));
            }
            Check.AreEqual(1.0, max, 1e-6);
        }

        [Test]
        public void NoLoad_NoForce()
        {
            TireModel.Forces(1.2, 0.0, 0.5, 0.2, out double fx, out double fy);
            Check.AreEqual(0.0, fx);
            Check.AreEqual(0.0, fy);
        }

        [Test]
        public void LateralForce_OpposesSlip()
        {
            TireModel.Forces(1.0, 4000.0, 0.0, 0.05, out _, out double fy);
            Assert.That(fy, Is.LessThan(0.0), "wheel sliding right must be pushed left");
            TireModel.Forces(1.0, 4000.0, 0.0, -0.05, out _, out fy);
            Assert.That(fy, Is.GreaterThan(0.0));
        }

        [Test]
        public void CombinedForce_NeverExceedsFrictionLimit()
        {
            double mu = 1.1;
            double fz = 4000.0;
            for (double k = -1.0; k <= 1.0; k += 0.05)
            {
                for (double a = -0.6; a <= 0.6; a += 0.02)
                {
                    TireModel.Forces(mu, fz, k, a, out double fx, out double fy);
                    double mag = System.Math.Sqrt(fx * fx + fy * fy);
                    Assert.That(mag, Is.LessThanOrEqualTo(mu * fz * 1.0001), $"k={k:F2} a={a:F2}");
                }
            }
        }

        [Test]
        public void DrivingForce_PointsForwardForPositiveSlip()
        {
            TireModel.Forces(1.0, 4000.0, 0.08, 0.0, out double fx, out double fy);
            Assert.That(fx, Is.GreaterThan(0.0));
            Check.AreEqual(0.0, fy, 1e-9);
        }
    }
}
