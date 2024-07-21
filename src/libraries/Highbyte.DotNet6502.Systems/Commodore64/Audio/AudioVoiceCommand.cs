namespace Highbyte.DotNet6502.Systems.Commodore64.Audio;

public enum AudioVoiceCommand
{
    None,
    StartADS,           // Start attack/decay/sustain cycle.
    StartRelease,       // Start release cycle, which will fade volume down to 0 during the release period.
    ChangeFrequency,    // Change frequency on current playing audio.
    ChangePulseWidth,   // Change pulse width on current playing audio (only for pulse oscillator)
    Stop                // Stop current playing audio right away.
}

public static class AudioVoiceCommandBuilder
{
    public static AudioVoiceCommand GetAudioCommand(
        byte voice,
        AudioVoiceStatus currentVoiceAudioStatus,
        InternalSidState sidState)
    {
        AudioVoiceCommand command = AudioVoiceCommand.None;

        var gateControl = sidState.GetGateControl(voice);
        var isFrequencyChanged = sidState.IsFrequencyChanged(voice);
        var isPulseWidthChanged = sidState.IsPulseWidthChanged(voice);
        var isVolumeChanged = sidState.IsVolumeChanged;

        // New audio (ADS cycle) is started when
        // - Starting ADS is selected in the SID gate register
        // - and no audio is playing (or when the release cycle has started)
        if (gateControl == InternalSidState.GateControl.StartAttackDecaySustain
            && (currentVoiceAudioStatus == AudioVoiceStatus.Stopped ||
                currentVoiceAudioStatus == AudioVoiceStatus.ReleaseCycleStarted))
        {
            command = AudioVoiceCommand.StartADS;
        }

        // Release cycle can be started when
        // - Starting Release is selected in the SID gate register
        // - ADS cycle has already been started
        // - or ADS cycle has already stopped (which in case nothing will really happen
        else if (gateControl == InternalSidState.GateControl.StartRelease
               && (currentVoiceAudioStatus == AudioVoiceStatus.ADSCycleStarted || currentVoiceAudioStatus == AudioVoiceStatus.Stopped))
        {
            command = AudioVoiceCommand.StartRelease;
        }

        // Audio is stopped immediately when
        // - Gate is off (in gate register when gate bit is 0 and no waveform selected)
        else if (gateControl == InternalSidState.GateControl.StopAudio)
        {
            command = AudioVoiceCommand.Stop;
        }

        // Check if frequency has changed, and if any audio is currently playing.
        else if (isFrequencyChanged
                && (currentVoiceAudioStatus == AudioVoiceStatus.ADSCycleStarted
                || currentVoiceAudioStatus == AudioVoiceStatus.ReleaseCycleStarted))
        {
            command = AudioVoiceCommand.ChangeFrequency;
        }

        // Check if pulsewidth has changed, and if any audio is currently playing.
        else if (isPulseWidthChanged
                && (currentVoiceAudioStatus == AudioVoiceStatus.ADSCycleStarted
                || currentVoiceAudioStatus == AudioVoiceStatus.ReleaseCycleStarted))
        {
            command = AudioVoiceCommand.ChangePulseWidth;
        }

        return command;
    }
}
