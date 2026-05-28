namespace Highbyte.DotNet6502.Systems.Commodore64.Cartridge.SwiftLink;

public interface IC64SwiftLinkTcpHostConfig
{
    C64SwiftLinkTransportMode SwiftLinkTransportMode { get; }
    string SwiftLinkTcpHost { get; }
    int SwiftLinkTcpPort { get; }
    bool SwiftLinkConnectOnBoot { get; }
}
