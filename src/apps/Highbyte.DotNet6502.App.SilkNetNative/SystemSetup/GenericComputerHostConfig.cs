using System.Text.Json.Serialization;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Generic.Config;

namespace Highbyte.DotNet6502.App.SilkNetNative.SystemSetup;

public class GenericComputerHostConfig : IHostSystemConfig, ICloneable
{
    public const string ConfigSectionName = "Highbyte.DotNet6502.GenericComputer.SilkNetNative";

    private GenericComputerSystemConfig _systemConfig;
    ISystemConfig IHostSystemConfig.SystemConfig => _systemConfig;

    public GenericComputerSystemConfig SystemConfig => _systemConfig;

    [JsonIgnore]
    public bool AudioSupported => false;

    private bool _isDirty = false;
    [JsonIgnore]
    public bool IsDirty => _isDirty || _systemConfig.IsDirty;
    public void ClearDirty()
    {
        _isDirty = false;
        _systemConfig.ClearDirty();
    }

    public GenericComputerHostConfig()
    {
        _systemConfig = new();
    }

    public void Validate()
    {
        if (!IsValid(out List<string> validationErrors))
            throw new DotNet6502Exception($"Config errors: {string.Join(',', validationErrors)}");
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
        var clone = (GenericComputerHostConfig)this.MemberwiseClone();
        clone._systemConfig = (GenericComputerSystemConfig)SystemConfig.Clone();
        return clone;
    }
}
