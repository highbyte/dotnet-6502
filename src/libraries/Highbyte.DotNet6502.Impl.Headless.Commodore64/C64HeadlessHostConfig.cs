using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64.Cartridge.SwiftLink;
using Highbyte.DotNet6502.Systems.Commodore64.Config;

namespace Highbyte.DotNet6502.Impl.Headless.Commodore64;

/// <summary>C64 host config for the Headless host — no audio, no host-tech settings.</summary>
public class C64HeadlessHostConfig : HostSystemConfigBase<C64SystemConfig>, IC64SwiftLinkTcpHostConfig
{
    public const string ConfigSectionName = "Highbyte.DotNet6502.C64.Headless";

    public override bool AudioSupported => false;

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

    public C64HeadlessHostConfig()
    {
        _swiftLinkHost.SetDirtyCallback(MarkDirty);
    }

    public override object Clone()
    {
        var clone = (C64HeadlessHostConfig)base.Clone();
        clone._swiftLinkHost = SwiftLinkHost.Clone();
        clone._swiftLinkHost.SetDirtyCallback(clone.MarkDirty);
        return clone;
    }

    public override bool IsValid(out List<string> validationErrors)
    {
        var isValid = base.IsValid(out validationErrors);
        if (!SwiftLinkHost.IsValid(out var swiftLinkHostValidationErrors, nameof(SwiftLinkHost)))
            validationErrors.AddRange(swiftLinkHostValidationErrors);
        return isValid && validationErrors.Count == 0;
    }
}
