using Highbyte.DotNet6502.Systems;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Highbyte.DotNet6502.Impl.NAudio
{
    public class NAudioAudioHandlerContext : IAudioHandlerContext
    {
        private readonly IWavePlayer _wavePlayer;
        private VolumeSampleProvider _masterVolumeControl = default!;

        private float _initialVolumePercent;

        public NAudioAudioHandlerContext(
            IWavePlayer wavePlayer,
            float initialVolumePercent
            )
        {
            _wavePlayer = wavePlayer;
            _initialVolumePercent = initialVolumePercent;
        }

        public void Init(ISampleProvider sampleProvider)
        {
            // Route all audio through a maste volume control
            _masterVolumeControl = new VolumeSampleProvider(sampleProvider)
            {
                Volume = _initialVolumePercent / 100f
            };
            _wavePlayer.Init(_masterVolumeControl);

            // Executing StartWavePlayer method will not start producing audio until oscillators are added to the Mixer
            StartWavePlayer();
        }

        public void SetMasterVolumePercent(float masterVolumePercent)
        {
            _initialVolumePercent = masterVolumePercent;
            if (_masterVolumeControl != null)
                _masterVolumeControl.Volume = masterVolumePercent / 100f;
        }

        public void StartWavePlayer()
        {
            if (_wavePlayer.PlaybackState != PlaybackState.Playing)
                _wavePlayer.Play();
        }

        public void StopWavePlayer()
        {
            if (_wavePlayer.PlaybackState != PlaybackState.Stopped)
                _wavePlayer.Stop();
        }

        public void PauseWavePlayer()
        {
            if (_wavePlayer.PlaybackState != PlaybackState.Paused)
                _wavePlayer.Pause();
        }

        public void Cleanup()
        {
            if (_wavePlayer.PlaybackState != PlaybackState.Stopped)
                _wavePlayer.Stop();
            _wavePlayer.Dispose();
        }
    }
}
