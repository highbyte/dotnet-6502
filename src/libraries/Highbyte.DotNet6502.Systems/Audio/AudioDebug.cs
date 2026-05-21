namespace Highbyte.DotNet6502.Systems.Audio;

/// <summary>
/// Shared formatting for command-stream audio debug/trace messages, used by the host audio
/// command targets. System-agnostic.
/// </summary>
public static class AudioDebug
{
    public static string Format(string msg, int? voice, AudioOscillatorType? oscillatorType, AudioVoiceStatus? voiceStatus)
    {
        if (oscillatorType.HasValue && voiceStatus.HasValue)
            return $"(Voice{voice}-{oscillatorType}-{voiceStatus}): {msg}";
        if (oscillatorType.HasValue)
            return $"(Voice{voice}-{oscillatorType}): {msg}";
        if (voiceStatus.HasValue)
            return $"(Voice{voice}-{voiceStatus}): {msg}";
        if (voice.HasValue)
            return $"(Voice{voice}): {msg}";
        return msg;
    }
}
