using System.Text.Json.Serialization;
using Highbyte.DotNet6502.AI.CodingAssistant;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64.Cartridge.SwiftLink;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Highbyte.DotNet6502.Systems.Commodore64.Input;

namespace Highbyte.DotNet6502.Impl.AspNet.Commodore64;

/// <summary>C64 host config for the WASM (Blazor) host.</summary>
public class C64HostConfig : HostSystemConfigBase<C64SystemConfig>, IC64SwiftLinkTcpHostConfig
{
    public const string ConfigSectionName = "Highbyte.DotNet6502.C64.WASM";

    public const string DefaultCorsProxyURL = BrowserServiceDefaults.DefaultCorsProxyUrl;

    public C64InputConfig InputConfig { get; set; } = new C64InputConfig();

    private C64SwiftLinkHostConfig _swiftLinkHost = new();
    public C64SwiftLinkHostConfig SwiftLinkHost
    {
        get => _swiftLinkHost;
        set
        {
            _swiftLinkHost = value ?? new C64SwiftLinkHostConfig();
            _swiftLinkHost.SetDirtyCallback(MarkDirty);
            MarkDirty();
        }
    }

    [JsonIgnore]
    public C64SwiftLinkTransportMode SwiftLinkTransportMode => SwiftLinkHost.TransportMode;

    [JsonIgnore]
    public string SwiftLinkTcpHost => SwiftLinkHost.TcpHost;

    [JsonIgnore]
    public int SwiftLinkTcpPort => SwiftLinkHost.TcpPort;

    [JsonIgnore]
    public bool SwiftLinkConnectOnBoot => SwiftLinkHost.ConnectOnBoot;

    public string CorsProxyURL { get; set; } = DefaultCorsProxyURL;

    private bool _basicAIAssistantDefaultEnabled;
    [JsonIgnore]
    public bool BasicAIAssistantDefaultEnabled
    {
        get => _basicAIAssistantDefaultEnabled;
        set { _basicAIAssistantDefaultEnabled = value; MarkDirty(); }
    }

    private CodeSuggestionBackendTypeEnum _codeSuggestionBackendType;
    public CodeSuggestionBackendTypeEnum CodeSuggestionBackendType
    {
        get => _codeSuggestionBackendType;
        set { _codeSuggestionBackendType = value; MarkDirty(); }
    }

    public C64HostConfig()
    {
        BasicAIAssistantDefaultEnabled = false;
        CodeSuggestionBackendType = CodeSuggestionBackendTypeEnum.CustomEndpoint;
        _swiftLinkHost.SetDirtyCallback(MarkDirty);
    }

    public override object Clone()
    {
        var clone = (C64HostConfig)base.Clone();
        clone.InputConfig = (C64InputConfig)InputConfig.Clone();
        clone._swiftLinkHost = SwiftLinkHost.Clone();
        clone._swiftLinkHost.SetDirtyCallback(clone.MarkDirty);
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
