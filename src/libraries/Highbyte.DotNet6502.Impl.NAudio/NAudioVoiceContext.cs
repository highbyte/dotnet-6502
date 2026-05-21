using Highbyte.DotNet6502.Impl.NAudio.Synth;
using Highbyte.DotNet6502.Systems.Audio;
using NAudio.Wave.SampleProviders;

namespace Highbyte.DotNet6502.Impl.NAudio;

/// <summary>
/// One NAudio synth voice for the command-stream audio style. System-agnostic — driven by the
/// host-neutral <see cref="AudioVoiceParameter"/> / <see cref="AudioOscillatorType"/> vocabulary.
/// </summary>
public class NAudioVoiceContext
{
    private readonly bool _disconnectOscillatorOnStop = true;

    // The NAudio mixer the voice's oscillators connect to. Supplied by the audio command target.
    private MixingSampleProvider _mixer = default!;

    private Action<string, int?, AudioOscillatorType?, AudioVoiceStatus?> _addDebugMessage = default!;

    internal void AddDebugMessage(string msg)
    {
        _addDebugMessage(msg, _voice, CurrentOscillatorType, Status);
    }

    private readonly byte _voice;
    public byte Voice => _voice;
    public AudioVoiceStatus Status = AudioVoiceStatus.Stopped;
    public AudioOscillatorType CurrentOscillatorType = AudioOscillatorType.None;

    public SynthEnvelopeProvider? GetOscillator(AudioOscillatorType oscillatorType) => oscillatorType switch
    {
        AudioOscillatorType.None => null,
        AudioOscillatorType.Triangle => TriangleOscillator,
        AudioOscillatorType.Sawtooth => SawToothOscillator,
        AudioOscillatorType.Pulse => PulseOscillator,
        AudioOscillatorType.Noise => NoiseOscillator,
        _ => null
    };
    public SynthEnvelopeProvider? CurrentOscillator => GetOscillator(CurrentOscillatorType);

    // Triangle Oscillator
    public SynthEnvelopeProvider TriangleOscillator { get; private set; } = default!;

    // Sawtooth Oscillator
    public SynthEnvelopeProvider SawToothOscillator { get; private set; } = default!;

    // Pulse oscillator
    public SynthEnvelopeProvider PulseOscillator { get; private set; } = default!;

    // Noise oscillator
    public SynthEnvelopeProvider NoiseOscillator { get; private set; } = default!;

    public NAudioVoiceContext(byte voice)
    {
        _voice = voice;
    }

    internal void Init(
        MixingSampleProvider mixer,
        Action<string, int?, AudioOscillatorType?, AudioVoiceStatus?> addDebugMessage)
    {
        Status = AudioVoiceStatus.Stopped;

        _mixer = mixer;
        _addDebugMessage = addDebugMessage;

        // Create oscillators in advance
        foreach (var oscillatorType in Enum.GetValues<AudioOscillatorType>())
        {
            var audioVoiceParameter = new AudioVoiceParameter
            {
                OscillatorType = oscillatorType,
                Frequency = 300f,
                PulseWidth = -0.22f,
            };
            CreateOscillator(audioVoiceParameter);
        }

        CurrentOscillatorType = AudioOscillatorType.None;
    }

    internal void Stop()
    {
        AddDebugMessage($"StopWavePlayer issued");

        // In this scenario, the oscillator is still running. Set volume to 0
        AddDebugMessage($"Mute oscillator");

        // Set ADSR state to idle
        ResetOscillatorADSR(CurrentOscillatorType);

        // If configured, disconnect the oscillator when stopping
        if (_disconnectOscillatorOnStop)
        {
            DisconnectOscillator(CurrentOscillatorType);
            CurrentOscillatorType = AudioOscillatorType.None;
        }

        if (Status != AudioVoiceStatus.Stopped)
        {
            Status = AudioVoiceStatus.Stopped;
            AddDebugMessage($"Status changed.");
        }
        else
        {
            AddDebugMessage($"Status already was Stopped");
        }
    }

    internal void StopAllOscillatorsNow()
    {
        foreach (var oscillatorType in Enum.GetValues<AudioOscillatorType>())
        {
            StopOscillatorNow(oscillatorType);
        }
    }

    private void StopOscillatorNow(AudioOscillatorType oscillatorType)
    {
        AddDebugMessage($"Stopping oscillator: {oscillatorType}");
        var oscillator = GetOscillator(oscillatorType);
        if (oscillator != null)
        {
            ResetOscillatorADSR(oscillatorType);
            DisconnectOscillator(oscillatorType);
        }
    }

    private void StopOscillatorLater(AudioOscillatorType oscillatorType)
    {
        AddDebugMessage($"Stopping oscillator: {oscillatorType} later");
        var oscillator = GetOscillator(oscillatorType);
        oscillator?.StartRelease();
    }

    private void ConnectOscillator(AudioOscillatorType oscillatorType)
    {
        AddDebugMessage($"Connecting oscillator: {oscillatorType}");

        var oscillator = GetOscillator(oscillatorType);
        if (oscillator != null)
            _mixer.AddMixerInput(oscillator);
    }

    private void DisconnectOscillator(AudioOscillatorType oscillatorType)
    {
        AddDebugMessage($"Disconnecting oscillator: {oscillatorType}");
        var oscillator = GetOscillator(oscillatorType);
        if (oscillator != null)
            _mixer.RemoveMixerInput(oscillator);
    }

