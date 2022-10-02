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

        // public void Handle(IKeyboardEvent keyboardEvent)
        // {
        // }

        private void CaptureKeyboard(C64 c64)
        {
            var sadConsoleKeyboard = GameHost.Instance.Keyboard;
            var petsciiCode = MapSadConsoleKeyToPETSCII(c64.Keyboard, sadConsoleKeyboard);
            if (petsciiCode != 0)
                c64.Keyboard.KeyPressed(petsciiCode);
        }
        private byte MapSadConsoleKeyToPETSCII(
            Systems.Commodore64.Keyboard c64Keyboard,
            global::SadConsole.Input.Keyboard sadConsoleKeyboard)
        {
            if (sadConsoleKeyboard.KeysReleased.Count == 0 && sadConsoleKeyboard.KeysPressed.Count == 0 && sadConsoleKeyboard.KeysDown.Count == 0)
                return 0;

            // ----------
            // Special keys
            // ----------
            // STOP (ESC) down
            if (sadConsoleKeyboard.IsKeyDown(Keys.Escape))
            {
                c64Keyboard.StopKeyFlag = 0x7f;
                return 0;
            }
            // STOP (ESC) released
            if (sadConsoleKeyboard.KeysReleased.Count == 1 && sadConsoleKeyboard.KeysReleased[0] == Keys.Escape)
            {
                c64Keyboard.StopKeyFlag = 0xff;
                return 0;
            }

            // ----------
            // Map normal pressed characters
            // ----------
            if (sadConsoleKeyboard.KeysPressed.Count != 1)
                return 0;
            var sadConsoleKey = sadConsoleKeyboard.KeysPressed[0];
            var petsciiCode = sadConsoleKey.Key switch
            {
                // ref: https://www.c64-wiki.com/wiki/PETSCII_Codes_in_Listings
                Keys.Enter => (byte)13,
                Keys.Back => (byte)20,
                Keys.Up => (byte)145,
                Keys.Down => (byte)17,
                Keys.Left => (byte)157,
                Keys.Right => (byte)29,
                _ => (byte)sadConsoleKey.Character,
            };
            return petsciiCode;
        }

    }
}
