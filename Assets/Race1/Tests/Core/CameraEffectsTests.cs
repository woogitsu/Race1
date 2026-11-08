using NUnit.Framework;
using Race1.Core;

namespace Race1.Tests.Core
{
    [TestFixture]
    public sealed class CameraEffectsTests
    {
        private const double Dt = 1.0 / 60.0;
        private const double Kmh = 1.0 / 3.6;

        [Test]
        public void Impact_DecaysToNearZeroAfterTwoSeconds()
        {
            var fx = new CameraEffects();
            fx.TriggerImpact(1.0);
            var still = new CameraEffectsInput();

            CameraEffectsOutput first = fx.Step(still, Dt);
            double radius = System.Math.Sqrt(first.ShakeX * first.ShakeX + first.ShakeY * first.ShakeY);
            Assert.That(radius, Is.GreaterThan(0.03).And.LessThanOrEqualTo(0.06), "shake starts near 0.06 m");

            CameraEffectsOutput o = first;
            for (int i = 1; i < 120; i++)
            {
                o = fx.Step(still, Dt);
            }

            Assert.That(System.Math.Abs(o.ShakeX), Is.LessThan(1e-3));
            Assert.That(System.Math.Abs(o.ShakeY), Is.LessThan(1e-3));
            Assert.That(o.ImpactFlash01, Is.EqualTo(0.0).Within(1e-9));
        }

        [Test]
        public void Impact_FlashIsBrightThenGoneAfterThreeTenthsOfASecond()
        {
            var fx = new CameraEffects();
            fx.TriggerImpact(0.8);
            var still = new CameraEffectsInput();

            CameraEffectsOutput first = fx.Step(still, Dt);
            Assert.That(first.ImpactFlash01, Is.GreaterThan(0.7));

            CameraEffectsOutput o = first;
            for (int i = 1; i < 21; i++) // 0.35 s in total
            {
                o = fx.Step(still, Dt);
            }

            Assert.That(o.ImpactFlash01, Is.EqualTo(0.0).Within(1e-9));
        }

        [Test]
        public void WindLines_AreZeroAt30Kmh_AndOneAt260Kmh()
        {
            var fx = new CameraEffects();
            CameraEffectsOutput slow = fx.Step(new CameraEffectsInput { SpeedMs = 30 * Kmh }, Dt);
            Assert.That(slow.WindLines01, Is.EqualTo(0.0).Within(1e-9));

            CameraEffectsOutput fast = fx.Step(new CameraEffectsInput { SpeedMs = 260 * Kmh }, Dt);
            Assert.That(fast.WindLines01, Is.EqualTo(1.0).Within(1e-9));
        }

        [Test]
        public void FovOffset_IsZeroAtStandstill_AndPositiveAt250Kmh()
        {
            var fx = new CameraEffects();
            CameraEffectsOutput standing = fx.Step(new CameraEffectsInput { SpeedMs = 0.0 }, Dt);
            Assert.That(standing.FovOffsetDeg, Is.EqualTo(0.0).Within(1e-9));

            CameraEffectsOutput fast = fx.Step(new CameraEffectsInput { SpeedMs = 250 * Kmh }, Dt);
            Assert.That(fast.FovOffsetDeg, Is.GreaterThan(0.0));
            Assert.That(fast.FovOffsetDeg, Is.EqualTo(4.0).Within(1e-9));
        }

        [Test]
        public void Roll_IsNegativeForPositiveLateralG_AndPositiveForNegative()
        {
            var fx = new CameraEffects();
            CameraEffectsOutput o = default(CameraEffectsOutput);
            for (int i = 0; i < 120; i++)
            {
                o = fx.Step(new CameraEffectsInput { SpeedMs = 20.0, LateralG = 1.0 }, Dt);
            }

            Assert.That(o.RollRad, Is.LessThan(0.0));
            Assert.That(o.RollRad, Is.EqualTo(-0.012).Within(1e-4));

            fx.Reset();
            for (int i = 0; i < 120; i++)
            {
                o = fx.Step(new CameraEffectsInput { SpeedMs = 20.0, LateralG = -1.0 }, Dt);
            }

            Assert.That(o.RollRad, Is.GreaterThan(0.0));
            Assert.That(o.RollRad, Is.EqualTo(0.012).Within(1e-4));
        }

        [Test]
        public void Roll_IsClampedToThirtyMilliradians()
        {
            var fx = new CameraEffects();
            CameraEffectsOutput o = default(CameraEffectsOutput);
            for (int i = 0; i < 180; i++)
            {
                o = fx.Step(new CameraEffectsInput { SpeedMs = 40.0, LateralG = 5.0 }, Dt);
            }

            Assert.That(o.RollRad, Is.EqualTo(-0.03).Within(1e-4));
        }

        [Test]
        public void Vibration_FollowsRoughnessAndClamps()
        {
            var fx = new CameraEffects();
            CameraEffectsOutput o = default(CameraEffectsOutput);
            for (int i = 0; i < 60; i++)
            {
                o = fx.Step(new CameraEffectsInput { SpeedMs = 30.0, SuspensionRoughness = 0.25 }, Dt);
            }

            Assert.That(o.Vibration01, Is.EqualTo(0.5).Within(1e-3));

            for (int i = 0; i < 60; i++)
            {
                o = fx.Step(new CameraEffectsInput { SpeedMs = 30.0, SuspensionRoughness = 3.0 }, Dt);
            }

            Assert.That(o.Vibration01, Is.EqualTo(1.0).Within(1e-3));
        }

