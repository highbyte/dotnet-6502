using System.Globalization;
using Highbyte.DotNet6502.Instrumentation;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.Impl.AspNet.Commodore64.Input;

public class C64AspNetInputHandler : IInputHandler
{
    private readonly C64 _c64;
    public ISystem System => _c64;
    private readonly AspNetInputHandlerContext _inputHandlerContext;
    private readonly ILogger<C64AspNetInputHandler> _logger;
    private C64AspNetKeyboard _c64AspNetKeyboard = default!;
    private readonly C64AspNetInputConfig _c64AspNetConfig;

    // Instrumentations
    public Instrumentations Instrumentations { get; } = new();

    public C64AspNetInputHandler(C64 c64, AspNetInputHandlerContext inputHandlerContext, ILoggerFactory loggerFactory, C64AspNetInputConfig c64AspNetConfig)
    {
        _c64 = c64;
        _inputHandlerContext = inputHandlerContext;
        _logger = loggerFactory.CreateLogger<C64AspNetInputHandler>();
        _c64AspNetConfig = c64AspNetConfig;

        Init();
    }

    public void Init()
    {
        _inputHandlerContext.Init();

        // There doesn't seem a way to determine the users keyboard layout in Javascript/WASM.
        // Best guess is to use the current UI culture (sent by the browser in the Accept-Language header).
        // This will be incorrect if for example the user as a Swedish keyboard layout, but the browser is set to English.
        var currentUICulture = CultureInfo.CurrentUICulture;
        var keyboardLayoutId = currentUICulture.KeyboardLayoutId;
        var languageName = currentUICulture.TwoLetterISOLanguageName;
        _logger.LogInformation($"KbLayoutId: {keyboardLayoutId}");
        _logger.LogInformation($"KbLanguage: {languageName}");

        _c64AspNetKeyboard = new C64AspNetKeyboard(languageName);
    }

    public void BeforeFrame()
    {
        _c64.Cia.Joystick.ClearJoystickActions();
        CaptureKeyboard(_c64);
        CaptureJoystick(_c64);
    }

    public void BeforeFrame(ISystem system)
    {
        BeforeFrame((C64)system);
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
        var c64JoystickActions = GetC64JoystickActionsFromAspNetGamepad(_inputHandlerContext!.GamepadButtonsDown);
        // Note: Assume Keyboard input has been processed before this, so that Joystick actions based on keypresses has resulted 
        //       in the current joystick actions being initialized this frame (and may contain actions from keyboard).
        //       Thus "overwrite" is set to false so that keyboard actions are not overwritten.
        c64.Cia.Joystick.SetJoystickActions(_c64AspNetConfig.CurrentJoystick, c64JoystickActions);
    }

    private HashSet<C64JoystickAction> GetC64JoystickActionsFromAspNetGamepad(HashSet<int> gamepadButtonsDown)
    {
        var c64JoystickActions = new HashSet<C64JoystickAction>();
        var foundMappings = new List<int[]>();
        var map = _c64AspNetConfig.GamePadToC64JoystickMap[_c64AspNetConfig.CurrentJoystick];
        foreach (var mapKeys in map.Keys)
        {
            int matchCount = 0;
            foreach (var mapKeysKey in mapKeys)
            {
                if (gamepadButtonsDown.Contains(mapKeysKey))
                    matchCount++;
            }
            if (matchCount == mapKeys.Length)
            {
                // Remove any other mappings found that contains any of the Gamepad buttons in this mapping.
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
            var c64Keys = map[mapKeys];
            foreach (var c64Key in c64Keys)
            {
                if (!c64JoystickActions.Contains(c64Key))
                    c64JoystickActions.Add(c64Key);
            }
        }
        return c64JoystickActions;
    }

    public List<string> GetDebugInfo()
    {
        List<string> list = new();
        if (_inputHandlerContext == null)
            return list;

        if (_inputHandlerContext.KeysDown.Count > 0)
            list.Add($"KeysDown: {string.Join(',', _inputHandlerContext.KeysDown)}");
        if (_inputHandlerContext.GamepadButtonsDown.Count > 0)
            list.Add($"GamepadDown: {string.Join(',', _inputHandlerContext.GamepadButtonsDown)}");
        return list;
    }
}
