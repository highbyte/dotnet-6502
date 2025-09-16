using System.Text.Json.Serialization;
using Highbyte.DotNet6502.Impl.SilkNet.Commodore64.Input;
using Highbyte.DotNet6502.Impl.SilkNet.Commodore64.Render;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64.Config;

namespace Highbyte.DotNet6502.App.SilkNetNative.SystemSetup;

public class C64HostConfig : IHostSystemConfig, ICloneable
{
    public const string ConfigSectionName = "Highbyte.DotNet6502.C64.SilkNetNative";

    private C64SystemConfig _systemConfig;
    ISystemConfig IHostSystemConfig.SystemConfig => _systemConfig;

    public C64SystemConfig SystemConfig => _systemConfig;

    [JsonIgnore]
    public bool AudioSupported => true;

    public C64SilkNetOpenGlRendererConfig SilkNetOpenGlRendererConfig { get; set; } = new C64SilkNetOpenGlRendererConfig();
    public C64SilkNetInputConfig InputConfig { get; set; } = new C64SilkNetInputConfig();

    private bool _isDirty = false;
    [JsonIgnore]
    public bool IsDirty => _isDirty || _systemConfig.IsDirty;
    public void ClearDirty()
    {
        _isDirty = false;
        _systemConfig.ClearDirty();
    }

    public C64HostConfig()
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
        var clone = (C64HostConfig)this.MemberwiseClone();
        clone._systemConfig = (C64SystemConfig)SystemConfig.Clone();
        clone.InputConfig = (C64SilkNetInputConfig)InputConfig.Clone();
        clone.SilkNetOpenGlRendererConfig = (C64SilkNetOpenGlRendererConfig)SilkNetOpenGlRendererConfig.Clone();
        return clone;
    }
}
