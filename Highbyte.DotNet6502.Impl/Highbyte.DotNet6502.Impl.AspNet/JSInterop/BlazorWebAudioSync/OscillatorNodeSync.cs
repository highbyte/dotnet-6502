// --------------------------------------------------------------------------------
// Synchronous Blazor WebAudio interop code based on KristofferStrube's Blazor.WebAudio library.
// https://github.com/KristofferStrube/Blazor.WebAudio
// --------------------------------------------------------------------------------

using Highbyte.DotNet6502.Impl.AspNet.JSInterop.BlazorWebAudioSync.Options;
using Microsoft.JSInterop;

namespace Highbyte.DotNet6502.Impl.AspNet.JSInterop.BlazorWebAudioSync;

public class OscillatorNodeSync : AudioScheduledSourceNodeSync
{
    public static OscillatorNodeSync Create(IJSRuntime jSRuntime, BaseAudioContextSync context, OscillatorOptions? options = null)
    {
        var helper = context.WebAudioHelper;

        IJSInProcessObjectReference jSInstance;
        if (options is null)
        {
            jSInstance = helper.Invoke<IJSInProcessObjectReference>("constructOcillatorNode", context.JSReference);
        }
        else
        {
            if (options.PeriodicWave == null)
            {
                var args = new
                {
                    type = options!.Type.AsString(),
                    frequency = options!.Frequency,
                    detune = options!.Detune,
                };
                jSInstance = helper.Invoke<IJSInProcessObjectReference>("constructOcillatorNode", context.JSReference, args);
            }
            else
            {
                var args = new
                {
                    type = options!.Type.AsString(),
                    frequency = options!.Frequency,
                    detune = options!.Detune,
                    periodicWave = options!.PeriodicWave.JSReference
                };
                jSInstance = helper.Invoke<IJSInProcessObjectReference>("constructOcillatorNode", context.JSReference, args);
            }
        }

        return new OscillatorNodeSync(helper, jSRuntime, jSInstance);
    }

    protected OscillatorNodeSync(IJSInProcessObjectReference helper, IJSRuntime jSRuntime, IJSInProcessObjectReference jSReference) : base(helper, jSRuntime, jSReference)
    {
    }

    public AudioParamSync GetFrequency()
    {
        var helper = WebAudioHelper;
        var jSInstance = helper.Invoke<IJSInProcessObjectReference>("getAttribute", JSReference, "frequency");
        return AudioParamSync.Create(_helper, JSRuntime, jSInstance);
    }

    public void SetPeriodicWave(PeriodicWaveSync wave)
    {
        JSReference.InvokeVoid("setPeriodicWave", wave.JSReference);
    }
}
