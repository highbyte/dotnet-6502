using Highbyte.DotNet6502.Impl.AspNet.JSInterop.BlazorWebAudioSync.Options;
using Microsoft.JSInterop;

namespace Highbyte.DotNet6502.Impl.AspNet.JSInterop.BlazorWebAudioSync;

/// <summary>
/// A special form of oscillator that can be used to modulate an oscillator with pulse width.
/// Based on JS example: https://github.com/pendragon-andyh/WebAudio-PulseOscillator/blob/master/example-synth.html
/// 
/// Note: This is not a standard WebAudio node, but a custom implementation.
/// </summary>
public class CustomPulseOscillatorNodeSync : AudioScheduledSourceNodeSync
{
    private static Float32ArraySync? s_pulseCurve;
    private static Float32ArraySync? s_constantOneCurve;

    private WaveShaperNodeSync _pulseShaper;

    public GainNodeSync WidthGainNode { get; private set; }

    public static CustomPulseOscillatorNodeSync Create(
        IJSRuntime jSRuntime,
        BaseAudioContextSync context,
        CustomPulseOscillatorOptions? options = null)
    {
        // Pre-calculate curves
        InitCurves(jSRuntime, context);

        var helper = context.WebAudioHelper;

        // Create a "normal" oscillator instance
        var oscillatorJSInstance = CreateOscillator(context, options);
        var oscillator = new CustomPulseOscillatorNodeSync(helper, jSRuntime, oscillatorJSInstance);

        //Shape the output into a pulse wave.
        oscillator._pulseShaper = WaveShaperNodeSync.Create(jSRuntime, context);
        oscillator._pulseShaper.SetCurve(s_pulseCurve!);
        ((AudioNodeSync)oscillator).Connect(oscillator._pulseShaper);

        //Use a GainNode as our new "width" audio parameter.
        var initialDefaultWidth = options?.DefaultWidth ?? 0;
        var widthGainNode = GainNodeSync.Create(jSRuntime, context, new GainOptions() { Gain = initialDefaultWidth });
        //widthGainNode.GetGain().SetValue(initialDefaultWidth);
        oscillator.WidthGainNode = widthGainNode;
        widthGainNode.Connect(oscillator._pulseShaper);

        //Pass a constant value of 1 into the widthGain – so the "width" setting is
        //duplicated to its output.
        var constantOneShaper = WaveShaperNodeSync.Create(jSRuntime, context);
        constantOneShaper.SetCurve(s_constantOneCurve!);
        ((AudioNodeSync)oscillator).Connect(constantOneShaper);
        constantOneShaper.Connect(widthGainNode);

        return oscillator;
    }

    // override the oscillator's "connect" and "disconnect" method so that the
    // new node's output actually comes from the squareShaper.
    public new AudioNodeSync Connect(AudioNodeSync destinationNode, ulong output = 0, ulong input = 0)
    {
        return _pulseShaper.Connect(destinationNode, output, input);
    }

    public new void Disconnect()
    {
        _pulseShaper.Disconnect();
    }

    public AudioParamSync GetFrequency()
    {
        var helper = WebAudioHelper;
        var jSInstance = helper.Invoke<IJSInProcessObjectReference>("getAttribute", JSReference, "frequency");
        return AudioParamSync.Create(_helper, JSRuntime, jSInstance);
    }

    private static IJSInProcessObjectReference CreateOscillator(BaseAudioContextSync context, CustomPulseOscillatorOptions? options = null)
    {
        var helper = context.WebAudioHelper;
        IJSInProcessObjectReference jSInstance;

        if (options is null)
        {
            jSInstance = helper.Invoke<IJSInProcessObjectReference>("constructOcillatorNode", context.JSReference);
        }
        else
        {
            var args = new
            {
                type = OscillatorType.Sawtooth.AsString(),
                frequency = options!.Frequency,
                detune = options!.Detune
                //periperiodicWave = options!.PeriodicWave! != null ? options!.PeriodicWave.JSReference : null
            };
            jSInstance = helper.Invoke<IJSInProcessObjectReference>("constructOcillatorNode", context.JSReference, args);
        }
        return jSInstance;
    }

    private static void InitCurves(IJSRuntime jSRuntime, BaseAudioContextSync context)
    {
        // Skip if curves already pre-calculated
        if (s_pulseCurve is not null)
            return;

        var pulseCurveValues = new float[256];
        for (var i = 0; i < 256; i++)
        {
            pulseCurveValues[i] = i < 128 ? -1 : 1;
        }
        s_pulseCurve = Float32ArraySync.Create(context.WebAudioHelper, jSRuntime, pulseCurveValues);

        var constantOneCurveValues = new float[2] { 1, 1 };
        s_constantOneCurve = Float32ArraySync.Create(context.WebAudioHelper, jSRuntime, constantOneCurveValues);
    }

    protected CustomPulseOscillatorNodeSync(
        IJSInProcessObjectReference helper,
        IJSRuntime jSRuntime,
        IJSInProcessObjectReference jSReference
        ) : base(helper, jSRuntime, jSReference)
    {
    }
}
