using Highbyte.DotNet6502.AI.CodingAssistant;
using Highbyte.DotNet6502.Impl.SadConsole;
using Highbyte.DotNet6502.Systems.Commodore64.Cartridge.SwiftLink;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using SadConsole;

namespace Highbyte.DotNet6502.Impl.SadConsole.Commodore64;

/// <summary>C64 host config for the SadConsole host.</summary>
public class C64HostConfig : SadConsoleHostSystemConfigBase<C64SystemConfig>, IC64SwiftLinkTcpHostConfig
{
    public const string ConfigSectionName = "Highbyte.DotNet6502.C64.SadConsole";

    public bool BasicAIAssistantDefaultEnabled { get; set; }

    public C64SwiftLinkHostConfig SwiftLinkHost { get; set; } = new();
    public C64SwiftLinkTransportMode SwiftLinkTransportMode => SwiftLinkHost.TransportMode;
    public string SwiftLinkTcpHost => SwiftLinkHost.TcpHost;
    public int SwiftLinkTcpPort => SwiftLinkHost.TcpPort;
    public bool SwiftLinkConnectOnBoot => SwiftLinkHost.ConnectOnBoot;

    //TODO: CodeSuggestionBackendType setting should be common and not specific for a system
    public CodeSuggestionBackendTypeEnum CodeSuggestionBackendType { get; set; }

    public C64HostConfig()
    {
        BasicAIAssistantDefaultEnabled = false;
        CodeSuggestionBackendType = CodeSuggestionBackendTypeEnum.OpenAI;

        Font = "Fonts/C64_ROM.font";
        DefaultFontSize = IFont.Sizes.Two;
    }

    public override object Clone()
    {
        var clone = (C64HostConfig)base.Clone();
        clone.SwiftLinkHost = SwiftLinkHost.Clone();
        return clone;
    }

    public override bool IsValid(out List<string> validationErrors)
    {
        var isValid = base.IsValid(out validationErrors);
        if (!SwiftLinkHost.IsValid(out var swiftLinkHostValidationErrors, nameof(SwiftLinkHost)))
            validationErrors.AddRange(swiftLinkHostValidationErrors);
        return isValid && validationErrors.Count == 0;
    }
}
