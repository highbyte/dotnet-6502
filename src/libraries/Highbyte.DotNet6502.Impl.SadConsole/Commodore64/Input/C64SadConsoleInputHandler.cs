using Highbyte.DotNet6502;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral;
using Highbyte.DotNet6502.Systems.Commodore64.Utils.BasicAssistant;
using Highbyte.DotNet6502.Systems.Instrumentation;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.Impl.SadConsole.Commodore64.Input;

public class C64SadConsoleInputHandler : IInputHandler
{
    private readonly C64 _c64;
    public ISystem System => _c64;
    private readonly SadConsoleInputHandlerContext _inputHandlerContext;
    private readonly List<string> _debugInfo = new();
    private readonly C64SadConsoleKeyboard _c64SadConsoleKeyboard;
    private readonly ILogger<C64SadConsoleInputHandler> _logger;

    private readonly C64BasicCodingAssistant _c64BasicCodingAssistant;



    // Instrumentations
    public Instrumentations Instrumentations { get; } = new();

    public C64SadConsoleInputHandler(C64 c64, SadConsoleInputHandlerContext inputHandlerContext, ILoggerFactory loggerFactory, Func<string, string, string>? getCodeCompletion = null)
    {
        _c64 = c64;
        _inputHandlerContext = inputHandlerContext;
        _logger = loggerFactory.CreateLogger<C64SadConsoleInputHandler>();

        // TODO: Is there a better way to current keyboard input language?
        var currentUICulture = Thread.CurrentThread.CurrentUICulture;
        var keyboardLayoutId = currentUICulture.KeyboardLayoutId;
        var languageName = currentUICulture.TwoLetterISOLanguageName;
        _logger.LogInformation($"KbLayoutId: {keyboardLayoutId}");
        _logger.LogInformation($"KbLanguage: {languageName}");

        _c64SadConsoleKeyboard = new C64SadConsoleKeyboard(languageName);

        _c64BasicCodingAssistant = new C64BasicCodingAssistant(_c64, getCodeCompletion, loggerFactory);
    }

    public void Init()
    {
    }

    public void BeforeFrame()
    {
        CaptureKeyboard(_c64);
    }
    public void Cleanup()
    {
    }

    private void CaptureKeyboard(C64 c64)
    {

        var c64KeysDown = GetC64KeysFromSadConsoleKeys(_inputHandlerContext!.KeysDown, out bool restoreKeyPressed, out bool capsLockOn);

        if (_c64BasicCodingAssistant.IsEnabled && c64KeysDown.Count > 0)
        {
            _c64BasicCodingAssistant.KeyWasPressed(c64KeysDown);
        }

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
