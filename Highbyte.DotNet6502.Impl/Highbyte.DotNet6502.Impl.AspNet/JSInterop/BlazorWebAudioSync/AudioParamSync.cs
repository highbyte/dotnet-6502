// --------------------------------------------------------------------------------
// Synchronous Blazor WebAudio interop code based on KristofferStrube's Blazor.WebAudio library.
// https://github.com/KristofferStrube/Blazor.WebAudio
// --------------------------------------------------------------------------------

using Highbyte.DotNet6502.Impl.AspNet.JSInterop.BlazorDOMSync;
using Microsoft.JSInterop;

namespace Highbyte.DotNet6502.Impl.AspNet.JSInterop.BlazorWebAudioSync;

public class AudioParamSync : BaseJSWrapperSync
{
    public static AudioParamSync Create(IJSInProcessObjectReference helper, IJSRuntime jSRuntime, IJSInProcessObjectReference jSReference)
    {
        return new AudioParamSync(helper, jSRuntime, jSReference);
    }

    public AudioParamSync(IJSInProcessObjectReference helper, IJSRuntime jSRuntime, IJSInProcessObjectReference jSReference) : base(helper, jSRuntime, jSReference)
    {
    }

    public float GetCurrentValue()
    {
        //var helper = webAudioHelper.Value;
        var helper = _helper;
        return helper.Invoke<float>("getAttribute", JSReference, "value");
    }

    /// <summary>
    /// Changes the value <paramref name="value"/> linearly starting at the previous event and ending at <paramref name="endTime"/>.
    /// </summary>
    /// <param name="value">The value to change to.</param>
    /// <param name="endTime">The endtime in relation to the current <see cref="AudioContextSync"/>'s <see cref="AudioContextSync.GetCurrentTimeAsync"/>.</param>
    /// <returns></returns>
    public AudioParamSync LinearRampToValueAtTime(float value, double endTime)
    {
        var jSInstance = JSReference.Invoke<IJSInProcessObjectReference>("linearRampToValueAtTime", value, endTime);

        return Create(_helper, JSRuntime, jSInstance);
    }

    /// <summary>
    /// The SetValueAtTimeAsync() method schedules an instant change to the AudioParam value at a precise time, 
    /// as measured against AudioContext.currentTime. The new value is given in the value parameter.
    /// </summary>
    /// <param name="value">The value to change to.</param>
    /// <param name="startTime">The time the value should instantly be set in relation to the current <see cref="AudioContextSync"/>'s <see cref="AudioContextSync.GetCurrentTimeAsync"/>.</param>
    /// <returns></returns>
    public AudioParamSync SetValueAtTime(float value, double startTime)
    {
        var jSInstance = JSReference.Invoke<IJSInProcessObjectReference>("setValueAtTime", value, startTime);
        return Create(_helper, JSRuntime, jSInstance);
    }

    /// <summary>
    /// The SetTargetAtTimeAsync() method of the schedules the start of a gradual change to the AudioParam value.
    /// This is useful for decay or release portions of ADSR envelopes.
    /// </summary>
    /// <param name="target">The value the parameter will start to transition towards at the given start time.</param>
    /// <param name="startTime">The time that the exponential transition will begin, in the same time coordinate system as <see cref="AudioContextSync"/>'s <see cref="AudioContextSync.GetCurrentTimeAsync"/>. If it is less than or equal to AudioContext current time, the parameter will start changing immediately.</param>
    /// <param name="timeConstant">The time-constant value, given in seconds, of an exponential approach to the target value. The larger this value is, the slower the transition will be.</param>
    /// <returns></returns>
    public AudioParamSync SetTargetAtTime(float target, double startTime, double timeConstant)
    {
        var jSInstance = JSReference.Invoke<IJSInProcessObjectReference>("setTargetAtTime", target, startTime, timeConstant);
        return Create(_helper, JSRuntime, jSInstance);
    }

    /// <summary>
    /// The CancelScheduledValuesAsync() method cancels all scheduled future changes to the AudioParam.
    /// </summary>
    /// <param name="startTime">A double representing the time (in seconds) after the <see cref="AudioContextSync"/>'s <see cref="AudioContextSync.GetCurrentTimeAsync"/> was first created after which all scheduled changes will be cancelled.</param>
    /// <returns></returns>
    public AudioParamSync CancelScheduledValues(double startTime)
    {
        var jSInstance = JSReference.Invoke<IJSInProcessObjectReference>("cancelScheduledValues", startTime);
        return Create(_helper, JSRuntime, jSInstance);
    }
}
