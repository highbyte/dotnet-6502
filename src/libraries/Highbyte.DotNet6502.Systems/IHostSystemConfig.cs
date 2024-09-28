namespace Highbyte.DotNet6502.Systems;

public interface IHostSystemConfig : ICloneable
{
    void ApplySettingsToSystemConfig(ISystemConfig systemConfig);
}
