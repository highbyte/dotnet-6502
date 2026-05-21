using System.Text.Json.Serialization;

namespace Highbyte.DotNet6502.Systems;

/// <summary>
/// Shared base for <see cref="IHostSystemConfig"/> implementations. Carries the common boilerplate
/// — the typed <see cref="SystemConfig"/>, dirty tracking, validation and clone — so each host's
/// config class only adds its config-section name and any host-tech-specific settings.
/// </summary>
/// <remarks>See <c>docs/system-configurer-consolidation.md</c>.</remarks>
public abstract class HostSystemConfigBase<TSystemConfig> : IHostSystemConfig
    where TSystemConfig : ISystemConfig, new()
{
    private TSystemConfig _systemConfig = new();

    ISystemConfig IHostSystemConfig.SystemConfig => _systemConfig;

    public TSystemConfig SystemConfig
    {
        get => _systemConfig;
        set => _systemConfig = value;
    }

    /// <summary>Whether the host can play audio. Defaults to <c>true</c>; hosts without audio override.</summary>
    [JsonIgnore]
    public virtual bool AudioSupported => true;

    private bool _isDirty;

    [JsonIgnore]
    public bool IsDirty => _isDirty || _systemConfig.IsDirty;

    /// <summary>Marks this host config dirty — for use by a subclass's own property setters.</summary>
    protected void MarkDirty() => _isDirty = true;

    public virtual void ClearDirty()
    {
        _isDirty = false;
        _systemConfig.ClearDirty();
    }

    public void Validate()
    {
        if (!IsValid(out var validationErrors))
            throw new DotNet6502Exception($"Config errors: {string.Join(", ", validationErrors)}");
    }

    public virtual bool IsValid(out List<string> validationErrors)
    {
        validationErrors = new List<string>();
        SystemConfig.IsValid(out var systemConfigValidationErrors);
        validationErrors.AddRange(systemConfigValidationErrors);
        return validationErrors.Count == 0;
    }

    /// <summary>
    /// Shallow-copies this config and deep-copies the contained <see cref="SystemConfig"/>. A
    /// subclass with extra reference-typed settings overrides this, calls <c>base.Clone()</c>, and
    /// deep-copies those too.
    /// </summary>
    public virtual object Clone()
    {
        var clone = (HostSystemConfigBase<TSystemConfig>)MemberwiseClone();
        clone._systemConfig = (TSystemConfig)_systemConfig.Clone();
        return clone;
    }
}
