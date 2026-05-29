using System.Text.Json.Serialization;
using Highbyte.DotNet6502.Systems.Rendering;

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
    /// <summary>Whether the config has unsaved changes since the last <see cref="ClearDirty"/>.</summary>
    bool IsDirty { get; }

    /// <summary>Clears the dirty flag.</summary>
    void ClearDirty();

    void Validate();

    bool IsValid(out List<string> validationErrors);

    /// <summary>
    /// Should return a list of types that implement IRenderProvider and that the system supports.
    /// </summary>
    /// <returns></returns>
    public List<Type> GetSupportedRenderProviderTypes();

    public void SetRenderProviderType(Type? renderProviderType);
    public void SetRenderTargetType(Type? renderTargetType);

    public Type? RenderProviderType { get; }
    public Type? RenderTargetType { get; }

    /// <summary>
    /// Should return a list of types that implement IAudioProvider and that the system supports
    /// (e.g. a C64 supports both C64SidCommandStream and C64SidSampleProvider). Empty for systems
    /// with no audio.
    /// </summary>
    public List<Type> GetSupportedAudioProviderTypes();

    public void SetAudioProviderType(Type? audioProviderType);
    public void SetAudioTargetType(Type? audioTargetType);

    public Type? AudioProviderType { get; }
    public Type? AudioTargetType { get; }

    /// <summary>
    /// Whether audio output is enabled for this system. Systems with no audio must still
    /// implement this property — declare it as a plain auto-property that always returns
    /// <c>false</c> (and whose setter is a no-op or ignored). Omitting it causes a compile
    /// error that is not immediately obvious without reading the full interface.
    /// </summary>
    public bool AudioEnabled { get; set; }
}
