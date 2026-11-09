using System;
using NUnit.Framework;
using Race1.Core;

namespace Race1.Tests.Core
{
    [TestFixture]
    public sealed class AmbientSoundTests
    {
        private const double SampleRate = 48000.0;
        private const int Warmup = 12000;      // a quarter second, so the filters settle before measuring
        private const int Measure = 48000;     // one second

        [Test]
        public void WindLevel_IsSilentAtLowSpeed_FullAtHighSpeed_AndRisesBetween()
        {
            Assert.That(AmbientSoundModel.WindLevel(0.0), Is.EqualTo(0.0));
            Assert.That(AmbientSoundModel.WindLevel(AmbientSoundModel.WindStartSpeedMs), Is.EqualTo(0.0).Within(1e-9));
            Assert.That(AmbientSoundModel.WindLevel(AmbientSoundModel.WindFullSpeedMs + 50.0), Is.EqualTo(1.0).Within(1e-9));
            Assert.That(AmbientSoundModel.WindLevel(double.NaN), Is.EqualTo(0.0));

            double previous = -1.0;
            for (double v = 0.0; v <= 100.0; v += 1.0)
            {
                double level = AmbientSoundModel.WindLevel(v);
                Assert.That(level, Is.InRange(0.0, 1.0));
                Assert.That(level, Is.GreaterThanOrEqualTo(previous - 1e-12), $"wind fell at {v} m/s");
                previous = level;
            }
        }

        [Test]
        public void SquealLevel_IsZeroForSmallSlip_FullForLargeSlip_AndIgnoresSign()
        {
            Assert.That(AmbientSoundModel.SquealLevel(0.02, 0.0), Is.EqualTo(0.0));
            Assert.That(AmbientSoundModel.SquealLevel(0.5, 0.0), Is.EqualTo(1.0));
            Assert.That(AmbientSoundModel.SquealLevel(-0.2, 0.0), Is.EqualTo(AmbientSoundModel.SquealLevel(0.2, 0.0)).Within(1e-12));
            Assert.That(AmbientSoundModel.SquealLevel(0.0, -0.2), Is.EqualTo(AmbientSoundModel.SquealLevel(0.0, 0.2)).Within(1e-12));
            Assert.That(AmbientSoundModel.SquealLevel(double.NaN, 0.0), Is.EqualTo(0.0));

            double previous = -1.0;
            for (double slip = 0.0; slip <= 0.5; slip += 0.01)
            {
                double level = AmbientSoundModel.SquealLevel(slip, 0.0);
                Assert.That(level, Is.GreaterThanOrEqualTo(previous - 1e-12), $"squeal fell at slip {slip:F2}");
                previous = level;
            }
        }

        [Test]
        public void RainLevel_IsClampedToTheUnitRange()
        {
            Assert.That(AmbientSoundModel.RainLevel(-1.0), Is.EqualTo(0.0));
            Assert.That(AmbientSoundModel.RainLevel(0.4), Is.EqualTo(0.4).Within(1e-12));
            Assert.That(AmbientSoundModel.RainLevel(3.0), Is.EqualTo(1.0));
            Assert.That(AmbientSoundModel.RainLevel(double.NaN), Is.EqualTo(0.0));
        }

        [Test]
        public void Silence_WhenAllLevelsAreZero()
        {
            var synth = new AmbientSynth(SampleRate);
            var buffer = new float[Measure];
            synth.Render(buffer, 0, buffer.Length);
            for (int i = 0; i < buffer.Length; i++)
            {
                Assert.That(buffer[i], Is.EqualTo(0f), $"sample {i}");
            }
        }

        [Test]
        public void Output_StaysFiniteAndWithinUnitRange_AtFullLevels()
        {
            var synth = new AmbientSynth(SampleRate) { Wind = 1.0, Squeal = 1.0, Rain = 1.0 };
            var buffer = new float[Measure];
            synth.Render(buffer, 0, buffer.Length);
            for (int i = 0; i < buffer.Length; i++)
            {
                Assert.That(float.IsNaN(buffer[i]), Is.False, $"NaN at {i}");
                Assert.That(Math.Abs(buffer[i]), Is.LessThanOrEqualTo(1.0f), $"sample {i}");
            }
        }

