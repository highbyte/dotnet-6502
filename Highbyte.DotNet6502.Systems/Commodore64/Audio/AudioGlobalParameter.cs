namespace Highbyte.DotNet6502.Systems.Commodore64.Audio
{
    public class AudioGlobalParameter
    {
        public AudioGlobalCommand AudioCommand { get; set; }

        public float Gain { get; set; }

        public static AudioGlobalParameter BuildAudioGlobalParameter(
            InternalSidState sidState
            )
        {
            // ----------
            // Map SID register values to audio parameters usable by Web Audio, and what to do with the audio.
            // ----------
            var audioVoiceParameter = new AudioGlobalParameter
            {
                // What to do with the audio (Start ADS cycle, start Release cycle, stop audio, change frequency, change volume)
                AudioCommand = AudioGlobalCommandBuilder.GetGlobalAudioCommand(
                    sidState),

                // Translate SID volume 0-15 to Gain 0.0-1.0
                // SID volume in lower 4 bits of SIGVOL register.
                Gain = Math.Clamp((float)(sidState.GetVolume() / 15.0f), 0.0f, 1.0f),
            };
            return audioVoiceParameter;
        }
    }
}
