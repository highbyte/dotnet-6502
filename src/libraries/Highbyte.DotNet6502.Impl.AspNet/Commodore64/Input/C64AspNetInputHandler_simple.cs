//using Highbyte.DotNet6502.Instructions;
//using Highbyte.DotNet6502.Systems;
//using Highbyte.DotNet6502.Systems.Commodore64;
//using Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral;
//using Highbyte.DotNet6502.Systems.Commodore64.Video;
//using static Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral.C64Joystick;

//namespace Highbyte.DotNet6502.Impl.AspNet.Commodore64.Input;

//public class C64AspNetInputHandler_simple : IInputHandler<C64, AspNetInputHandlerContext>
//{
//    private AspNetInputHandlerContext? _inputHandlerContext = default!;

//    public C64AspNetInputHandler_simple()
//    {
//    }

//    public void Init(C64 system, AspNetInputHandlerContext inputHandlerContext)
//    {
//        _inputHandlerContext = inputHandlerContext;
//        _inputHandlerContext.Init();
//    }

//    public void Init(ISystem system, IInputHandlerContext inputHandlerContext)
//    {
//        Init((C64)system, (AspNetInputHandlerContext)inputHandlerContext);
//    }

//    public void ProcessInput(C64 c64)
//    {
//        CaptureKeyboard(c64);

//        CaptureJoystick(c64);

//        _inputHandlerContext!.ClearKeys();   // Clear our captured keys so far
//    }

//    public void ProcessInput(ISystem system)
//    {
//        ProcessInput((C64)system);
//    }

//    private void CaptureKeyboard(C64 c64)
//    {
//        HandleNonPrintedC64Keys(c64);
//        HandlePrintedC64Keys(c64);
//    }

//    private void HandleNonPrintedC64Keys(C64 c64)
//    {
//        var c64Keyboard = c64.Keyboard;

//        // STOP (ESC) down
//        if (_inputHandlerContext!.KeysDown.Contains("Escape"))
//        //if (_inputHandlerContext.SpecialKeyReceived.Count == 1 && _inputHandlerContext.SpecialKeyReceived.First() == Key.Escape)
//        {
//            c64.Mem[CiaAddr.CIA1_DATAB] = 0x00;  // Hack: not yet handling the CIA Data B register to scan keyboard.

//            // Pressing STOP (RUN/STOP) will stop any running Basic program.
//            c64Keyboard.StopKeyFlag = 0x7f;

//            // RESTORE (PageUp) down. Together with STOP it will issue a NMI (which will jump to code that detects STOP is pressed and resets any running program, and clears screen.)
//            if (_inputHandlerContext.KeysDown.Contains("PageUp"))
//                c64.CPU.CPUInterrupts.SetNMISourceActive("KeyboardReset");
//            return;
//        }

//        // STOP (ESC) released
//        if (_inputHandlerContext.KeysUp.Count == 1 && _inputHandlerContext.KeysUp.First() == "Escape")
//        {
//            c64Keyboard.StopKeyFlag = 0xff;
//            return;
//        }

//        if (_inputHandlerContext.KeysDown.Count == 0)
//            c64.Mem[CiaAddr.CIA1_DATAB] = 0xff; // Hack: not yet handling the CIA Data B register to scan keyboard.

//    }

//    private void HandlePrintedC64Keys(C64 c64)
//    {
//        var c64Keyboard = c64.Keyboard;

//        // Check if modifier key is down.
//        var modifierKeyDown = "";
//        foreach (var modifierKey in C64AspNetKeyboard.AllModifierKeys)
//        {
//            var modifierKeyPressed = _inputHandlerContext!.KeysDown.Contains(modifierKey);
//            if (modifierKeyPressed)
//            {
//                modifierKeyDown = modifierKey;
//                break;
//            }
//        }

//        if (C64AspNetKeyboard.SpecialKeyMaps.ContainsKey(modifierKeyDown))
//        {
//            var specialKeyMap = C64AspNetKeyboard.SpecialKeyMaps[modifierKeyDown];

//            foreach (var key in specialKeyMap.Keys)
//            {
//                if (_inputHandlerContext!.KeysDown.Contains(key))
//                {
//                    var petsciiCode = specialKeyMap[key];
//                    c64Keyboard.KeyPressed(petsciiCode);

//                    _inputHandlerContext.KeysDown.Remove(key);
//                    // If we detected a special Key/Combo pressed, don't process anymore. Some of them may also be in the _inputHandlerContext.CharactersReceived list processed below.
//                    return;
//                }
//            }
//        }

//        // Check if nothing to do with captured characters.
//        if (_inputHandlerContext!.KeysPressed.Count == 0)
//            return;

//        foreach (var key in _inputHandlerContext.KeysPressed)
//        {
//            var character = MapAspNetKeyStringToCharacter(key);
//            if (!Petscii.CharToPetscii.ContainsKey(character))
//                continue;
//            var petsciiCode = Petscii.CharToPetscii[character];
//            c64Keyboard.KeyPressed(petsciiCode);
//        }
//    }

//    private char MapAspNetKeyStringToCharacter(string key)
//    {
//        char character;
//        if (key.Length == 1)
//            return key[0];

//        character = (char)0;
//        if (C64AspNetKeyboard.AspNetKeyStringToCharacter.ContainsKey(key))
//            character = C64AspNetKeyboard.AspNetKeyStringToCharacter[key];
//        return character;
//    }

//    private void CaptureJoystick(C64 c64)
//    {
//        var joystick = c64.Cia.Joystick;

//        // Use keypresses as joystick input for now.
//        if (joystick.KeyboardJoystickEnabled)
//        {
//            var joystick1KeyboardMap = joystick.KeyboardJoystickMap.KeyToJoystick1Map;
//            var joystick1Actions = new HashSet<C64JoystickAction>();
//            foreach (var charCode in joystick1KeyboardMap.Keys)
//            {
//                string key = charCode.ToString().ToLower();
//                if (_inputHandlerContext!.KeysDown.Contains(key))
//                    joystick1Actions.Add(joystick1KeyboardMap[charCode]);
//            }
//            c64.Cia.Joystick.SetJoystick1Actions(joystick1Actions);

//            var joystick2KeyboardMap = joystick.KeyboardJoystickMap.KeyToJoystick2Map;
//            var joystick2Actions = new HashSet<C64JoystickAction>();
//            foreach (var charCode in joystick2KeyboardMap.Keys)
//            {
//                string key = charCode.ToString().ToLower();
//                if (_inputHandlerContext!.KeysDown.Contains(key))
//                    joystick2Actions.Add(joystick2KeyboardMap[charCode]);
//            }
//            c64.Cia.Joystick.SetJoystick2Actions(joystick2Actions);
//        }
//    }

//    public List<string> GetStats()
//    {
//        List<string> list = new();
//        if (_inputHandlerContext == null)
//            return list;

//        if (_inputHandlerContext.KeysDown.Count > 0)
//            list.Add($"KeysDown: {string.Join(',', _inputHandlerContext.KeysDown)}");
//        if (_inputHandlerContext.KeysUp.Count > 0)
//            list.Add($"KeysUp: {string.Join(',', _inputHandlerContext.KeysUp)}");
//        return list;
//    }
//}
