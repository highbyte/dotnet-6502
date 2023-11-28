using Highbyte.DotNet6502;
using Highbyte.DotNet6502.Instrumentation;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.Impl.SadConsole.Commodore64.Input;

public class C64SadConsoleInputHandler : IInputHandler<C64, SadConsoleInputHandlerContext>
{
    private SadConsoleInputHandlerContext? _inputHandlerContext;
    private readonly List<string> _debugInfo = new();
    private readonly C64SadConsoleKeyboard _c64SadConsoleKeyboard;
    private readonly ILogger<C64SadConsoleInputHandler> _logger;

    // Stats
    public Instrumentations Stats { get; } = new();

    public C64SadConsoleInputHandler(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<C64SadConsoleInputHandler>();

        // TODO: Is there a better way to current keyboard input language?
        var currentUICulture = Thread.CurrentThread.CurrentUICulture;
        var keyboardLayoutId = currentUICulture.KeyboardLayoutId;
        var languageName = currentUICulture.TwoLetterISOLanguageName;
        _logger.LogInformation($"KbLayoutId: {keyboardLayoutId}");
        _logger.LogInformation($"KbLanguage: {languageName}");

        _c64SadConsoleKeyboard = new C64SadConsoleKeyboard(languageName);

    }

    public void Init(C64 system, SadConsoleInputHandlerContext inputHandlerContext)
    {
        _inputHandlerContext = inputHandlerContext;
        _inputHandlerContext.Init();
    }

    public void Init(ISystem system, IInputHandlerContext inputHandlerContext)
    {
        Init((C64)system, (SadConsoleInputHandlerContext)inputHandlerContext);
    }

    public void ProcessInput(C64 c64)
    {
        CaptureKeyboard(c64);
    }

    public void ProcessInput(ISystem system)
    {
        ProcessInput((C64)system);
    }

    private void CaptureKeyboard(C64 c64)
    {

        var c64KeysDown = GetC64KeysFromSadConsoleKeys(_inputHandlerContext!.KeysDown, out bool restoreKeyPressed, out bool capsLockOn);
        var keyboard = c64.Cia.Keyboard;
        keyboard.SetKeysPressed(c64KeysDown, restoreKeyPressed, capsLockOn);
    }

    private List<C64Key> GetC64KeysFromSadConsoleKeys(List<Keys> keysDown, out bool restoreKeyPressed, out bool capsLockOn)
    {
        restoreKeyPressed = keysDown.Contains(Keys.PageUp) ? true : false;
        capsLockOn = _inputHandlerContext!.GetCapsLockState();

        var c64KeysDown = new List<C64Key>();
        var foundMappings = new List<Keys[]>();
        foreach (var mapKeys in _c64SadConsoleKeyboard.SadConsoleToC64KeyMap.Keys)
        {
            int matchCount = 0;
            foreach (var mapKeysKey in mapKeys)
            {
                if (keysDown.Contains(mapKeysKey))
                    matchCount++;
            }
            if (matchCount == mapKeys.Length)
            {
                // Remove any other mappings found that contains any of the keys in this mapping.
                for (int i = foundMappings.Count - 1; i >= 0; i--)
                {
                    var currentlyFoundMapKeys = foundMappings[i];
                    if (currentlyFoundMapKeys.Any(x => mapKeys.Contains(x)))
                    {
                        foundMappings.RemoveAt(i);
                    }
                }
                foundMappings.Add(mapKeys);
            }
        }

        foreach (var mapKeys in foundMappings)
        {
            var c64Keys = _c64SadConsoleKeyboard.SadConsoleToC64KeyMap[mapKeys];
            foreach (var c64Key in c64Keys)
            {
                if (!c64KeysDown.Contains(c64Key))
                    c64KeysDown.Add(c64Key);
            }
        }
        return c64KeysDown;
    }

    public List<string> GetDebugInfo()
    {
        return _debugInfo;
    }
}
