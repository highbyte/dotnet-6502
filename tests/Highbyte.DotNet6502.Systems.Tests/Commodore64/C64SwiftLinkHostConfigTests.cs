using Highbyte.DotNet6502.Systems.Commodore64.Cartridge.SwiftLink;

namespace Highbyte.DotNet6502.Systems.Tests.Commodore64;

public class C64SwiftLinkHostConfigTests
{
    [Fact]
    public void IsValid_Rejects_Blank_Host_In_RawTcp_Mode()
    {
        var config = new C64SwiftLinkHostConfig
        {
            TransportMode = C64SwiftLinkTransportMode.RawTcp,
            TcpHost = "",
            TcpPort = 6400
        };

        var isValid = config.IsValid(out var validationErrors, "SwiftLinkHost");

        Assert.False(isValid);
        Assert.Contains("SwiftLinkHost.TcpHost must be set for RawTcp mode.", validationErrors);
    }

    [Fact]
    public void IsValid_Rejects_ConnectOnBoot_In_Hayes_Mode()
    {
        var config = new C64SwiftLinkHostConfig
        {
            TransportMode = C64SwiftLinkTransportMode.HayesModem,
            ConnectOnBoot = true,
            TcpPort = 6400
        };

        var isValid = config.IsValid(out var validationErrors, "SwiftLinkHost");

        Assert.False(isValid);
        Assert.Contains("SwiftLinkHost.ConnectOnBoot can only be enabled in RawTcp mode.", validationErrors);
    }

    [Fact]
    public void IsValid_Accepts_Valid_RawTcp_Config()
    {
        var config = new C64SwiftLinkHostConfig
        {
            TransportMode = C64SwiftLinkTransportMode.RawTcp,
            TcpHost = "127.0.0.1",
            TcpPort = 5000,
            ConnectOnBoot = true
        };

        var isValid = config.IsValid(out var validationErrors, "SwiftLinkHost");

        Assert.True(isValid);
        Assert.Empty(validationErrors);
    }
}
