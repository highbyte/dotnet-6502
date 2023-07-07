namespace Highbyte.DotNet6502.Systems.Commodore64.Audio
{
    public enum AudioCommand
    {
        None,
        StartADS,           // Start attack/decay/sustain cycle.
        StartRelease,       // Start release cycle, which will fade volume down to 0 during the release period.
        ChangeFrequency,    // Change frequency on current playing audio.
        ChangePulseWidth,    // Change pulse width on current playing audio (only for pulse oscillator)
        ChangeVolume,       // Change volume on current playing audio.
        Stop                // Stop current playing audio right away.
    }

    public static class AudioCommandBuilder
    {
        public static AudioCommand GetAudioCommand(
            byte voice,
            AudioStatus audioStatus,
            InternalSidState sidState)
        {
            AudioCommand command = AudioCommand.None;

            var gateControl = sidState.GetGateControl(voice);
            var isFrequencyChanged = sidState.IsFrequencyChanged(voice);
            var isPulseWidthChanged = sidState.IsPulseWidthChanged(voice);
            var isVolumeChanged = sidState.IsVolumeChanged;

            // New audio (ADS cycle) is started when
            // - Starting ADS is selected in the SID gate register
            // - and no audio is playing (or when the release cycle has started)
            if (gateControl == InternalSidState.GateControl.StartAttackDecaySustain
                && (audioStatus == AudioStatus.Stopped ||
                    audioStatus == AudioStatus.ReleaseCycleStarted))
            {
                command = AudioCommand.StartADS;
            }

            // Release cycle can be started when
            // - Starting Release is selected in the SID gate register
            // - ADS cycle has already been started
            // - or ADS cycle has already stopped (which in case nothing will really happen
            else if (gateControl == InternalSidState.GateControl.StartRelease
                   && (audioStatus == AudioStatus.ADSCycleStarted || audioStatus == AudioStatus.Stopped))
            {
                command = AudioCommand.StartRelease;
            }

            // Audio is stopped immediately when
            // - Gate is off (in gate register when gate bit is 0 and no waveform selected)
            else if (gateControl == InternalSidState.GateControl.StopAudio)
            {
                command = AudioCommand.Stop;
            }

            // Check if frequency has changed, and if any audio is currently playing.
            else if (isFrequencyChanged
                    && (audioStatus == AudioStatus.ADSCycleStarted
                    || audioStatus == AudioStatus.ReleaseCycleStarted))
            {
                command = AudioCommand.ChangeFrequency;
            }

            // Check if pulsewidth has changed, and if any audio is currently playing.
            else if (isPulseWidthChanged
                    && (audioStatus == AudioStatus.ADSCycleStarted
                    || audioStatus == AudioStatus.ReleaseCycleStarted))
            {
                command = AudioCommand.ChangePulseWidth;
            }

            // Check if volume has changed, and if any audio is currently playing.
            else if (isVolumeChanged
                    && (audioStatus == AudioStatus.ADSCycleStarted
                    || audioStatus == AudioStatus.ReleaseCycleStarted))
            {
                command = AudioCommand.ChangeVolume;
            }

            return command;
        }
    }
}
