using System;
using Highbyte.DotNet6502.Impl.AspNet.JSInterop.BlazorWebAudioSync;
using Highbyte.DotNet6502.Impl.AspNet.JSInterop.BlazorWebAudioSync.Options;
using Highbyte.DotNet6502.Impl.AspNet.JSInterop.BlazorWebAudioSync.WaveTables;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Highbyte.DotNet6502.App.SkiaWASM.Pages;

public partial class DebugSound
{
    AudioContextSync _audioContext;
    OscillatorNodeSync? _oscillator;
    CustomPulseOscillatorNodeSync? _customPulseOscillator;
    GainNodeSync? _ampGainNode;
    GainNodeSync? _widthDepthGainNode;

    // Input
    int _oscFrequency = 110;

    //float _ampScale = 1.0f;

    float _ampGain = 0.22f;
    double _ampAttack = 0.05f;
    double _ampDecay = 0.4f;
    float _ampSustain = 0.4f;
    double _ampRelease = 0.4f;

    bool _automaticRelease = true;  // Set to false to not start Release phase after decay/sustain phase is finished. Sound will play until manually stopped.

    float _pulseWidth = 0;

    private readonly SemaphoreSlim _semaphoreSlim = new(1);

    protected override async Task OnInitializedAsync()
    {
        _audioContext = await AudioContextSync.CreateAsync(Js);
    }

    protected void StartSound(MouseEventArgs mouseEventArgs)
    {
        var currentTime = _audioContext.GetCurrentTime();

        StopAllSoundNow(mouseEventArgs);

        var type = OscillatorType.Triangle;

        // Audio destination
        var destination = _audioContext.GetDestination();

        // Create oscillator
        OscillatorOptions oscillatorOptions = new()
        {
            Type = type,
            //Frequency = (float)Frequency(octave, pitch)
            Frequency = _oscFrequency
        };
        _oscillator = OscillatorNodeSync.Create(Js, _audioContext, oscillatorOptions);

        // Gain node for oscillator volume
        _ampGainNode = GainNodeSync.Create(Js, _audioContext);
        _ampGainNode.Connect(destination);

        var audioParam = _ampGainNode.GetGain();
        // Attack -> Decay -> Sustain -> Release
        SetADSR(audioParam, currentTime, out double? endTime,
            _ampGain,
            _ampAttack,
            _ampDecay,
            _ampSustain,
            _ampRelease,
            _automaticRelease);

        _oscillator.Connect(_ampGainNode);
        _oscillator.Start();
        if (_automaticRelease)
            _oscillator!.Stop(endTime!.Value);
    }

    protected void StartSoundPeriodicWave(MouseEventArgs mouseEventArgs)
    {
        var currentTime = _audioContext.GetCurrentTime();

        StopAllSoundNow(mouseEventArgs);

        AudioDestinationNodeSync destination = _audioContext.GetDestination();

        // Note: the "Noise" wavetable example is NOT white noise
        PeriodicWaveSync wave = _audioContext.CreatePeriodicWave(Noise.Real, Noise.Imag);

        OscillatorOptions oscillatorOptions = new()
        {
            Type = OscillatorType.Custom, // Not working currently 
            Frequency = _oscFrequency,
            PeriodicWave = wave 
        };
        _oscillator = OscillatorNodeSync.Create(Js, _audioContext, oscillatorOptions);

        // Gain node for oscillator volume
        _ampGainNode = GainNodeSync.Create(Js, _audioContext);
        _ampGainNode.Connect(destination);

        var audioParam = _ampGainNode.GetGain();
        // Attack -> Decay -> Sustain -> Release
        SetADSR(audioParam, currentTime, out double? endTime,
            _ampGain,
            _ampAttack,
            _ampDecay,
            _ampSustain,
            _ampRelease,
            _automaticRelease);

        _oscillator.Connect(_ampGainNode);
        _oscillator.Start();
        if (_automaticRelease)
            _oscillator!.Stop(endTime!.Value);
    }

