using System;
using Highbyte.DotNet6502.Systems;
namespace Highbyte.DotNet6502.Impl.SadConsole;

public class SadConsoleRenderContext : IRenderContext
{
    private readonly Func<SadConsoleScreenObject> _getSadConsoleScreen;
    public SadConsoleScreenObject Screen => _getSadConsoleScreen();

    public SadConsoleRenderContext(Func<SadConsoleScreenObject> getSadConsoleScreen)
    {
        _getSadConsoleScreen = getSadConsoleScreen;
    }

    public void CleanUp()
    {
    }

}
