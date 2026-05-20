using Highbyte.DotNet6502.Systems;

namespace Highbyte.DotNet6502.Impl.SadConsole;

/// <summary>
/// Optional capability interface an engine plug-in (<c>ISystemEnginePlugin</c>) implements when
/// its system needs a system-specific character/colour transform applied while rendering to a
/// <see cref="SadConsoleCommandTarget"/>.
/// </summary>
/// <remarks>
/// SadConsole has a single, system-agnostic render-target type (<see cref="SadConsoleCommandTarget"/>),
/// which the host registers itself. The only per-system variance is an optional glyph/colour
/// transform — e.g. the C64 maps screen codes onto the bundled <c>C64_ROM</c> font and swaps
/// fg/bg for inverted glyphs. The host asks every discovered implementation for a transform for
/// the running system, keeping system-specific render code out of <c>SadConsoleHostApp</c>.
/// </remarks>
public interface ISadConsoleRenderCustomizationPlugin
{
    /// <summary>
    /// Returns the SadConsole character/colour transform for <paramref name="system"/>, or
    /// <c>null</c> if this plug-in's system does not match (or needs no transform).
    /// </summary>
    Func<int, Color, Color, (int transformedCharacter, Color transformedFgColor, Color transformedBgColor)>?
        GetCharacterAndColorTransform(ISystem system);
}
