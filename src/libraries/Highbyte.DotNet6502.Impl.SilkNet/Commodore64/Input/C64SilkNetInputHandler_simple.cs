//using System.Diagnostics;
//using Highbyte.DotNet6502.Systems;
//using Highbyte.DotNet6502.Systems.Commodore64;
//using Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral;
//using Highbyte.DotNet6502.Systems.Commodore64.Video;
//using static Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral.C64Joystick;
//using static Highbyte.DotNet6502.Systems.Commodore64.Video.Vic2;

//namespace Highbyte.DotNet6502.Impl.SilkNet.Commodore64.Input;

//public class C64SilkNetInputHandler_simple : IInputHandler<C64, SilkNetInputHandlerContext>
//{
//    private SilkNetInputHandlerContext? _inputHandlerContext;
//    private readonly List<string> _stats = new();

//    public C64SilkNetInputHandler()
//    {
//    }

//    public void Init(C64 system, SilkNetInputHandlerContext inputHandlerContext)
//    {
//        _inputHandlerContext = inputHandlerContext;
//        _inputHandlerContext.Init();
//    }

//    public void Init(ISystem system, IInputHandlerContext inputHandlerContext)
//    {
//        Init((C64)system, (SilkNetInputHandlerContext)inputHandlerContext);
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
//        if (_inputHandlerContext!.IsKeyPressed(Key.Escape))
//        //if (_inputHandlerContext.SpecialKeyReceived.Count == 1 && _inputHandlerContext.SpecialKeyReceived.First() == Key.Escape)
//        {
//            c64.Mem[CiaAddr.CIA1_DATAB] = 0x00;  // Hack: not yet handling the CIA Data B register to scan keyboard.

//            // Pressing STOP (RUN/STOP) will stop any running Basic program.
//            c64Keyboard.StopKeyFlag = 0x7f;

//            // RESTORE (PageUp) down. Together with STOP it will issue a NMI (which will jump to code that detects STOP is pressed and resets any running program, and clears screen.)
//            if (_inputHandlerContext.IsKeyPressed(Key.PageUp))
//                c64.CPU.CPUInterrupts.SetNMISourceActive("KeyboardReset");

//            return;
//        }
//        // STOP (ESC) released
//        if (_inputHandlerContext.KeysUp.Count == 1 && _inputHandlerContext.KeysUp.First() == Key.Escape)
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
//        var modifierKeyDown = Key.Unknown;
//        foreach (var modifierKey in C64SilkNetKeyboard.AllModifierKeys)
//        {
//            var modifierKeyPressed = _inputHandlerContext!.IsKeyPressed(modifierKey);
//            if (modifierKeyPressed)
//            {
//                modifierKeyDown = modifierKey;
//                break;
//            }
//        }

//        if (C64SilkNetKeyboard.SpecialKeyMaps.ContainsKey(modifierKeyDown))
//        {
//            var specialKeyMap = C64SilkNetKeyboard.SpecialKeyMaps[modifierKeyDown];

//            foreach (var key in specialKeyMap.Keys)
//            {
//                if (_inputHandlerContext!.KeysDown.Contains(key))
//                {
//                    var petsciiCode = specialKeyMap[key];
//                    Debug.WriteLine($"SilkNet special key pressed: {key} with modifier: {modifierKeyDown} and mapped to Petscii: {petsciiCode}");
//                    c64Keyboard.KeyPressed(petsciiCode);

//                    // If we detected a special Key/Combo pressed, don't process anymore. Some of them may also be in the _inputHandlerContext.CharactersReceived list processed below.
//                    return;
//                }
//            }
//        }

//        // Check if nothing to do with captured characters.
//        if (_inputHandlerContext!.CharactersReceived.Count == 0)
//            return;

//        foreach (var character in _inputHandlerContext.CharactersReceived)
//        {
//            if (!Petscii.CharToPetscii.ContainsKey(character))
//            {
//                Debug.WriteLine($"SilkNet normal character pressed {character} couldn't be found as a Petscii code.");
//                continue;
//            }
//            var petsciiCode = Petscii.CharToPetscii[character];
//            Debug.WriteLine($"SilkNet normal character pressed {character} and mapped to Petscii: {petsciiCode}");
//            c64Keyboard.KeyPressed(petsciiCode);
//        }
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
//                Key key = (Key)charCode;
//                if (_inputHandlerContext!.IsKeyPressed(key))
//                    joystick1Actions.Add(joystick1KeyboardMap[charCode]);
//            }
//            c64.Cia.Joystick.SetJoystick1Actions(joystick1Actions);

//            var joystick2KeyboardMap = joystick.KeyboardJoystickMap.KeyToJoystick2Map;
//            var joystick2Actions = new HashSet<C64JoystickAction>();
//            foreach (var charCode in joystick2KeyboardMap.Keys)
//            {
//                Key key = (Key)charCode;
//                if (_inputHandlerContext!.IsKeyPressed(key))
//                    joystick2Actions.Add(joystick2KeyboardMap[charCode]);
//            }
//            c64.Cia.Joystick.SetJoystick2Actions(joystick2Actions);
//        }
//    }

//    public List<string> GetStats()
//    {
//        return _stats;
//    }
//}
