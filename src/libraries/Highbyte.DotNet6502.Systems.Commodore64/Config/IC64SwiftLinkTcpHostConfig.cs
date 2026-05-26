namespace Highbyte.DotNet6502.Systems.Commodore64.Config;

public interface IC64SwiftLinkTcpHostConfig
{
    string SwiftLinkTcpHost { get; }
    int SwiftLinkTcpPort { get; }
    bool SwiftLinkConnectOnBoot { get; }
}
