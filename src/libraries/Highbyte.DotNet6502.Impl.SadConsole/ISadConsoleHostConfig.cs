using Highbyte.DotNet6502.Systems;
using static SadConsole.IFont;

namespace Highbyte.DotNet6502.Impl.SadConsole;

/// <summary>
/// Non-generic view of a SadConsole host config — the SadConsole-common font settings on top of
/// the base <see cref="IHostSystemConfig"/> surface. Lets the host app treat any system's
/// SadConsole host config uniformly, without knowing its system-config type parameter.
/// </summary>
public interface ISadConsoleHostConfig : IHostSystemConfig
{
    /// <summary>Optional SadConsole font for the emulator console (e.g. <c>Fonts/C64_ROM.font</c>).</summary>
    string? Font { get; set; }

    /// <summary>Default font size for the emulator console.</summary>
    Sizes DefaultFontSize { get; set; }
}
