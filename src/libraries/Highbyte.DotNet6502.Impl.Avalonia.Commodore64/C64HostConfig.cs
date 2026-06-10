using System.Text.Json.Serialization;
using Highbyte.DotNet6502.AI.CodingAssistant;
using Highbyte.DotNet6502.Impl.Avalonia;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64.Cartridge.SwiftLink;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Highbyte.DotNet6502.Systems.Commodore64.Input;

namespace Highbyte.DotNet6502.Impl.Avalonia.Commodore64;

/// <summary>C64 host config for the Avalonia host.</summary>
public class C64HostConfig : HostSystemConfigBase<C64SystemConfig>, IC64SwiftLinkTcpHostConfig
{
    public const string ConfigSectionName = "Highbyte.DotNet6502.C64.Avalonia";

    [JsonIgnore]
    public override bool AudioSupported =>
        PlatformDetection.IsRunningOnDesktop() || PlatformDetection.IsRunningInWebAssembly();

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

    public const string DefaultSwiftLinkWebSocketBridgeUrl = BrowserServiceDefaults.DefaultSwiftLinkWebSocketBridgeUrl;
    public const string DefaultSwiftLinkBridgeTargetId = "compunet-reborn";

    private string? _swiftLinkWebSocketBridgeUrl = DefaultSwiftLinkWebSocketBridgeUrl;
    /// <summary>
    /// Browser-only WebSocket endpoint used to bridge SwiftLink traffic to a Cloudflare Worker.
    /// Ignored on desktop hosts, which use the native TCP host/port configuration instead.
    /// </summary>
    public string? SwiftLinkWebSocketBridgeUrl
    {
        get => _swiftLinkWebSocketBridgeUrl;
        set
        {
            _swiftLinkWebSocketBridgeUrl = value;
            MarkDirty();
        }
    }

    private string? _swiftLinkSharedToken;
    /// <summary>
    /// Optional shared token appended to the browser WebSocket bridge URL as <c>?token=...</c>.
    /// </summary>
    public string? SwiftLinkSharedToken
    {
        get => _swiftLinkSharedToken;
        set
        {
            _swiftLinkSharedToken = value;
            MarkDirty();
        }
    }

    private string? _swiftLinkBridgeTargetId = DefaultSwiftLinkBridgeTargetId;
    /// <summary>
    /// Optional logical target id appended to the browser WebSocket bridge URL as <c>?target=...</c>.
    /// The Cloudflare Worker uses this to select an allowlisted TCP destination.
    /// </summary>
    public string? SwiftLinkBridgeTargetId
    {
        get => _swiftLinkBridgeTargetId;
        set
        {
            _swiftLinkBridgeTargetId = value;
            MarkDirty();
        }
    }

    private List<string> _swiftLinkBridgeTargetIds = new()
    {
        "compunet-reborn",
        "local-echo",
    };
    /// <summary>
    /// Browser-visible logical target ids that can be selected for the SwiftLink WebSocket bridge.
    /// These ids must match the Worker-side allowlist.
    /// </summary>
    public List<string> SwiftLinkBridgeTargetIds
    {
        get => _swiftLinkBridgeTargetIds;
        set
        {
            _swiftLinkBridgeTargetIds = value ?? new List<string>();
            MarkDirty();
        }
    }

    // The CORS proxy is now a general browser setting (EmulatorConfig.CorsProxyUrl), no longer
    // per-system. See AvaloniaHostApp.GetCorsProxyUrl().

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
        SystemConfig.SwiftLink.InterruptMode = C64SwiftLinkInterruptMode.NMI;
        _swiftLinkHost.TransportMode = C64SwiftLinkTransportMode.HayesModem;
        _swiftLinkHost.SetDirtyCallback(MarkDirty);
    }

    public override object Clone()
    {
        var clone = (C64HostConfig)base.Clone();
        clone.InputConfig = (C64InputConfig)InputConfig.Clone();
        clone._swiftLinkHost = SwiftLinkHost.Clone();
        clone._swiftLinkHost.SetDirtyCallback(clone.MarkDirty);
        clone._swiftLinkBridgeTargetIds = new List<string>(SwiftLinkBridgeTargetIds);
        return clone;
    }

    public override bool IsValid(out List<string> validationErrors)
    {
        var isValid = base.IsValid(out validationErrors);
        if (!SwiftLinkHost.IsValid(out var swiftLinkHostValidationErrors, nameof(SwiftLinkHost)))
            validationErrors.AddRange(swiftLinkHostValidationErrors);

        if (PlatformDetection.IsRunningInWebAssembly() && SystemConfig.SwiftLink.Enabled)
        {
            if (SwiftLinkHost.TransportMode is not (C64SwiftLinkTransportMode.RawTcp or C64SwiftLinkTransportMode.HayesModem))
            {
                validationErrors.Add(
                    $"{nameof(SwiftLinkHost)}.{nameof(C64SwiftLinkHostConfig.TransportMode)} must be RawTcp or HayesModem when running in WebAssembly.");
            }

            if (string.IsNullOrWhiteSpace(SwiftLinkWebSocketBridgeUrl))
            {
                validationErrors.Add($"{nameof(SwiftLinkWebSocketBridgeUrl)} must be set when SwiftLink is enabled in WebAssembly.");
            }
            else if (!Uri.TryCreate(SwiftLinkWebSocketBridgeUrl.Trim(), UriKind.Absolute, out var bridgeUri)
                     || (bridgeUri.Scheme != Uri.UriSchemeWs && bridgeUri.Scheme != Uri.UriSchemeWss))
            {
                validationErrors.Add($"{nameof(SwiftLinkWebSocketBridgeUrl)} must be an absolute ws:// or wss:// URL.");
            }
        }

        return isValid && validationErrors.Count == 0;
    }
}
