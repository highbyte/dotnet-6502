using System.Linq;
using Highbyte.DotNet6502.Impl.SadConsole.Commodore64.Config;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Video;
using SadConsole;
using SadConsole.Input;

namespace Highbyte.DotNet6502.Impl.SadConsole.Commodore64
{
    public class C64SadConsoleInputHandler : IInputHandler<C64, SadConsoleInputHandlerContext>, IInputHandler
    {
        private SadConsoleInputHandlerContext _inputHandlerContext;

        public C64SadConsoleInputHandler()
        {
        }

        public void Init(C64 system, SadConsoleInputHandlerContext inputHandlerContext)
        {
            _inputHandlerContext = inputHandlerContext;
            //_inputHandlerContext.Init();
        }

        public void Init(ISystem system, IInputHandlerContext inputHandlerContext)
        {
            Init((C64)system, (SadConsoleInputHandlerContext)inputHandlerContext);
        }

        public void ProcessInput(C64 c64)
        {
            CaptureKeyboard(c64);
        }

        public void ProcessInput(ISystem system)
        {
            ProcessInput((C64)system);
        }

        private void CaptureKeyboard(C64 c64)
        {
            var sadConsoleKeyboard = GameHost.Instance.Keyboard;
            var c64Keyboard = c64.Keyboard;

            HandleNonPrintedKeys(c64Keyboard, sadConsoleKeyboard);

            var petsciiCode = GetPetsciiCode(sadConsoleKeyboard);
            if (petsciiCode != 0)
                c64.Keyboard.KeyPressed(petsciiCode);
        }

        private void HandleNonPrintedKeys(
            Systems.Commodore64.Keyboard c64Keyboard,
            global::SadConsole.Input.Keyboard sadConsoleKeyboard)
        {
            // STOP (ESC) down
            if (sadConsoleKeyboard.IsKeyDown(Keys.Escape))
            {
                c64Keyboard.StopKeyFlag = 0x7f;
                return;
            }
            // STOP (ESC) released
            if (sadConsoleKeyboard.KeysReleased.Count == 1 && sadConsoleKeyboard.KeysReleased[0] == Keys.Escape)
            {
                c64Keyboard.StopKeyFlag = 0xff;
                return;
            }
        }

        private byte GetPetsciiCode(
            global::SadConsole.Input.Keyboard sadConsoleKeyboard)
        {
            // NOTE ON CURRENT ISSUED WITH KEYBOARD
            // - SadConsole doesn't return the character pressed on a international keyboard correct.
            // - The character pressed is not what is returned. Ex: Pressing shift-0 which should return '=' character instead returns ')'. Seems no way to get a '=' on any key.
            // - Some keys on a Swedish keyboard are not reported at all (returned as character 0)

            // If no keys detected, skip
            if (sadConsoleKeyboard.KeysReleased.Count == 0 && sadConsoleKeyboard.KeysPressed.Count == 0 && sadConsoleKeyboard.KeysDown.Count == 0)
                return 0;

            // Only handle the KeyPressed keys
            if (sadConsoleKeyboard.KeysPressed.Count == 0)
                return 0;

            // Skip key presses of modifier keys (they will be checked together for "key down" in combination with key presses on normal keys)
            var allPressedKeys = sadConsoleKeyboard.KeysPressed.Select(y => y.Key);
            if (C64SadConsoleKeyboard.AllModifierKeys.Any(x => allPressedKeys.Contains(x)))
            {
                //System.Diagnostics.Debug.WriteLine($"Skipping KeyPressed modifier: {string.Join(',', allPressedKeys)}");
                return 0;
            }

            var sadConsoleKey = sadConsoleKeyboard.KeysPressed[0];

            // Check which modifier key is down.
            Keys modifierKeyDown = Keys.None;
            foreach (var modifierKey in C64SadConsoleKeyboard.AllModifierKeys)
            {
                if (sadConsoleKeyboard.IsKeyDown(modifierKey))
                {
                    modifierKeyDown = modifierKey;
                    break;
                }
            }

            // Check if any special key is pressed based on modifier key.
            if (C64SadConsoleKeyboard.SpecialKeyMaps.ContainsKey(modifierKeyDown))
            {
                var specialKeyMap = C64SadConsoleKeyboard.SpecialKeyMaps[modifierKeyDown];
                if (specialKeyMap.ContainsKey(sadConsoleKey.Key))
                {
                    var petsciiCodeSpecial = specialKeyMap[sadConsoleKey.Key];
                    System.Diagnostics.Debug.WriteLine($"SadConsole special key pressed: {sadConsoleKey.Key} with modifier: {modifierKeyDown} and mapped to Petscii: {petsciiCodeSpecial}");
                    return petsciiCodeSpecial;
                }
            }

            // "Normal" key is pressed, map via char-to-petscii table
            if (!Petscii.CharToPetscii.ContainsKey(sadConsoleKey.Character))
            {
                System.Diagnostics.Debug.WriteLine($"SadConsole character pressed but not mapped: {sadConsoleKey.Character}");
                return 0;
            }
            var petsciiCode = Petscii.CharToPetscii[sadConsoleKey.Character];
            System.Diagnostics.Debug.WriteLine($"SadConsole normal character pressed {sadConsoleKey.Character} and mapped to Petscii: {petsciiCode}");
            return petsciiCode;
        }

        public string GetDebugMessage()
        {
            return "";
        }

    }
}
