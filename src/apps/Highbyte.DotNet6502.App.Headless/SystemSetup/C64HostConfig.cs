using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64.Config;

namespace Highbyte.DotNet6502.App.Headless.SystemSetup;

public class C64HostConfig : IHostSystemConfig
{
    public const string ConfigSectionName = "Highbyte.DotNet6502.C64.Headless";

    private C64SystemConfig _systemConfig = new();
    ISystemConfig IHostSystemConfig.SystemConfig => _systemConfig;

    public C64SystemConfig SystemConfig
    {
        get => _systemConfig;
        set => _systemConfig = value;
    }

    public bool AudioSupported => false;

    private bool _isDirty = false;
    public bool IsDirty => _isDirty || _systemConfig.IsDirty;
    public void ClearDirty()
    {
        _isDirty = false;
        _systemConfig.ClearDirty();
    }

    public void Validate()
    {
        if (!IsValid(out var validationErrors))
            throw new Exception($"Invalid configuration: {string.Join(", ", validationErrors)}");
    }

    public bool IsValid(out List<string> validationErrors)
    {
        validationErrors = new List<string>();
        SystemConfig.IsValid(out var systemConfigValidationErrors);
        validationErrors.AddRange(systemConfigValidationErrors);
        return validationErrors.Count == 0;
    }

    public object Clone()
    {
        var clone = (C64HostConfig)MemberwiseClone();
        clone._systemConfig = (C64SystemConfig)_systemConfig.Clone();
        return clone;
    }
}
