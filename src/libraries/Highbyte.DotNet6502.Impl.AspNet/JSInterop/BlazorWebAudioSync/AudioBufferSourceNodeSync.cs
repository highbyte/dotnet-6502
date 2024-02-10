using Highbyte.DotNet6502.Impl.AspNet.JSInterop.BlazorWebAudioSync.Options;
using Microsoft.JSInterop;

namespace Highbyte.DotNet6502.Impl.AspNet.JSInterop.BlazorWebAudioSync;

public class AudioBufferSourceNodeSync : AudioScheduledSourceNodeSync
{
    public static AudioBufferSourceNodeSync Create(IJSRuntime jSRuntime, BaseAudioContextSync context, AudioBufferSourceNodeOptions options)
    {
        var helper = context.WebAudioHelper;

        IJSInProcessObjectReference jSInstance;

        if (options.Buffer == null)
        {
            var args = new
            {
                detune = options.Detune,
                loop = options.Loop,
                loopEnd = options.LoopEnd,
                loopStart = options.LoopStart,
                playbackRate = options.PlaybackRate,
                channelCount = options.ChannelCount,
                channelCountMode = options.ChannelCountMode,
                channelInterpretation = options.ChannelInterpretation
            };
            jSInstance = helper.Invoke<IJSInProcessObjectReference>("constructAudioBufferSourceNode", context.JSReference, args);
        }
        else
        {
            var args = new
            {
                buffer = options!.Buffer.JSReference,
                detune = options.Detune,
                loop = options.Loop,
                loopEnd = options.LoopEnd,
                loopStart = options.LoopStart,
                playbackRate = options.PlaybackRate,
                channelCount = options.ChannelCount,
                channelCountMode = options.ChannelCountMode,
                channelInterpretation = options.ChannelInterpretation
            };
            jSInstance = helper.Invoke<IJSInProcessObjectReference>("constructAudioBufferSourceNode", context.JSReference, args);
        }

        return new AudioBufferSourceNodeSync(helper, jSRuntime, jSInstance);
    }

    protected AudioBufferSourceNodeSync(IJSInProcessObjectReference helper, IJSRuntime jSRuntime, IJSInProcessObjectReference jSReference) : base(helper, jSRuntime, jSReference)
    {
    }

    public AudioParamSync GetPlaybackRate()
    {
        var jSInstance = WebAudioHelper.Invoke<IJSInProcessObjectReference>("getAttribute", JSReference, "playbackRate");
        return AudioParamSync.Create(_helper, JSRuntime, jSInstance);
    }
}
