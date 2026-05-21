using Highbyte.DotNet6502.Impl.NAudio;
using Highbyte.DotNet6502.Impl.SadConsole;
using Highbyte.DotNet6502.Impl.SadConsole.Commodore64.Render;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Plugins;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SadRogue.Primitives;

[assembly: SystemPlugin(typeof(Highbyte.DotNet6502.Impl.SadConsole.Commodore64.C64SadConsoleEnginePlugin))]

namespace Highbyte.DotNet6502.Impl.SadConsole.Commodore64;

/// <summary>
/// Engine-side plugin for the C64 on the SadConsole + NAudio host pair. Registers the C64
/// <see cref="ISystemConfigurer"/> (<see cref="C64Setup"/>) into DI and contributes the C64's
/// SadConsole glyph/colour transform (via <see cref="ISadConsoleRenderCustomizationPlugin"/>).
/// </summary>
public sealed class C64SadConsoleEnginePlugin
    : ISystemEnginePlugin, ISadConsoleRenderCustomizationPlugin
{
    public string SystemName => C64.SystemName;

    public string HostTechName => "SadConsole.NAudio";

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        // Engine-side configurer for the SadConsole + NAudio host pair.
        services.AddSingleton<ISystemConfigurer>(sp =>
            new C64Setup(
                sp.GetRequiredService<ILoggerFactory>(),
                sp.GetRequiredService<IConfiguration>()));
    }

    /// <summary>
    /// The C64 needs a screen-code -> SadConsole <c>C64_ROM</c> font-index transform (and fg/bg
    /// swap for inverted glyphs) — see <see cref="C64SadConsoleRenderTargetCustomization"/>.
    /// Returns <c>null</c> for any non-C64 system.
    /// </summary>
    public Func<int, Color, Color, (int transformedCharacter, Color transformedFgColor, Color transformedBgColor)>?
        GetCharacterAndColorTransform(ISystem system)
    {
        if (system is not C64 c64)
            return null;
        return new C64SadConsoleRenderTargetCustomization(c64).TransformCharacterAndColor;
    }
}
