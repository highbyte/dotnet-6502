using Highbyte.DotNet6502.Systems.Audio;
using Highbyte.DotNet6502.Systems.Utils;

namespace Highbyte.DotNet6502.Systems.Commodore64.Audio;

/// <summary>
/// The single, host-agnostic C64 audio provider for the command-stream style.
///
/// This is the reusable replacement for the duplicated decode/dispatch skeleton previously copied
/// into <c>C64NAudioAudioHandler</c> and <c>C64WASMAudioHandler</c> (<c>PlayAllVoices</c> /
/// <c>PlayVoice</c>, <c>_enabledVoices</c> / <c>_enabledOscillators</c>, per-voice status). After
/// each CPU instruction it decodes changed SID state into <see cref="IAudioCommand"/>s and pushes
/// them via <see cref="CommandEmitted"/>; a host <see cref="IAudioCommandTarget"/> turns them into
/// sound. The genuinely host-specific synthesis (NAudio / WebAudio) stays in the targets.
/// </summary>
[DisplayName("Synth commands")]
[HelpText("Decodes SID register changes into host-agnostic synth commands (volume, voice ADSR + oscillator).\nThe host audio target (NAudio / WebAudio) drives an oscillator graph from those commands.\nLow CPU, but cannot reproduce SID filter, combined waveforms, ring mod, or sample playback.")]
public sealed class C64SidCommandStream : IAudioProvider, IAudioCommandStream
{
    private readonly C64 _c64;

    public string Name => "C64SidCommandStream";

    /// <summary>The C64 SID has 3 voices.</summary>
    public int VoiceCount => 3;

    public event Action<IAudioCommand>? CommandEmitted;

    // TODO: Make enabled voices / oscillators configurable (carried over as-is from the old handlers).
    private readonly List<byte> _enabledVoices = new() { 1, 2, 3 };
    private readonly List<AudioOscillatorType> _enabledOscillators = new()
        { AudioOscillatorType.Triangle, AudioOscillatorType.Sawtooth, AudioOscillatorType.Pulse, AudioOscillatorType.Noise };

    // Per-voice ADSR status. Owned here because the SID command decode depends on it
    // (C64AudioVoiceParameterBuilder needs the current voice status).
    private readonly Dictionary<byte, AudioVoiceStatus> _voiceStatus = new()
    {
        { 1, AudioVoiceStatus.Stopped },
        { 2, AudioVoiceStatus.Stopped },
        { 3, AudioVoiceStatus.Stopped },
    };

    public C64SidCommandStream(C64 c64)
    {
        _c64 = c64;
    }

    /// <summary>
    /// Called after each CPU instruction (SID register writes happen between instructions). When
    /// the SID state has changed, decode it into audio commands.
    /// </summary>
    public void OnAfterInstruction()
    {
        var sid = _c64.Sid;
        if (!sid.InternalSidState.IsAudioChanged)
            return;

        Decode(sid.InternalSidState);
        sid.InternalSidState.ClearAudioChanged();
    }

    public void OnEndFrame()
    {
        // Audio is generated per instruction; nothing to do at frame end.
    }

    private void Decode(InternalSidState sidState)
    {
        // Global SID volume change takes precedence (matches the original handler behaviour).
        var globalParameter = AudioGlobalParameter.BuildAudioGlobalParameter(sidState);
        if (globalParameter.AudioCommand == AudioGlobalCommand.ChangeVolume)
        {
            CommandEmitted?.Invoke(new SetVolumeAudioCommand(globalParameter.Gain));
            return;
        }

        foreach (var voice in _enabledVoices)
        {
            var status = _voiceStatus[voice];
            var parameter = C64AudioVoiceParameterBuilder.BuildAudioVoiceParameter(voice, status, sidState);

            if (parameter.AudioCommand == AudioVoiceCommand.None)
                continue;

            // Stop is unconditional; the other actions are skipped if the oscillator is disabled.
            if (parameter.AudioCommand != AudioVoiceCommand.Stop
                && !_enabledOscillators.Contains(parameter.OscillatorType))
                continue;

            // A release on an already-stopped voice is a no-op.
            if (parameter.AudioCommand == AudioVoiceCommand.StartRelease
                && status == AudioVoiceStatus.Stopped)
                continue;

            CommandEmitted?.Invoke(new VoiceAudioCommand(voice, parameter));

            _voiceStatus[voice] = parameter.AudioCommand switch
            {
                AudioVoiceCommand.StartADS => AudioVoiceStatus.ADSCycleStarted,
                AudioVoiceCommand.StartRelease => AudioVoiceStatus.ReleaseCycleStarted,
                AudioVoiceCommand.Stop => AudioVoiceStatus.Stopped,
                _ => status,
            };
        }
    }
}
