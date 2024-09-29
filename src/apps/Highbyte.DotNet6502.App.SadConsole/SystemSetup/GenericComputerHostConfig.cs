using System.Text.Json.Serialization;
using Highbyte.DotNet6502.Systems.Generic.Config;

namespace Highbyte.DotNet6502.App.SadConsole.SystemSetup;

public class GenericComputerHostConfig : SadConsoleHostSystemConfigBase
{
    public const string ConfigSectionName = "Highbyte.DotNet6502.GenericComputer.SadConsole";

    public GenericComputerSystemConfig SystemConfig
    {
        get { return (GenericComputerSystemConfig)base.SystemConfig; }
        set { base.SystemConfig = value; }
    }

    public override bool AudioSupported => false;

    private bool _isDirty = false;
    [JsonIgnore]
    public bool IsDirty => _isDirty || SystemConfig.IsDirty;
    public void ClearDirty()
    {
        _isDirty = false;
        SystemConfig.ClearDirty();
    }

    public GenericComputerHostConfig()
    {
        SystemConfig = new GenericComputerSystemConfig();
        Font = null;
        DefaultFontSize = IFont.Sizes.One;
    }

    public override bool IsValid(out List<string> validationErrors)
    {
        validationErrors = new List<string>();

        SystemConfig.IsValid(out var systemConfigValidationErrors);
        validationErrors.AddRange(systemConfigValidationErrors);

        return validationErrors.Count == 0;
    }

    public new object Clone()
    {
        var clone = (GenericComputerHostConfig)MemberwiseClone();
        clone.SystemConfig = (GenericComputerSystemConfig)SystemConfig.Clone();
        return clone;
    }
}
