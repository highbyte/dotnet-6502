using Highbyte.DotNet6502.Systems;
namespace Highbyte.DotNet6502.Impl.SadConsole;

public class SadConsoleRenderContext : IRenderContext
{
    private readonly Func<EmulatorConsole> _getSadConsoleEmulatorConsole;
    public EmulatorConsole Console => _getSadConsoleEmulatorConsole();

    public bool IsInitialized { get; private set; } = false;

    public SadConsoleRenderContext(Func<EmulatorConsole> getSadConsoleEmulatorConsole)
    {
        _getSadConsoleEmulatorConsole = getSadConsoleEmulatorConsole;
    }

    public void Init()
    {
        IsInitialized = true;
    }

    public void Cleanup()
    {
    }
}
