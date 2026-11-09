using Race1.Core;
using UnityEngine;

namespace Race1.Game
{
    /// <summary>
    /// Plays the ambient synth (wind, tyre squeal, rain) through OnAudioFilterRead, like the engine sound. The
    /// scratch buffer is reused, so the audio thread does not allocate, and the levels are volatile because the main
    /// thread sets them once per frame. A silent looping clip keeps the callback running.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class AmbientAudio : MonoBehaviour
    {
        private AmbientSynth _synth;
        private float[] _scratch = new float[4096];
        private volatile float _wind;
        private volatile float _squeal;
        private volatile float _rain;

        private void Awake()
        {
            var source = GetComponent<AudioSource>();
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.clip = AudioClip.Create("silence", 44100, 1, 44100, false);
            source.volume = 1f;
            source.Play();
        }

        /// <summary>Sets the three levels, each 0..1. Call once per frame; zero everything to go quiet.</summary>
        public void Set(double wind, double squeal, double rain)
        {
            _wind = (float)wind;
            _squeal = (float)squeal;
            _rain = (float)rain;
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            if (_synth == null)
            {
                _synth = new AmbientSynth(AudioSettings.outputSampleRate);
            }
            int frames = data.Length / System.Math.Max(1, channels);
            if (_scratch.Length < frames)
            {
                _scratch = new float[frames];
            }
            _synth.Wind = _wind;
            _synth.Squeal = _squeal;
            _synth.Rain = _rain;
            _synth.Render(_scratch, 0, frames);
            for (int i = 0; i < frames; i++)
            {
                for (int c = 0; c < channels; c++)
                {
                    data[i * channels + c] = _scratch[i];
                }
            }
        }
    }
}
