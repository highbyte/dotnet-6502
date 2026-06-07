namespace Highbyte.DotNet6502.Systems;

/// <summary>
/// Default endpoints for hosted browser-side services (Cloudflare Workers) used when running in
/// WebAssembly. Centralized here so the various host-config libraries share a single source of truth
/// instead of repeating the URLs. These defaults can be overridden per host config.
/// </summary>
public static class BrowserServiceDefaults
{
    // Note: For now, use a public visible key as default just to prevent at least some random users to access the endpoint...
    public const string DefaultCorsProxyUrl = "https://browser-fetch-proxy.highbyte.workers.dev/fetch?url=";

    public const string DefaultSwiftLinkWebSocketBridgeUrl = "wss://ws-tcp-bridge.highbyte.workers.dev/bridge";
}
