using System.Text.Json.Serialization;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64.Config;

namespace Highbyte.DotNet6502.Util.MCPServer.Emulator.SystemSetup;

public class C64HostConfig : IHostSystemConfig, ICloneable
{
    public const string ConfigSectionName = "Highbyte.DotNet6502.C64.MCPServer";

    private C64SystemConfig _systemConfig;
    ISystemConfig IHostSystemConfig.SystemConfig => _systemConfig;
    public C64SystemConfig SystemConfig => _systemConfig;

    [JsonIgnore]
    public bool AudioSupported => false;

    private bool _isDirty = false;
    [JsonIgnore]
    public bool IsDirty => _isDirty;
    public void ClearDirty()
    {
        _isDirty = false;
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

    public C64HostConfig()
    {
        _systemConfig = new();
    }

    public new object Clone()
    {
        var clone = (C64HostConfig)MemberwiseClone();
        clone._systemConfig = (C64SystemConfig)SystemConfig.Clone();
        return clone;
    }
}