    protected void StartSoundWhiteNoise(MouseEventArgs mouseEventArgs)
    {
        StopAllSoundNow(mouseEventArgs);
        var currentTime = _audioContext.GetCurrentTime();
        AudioDestinationNodeSync destination = _audioContext.GetDestination();

        float noiseDuration = 1.0f;  // Seconds

        var sampleRate = _audioContext.GetSampleRate();
        int bufferSize = (int)(sampleRate * noiseDuration);
        // Create an empty buffer
        var noiseBuffer = AudioBufferSync.Create(
            _audioContext.WebAudioHelper,
            _audioContext.JSRuntime,
            new AudioBufferOptions
            {
                Length = bufferSize,
                SampleRate = sampleRate,
            });

        var data = noiseBuffer.GetChannelData(0);
        var random = new Random();
        for (var i = 0; i < bufferSize; i++)
        {
            data[i] = ((float)random.NextDouble()) * 2 - 1;
        }

        var noise = AudioBufferSourceNodeSync.Create(
            Js,
            _audioContext,
            new AudioBufferSourceNodeOptions
            {
                Buffer = noiseBuffer
            });

        //noise.Connect(bandpass).Connect(destination);
        noise.Connect(destination);
        noise.Start(currentTime);
    }

    protected void StartSoundPulse(MouseEventArgs mouseEventArgs)
    {
        var currentTime = _audioContext.GetCurrentTime();

        StopAllSoundNow(mouseEventArgs);

        AudioDestinationNodeSync destination = _audioContext.GetDestination();

        // Volume gain
        _ampGainNode = GainNodeSync.Create(Js, _audioContext, new() { Gain = 0 });
        _ampGainNode.Connect(destination);
        var ampGainNodeAudioParam = _ampGainNode.GetGain();
        // Attack -> Decay -> Sustain -> Release
        SetADSR(ampGainNodeAudioParam, currentTime, out double? endTime,
            _ampGain,
            _ampAttack,
            _ampDecay,
            _ampSustain,
            _ampRelease,
            _automaticRelease);
        //var ampSustainTime = currentTime + _ampAttack + _ampRelease;
        //ampGainNodeAudioParam.LinearRampToValueAtTime(_ampGain, currentTime + _ampAttack);
        //ampGainNodeAudioParam.LinearRampToValueAtTime(_ampGain * _ampSustain, ampSustainTime);
        //double? endTime = ampSustainTime + _ampRelease;
        //ampGainNodeAudioParam.LinearRampToValueAtTime(0, endTime.Value);

        // Pulse oscillator
        CustomPulseOscillatorOptions customPulseOscillatorOptions = new()
        {
            Frequency = _oscFrequency,
            DefaultWidth = _pulseWidth
        };
        _customPulseOscillator = CustomPulseOscillatorNodeSync.Create(Js, _audioContext, customPulseOscillatorOptions);
        _customPulseOscillator.Connect(_ampGainNode);

        // Pulse width modulation
        _widthDepthGainNode = GainNodeSync.Create(Js, _audioContext, new() { Gain = 0 });
        _widthDepthGainNode.Connect(_customPulseOscillator.WidthGainNode);
        var widthDepthGainNodeAudioParam = _widthDepthGainNode.GetGain();
        // Pulse width: Attack->Decay->Sustain->Release
        SetADSR(widthDepthGainNodeAudioParam, currentTime, out double? endTimeWidth,
            0.5f,       // Pulse width depth (LFO depth)
            0.05f,          // Pulse width attack
            0.4f,           // Pulse width decay
            0.4f,           // Pulse width sustain
            0.4f            // Pulse width release
            );
        //var oscWidthDepth = 0.5f;   // LFO depth
        //var oscWidthAttack = 0.05f;
        //var oscWidthDecay = 0.4f;
        //var oscWidthSustain = 0.4f;
        //var oscWidthRelease = 0.4f;
        //var widthDepthSustainTime = currentTime + oscWidthAttack + oscWidthRelease;
        //widthDepthGainNodeAudioParam.LinearRampToValueAtTime(0.5f * oscWidthDepth, currentTime + oscWidthAttack);
        //widthDepthGainNodeAudioParam.LinearRampToValueAtTime(0.5f * oscWidthDepth * oscWidthSustain, widthDepthSustainTime);
        //widthDepthGainNodeAudioParam.LinearRampToValueAtTime(0, oscWidthSustain + oscWidthRelease);

        // Low frequency oscillator
        OscillatorOptions lfoOscillatorOptions = new()
        {
            Type = OscillatorType.Triangle,
            Frequency = 10
        };
        var _lfoOscillator = OscillatorNodeSync.Create(Js, _audioContext, lfoOscillatorOptions);
        //_lfoOscillator.Connect(detuneDepth);
        _lfoOscillator.Connect(_widthDepthGainNode);

        _customPulseOscillator.Start();
        _lfoOscillator.Start();

        if (_automaticRelease)
        {
            _customPulseOscillator!.Stop(endTime!.Value);
            _lfoOscillator!.Stop(endTime!.Value);
        }
    }

