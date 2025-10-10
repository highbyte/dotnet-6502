using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Avalonia.Input;
using Highbyte.DotNet6502.App.Avalonia.Core.Config;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral;
using Highbyte.DotNet6502.Systems.Instrumentation;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.App.Avalonia.Core.Input;

public class AvaloniaC64InputHandler : IInputHandler
{
    private readonly C64 _c64;
    private readonly AvaloniaInputHandlerContext _inputHandlerContext;
    private readonly ILogger _logger;
    private readonly C64AvaloniaInputConfig _inputConfig;

    private C64AvaloniaKeyboard _c64AvaloniaKeyboard = default!;

    public Instrumentations Instrumentations { get; } = new();
    public ISystem System => _c64;

    public List<string> GetDebugInfo() => new();

    public AvaloniaC64InputHandler(
        C64 c64,
        AvaloniaInputHandlerContext inputHandlerContext,
        ILoggerFactory loggerFactory,
        C64AvaloniaInputConfig inputConfig)
    {
        _c64 = c64;
        _inputHandlerContext = inputHandlerContext;
        _logger = loggerFactory.CreateLogger(typeof(AvaloniaC64InputHandler).Name);
        _inputConfig = inputConfig;
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
        CaptureJoystick(_c64);
    }

    public void Cleanup()
    {
        // Cleanup input handling if needed
    }

    private void CaptureKeyboard(C64 c64)
    {
        var c64KeysDown = GetC64KeysFromAvaloniaKeys(_inputHandlerContext.KeysDown, out bool restoreKeyPressed, out bool capsLockOn);
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

    private void CaptureJoystick(C64 c64)
    {
        var c64JoystickActions = GetC64JoystickActionsFromAvaloniaKeys(_inputHandlerContext.KeysDown);
        // Note: Assume Keyboard input has been processed before this, so that Joystick actions based on keypresses has resulted 
        //       in the current joystick actions being initialized this frame (and may contain actions from keyboard).
        //       Thus "overwrite" is set to false so that keyboard actions are not overwritten.
        c64.Cia1.Joystick.SetJoystickActions(_inputConfig.CurrentJoystick, c64JoystickActions, overwrite: false);
    }

    private HashSet<C64JoystickAction> GetC64JoystickActionsFromAvaloniaKeys(HashSet<Key> keysDown)
    {
        var c64JoystickActions = new HashSet<C64JoystickAction>();
        var map = _inputConfig.KeyToC64JoystickMap[_inputConfig.CurrentJoystick];
        
        foreach (var keyDown in keysDown)
        {
            if (map.TryGetValue(keyDown, out var joystickAction))
            {
                c64JoystickActions.Add(joystickAction);
            }
        }
        
        return c64JoystickActions;
    }
}
