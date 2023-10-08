using System.Globalization;
using Highbyte.DotNet6502.Instructions;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral;
using Highbyte.DotNet6502.Systems.Commodore64.Video;
using Microsoft.Extensions.Logging;
using static Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral.C64Joystick;

namespace Highbyte.DotNet6502.Impl.AspNet.Commodore64.Input;

public class C64AspNetInputHandler : IInputHandler<C64, AspNetInputHandlerContext>
{
    private AspNetInputHandlerContext? _inputHandlerContext = default!;
    private ILogger<C64AspNetInputHandler> _logger;
    private C64AspNetKeyboard _c64AspNetKeyboard;

    public C64AspNetInputHandler(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<C64AspNetInputHandler>();

    }

    public void Init(C64 system, AspNetInputHandlerContext inputHandlerContext)
    {
        _inputHandlerContext = inputHandlerContext;
        _inputHandlerContext.Init();

        // There doesn't seem a way to determine the users keyboard layout in Javascript/WASM.
        // Best guess is to use the current UI culture (sent by the browser in the Accept-Language header).
        // This will be incorrect if for example the user as a Swedish keyboard layout, but the browser is set to English.
        var uiCulture = CultureInfo.CurrentUICulture;
        var keyboardLayoutId = uiCulture.KeyboardLayoutId;
        var languageName = uiCulture.TwoLetterISOLanguageName;
        _logger.LogInformation($"KbLayoutId: {keyboardLayoutId}");
        _logger.LogInformation($"KbLanguage: {languageName}");

        _c64AspNetKeyboard = new C64AspNetKeyboard(languageName);
    }

    public void Init(ISystem system, IInputHandlerContext inputHandlerContext)
    {
        Init((C64)system, (AspNetInputHandlerContext)inputHandlerContext);
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
        var c64KeysDown = GetC64KeysFromAspNetKeys(_inputHandlerContext!.KeysDown, out bool restoreKeyPressed, out bool capsLockOn);
        var keyboard = c64.Cia.Keyboard;
        keyboard.SetKeysPressed(c64KeysDown, restoreKeyPressed, capsLockOn);
    }

    private List<C64Key> GetC64KeysFromAspNetKeys(HashSet<string> keysDown, out bool restoreKeyPressed, out bool capsLockOn)
    {
        restoreKeyPressed = keysDown.Contains("PageUp") ? true : false;
        capsLockOn = _inputHandlerContext!.GetCapsLockState();

        var c64KeysDown = new List<C64Key>();
        var foundMappings = new List<string[]>();
        foreach (var mapKeys in _c64AspNetKeyboard.AspNetToC64KeyMap.Keys)
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
            var c64Keys = _c64AspNetKeyboard.AspNetToC64KeyMap[mapKeys];
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
                string key = charCode.ToString().ToLower();
                if (_inputHandlerContext!.KeysDown.Contains(key))
                    joystick1Actions.Add(joystick1KeyboardMap[charCode]);
            }
            c64.Cia.Joystick.SetJoystick1Actions(joystick1Actions);

            var joystick2KeyboardMap = joystick.KeyboardJoystickMap.KeyToJoystick2Map;
            var joystick2Actions = new HashSet<C64JoystickAction>();
            foreach (var charCode in joystick2KeyboardMap.Keys)
            {
                string key = charCode.ToString().ToLower();
                if (_inputHandlerContext!.KeysDown.Contains(key))
                    joystick2Actions.Add(joystick2KeyboardMap[charCode]);
            }
            c64.Cia.Joystick.SetJoystick2Actions(joystick2Actions);
        }
    }

    public List<string> GetStats()
    {
        List<string> list = new();
        if (_inputHandlerContext == null)
            return list;

        if (_inputHandlerContext.KeysDown.Count > 0)
            list.Add($"KeysDown: {string.Join(',', _inputHandlerContext.KeysDown)}");
        return list;
    }
}