        [Test]
        public void Deterministic_SameSettingsGiveSameSound()
        {
            var a = new AmbientSynth(SampleRate) { Wind = 0.6, Squeal = 0.4, Rain = 0.5 };
            var b = new AmbientSynth(SampleRate) { Wind = 0.6, Squeal = 0.4, Rain = 0.5 };
            var bufferA = new float[4800];
            var bufferB = new float[4800];
            a.Render(bufferA, 0, bufferA.Length);
            b.Render(bufferB, 0, bufferB.Length);
            for (int i = 0; i < bufferA.Length; i++)
            {
                Assert.That(bufferA[i], Is.EqualTo(bufferB[i]), $"sample {i}");
            }
        }

        [Test]
        public void Wind_GetsLouderWithItsLevel()
        {
            double quiet = Rms(Render(new AmbientSynth(SampleRate) { Wind = 0.2 }));
            double loud = Rms(Render(new AmbientSynth(SampleRate) { Wind = 0.9 }));
            TestContext.WriteLine($"wind rms: 0.2 -> {quiet:F4}, 0.9 -> {loud:F4}");
            Assert.That(loud, Is.GreaterThan(quiet));
        }

        [Test]
        public void Wind_IsLowFrequency()
        {
            float[] wind = Render(new AmbientSynth(SampleRate) { Wind = 0.8 });
            double low = Power(wind, 100.0);
            double high = Power(wind, 5000.0);
            TestContext.WriteLine($"wind power: 100 Hz {low:E2}, 5 kHz {high:E2}");
            Assert.That(low, Is.GreaterThan(high * 10.0));
        }

        [Test]
        public void Squeal_IsBandLimitedAroundItsCentre()
        {
            float[] squeal = Render(new AmbientSynth(SampleRate) { Squeal = 1.0 });
            double centre = Power(squeal, AmbientSynth.SquealCentreHz);
            double below = Power(squeal, 300.0);
            double above = Power(squeal, 8000.0);
            TestContext.WriteLine($"squeal power: 300 Hz {below:E2}, 2 kHz {centre:E2}, 8 kHz {above:E2}");
            Assert.That(centre, Is.GreaterThan(below * 4.0));
            Assert.That(centre, Is.GreaterThan(above * 4.0));
        }

        [Test]
        public void Rain_HasMostlyHighFrequencies()
        {
            float[] rain = Render(new AmbientSynth(SampleRate) { Rain = 1.0 });
            double low = Power(rain, 200.0);
            double high = Power(rain, 6000.0);
            TestContext.WriteLine($"rain power: 200 Hz {low:E2}, 6 kHz {high:E2}");
            Assert.That(high, Is.GreaterThan(low * 4.0));
        }

        [Test]
        public void Rain_GetsLouderWithItsLevel()
        {
            double quiet = Rms(Render(new AmbientSynth(SampleRate) { Rain = 0.2 }));
            double loud = Rms(Render(new AmbientSynth(SampleRate) { Rain = 0.9 }));
            Assert.That(loud, Is.GreaterThan(quiet));
        }

        /// <summary>Renders a warm-up followed by one measured second, and returns only the measured part.</summary>
        private static float[] Render(AmbientSynth synth)
        {
            var buffer = new float[Warmup + Measure];
            synth.Render(buffer, 0, buffer.Length);
            var measured = new float[Measure];
            Array.Copy(buffer, Warmup, measured, 0, Measure);
            return measured;
        }

        private static double Rms(float[] x)
        {
            double sum = 0.0;
            foreach (float v in x) sum += v * (double)v;
            return Math.Sqrt(sum / x.Length);
        }

        /// <summary>Goertzel power of one frequency in a buffer. Used to compare energy at two frequencies.</summary>
        private static double Power(float[] x, double frequency)
        {
            double w = 2.0 * Math.PI * frequency / SampleRate;
            double coeff = 2.0 * Math.Cos(w);
            double s1 = 0.0, s2 = 0.0;
            foreach (float v in x)
            {
                double s0 = v + coeff * s1 - s2;
                s2 = s1;
                s1 = s0;
            }
            return s1 * s1 + s2 * s2 - coeff * s1 * s2;
        }
    }
}
