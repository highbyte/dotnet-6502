using Highbyte.DotNet6502.Impl.AspNet.JSInterop.BlazorWebAudioSync;
using Highbyte.DotNet6502.Systems;
using Microsoft.JSInterop;

namespace Highbyte.DotNet6502.Impl.AspNet;

public class WASMAudioHandlerContext : IAudioHandlerContext
{
    private readonly Func<AudioContextSync> _getAudioContext;
    public AudioContextSync AudioContext => _getAudioContext();

    private readonly IJSRuntime _jsRuntime;
    private readonly float _initialVolumePercent;

    public IJSRuntime JSRuntime => _jsRuntime;

    private GainNodeSync _masterVolumeGainNode = default!;
    internal GainNodeSync MasterVolumeGainNode => _masterVolumeGainNode;

    public bool IsInitialized { get; private set; }

    public WASMAudioHandlerContext(
        Func<AudioContextSync> getAudioContext,
        IJSRuntime jsRuntime,
        float initialVolumePercent
        )
    {
        _getAudioContext = getAudioContext;
        _jsRuntime = jsRuntime;
        _initialVolumePercent = initialVolumePercent;
    }

    public void Init()
    {
        // Create GainNode for master volume
        _masterVolumeGainNode = GainNodeSync.Create(JSRuntime, AudioContext);

        // Set initial master volume %
        SetMasterVolumePercent(_initialVolumePercent);

        IsInitialized = true;
    }

    public void Cleanup()
    {
    }

    public void SetMasterVolumePercent(float masterVolumePercent)
    {
        var currentTime = AudioContext.GetCurrentTime();
        var gain = MasterVolumeGainNode.GetGain();
        gain.CancelScheduledValues(currentTime);
        float newGain = Math.Clamp(masterVolumePercent, 0f, 100f) / 100f;
        gain.SetValueAtTime(newGain, currentTime);
    }
}
