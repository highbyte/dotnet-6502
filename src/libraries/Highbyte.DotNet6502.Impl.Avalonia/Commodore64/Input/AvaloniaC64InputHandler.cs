using Avalonia.Input;
using Highbyte.DotNet6502;
using Highbyte.DotNet6502.Impl.Avalonia.Input;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral;
using Highbyte.DotNet6502.Systems.Commodore64.Utils.BasicAssistant;
using Highbyte.DotNet6502.Systems.Instrumentation;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.Impl.Avalonia.Commodore64.Input;

public class AvaloniaC64InputHandler : IInputHandler
{
    public Instrumentations Instrumentations { get; } = new();
    private readonly C64 _c64;
    public ISystem System => _c64;

    public List<string> GetDebugInfo() => new();

    private readonly ILogger _logger;
    private readonly AvaloniaInputHandlerContext _inputHandlerContext;
    private C64AvaloniaKeyboard _c64AvaloniaKeyboard = default!;
    private readonly C64AvaloniaInputConfig _inputConfig;
    private readonly C64BasicCodingAssistant _c64BasicCodingAssistant;

    public bool CodingAssistantAvailable => _c64BasicCodingAssistant.IsAvailable;
    private bool _codingAssistantEnabled;
    public bool CodingAssistantEnabled
    {
        get
        {
            return _codingAssistantEnabled && CodingAssistantAvailable;
        }
        set
        {
            if (!CodingAssistantAvailable && value)
                return;
            _codingAssistantEnabled = value;
        }
    }

    public AvaloniaC64InputHandler(
        C64 c64,
        AvaloniaInputHandlerContext inputHandlerContext,
        ILoggerFactory loggerFactory,
        C64AvaloniaInputConfig inputConfig,
        C64BasicCodingAssistant c64BasicCodingAssistant,
        bool c64BasicCodingAssistantDefaultEnabled)
    {
        _c64 = c64;
        _inputHandlerContext = inputHandlerContext;
        _logger = loggerFactory.CreateLogger(typeof(AvaloniaC64InputHandler).Name);
        _inputConfig = inputConfig;
        _c64BasicCodingAssistant = c64BasicCodingAssistant;
        _codingAssistantEnabled = c64BasicCodingAssistantDefaultEnabled;
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

        _c64AvaloniaKeyboard = new C64AvaloniaKeyboard(languageName);
    }

    public void BeforeFrame()
    {
        _c64.Cia1.Joystick.ClearJoystickActions();
        CaptureKeyboard(_c64);

        // Update gamepad state before processing input
        _inputHandlerContext.UpdateGamepad();
        CaptureGamepad(_c64);
    }

    public void Cleanup()
    {
        // Cleanup input handling if needed
    }

    private void CaptureKeyboard(C64 c64)
    {
        var c64KeysDown = GetC64KeysFromAvaloniaKeys(_inputHandlerContext.KeysDown, out bool restoreKeyPressed, out bool capsLockOn);

        if (CodingAssistantEnabled && c64KeysDown.Count > 0)
        {
            _c64BasicCodingAssistant.KeyWasPressed(c64KeysDown);
        }

        var keyboard = c64.Cia1.Keyboard;
        keyboard.SetKeysPressed(c64KeysDown, restoreKeyPressed, capsLockOn);
    }

    private List<C64Key> GetC64KeysFromAvaloniaKeys(HashSet<Key> keysDown, out bool restoreKeyPressed, out bool capsLockOn)
    {
        restoreKeyPressed = keysDown.Contains(Key.PageUp) ? true : false;
        capsLockOn = _inputHandlerContext.GetCapsLockState();
        var c64KeysDown = new List<C64Key>();
        var foundMappings = new List<Key[]>();
        var map = _c64AvaloniaKeyboard.AvaloniaToC64KeyMap;

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

    private void CaptureGamepad(C64 c64)
    {
        var c64JoystickActions = GetC64JoystickActionsFromGamepad(_inputHandlerContext.GamepadButtonsDown);
        // Note: Joystick actions from keyboard have already been set, so use overwrite: false
        //       to combine with any gamepad actions.
        c64.Cia1.Joystick.SetJoystickActions(_inputConfig.CurrentJoystick, c64JoystickActions, overwrite: false);
    }

    private HashSet<C64JoystickAction> GetC64JoystickActionsFromGamepad(HashSet<GamepadButton> gamepadButtonsDown)
    {
        var c64JoystickActions = new HashSet<C64JoystickAction>();
        var foundMappings = new List<GamepadButton[]>();
        var map = _inputConfig.GamepadToC64JoystickMap[_inputConfig.CurrentJoystick];

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
                // Remove any other mappings found that contains any of the gamepad buttons in this mapping.
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
            var c64Actions = map[mapKeys];
            foreach (var c64Action in c64Actions)
            {
                if (!c64JoystickActions.Contains(c64Action))
                    c64JoystickActions.Add(c64Action);
            }
        }
        return c64JoystickActions;
    }
}
