using Highbyte.DotNet6502.Impl.AspNet.JSInterop.BlazorWebAudioSync;
using Highbyte.DotNet6502.Systems;
using Microsoft.JSInterop;

namespace Highbyte.DotNet6502.Impl.AspNet
{
    public class WASMAudioHandlerContext : IAudioHandlerContext
    {
        private readonly AudioContextSync _audioContext;
        public AudioContextSync AudioContext => _audioContext;

        private readonly IJSRuntime _jsRuntime;
        private readonly float _initialVolumePercent;

        public IJSRuntime JSRuntime => _jsRuntime;

        private GainNodeSync _masterVolumeGainNode;
        public GainNodeSync MasterVolumeGainNode => _masterVolumeGainNode;

        public WASMAudioHandlerContext(
            AudioContextSync audioContext,
            IJSRuntime jsRuntime,
            float initialVolumePercent
            )
        {
            _audioContext = audioContext;
            _jsRuntime = jsRuntime;
            _initialVolumePercent = initialVolumePercent;
        }

        public void Init()
        {
            // Create GainNode for master volume
            _masterVolumeGainNode = GainNodeSync.Create(JSRuntime, AudioContext);

            // Set initial master volume %
            SetMasterVolume(_initialVolumePercent);
        }

        public void SetMasterVolume(float masterVolumePercent)
        {
            var currentTime = AudioContext.GetCurrentTime();
            var gain = MasterVolumeGainNode.GetGain();
            gain.CancelScheduledValues(currentTime);
            float newGain = Math.Clamp(masterVolumePercent, 0f, 100f) / 100f;
            gain.SetValueAtTime(newGain, currentTime);
        }
    }
}
