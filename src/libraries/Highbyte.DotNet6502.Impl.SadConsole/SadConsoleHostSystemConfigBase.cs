using Highbyte.DotNet6502.Systems;
using static SadConsole.IFont;

namespace Highbyte.DotNet6502.Impl.SadConsole;

/// <summary>
/// Base for SadConsole per-system host configs. Adds the SadConsole-specific font settings on top
/// of the shared <see cref="HostSystemConfigBase{TSystemConfig}"/> boilerplate.
/// </summary>
/// <remarks>See <c>docs/system-configurer-consolidation.md</c>.</remarks>
public abstract class SadConsoleHostSystemConfigBase<TSystemConfig>
    : HostSystemConfigBase<TSystemConfig>, ISadConsoleHostConfig
    where TSystemConfig : ISystemConfig, new()
{
    /// <summary>
    /// Optional. If not specified, the default SadConsole font is used. To use a specific
    /// SadConsole font, include it in your program output directory. Example: Fonts/C64.font
    /// </summary>
    public string? Font { get; set; }

    /// <summary>Default font size for the emulator console only. UI is not affected.</summary>
    public Sizes DefaultFontSize { get; set; }
}
