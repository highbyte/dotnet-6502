using System.Diagnostics;
using System.Linq;
using Highbyte.DotNet6502.Impl.SilkNet;
using Highbyte.DotNet6502.Impl.SilkNet.Commodore64.Config;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Silk.NET.Input;

namespace Highbyte.DotNet6502.Impl.SilkNet.Commodore64
{
    public class C64SilkNetInputHandler : IInputHandler<C64, SilkNetInputHandlerContext>, IInputHandler
    {
        private SilkNetInputHandlerContext _inputHandlerContext;

        public C64SilkNetInputHandler()
        {
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

            _inputHandlerContext.ClearKeys();   // Clear our captured keys so far
        }

        public void ProcessInput(ISystem system)
        {
            ProcessInput((C64)system);
        }

        private void CaptureKeyboard(C64 c64)
        {
            var c64Keyboard = c64.Keyboard;

            HandleNonPrintedKeys(c64Keyboard);

            var petsciiCode = GetPetsciiCode();
            if (petsciiCode != 0)
                c64.Keyboard.KeyPressed(petsciiCode);
        }

        private void HandleNonPrintedKeys(
            Systems.Commodore64.Keyboard c64Keyboard)
        {
            // STOP (ESC) down
            if (_inputHandlerContext.IsKeyPressed(Key.Escape))
            //if (_inputHandlerContext.SpecialKeyReceived.Count == 1 && _inputHandlerContext.SpecialKeyReceived.First() == Key.Escape)
            {
                c64Keyboard.StopKeyFlag = 0x7f;
                return;
            }
            // STOP (ESC) released
            if (_inputHandlerContext.KeysUp.Count == 1 && _inputHandlerContext.KeysUp.First() == Key.Escape)
            {
                c64Keyboard.StopKeyFlag = 0xff;
                return;
            }
        }

        private byte GetPetsciiCode()
        {

            // Only handle the KeyPressed/Received keys
            if (_inputHandlerContext.KeysReceived.Count == 0 && _inputHandlerContext.SpecialKeyReceived.Count == 0)
                return 0;

            if (_inputHandlerContext.KeysReceived.Count == 0 && _inputHandlerContext.SpecialKeyReceived.Count > 0)
            {
                // Special (non character like Enter, Backspace, etc.)
                var silkNetKey = _inputHandlerContext.SpecialKeyReceived.First();
                // Check which modifier key is down.
                Key? modifierKeyDown = null;
                foreach (var modifierKey in C64SilkNetKeyboard.AllModifierKeys)
                {
                    var modifierKeyPressed = _inputHandlerContext.IsKeyPressed(modifierKey);
                    if (modifierKeyPressed)
                    {
                        modifierKeyDown = modifierKey;
                        break;
                    }
                }

                Dictionary<Key, byte> specialKeyMap;
                // Check if any special key is pressed based on modifier key.
                if (modifierKeyDown.HasValue && C64SilkNetKeyboard.SpecialKeyMaps.ContainsKey(modifierKeyDown.Value))
                    specialKeyMap = C64SilkNetKeyboard.SpecialKeyMaps[modifierKeyDown.Value];
                else
                    specialKeyMap = C64SilkNetKeyboard.SpecialKeys; // With no modifier

                if (specialKeyMap.ContainsKey(silkNetKey))
                {
                    var petsciiCodeSpecial = specialKeyMap[silkNetKey];
                    System.Diagnostics.Debug.WriteLine($"SilkNet special key pressed: {silkNetKey} with modifier: {modifierKeyDown} and mapped to Petscii: {petsciiCodeSpecial}");
                    return petsciiCodeSpecial;
                }

                return 0;
            }
            else
            {
                // "Normal" key is pressed, map ASCII character code to PetscII
                var silkNetCharacter = _inputHandlerContext.KeysReceived.First();
                if (!Petscii.AscIICharToPetscii.ContainsKey(silkNetCharacter))
                {
                    System.Diagnostics.Debug.WriteLine($"SilkNet character pressed but not mapped: {silkNetCharacter}");
                    return 0;
                }
                var petsciiCode = Petscii.AscIICharToPetscii[silkNetCharacter];
                System.Diagnostics.Debug.WriteLine($"SilkNet normal character pressed {silkNetCharacter} and mapped to Petscii: {petsciiCode}");
                return petsciiCode;
            }
        }
    }
}
