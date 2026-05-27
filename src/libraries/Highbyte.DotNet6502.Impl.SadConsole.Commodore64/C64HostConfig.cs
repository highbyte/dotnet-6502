using Highbyte.DotNet6502.AI.CodingAssistant;
using Highbyte.DotNet6502.Impl.SadConsole;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using SadConsole;

namespace Highbyte.DotNet6502.Impl.SadConsole.Commodore64;

/// <summary>C64 host config for the SadConsole host.</summary>
public class C64HostConfig : SadConsoleHostSystemConfigBase<C64SystemConfig>, IC64SwiftLinkTcpHostConfig
{
    public const string ConfigSectionName = "Highbyte.DotNet6502.C64.SadConsole";

    public bool BasicAIAssistantDefaultEnabled { get; set; }

    public C64SwiftLinkTransportMode SwiftLinkTransportMode { get; set; } = C64SwiftLinkTransportMode.RawTcp;
    public string SwiftLinkTcpHost { get; set; } = "127.0.0.1";
    public int SwiftLinkTcpPort { get; set; } = 5000;
    public bool SwiftLinkConnectOnBoot { get; set; }

    //TODO: CodeSuggestionBackendType setting should be common and not specific for a system
    public CodeSuggestionBackendTypeEnum CodeSuggestionBackendType { get; set; }

    public C64HostConfig()
    {
        BasicAIAssistantDefaultEnabled = false;
        CodeSuggestionBackendType = CodeSuggestionBackendTypeEnum.OpenAI;

        Font = "Fonts/C64_ROM.font";
        DefaultFontSize = IFont.Sizes.Two;
    }
}