    protected void ChangePulseWidth(MouseEventArgs mouseEventArgs)
    {
        if (_widthDepthGainNode is null) return;
        var widthDepthGainNodeAudioParam = _widthDepthGainNode.GetGain();
        widthDepthGainNodeAudioParam.SetValueAtTime(_pulseWidth, _audioContext.GetCurrentTime());
    }

    protected void StartSoundPulse2(MouseEventArgs mouseEventArgs)
    {
        var currentTime = _audioContext.GetCurrentTime();

        StopAllSoundNow(mouseEventArgs);

        AudioDestinationNodeSync destination = _audioContext.GetDestination();

        // Volume gain
        _ampGainNode = GainNodeSync.Create(Js, _audioContext, new() { Gain = 0 });
        _ampGainNode.Connect(destination);
        var ampGainNodeAudioParam = _ampGainNode.GetGain();
        // Attack -> Decay -> Sustain -> Release
        //SetADSR(ampGainNodeAudioParam, currentTime, out double? endTime,
        //    _ampGain,
        //    _ampAttack,
        //    _ampDecay,
        //    _ampSustain,
        //    _ampRelease,
        //    _automaticRelease);
        var ampSustainTime = currentTime + _ampAttack + _ampRelease;
        ampGainNodeAudioParam.LinearRampToValueAtTime(_ampGain, currentTime + _ampAttack);
        ampGainNodeAudioParam.LinearRampToValueAtTime(_ampGain * _ampSustain, ampSustainTime);
        double? endTime = ampSustainTime + _ampRelease;
        ampGainNodeAudioParam.LinearRampToValueAtTime(0, endTime.Value);

        // Pulse oscillator
        CustomPulseOscillatorOptions customPulseOscillatorOptions = new()
        {
            Frequency = _oscFrequency,
            DefaultWidth = _pulseWidth
        };
        _customPulseOscillator = CustomPulseOscillatorNodeSync.Create(Js, _audioContext, customPulseOscillatorOptions);
        _customPulseOscillator.Connect(_ampGainNode);

        // Pulse width modulation
        var widthDepthGainNode = GainNodeSync.Create(Js, _audioContext, new() { Gain = 0 });
        widthDepthGainNode.Connect(_customPulseOscillator.WidthGainNode);
        var widthDepthGainNodeAudioParam = widthDepthGainNode.GetGain();
        // Pulse width: Attack->Decay->Sustain->Release
        //SetADSR(widthDepthGainNodeAudioParam, currentTime, out double? endTimeWidth,
        //0.5f * 0,       // 0.5 * Pulse width depth (LFO depth)
        //0.05f,          // Pulse width attack
        //0.4f,           // Pulse width decay
        //0.4f,           // Pulse width sustain
        //0.4f            // Pulse width release
        //);
        var oscWidthDepth = 0.5f;   // LFO depth
        var oscWidthAttack = 0.05f;
        var oscWidthDecay = 0.4f;
        var oscWidthSustain = 0.4f;
        var oscWidthRelease = 0.4f;
        var widthDepthSustainTime = currentTime + oscWidthAttack + oscWidthRelease;
        widthDepthGainNodeAudioParam.LinearRampToValueAtTime(0.5f * oscWidthDepth, currentTime + oscWidthAttack);
        widthDepthGainNodeAudioParam.LinearRampToValueAtTime(0.5f * oscWidthDepth * oscWidthSustain, widthDepthSustainTime);
        widthDepthGainNodeAudioParam.LinearRampToValueAtTime(0, oscWidthSustain + oscWidthRelease);

        // Low frequency oscillator
        OscillatorOptions lfoOscillatorOptions = new()
        {
            Type = OscillatorType.Triangle,
            Frequency = 10
        };
        var _lfoOscillator = OscillatorNodeSync.Create(Js, _audioContext, lfoOscillatorOptions);
        //_lfoOscillator.Connect(detuneDepth);
        _lfoOscillator.Connect(widthDepthGainNode);

        _customPulseOscillator.Start();
        _lfoOscillator.Start();

        if (_automaticRelease)
        {
            _customPulseOscillator!.Stop(endTime!.Value);
            _lfoOscillator!.Stop(endTime!.Value);
        }
    }

