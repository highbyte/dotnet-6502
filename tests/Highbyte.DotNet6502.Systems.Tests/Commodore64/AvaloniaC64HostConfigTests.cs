using Highbyte.DotNet6502.Impl.Avalonia.Commodore64;
using Highbyte.DotNet6502.Impl.Avalonia.Commodore64.Transport;
using Highbyte.DotNet6502.Systems.Commodore64.Cartridge.SwiftLink;

namespace Highbyte.DotNet6502.Systems.Tests.Commodore64;

public class AvaloniaC64HostConfigTests
{
    [Fact]
    public void WebSocketTransport_BuildConnectionUri_Appends_SharedToken_As_QueryParameter()
    {
        var uri = WebSocketTransport.BuildConnectionUri("wss://bridge.example.com/bridge?mode=raw", "secret");

        Assert.Equal("wss", uri.Scheme);
        Assert.Contains("mode=raw", uri.Query);
        Assert.Contains("token=secret", uri.Query);
    }

    [Fact]
    public void WebSocketTransport_BuildConnectionUri_Rejects_Http_Url()
    {
        Assert.Throws<ArgumentException>(() =>
            WebSocketTransport.BuildConnectionUri("https://bridge.example.com/bridge", null));
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
}
