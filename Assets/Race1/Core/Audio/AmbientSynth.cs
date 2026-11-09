using System;

namespace Race1.Core
{
    /// <summary>
    /// Procedural ambient sound: wind, tyre squeal and rain, mixed into one mono stream for OnAudioFilterRead.
    /// Each layer is generated sample by sample from a deterministic noise source, so no recorded clips are needed.
    /// The levels are set by the caller once per frame. With all levels at zero the output is exact silence.
    /// </summary>
    public sealed class AmbientSynth
    {
        /// <summary>Centre of the squeal band (Hz). Tyre squeal sits around 1.5 to 2.5 kHz.</summary>
        public const double SquealCentreHz = 2000.0;

        /// <summary>Q of the squeal band. A high value keeps the sound tonal rather than hissy.</summary>
        public const double SquealQ = 6.0;

        /// <summary>Rain highs are the noise above this frequency (Hz).</summary>
        private const double RainHighPassHz = 1200.0;

        /// <summary>
        /// Decay of one rain droplet click (s). The click is differentiated, which lifts its highs over its lows, as
        /// real rain is brighter than low rumble.
        /// </summary>
        private const double DropletDecaySeconds = 0.0002;

        /// <summary>Droplet clicks per second at full rain.</summary>
        private const double DropletsPerSecondAtFullRain = 72.0;

        private readonly double _sampleRate;
        private readonly double _b0, _b2, _a1, _a2;   // squeal band-pass biquad, normalised
        private readonly double _dropletDecay;
        private ulong _noiseState = 0x2545F4914F6CDD1DUL;
        private double _windLow;
        private double _rainLow;
        private double _droplet;
        private double _dropletPrev;
        private double _lfoPhase;
        private double _squealX1, _squealX2, _squealY1, _squealY2;

        public AmbientSynth(double sampleRate)
        {
            _sampleRate = sampleRate > 0.0 ? sampleRate : 48000.0;

            double w0 = 2.0 * Math.PI * SquealCentreHz / _sampleRate;
            double alpha = Math.Sin(w0) / (2.0 * SquealQ);
            double a0 = 1.0 + alpha;
            _b0 = alpha / a0;
            _b2 = -alpha / a0;
            _a1 = -2.0 * Math.Cos(w0) / a0;
            _a2 = (1.0 - alpha) / a0;

            _dropletDecay = Math.Exp(-1.0 / (DropletDecaySeconds * _sampleRate));
        }

        /// <summary>Wind level 0..1.</summary>
        public double Wind { get; set; }

        /// <summary>Tyre squeal level 0..1.</summary>
        public double Squeal { get; set; }

        /// <summary>Rain level 0..1.</summary>
        public double Rain { get; set; }

        /// <summary>Fills a mono buffer. The mix goes through tanh, so peaks stay within -1..1.</summary>
        public void Render(float[] buffer, int offset, int count)
        {
            double wind = Clamp01(Wind);
            double squeal = Clamp01(Squeal);
            double rain = Clamp01(Rain);

            // Wind: low-passed noise. The cutoff opens with the level, and a slow swell moves the gain.
            double windCutoff = 180.0 + 900.0 * wind;
            double windAlpha = 1.0 - Math.Exp(-2.0 * Math.PI * windCutoff / _sampleRate);
            double windGain = 0.35 * wind;

            double squealGain = 0.25 * Math.Pow(squeal, 1.5);
            double rainGain = 0.3 * rain;
            double rainLowAlpha = 1.0 - Math.Exp(-2.0 * Math.PI * RainHighPassHz / _sampleRate);
            double dropletChance = rain * DropletsPerSecondAtFullRain / _sampleRate;
            double lfoStep = 0.3 / _sampleRate;

            for (int i = 0; i < count; i++)
            {
                double windNoise = Noise();
                _windLow += windAlpha * (windNoise - _windLow);
                double swell = 0.75 + 0.25 * Math.Sin(2.0 * Math.PI * _lfoPhase);
                _lfoPhase += lfoStep;
                if (_lfoPhase >= 1.0) _lfoPhase -= 1.0;
                double windSample = _windLow * windGain * swell;

                double squealNoise = Noise();
                double squealSample = BandPass(squealNoise) * squealGain;

                double rainNoise = Noise();
                _rainLow += rainLowAlpha * (rainNoise - _rainLow);
                double rainHighs = rainNoise - _rainLow;
                if (rain > 0.0 && Uniform() < dropletChance)
                {
                    _droplet += 0.6 * (0.5 + 0.5 * Uniform());
                }
                double dropletOut = _droplet - _dropletPrev;   // derivative of the click: highs over lows
                _dropletPrev = _droplet;
                _droplet *= _dropletDecay;
                double rainSample = (0.5 * rainHighs + dropletOut) * rainGain;

                double mix = windSample + squealSample + rainSample;
                buffer[offset + i] = (float)Math.Tanh(mix);
            }
        }

        private double BandPass(double x)
        {
            double y = _b0 * x + _b2 * _squealX2 - _a1 * _squealY1 - _a2 * _squealY2;
            _squealX2 = _squealX1;
            _squealX1 = x;
            _squealY2 = _squealY1;
            _squealY1 = y;
            return y;
        }

        /// <summary>White noise in -1..1.</summary>
        private double Noise() => Uniform() * 2.0 - 1.0;

        /// <summary>Uniform in [0, 1). Same generator as the engine synth.</summary>
        private double Uniform()
        {
            _noiseState ^= _noiseState << 13;
            _noiseState ^= _noiseState >> 7;
            _noiseState ^= _noiseState << 17;
            return (_noiseState >> 11) * (1.0 / 9007199254740992.0);
        }

        private static double Clamp01(double v)
        {
            if (double.IsNaN(v)) return 0.0;
            return Math.Max(0.0, Math.Min(1.0, v));
        }
    }
}
