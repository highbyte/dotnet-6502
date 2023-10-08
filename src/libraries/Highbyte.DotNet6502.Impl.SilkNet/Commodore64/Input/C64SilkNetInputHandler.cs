using System.Runtime.InteropServices;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral;
using Microsoft.Extensions.Logging;
using Silk.NET.Input;
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
        var c64KeysDown = GetC64KeysFromSilkNetKeys(_inputHandlerContext!.KeysDown, out bool restoreKeyPressed, out bool capsLockOn);
        var keyboard = c64.Cia.Keyboard;
        keyboard.SetKeysPressed(c64KeysDown, restoreKeyPressed, capsLockOn);
    }

    private List<C64Key> GetC64KeysFromSilkNetKeys(HashSet<Key> keysDown, out bool restoreKeyPressed, out bool capsLockOn)
    {
        restoreKeyPressed = keysDown.Contains(Key.PageUp) ? true : false;
        capsLockOn = _inputHandlerContext!.GetCapsLockState();

        var c64KeysDown = new List<C64Key>();
        var foundMappings = new List<Key[]>();
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
        //var joystick = c64.Cia.Joystick;
        // TODO: Capture joystick input via Silk.NET xbox controller?
        //       For now there is option to control C64 joystick via keyboard (see C64Keyboard class)

    }

    public List<string> GetStats()
    {
        return _stats;
    }
}
