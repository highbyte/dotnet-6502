namespace Highbyte.DotNet6502.Systems;

/// <summary>
/// Common interface for system configuration. Ex: C64SystemConfig.
/// 
/// Usage:
/// - common settings for a specific system that mostly is user configurable (via UI, settings files, etc).
/// - used regardless of UI host app (ex: C64HostConfig in each UI host app).
/// 
/// Not used for 
/// - IO (video, audio, input) specific implementations, see IHostSystemConfig for that (ex: C64HostConfig in each UI host app).
/// - Non-user configurable settings that a system needs to work. See separate config classes for that (ex: C64Config).
/// 
/// The main way ISystemConfig implementations are used is as a property in IHostSystemConfig implementations.
/// 
/// </summary>
public interface ISystemConfig : ICloneable
{
    void Validate();

    bool IsValid(out List<string> validationErrors);

    public bool AudioEnabled { get; set; }
}
