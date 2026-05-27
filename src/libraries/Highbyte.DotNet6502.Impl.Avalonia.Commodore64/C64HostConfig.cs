using System.Text.Json.Serialization;
using Highbyte.DotNet6502.AI.CodingAssistant;
using Highbyte.DotNet6502.Impl.Avalonia;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Highbyte.DotNet6502.Systems.Commodore64.Input;

namespace Highbyte.DotNet6502.Impl.Avalonia.Commodore64;

/// <summary>C64 host config for the Avalonia host.</summary>
public class C64HostConfig : HostSystemConfigBase<C64SystemConfig>, IC64SwiftLinkTcpHostConfig
{
    public const string ConfigSectionName = "Highbyte.DotNet6502.C64.Avalonia";

    public const string DefaultCorsProxyURL = "https://api.codetabs.com/v1/proxy?quest=";

    [JsonIgnore]
    public override bool AudioSupported =>
        PlatformDetection.IsRunningOnDesktop() || PlatformDetection.IsRunningInWebAssembly();

    public C64InputConfig InputConfig { get; set; } = new C64InputConfig();

    private C64SwiftLinkTransportMode _swiftLinkTransportMode;
    public C64SwiftLinkTransportMode SwiftLinkTransportMode
    {
        get => _swiftLinkTransportMode;
        set { _swiftLinkTransportMode = value; MarkDirty(); }
    }

    private string _swiftLinkTcpHost = "127.0.0.1";
    public string SwiftLinkTcpHost
    {
        get => _swiftLinkTcpHost;
        set { _swiftLinkTcpHost = value; MarkDirty(); }
    }

    private int _swiftLinkTcpPort = 5000;
    public int SwiftLinkTcpPort
    {
        get => _swiftLinkTcpPort;
        set { _swiftLinkTcpPort = value; MarkDirty(); }
    }

    private bool _swiftLinkConnectOnBoot;
    public bool SwiftLinkConnectOnBoot
    {
        get => _swiftLinkConnectOnBoot;
        set { _swiftLinkConnectOnBoot = value; MarkDirty(); }
    }

    /// <summary>
    /// CORS proxy address override. If null/empty, the default CORS proxy URL is used when running
    /// in WebAssembly. When running on desktop, this setting is ignored and no CORS proxy is used.
    /// </summary>
    public string? CorsProxyOverrideURL { get; set; } = null;

    /// <summary>
    /// The current CORS proxy URL to use. In WebAssembly: <see cref="CorsProxyOverrideURL"/> if set,
    /// else <see cref="DefaultCorsProxyURL"/>. On desktop: null (no proxy).
    /// </summary>
    public string GetCorsProxyURL()
    {
        if (!PlatformDetection.IsRunningInWebAssembly())
            return null!;
        return string.IsNullOrEmpty(CorsProxyOverrideURL) ? DefaultCorsProxyURL : CorsProxyOverrideURL;
    }

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
        SwiftLinkTransportMode = C64SwiftLinkTransportMode.RawTcp;
    }

    public override object Clone()
    {
        var clone = (C64HostConfig)base.Clone();
        clone.InputConfig = (C64InputConfig)InputConfig.Clone();
        return clone;
    }
}
