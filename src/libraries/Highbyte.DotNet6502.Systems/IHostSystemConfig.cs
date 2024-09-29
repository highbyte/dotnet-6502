namespace Highbyte.DotNet6502.Systems;

/// <summary>
/// Interface for a system running on a specific host system. Ex: C64HostConfig.
/// 
/// Usage:
/// - Common settings that indicates capabilities of the host system. Ex: AudioSupported.
/// - Setting for IO (video, audio, input) specific implementations for that system.
/// - May or may not be user configurable.
/// - Contains a ISystemConfig property for common settings for a specific system that mostly is user configurable.
/// 
/// Not used for 
/// - Common settings for a specific system that that is IO agnostic. See ISystemConfig for that (ex: C64SystemConfig).
/// 
/// </summary>

public interface IHostSystemConfig : ICloneable
{
    void Validate();

    bool IsValid(out List<string> validationErrors);

    ISystemConfig SystemConfig { get; }

    bool AudioSupported { get; }
}
