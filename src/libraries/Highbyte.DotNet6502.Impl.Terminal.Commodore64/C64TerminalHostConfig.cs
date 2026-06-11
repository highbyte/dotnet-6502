using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64.Cartridge.SwiftLink;
using Highbyte.DotNet6502.Systems.Commodore64.Config;

namespace Highbyte.DotNet6502.Impl.Terminal.Commodore64;

/// <summary>
/// C64 host config for the Terminal (TUI) host — no audio (terminals have no sound output),
/// no host-tech-specific display settings (the terminal renders glyph commands directly).
/// Mirrors the Headless C64 host config.
/// </summary>
public class C64TerminalHostConfig : HostSystemConfigBase<C64SystemConfig>, IC64SwiftLinkTcpHostConfig
{
    public const string ConfigSectionName = "Highbyte.DotNet6502.C64.Terminal";

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

    public C64TerminalHostConfig()
    {
        _swiftLinkHost.SetDirtyCallback(MarkDirty);
    }

    public override object Clone()
    {
        var clone = (C64TerminalHostConfig)base.Clone();
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
