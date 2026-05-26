using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64.Config;

namespace Highbyte.DotNet6502.Impl.Headless.Commodore64;

/// <summary>C64 host config for the Headless host — no audio, no host-tech settings.</summary>
public class C64HeadlessHostConfig : HostSystemConfigBase<C64SystemConfig>, IC64SwiftLinkTcpHostConfig
{
    public const string ConfigSectionName = "Highbyte.DotNet6502.C64.Headless";

    public override bool AudioSupported => false;

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
}
