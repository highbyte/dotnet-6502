using Highbyte.DotNet6502.Impl.SilkNet.Commodore64.Render;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64.Cartridge.SwiftLink;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Highbyte.DotNet6502.Systems.Commodore64.Input;

namespace Highbyte.DotNet6502.Impl.SilkNet.Commodore64;

/// <summary>C64 host config for the SilkNet host.</summary>
public class C64HostConfig : HostSystemConfigBase<C64SystemConfig>, IC64SwiftLinkTcpHostConfig
{
    public const string ConfigSectionName = "Highbyte.DotNet6502.C64.SilkNetNative";

    public C64SilkNetOpenGlRendererConfig SilkNetOpenGlRendererConfig { get; set; } = new C64SilkNetOpenGlRendererConfig();
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

    public C64SwiftLinkTransportMode SwiftLinkTransportMode => SwiftLinkHost.TransportMode;
    public string SwiftLinkTcpHost => SwiftLinkHost.TcpHost;
    public int SwiftLinkTcpPort => SwiftLinkHost.TcpPort;
    public bool SwiftLinkConnectOnBoot => SwiftLinkHost.ConnectOnBoot;

    public override object Clone()
    {
        var clone = (C64HostConfig)base.Clone();
        clone.InputConfig = (C64InputConfig)InputConfig.Clone();
        clone.SilkNetOpenGlRendererConfig = (C64SilkNetOpenGlRendererConfig)SilkNetOpenGlRendererConfig.Clone();
        clone._swiftLinkHost = SwiftLinkHost.Clone();
        clone._swiftLinkHost.SetDirtyCallback(clone.MarkDirty);
        return clone;
    }

    public C64HostConfig()
    {
        _swiftLinkHost.SetDirtyCallback(MarkDirty);
    }
}
