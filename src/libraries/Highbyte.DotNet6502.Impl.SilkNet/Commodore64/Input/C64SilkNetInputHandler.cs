using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral;
using Microsoft.Extensions.Logging;
using static Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral.C64Joystick;

namespace Highbyte.DotNet6502.Impl.SilkNet.Commodore64.Input;

public class C64SilkNetInputHandler : IInputHandler<C64, SilkNetInputHandlerContext>
{
    private SilkNetInputHandlerContext? _inputHandlerContext;
    private readonly List<string> _stats = new();
    private readonly C64SilkNetKeyboard _c64SilkNetKeyboard;
    private readonly ILogger<C64SilkNetInputHandler> _logger;

    public C64SilkNetInputHandler(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<C64SilkNetInputHandler>();

        // TODO: Is there a better way to current keyboard input language?
        var currentCulture = Thread.CurrentThread.CurrentCulture;
        var keyboardLayoutId = currentCulture.KeyboardLayoutId;
        var languageName = currentCulture.TwoLetterISOLanguageName;
        _logger.LogInformation($"KbLayoutId: {keyboardLayoutId}");
        _logger.LogInformation($"KbLanguage: {languageName}");

        _c64SilkNetKeyboard = new C64SilkNetKeyboard(languageName);
    }

    public void Init(C64 system, SilkNetInputHandlerContext inputHandlerContext)
    {
        _inputHandlerContext = inputHandlerContext;
        _inputHandlerContext.Init();
    }

    public void Init(ISystem system, IInputHandlerContext inputHandlerContext)
    {
        Init((C64)system, (SilkNetInputHandlerContext)inputHandlerContext);
    }

    public void ProcessInput(C64 c64)
    {
        CaptureKeyboard(c64);
        CaptureJoystick(c64);
    }

    public void ProcessInput(ISystem system)
    {
        ProcessInput((C64)system);
    }

    private void CaptureKeyboard(C64 c64)
    {
        var c64KeysDown = GetC64KeysFromSilkNetKeys(_inputHandlerContext!.KeysDown, out bool restoreKeyPressed);
        var keyboard = c64.Cia.Keyboard;
        keyboard.SetKeysPressed(c64KeysDown);
        if (restoreKeyPressed)
            keyboard.SetRestoreKeyPressed();
    }

    private List<C64Key> GetC64KeysFromSilkNetKeys(HashSet<Key> keysDown, out bool restoreKeyPressed)
    {
        var c64KeysDown = new List<C64Key>();
        var foundMappings = new List<Key[]>();

        if (keysDown.Contains(Key.PageUp))
            restoreKeyPressed = true;
        else
            restoreKeyPressed = false;

        foreach (var mapKeys in _c64SilkNetKeyboard.SilkNetToC64KeyMap.Keys)
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
            var c64Keys = _c64SilkNetKeyboard.SilkNetToC64KeyMap[mapKeys];
            foreach (var c64Key in c64Keys)
            {
                if (!c64KeysDown.Contains(c64Key))
                    c64KeysDown.Add(c64Key);
            }
        }
        return c64KeysDown;
    }

    private void CaptureJoystick(C64 c64)
    {
        var joystick = c64.Cia.Joystick;

        // Use keypresses as joystick input for now.
        if (joystick.KeyboardJoystickEnabled)
        {
            var joystick1KeyboardMap = joystick.KeyboardJoystickMap.KeyToJoystick1Map;
            var joystick1Actions = new HashSet<C64JoystickAction>();
            foreach (var charCode in joystick1KeyboardMap.Keys)
            {
                Key key = (Key)charCode;
                if (_inputHandlerContext!.IsKeyPressed(key))
                    joystick1Actions.Add(joystick1KeyboardMap[charCode]);
            }
            c64.Cia.Joystick.SetJoystick1Actions(joystick1Actions);

            var joystick2KeyboardMap = joystick.KeyboardJoystickMap.KeyToJoystick2Map;
            var joystick2Actions = new HashSet<C64JoystickAction>();
            foreach (var charCode in joystick2KeyboardMap.Keys)
            {
                Key key = (Key)charCode;
                if (_inputHandlerContext!.IsKeyPressed(key))
                    joystick2Actions.Add(joystick2KeyboardMap[charCode]);
            }
            c64.Cia.Joystick.SetJoystick2Actions(joystick2Actions);
        }
    }

    public List<string> GetStats()
    {
        return _stats;
    }
}
