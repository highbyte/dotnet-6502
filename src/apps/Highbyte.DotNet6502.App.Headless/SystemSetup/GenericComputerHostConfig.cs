using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Generic.Config;

namespace Highbyte.DotNet6502.App.Headless.SystemSetup;

public class GenericComputerHostConfig : IHostSystemConfig
{
    public const string ConfigSectionName = "Highbyte.DotNet6502.GenericComputer.Headless";

    private GenericComputerSystemConfig _systemConfig = new();
    ISystemConfig IHostSystemConfig.SystemConfig => _systemConfig;

    public GenericComputerSystemConfig SystemConfig
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
        var clone = (GenericComputerHostConfig)MemberwiseClone();
        clone._systemConfig = (GenericComputerSystemConfig)_systemConfig.Clone();
        return clone;
    }
}
