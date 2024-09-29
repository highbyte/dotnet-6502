using System.Text.Json.Serialization;
using Highbyte.DotNet6502.AI.CodingAssistant;
using Highbyte.DotNet6502.Systems.Commodore64.Config;

namespace Highbyte.DotNet6502.App.SadConsole.SystemSetup;

public class C64HostConfig : SadConsoleHostSystemConfigBase
{
    public const string ConfigSectionName = "Highbyte.DotNet6502.C64.SadConsole";

    public C64SystemConfig SystemConfig
    {
        get { return (C64SystemConfig)base.SystemConfig; }
        set { base.SystemConfig = value; }
    }

    [JsonIgnore]
    public override bool AudioSupported => true;

    private bool _isDirty = false;
    [JsonIgnore]
    public bool IsDirty => _isDirty;
    public void ClearDirty()
    {
        _isDirty = false;
    }

    public override bool IsValid(out List<string> validationErrors)
    {
        validationErrors = new List<string>();

        SystemConfig.IsValid(out var systemConfigValidationErrors);
        validationErrors.AddRange(systemConfigValidationErrors);

        return validationErrors.Count == 0;
    }

    public bool BasicAIAssistantDefaultEnabled { get; set; }

    //TODO: CodeSuggestionBackendType setting should be common and not specific for a system
    public CodeSuggestionBackendTypeEnum CodeSuggestionBackendType { get; set; }

    public C64HostConfig()
    {
        SystemConfig = new C64SystemConfig();

        BasicAIAssistantDefaultEnabled = false;

        CodeSuggestionBackendType = CodeSuggestionBackendTypeEnum.OpenAI;

        //Font = "Fonts/C64.font";
        //DefaultFontSize = IFont.Sizes.One;

        Font = "Fonts/C64_ROM.font";
        DefaultFontSize = IFont.Sizes.Two;
    }

    public new object Clone()
    {
        var clone = (C64HostConfig)MemberwiseClone();
        clone.SystemConfig = (C64SystemConfig)SystemConfig.Clone();
        return clone;
    }
}
