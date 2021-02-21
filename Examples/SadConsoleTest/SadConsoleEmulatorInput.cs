using Highbyte.DotNet6502;

namespace SadConsoleTest
{
    public class SadConsoleEmulatorInput
    {
        private readonly Memory _emulatorMem;
        private readonly EmulatorInputConfig _emulatorInputConfig;

        public SadConsoleEmulatorInput(
            Memory emulatorMem,
            EmulatorInputConfig emulatorInputConfig)
        {
            _emulatorMem = emulatorMem;
            _emulatorInputConfig = emulatorInputConfig;            
        }

        public void CaptureInput()
        {
            CaptureKeyboard();
        }

        private void CaptureKeyboard()
        {
            var keyboard = SadConsole.Global.KeyboardState;

            if(keyboard.KeysPressed.Count > 0)
                _emulatorMem[_emulatorInputConfig.KeyPressedAddress] = (byte)keyboard.KeysPressed[0].Character;
            else
                _emulatorMem[_emulatorInputConfig.KeyPressedAddress] = 0x00;

            if(keyboard.KeysDown.Count > 0)
                _emulatorMem[_emulatorInputConfig.KeyDownAddress] = (byte)keyboard.KeysDown[0].Character;
            else
                _emulatorMem[_emulatorInputConfig.KeyDownAddress] = 0x00;

            if(keyboard.KeysReleased.Count > 0)
                _emulatorMem[_emulatorInputConfig.KeyReleasedAddress] = (byte)keyboard.KeysReleased[0].Character;
            else
                _emulatorMem[_emulatorInputConfig.KeyReleasedAddress] = 0x00;
        }
    }
}