    private void ResetOscillatorADSR(AudioOscillatorType oscillatorType)
    {
        AddDebugMessage($"Reseting oscillator ADSR: {oscillatorType}");
        var oscillator = GetOscillator(oscillatorType);
        oscillator?.ResetADSR();
    }

    private void CreateOscillator(AudioVoiceParameter audioVoiceParameter)
    {
        AddDebugMessage($"Creating oscillator: {audioVoiceParameter.OscillatorType}");

        switch (audioVoiceParameter.OscillatorType)
        {
            case AudioOscillatorType.None:
                break;
            case AudioOscillatorType.Triangle:
                TriangleOscillator = new SynthEnvelopeProvider(SignalGeneratorType.Triangle);
                break;
            case AudioOscillatorType.Sawtooth:
                SawToothOscillator = new SynthEnvelopeProvider(SignalGeneratorType.SawTooth);
                break;
            case AudioOscillatorType.Pulse:
                PulseOscillator = new SynthEnvelopeProvider(SignalGeneratorType.Square);
                break;
            case AudioOscillatorType.Noise:
                NoiseOscillator = new SynthEnvelopeProvider(SignalGeneratorType.White);
                break;
            default:
                break;
        }
    }

    private void StartAttackPhase(AudioOscillatorType oscillatorType)
    {
        AddDebugMessage($"Starting oscillator: {oscillatorType}");

        var oscillator = GetOscillator(oscillatorType);
        oscillator?.StartAttack();
    }

    private void SetOscillatorParameters(AudioVoiceParameter audioVoiceParameter)
    {
        AddDebugMessage($"Setting oscillator parameters: {audioVoiceParameter.OscillatorType}");

        switch (audioVoiceParameter.OscillatorType)
        {
            case AudioOscillatorType.None:
            case AudioOscillatorType.Triangle:
            case AudioOscillatorType.Sawtooth:
            case AudioOscillatorType.Noise:
                // Set frequency
                SetFrequencyOnCurrentOscillator(audioVoiceParameter.Frequency);
                break;
            case AudioOscillatorType.Pulse:
                // Set frequency
                SetFrequencyOnCurrentOscillator(audioVoiceParameter.Frequency);
                // Set pulsewidth
                SetPulseWidthOnCurrentOscillator(audioVoiceParameter.PulseWidth);
                break;
            default:
                break;
        }
    }

    private void SwitchOscillatorConnection(AudioOscillatorType newOscillatorType, bool forceSwitch = true)
    {
        // If current oscillator is the same as the requested one, do nothing (assume it's already connected)
        if (!forceSwitch && newOscillatorType == CurrentOscillatorType)
            return;

        // If any other oscillator is currently connected
        if (CurrentOscillatorType != AudioOscillatorType.None)
            // StopWavePlayer any existing playing audio will also disconnect it's oscillator
            Stop();

        // Then connect the specified oscillator
        ConnectOscillator(newOscillatorType);

        // Remember the new current oscillator
        CurrentOscillatorType = newOscillatorType;
    }

    internal void StartAudioADSPhase(AudioVoiceParameter audioVoiceParameter)
    {
        // Assume oscillator is already created and started
        // 1. Add oscillator to Mixer (and remove previous oscillator if different)
        // 2. Set parameters on existing oscillator such as Frequency, PulseWidth, etc.
        // 3. Set Gain ADSR envelope -> This will start the audio

        SwitchOscillatorConnection(audioVoiceParameter.OscillatorType);
        SetOscillatorParameters(audioVoiceParameter);
        SetGainADS(audioVoiceParameter);

        StartAttackPhase(CurrentOscillatorType);

        Status = AudioVoiceStatus.ADSCycleStarted;
        AddDebugMessage($"Status changed");
    }

    internal void StartAudioReleasePhase(AudioVoiceParameter audioVoiceParameter)
    {
        SetGainRelease(audioVoiceParameter);

        StopOscillatorLater(CurrentOscillatorType);

        Status = AudioVoiceStatus.ReleaseCycleStarted;
        AddDebugMessage($"Status changed");
    }

    private void SetGainADS(AudioVoiceParameter audioVoiceParameter)
    {
        AddDebugMessage($"Setting Attack ({audioVoiceParameter.AttackDurationSeconds}) Decay ({audioVoiceParameter.DecayDurationSeconds}) Sustain ({audioVoiceParameter.SustainGain})");

        var oscillator = CurrentOscillator;
        if (oscillator != null)
        {
            oscillator.AttackSeconds = (float)audioVoiceParameter.AttackDurationSeconds;
            oscillator.DecaySeconds = (float)audioVoiceParameter.DecayDurationSeconds;
            oscillator.SustainLevel = (float)audioVoiceParameter.SustainGain;
        }
    }

    private void SetGainRelease(AudioVoiceParameter audioVoiceParameter)
    {
        AddDebugMessage($"Setting Gain Release ({audioVoiceParameter.ReleaseDurationSeconds})");

        var oscillator = CurrentOscillator;
        if (oscillator != null)
            oscillator.ReleaseSeconds = (float)audioVoiceParameter.ReleaseDurationSeconds;
    }

    internal void SetFrequencyOnCurrentOscillator(float frequency)
    {
        AddDebugMessage($"Changing freq to {frequency}.");

        var oscillator = CurrentOscillator;
        if (oscillator != null)
            oscillator.Frequency = (double)frequency;
    }

    internal void SetPulseWidthOnCurrentOscillator(float pulseWidth)
    {
        AddDebugMessage($"Changing pulsewidth to {pulseWidth}.");

        var oscillator = CurrentOscillator;
        if (oscillator != null)
            oscillator.Duty = (double)pulseWidth;
    }
}
