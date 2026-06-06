using Highbyte.DotNet6502.Impl.Avalonia.Commodore64;
using Highbyte.DotNet6502.Impl.Avalonia.Commodore64.Transport;
using Highbyte.DotNet6502.Systems.Commodore64.Cartridge.SwiftLink;

namespace Highbyte.DotNet6502.Systems.Tests.Commodore64;

public class AvaloniaC64HostConfigTests
{
    [Fact]
    public void WebSocketTransport_BuildConnectionUri_Appends_SharedToken_As_QueryParameter()
    {
        var uri = WebSocketTransport.BuildConnectionUri("wss://bridge.example.com/bridge?mode=raw", "secret", "compunet");

        Assert.Equal("wss", uri.Scheme);
        Assert.Contains("mode=raw", uri.Query);
        Assert.Contains("token=secret", uri.Query);
        Assert.Contains("target=compunet", uri.Query);
    }

    [Fact]
    public void WebSocketTransport_BuildConnectionUri_Appends_TargetId_Without_SharedToken()
    {
        var uri = WebSocketTransport.BuildConnectionUri("ws://127.0.0.1:8787/bridge", null, "compunet-reborn");

        Assert.Equal("ws", uri.Scheme);
        Assert.Contains("target=compunet-reborn", uri.Query);
    }

    [Fact]
    public void WebSocketTransport_BuildConnectionUri_Rejects_Http_Url()
    {
        Assert.Throws<ArgumentException>(() =>
            WebSocketTransport.BuildConnectionUri("https://bridge.example.com/bridge", null, null));
    }

    [Fact]
    public void IsValid_DoesNotRequire_Bridge_Url_On_Desktop()
    {
        var config = new C64HostConfig
        {
            SwiftLinkHost =
            {
                TransportMode = C64SwiftLinkTransportMode.RawTcp,
                TcpHost = "127.0.0.1",
                TcpPort = 5000,
                ConnectOnBoot = true,
            }
        };
        config.SystemConfig.SwiftLink.Enabled = true;

        config.IsValid(out var validationErrors);

        Assert.DoesNotContain(
            validationErrors,
            error => error.Contains(nameof(C64HostConfig.SwiftLinkWebSocketBridgeUrl), StringComparison.Ordinal));
    }

    [Fact]
    public void Clone_Preserves_SwiftLinkBridgeTargetIds()
    {
        var config = new C64HostConfig
        {
            SwiftLinkBridgeTargetIds = new List<string> { "compunet-reborn", "local-echo" },
            SwiftLinkBridgeTargetId = "compunet-reborn",
        };

        var clone = (C64HostConfig)config.Clone();

        Assert.Equal(config.SwiftLinkBridgeTargetIds, clone.SwiftLinkBridgeTargetIds);
        Assert.NotSame(config.SwiftLinkBridgeTargetIds, clone.SwiftLinkBridgeTargetIds);
        Assert.Equal("compunet-reborn", clone.SwiftLinkBridgeTargetId);
    }
}
