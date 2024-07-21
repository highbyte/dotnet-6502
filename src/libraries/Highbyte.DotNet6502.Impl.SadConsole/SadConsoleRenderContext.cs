using Highbyte.DotNet6502.Systems;
namespace Highbyte.DotNet6502.Impl.SadConsole;

public class SadConsoleRenderContext : IRenderContext
{
    private readonly Func<SadConsoleScreenObject> _getSadConsoleScreen;
    public SadConsoleScreenObject Screen => _getSadConsoleScreen();

    public bool IsInitialized { get; private set; } = false;

    public SadConsoleRenderContext(Func<SadConsoleScreenObject> getSadConsoleScreen)
    {
        _getSadConsoleScreen = getSadConsoleScreen;
    }

    public void Init()
    {
        IsInitialized = true;
    }

    public void Cleanup()
    {
    }
}
