namespace Highbyte.DotNet6502.Systems.Commodore64.Audio
{
    public enum AudioGlobalCommand
    {
        None,
        ChangeVolume,       // Change volume on current playing audio.
    }

    public static class AudioGlobalCommandBuilder
    {
        /// <summary>
        /// Get commands for global SID settings such as volume.
        /// </summary>
        /// <param name="audioStatus"></param>
        /// <param name="sidState"></param>
        /// <returns></returns>
        public static AudioGlobalCommand GetGlobalAudioCommand(
            InternalSidState sidState)
        {
            AudioGlobalCommand command = AudioGlobalCommand.None;

            var isVolumeChanged = sidState.IsVolumeChanged;

            // Check if SID volume has changed
            if (isVolumeChanged)
            {
                command = AudioGlobalCommand.ChangeVolume;
            }

            return command;
        }
    }
}
