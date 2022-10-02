using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64;
using SadConsole;
using SadConsole.Input;

namespace Highbyte.DotNet6502.SadConsoleHost.Commodore64
{
    public class C64SadConsoleInputHandler : IInputHandler<C64>, IInputHandler
    {
        public C64SadConsoleInputHandler()
        {
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

            HandleSpecialKeys(c64Keyboard, sadConsoleKeyboard);

            var petsciiCode = GetPetsciiCode(sadConsoleKeyboard);
            if (petsciiCode != 0)
                c64.Keyboard.KeyPressed(petsciiCode);
        }

        private void HandleSpecialKeys(
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
            // If no keys detected, skip
            if (sadConsoleKeyboard.KeysReleased.Count == 0 && sadConsoleKeyboard.KeysPressed.Count == 0 && sadConsoleKeyboard.KeysDown.Count == 0)
                return 0;

            // Only handle the KeyPressed keys
            if (sadConsoleKeyboard.KeysPressed.Count != 1)
                return 0;

            // Skip key presses of modifier keys (they will be checked together for "key down" in combination with key presses on normal keys)
            if (C64SadConsoleKeyboard.AllModifierKeys.Contains(sadConsoleKeyboard.KeysPressed[0].Key))
                return 0;

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

            // Check which keymap should be used depending on the modifier key.
            if (!C64SadConsoleKeyboard.KeyMaps.ContainsKey(modifierKeyDown))
                return 0;
            var keyMap = C64SadConsoleKeyboard.KeyMaps[modifierKeyDown];

            if (!keyMap.ContainsKey(sadConsoleKey.Key))
                return 0;

            return keyMap[sadConsoleKey.Key];
        }
    }
}
