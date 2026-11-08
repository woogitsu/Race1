using System;

namespace Race1.Core
{
    /// <summary>
    /// Procedural engine sound. A firing-frequency harmonic series with load-dependent brightness and
    /// intake noise. Output is generated sample by sample, so it fits Unity's OnAudioFilterRead
    /// without any recorded clips.
    /// </summary>
    public sealed class EngineSynth
    {
        private const int Harmonics = 8;

        private readonly double _sampleRate;
        private readonly int _cylinders;
        private readonly double[] _amplitude = new double[Harmonics + 1];
        private double _phase;
        private double _noiseLow;
        private ulong _noiseState = 0x9E3779B97F4A7C15UL;

        public EngineSynth(double sampleRate, int cylinders = 6)
        {
            _sampleRate = sampleRate;
            _cylinders = Math.Max(1, cylinders);
        }

        /// <summary>Engine speed in rpm. Set once per frame by the caller.</summary>
        public double Rpm { get; set; } = 900.0;

        /// <summary>Throttle load 0..1.</summary>
        public double Load { get; set; }

        /// <summary>Master gain 0..1.</summary>
        public double Volume { get; set; } = 0.6;

        /// <summary>Fundamental firing frequency in Hz for a four-stroke engine.</summary>
        public double FiringFrequency => Math.Max(0.0, Rpm) / 60.0 * _cylinders / 2.0;

        /// <summary>Fills a mono buffer. Soft clipping keeps peaks within -1..1.</summary>
        public void Render(float[] buffer, int offset, int count)
        {
            double f = FiringFrequency;
            double load = Mathx.Clamp01(Load);
            double step = f / _sampleRate;

            for (int h = 1; h <= Harmonics; h++)
            {
                // Higher harmonics come in under load, like an exhaust opening up.
                double rolloff = 1.0 / Math.Pow(h, 1.2);
                double brightness = h > 3 ? 0.4 + 0.9 * load : 1.0;
                _amplitude[h] = rolloff * brightness;
            }

            for (int i = 0; i < count; i++)
            {
                double sum = 0.0;
                for (int h = 1; h <= Harmonics; h++)
                {
                    sum += _amplitude[h] * Math.Sin(2.0 * Math.PI * h * _phase);
                }

                // Intake hiss: low-passed noise, louder at high load.
                double white = NextNoise() * 2.0 - 1.0;
                _noiseLow += (white - _noiseLow) * 0.08;
                sum += _noiseLow * (0.03 + 0.12 * load);

                _phase += step;
                if (_phase >= 1.0) _phase -= 1.0;

                double shaped = Math.Tanh(sum * (0.5 + 0.5 * load)) * Volume;
                buffer[offset + i] = (float)shaped;
            }
        }

        private double NextNoise()
        {
            _noiseState ^= _noiseState << 13;
            _noiseState ^= _noiseState >> 7;
            _noiseState ^= _noiseState << 17;
            return (_noiseState >> 11) * (1.0 / 9007199254740992.0);
        }
    }
}
