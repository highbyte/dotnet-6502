using System.Runtime.InteropServices;
using Highbyte.DotNet6502.Systems;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.Impl.SadConsole;

public class SadConsoleInputHandlerContext : IInputHandlerContext
{
    //private Keyboard _sadConsoleKeyboard;
    private Keyboard _sadConsoleKeyboard => GameHost.Instance.Keyboard;
    private readonly ILogger<SadConsoleInputHandlerContext> _logger;

    public bool IsInitialized { get; private set; }

    public List<Keys> KeysDown
    {
        get
        {
            var keysDown = _sadConsoleKeyboard.KeysDown;

            foreach (var key in keysDown)
            {
                _logger.LogDebug($"Host key down: {key.Key} ({(int)key.Character})");
            }
            return keysDown.Select(x => x.Key).ToList();
        }
    }

    public SadConsoleInputHandlerContext(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<SadConsoleInputHandlerContext>();
    }

    //public void Init(Keyboard keyboard)
    public void Init()
    {
        //_sadConsoleKeyboard = keyboard;
        IsInitialized = true;
    }

    public void Cleanup()
    {
    }

    public bool GetCapsLockState()
    {
        // On Windows, Console.CapsLock can be used to check if CapsLock is on.
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return System.Console.CapsLock;

        // On Linux and Mac: TODO: How to check this with SadConsole?
        return false;
    }

}
