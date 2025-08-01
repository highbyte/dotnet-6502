using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral;
using Highbyte.DotNet6502.Systems.Instrumentation;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.Impl.SilkNet.Commodore64.Input;

public class C64SilkNetInputHandler : IInputHandler
{
    private readonly C64 _c64;
    public ISystem System => _c64;

    private readonly SilkNetInputHandlerContext _inputHandlerContext;
    private readonly ILogger<C64SilkNetInputHandler> _logger;
    private readonly C64SilkNetInputConfig _c64SilkNetConfig;

    private C64SilkNetKeyboard _c64SilkNetKeyboard;
    //private readonly C64SilkNetGamepad _c64SilkNetGamepad;

    public List<string> GetDebugInfo() => new();

    // Instrumentations
    public Instrumentations Instrumentations { get; } = new();


    public C64SilkNetInputHandler(C64 c64, SilkNetInputHandlerContext inputHandlerContext, ILoggerFactory loggerFactory, C64SilkNetInputConfig c64SilkNetConfig)
    {
        _c64 = c64;
        _inputHandlerContext = inputHandlerContext;
        _logger = loggerFactory.CreateLogger<C64SilkNetInputHandler>();
        _c64SilkNetConfig = c64SilkNetConfig;
    }

    public void Init()
    {
        // TODO: Is there a better way to current keyboard input language?
        // Note: Using CurrentCulture instead of CurrentUICulture.
        //       Why does CurrentUICulture not return correct keyboard as it does in SadConsole project?
        var currentCulture = Thread.CurrentThread.CurrentCulture;
        var keyboardLayoutId = currentCulture.KeyboardLayoutId;
        var languageName = currentCulture.TwoLetterISOLanguageName;
        _logger.LogInformation($"KbLayoutId: {keyboardLayoutId}");
        _logger.LogInformation($"KbLanguage: {languageName}");

        _c64SilkNetKeyboard = new C64SilkNetKeyboard(languageName);

        //_c64SilkNetGamepad = new C64SilkNetGamepad();

    }

    public void BeforeFrame()
    {
        _c64.Cia1.Joystick.ClearJoystickActions();
        CaptureKeyboard(_c64);
        CaptureJoystick(_c64);
    }

    public void Cleanup()
    {
    }

    private void CaptureKeyboard(C64 c64)
    {
        var c64KeysDown = GetC64KeysFromSilkNetKeys(_inputHandlerContext!.KeysDown, out bool restoreKeyPressed, out bool capsLockOn);
        var keyboard = c64.Cia1.Keyboard;
        keyboard.SetKeysPressed(c64KeysDown, restoreKeyPressed, capsLockOn);
    }

    private List<C64Key> GetC64KeysFromSilkNetKeys(HashSet<Key> keysDown, out bool restoreKeyPressed, out bool capsLockOn)
    {
        restoreKeyPressed = keysDown.Contains(Key.PageUp) ? true : false;
        capsLockOn = _inputHandlerContext!.GetCapsLockState();
        var c64KeysDown = new List<C64Key>();
        var foundMappings = new List<Key[]>();
        var map = _c64SilkNetKeyboard.SilkNetToC64KeyMap;
        foreach (var mapKeys in map.Keys)
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
            var c64Keys = map[mapKeys];
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
        var c64JoystickActions = GetC64JoystickActionsFromSilkNetGamepad(_inputHandlerContext!.GamepadButtonsDown);
        // Note: Assume Keyboard input has been processed before this, so that Joystick actions based on keypresses has resulted 
        //       in the current joystick actions being initialized this frame (and may contain actions from keyboard).
        //       Thus "overwrite" is set to false so that keyboard actions are not overwritten.
        c64.Cia1.Joystick.SetJoystickActions(_c64SilkNetConfig.CurrentJoystick, c64JoystickActions, overwrite: false);
    }

    private HashSet<C64JoystickAction> GetC64JoystickActionsFromSilkNetGamepad(HashSet<ButtonName> gamepadButtonsDown)
    {
        var c64JoystickActions = new HashSet<C64JoystickAction>();
        var foundMappings = new List<ButtonName[]>();
        var map = _c64SilkNetConfig.GamePadToC64JoystickMap[_c64SilkNetConfig.CurrentJoystick];
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
}
