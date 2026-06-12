using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Vic20.Config;

namespace Highbyte.DotNet6502.Impl.Terminal.Vic20;

/// <summary>VIC-20 host config for the Terminal (TUI) host — no audio, no host-tech display settings.</summary>
public class Vic20TerminalHostConfig : HostSystemConfigBase<Vic20SystemConfig>
{
    public const string ConfigSectionName = "Highbyte.DotNet6502.Vic20.Terminal";

    public override bool AudioSupported => false;

    public override object Clone() => (Vic20TerminalHostConfig)base.Clone();
}