    protected void StopSoundR(MouseEventArgs mouseEventArgs)
    {
        if (_oscillator == null || _ampGainNode == null) return;

        var currentTime = _audioContext.GetCurrentTime();

        var gainAudioParam = _ampGainNode.GetGain();
        StartRelease(gainAudioParam, currentTime, out double _,
            _ampRelease);
    }

    protected void StopAllSoundNow(MouseEventArgs mouseEventArgs)
    {
        var currentTime = _audioContext.GetCurrentTime();

        if (_ampGainNode != null)
            StopGain(_ampGainNode, currentTime);

        //if (_gainNode2 != null)
        //    StopSound(_gainNode2 , currentTime);
    }


    /// <summary>
    /// Set Attack (duration), Decay (duration), Sustain (level), and Release (duration)
    /// </summary>
    /// <param name="audioParam"></param>
    /// <param name="startTime"></param>
    /// <param name="volume">
    /// Volume range 0.0 - 1.0
    /// </param>
    /// <param name="attackDuration">
    /// Time (s) when the gain reaches maximum volume (gain)
    /// </param>
    /// <param name="decayDuration">
    /// Time (s) between reading the maximum volume (gain) until it reaches the sustain level.
    /// </param>
    /// <param name="sustainLevel">
    /// The volume (gain) level at which the sound will sustain until the release phase.
    /// </param>
    /// <param name="releaseDuration">
    /// Time (s) between between initiating the stop of the sound until it reaches 0 volume (gain)
    /// </param>
    private void SetADSR(
        AudioParamSync gainAudioParam,
        double startTime,
        out double? endTime,
        float volume,
        double attackDuration = 0.1f,
        double decayDuration = 0.2f,
        float sustainLevel = 0.5f,
        double releaseDuration = 0.5f,
        bool automaticRelease = true)
    {
        gainAudioParam.SetValueAtTime(0, startTime);

        // Attack -> Volume
        var attackEndTime = startTime + attackDuration;
        gainAudioParam.LinearRampToValueAtTime(volume, attackEndTime);

        // Decay -> Sustain volume
        var decayStartTime = attackEndTime;
        gainAudioParam.SetTargetAtTime(sustainLevel * volume, decayStartTime, decayDuration);

        if (automaticRelease)
        {
            // Release -> 0 volume
            endTime = decayStartTime + decayDuration + releaseDuration;
            gainAudioParam.LinearRampToValueAtTime(0, endTime!.Value);
        }
        else
        {
            endTime = null;
        }
    }

    private void StartRelease(
        AudioParamSync gainAudioParam,
        double startTime,
        out double endTime,
        double releaseDuration = 0.5f)
    {
        gainAudioParam.CancelScheduledValues(startTime);

        var currentGainValue = gainAudioParam.GetCurrentValue();
        gainAudioParam.SetValueAtTime(currentGainValue, startTime);

        endTime = startTime + releaseDuration;
        gainAudioParam.LinearRampToValueAtTime(0, endTime);
    }

    protected void StopGain(GainNodeSync gainNode, double currentTime)
    {
        var audioParam = gainNode.GetGain();
        audioParam.CancelScheduledValues(currentTime);
        audioParam.LinearRampToValueAtTime(0, currentTime + 0.05);
    }

    private double Frequency(int octave, int pitch)
    {
        var noteIndex = octave * 12 + pitch;
        var a = Math.Pow(2, 1.0 / 12);
        var A4 = 440;
        var A4Index = 4 * 12 + 10;
        var halfStepDifference = noteIndex - A4Index;
        return A4 * Math.Pow(a, halfStepDifference);
    }
}