        [Test]
        public void Airborne_FadesShakeToZero()
        {
            var fx = new CameraEffects();
            fx.TriggerImpact(1.0);

            CameraEffectsOutput o = default(CameraEffectsOutput);
            for (int i = 0; i < 60; i++)
            {
                o = fx.Step(new CameraEffectsInput { Airborne = true }, Dt);
            }

            double radius = System.Math.Sqrt(o.ShakeX * o.ShakeX + o.ShakeY * o.ShakeY);
            Assert.That(radius, Is.LessThan(1e-4));
        }

        [Test]
        public void NonPositiveDt_ReturnsPreviousOutputUnchanged()
        {
            var fx = new CameraEffects();
            fx.TriggerImpact(1.0);
            CameraEffectsOutput before = fx.Step(new CameraEffectsInput { SpeedMs = 30.0, LateralG = 0.5 }, Dt);

            CameraEffectsOutput zero = fx.Step(new CameraEffectsInput { SpeedMs = 0.0, LateralG = -1.0 }, 0.0);
            Assert.That(zero.ShakeX, Is.EqualTo(before.ShakeX));
            Assert.That(zero.ShakeY, Is.EqualTo(before.ShakeY));
            Assert.That(zero.RollRad, Is.EqualTo(before.RollRad));
            Assert.That(zero.WindLines01, Is.EqualTo(before.WindLines01));
            Assert.That(zero.ImpactFlash01, Is.EqualTo(before.ImpactFlash01));

            CameraEffectsOutput negative = fx.Step(new CameraEffectsInput { SpeedMs = 90.0 }, -0.1);
            Assert.That(negative.FovOffsetDeg, Is.EqualTo(before.FovOffsetDeg));
            Assert.That(negative.HeadBobY, Is.EqualTo(before.HeadBobY));
        }

        [Test]
        public void Reset_ClearsImpact()
        {
            var fx = new CameraEffects();
            var still = new CameraEffectsInput();
            fx.TriggerImpact(1.0);
            fx.Step(still, Dt);

            fx.Reset();
            CameraEffectsOutput o = fx.Step(still, Dt);

            Assert.That(o.ShakeX, Is.EqualTo(0.0).Within(1e-12));
            Assert.That(o.ShakeY, Is.EqualTo(0.0).Within(1e-12));
            Assert.That(o.ImpactFlash01, Is.EqualTo(0.0).Within(1e-12));
        }

        [Test]
        public void Output_StaysFiniteAndBounded_ForSixtySecondsOfDeterministicInput()
        {
            var fx = new CameraEffects();
            uint seed = 0x2545F491u;
            int firstBadStep = -1;
            CameraEffectsOutput o = default(CameraEffectsOutput);

            for (int i = 0; i < 3600; i++)
            {
                double r0 = NextUnit(ref seed);
                double r1 = NextUnit(ref seed);
                double r2 = NextUnit(ref seed);
                double r3 = NextUnit(ref seed);
                double r4 = NextUnit(ref seed);
                double r5 = NextUnit(ref seed);

                if (r5 < 0.02)
                {
                    fx.TriggerImpact(NextUnit(ref seed));
                }

                var input = new CameraEffectsInput
                {
                    SpeedMs = r0 * 95.0,
                    LateralG = (r1 * 2.0 - 1.0) * 2.0,
                    LongitudinalG = (r2 * 2.0 - 1.0) * 1.2,
                    SuspensionRoughness = r3 * 3.0,
                    Airborne = r4 < 0.05,
                };

                o = fx.Step(input, Dt);

                bool ok = IsFinite(o.ShakeX) && IsFinite(o.ShakeY) && IsFinite(o.RollRad)
                    && IsFinite(o.FovOffsetDeg) && IsFinite(o.WindLines01) && IsFinite(o.Vibration01)
                    && IsFinite(o.HeadBobY) && IsFinite(o.ImpactFlash01)
                    && System.Math.Abs(o.ShakeX) <= 0.06 + 1e-12 && System.Math.Abs(o.ShakeY) <= 0.06 + 1e-12
                    && System.Math.Abs(o.RollRad) <= 0.03 + 1e-12
                    && o.WindLines01 >= 0.0 && o.WindLines01 <= 1.0
                    && o.Vibration01 >= 0.0 && o.Vibration01 <= 1.0
                    && o.ImpactFlash01 >= 0.0 && o.ImpactFlash01 <= 1.0;

                if (!ok)
                {
                    firstBadStep = i;
                    break;
                }
            }

            Assert.That(firstBadStep, Is.EqualTo(-1), "first step with a non-finite or out-of-range output");
        }

        /// <summary>Deterministic linear congruential generator. Returns values in [0, 1).</summary>
        private static double NextUnit(ref uint state)
        {
            unchecked
            {
                state = state * 1664525u + 1013904223u;
            }

            return (state >> 8) / 16777216.0;
        }

        private static bool IsFinite(double v)
        {
            return !double.IsNaN(v) && !double.IsInfinity(v);
        }
    }
}
