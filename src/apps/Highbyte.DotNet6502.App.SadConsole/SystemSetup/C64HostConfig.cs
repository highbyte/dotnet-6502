using Highbyte.DotNet6502.AI.CodingAssistant;

namespace Highbyte.DotNet6502.App.SadConsole.SystemSetup;

public class C64HostConfig : SadConsoleHostSystemConfigBase
{
    public bool BasicAIAssistantDefaultEnabled { get; set; }

    public CodeSuggestionBackendTypeEnum CodeSuggestionBackendType { get; set; }

    public C64HostConfig()
    {
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
        return clone;
    }
}
