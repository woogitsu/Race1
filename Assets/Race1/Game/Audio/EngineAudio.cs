using Race1.Core;
using UnityEngine;

namespace Race1.Game
{
    /// <summary>
    /// Plays the procedural engine synth through OnAudioFilterRead. The scratch buffer is allocated once,
    /// so the audio thread never allocates. A silent looping clip keeps the callback running.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class EngineAudio : MonoBehaviour
    {
        private EngineSynth _synth;
        private float[] _scratch = new float[4096];
        private volatile float _rpm = 900f;
        private volatile float _load;
        private volatile float _volume = 0.55f;

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

        public void Set(double rpm, double load, double volume)
        {
            _rpm = (float)rpm;
            _load = (float)load;
            _volume = (float)volume;
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            if (_synth == null)
            {
                _synth = new EngineSynth(AudioSettings.outputSampleRate, 6);
            }
            int frames = data.Length / System.Math.Max(1, channels);
            if (_scratch.Length < frames)
            {
                _scratch = new float[frames];
            }
            _synth.Rpm = _rpm;
            _synth.Load = _load;
            _synth.Volume = _volume;
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
