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

            bool processNextKey = true;
            while (processNextKey)
            {
                processNextKey = GetNextPetsciiCode(out byte petsciiCode);
                if (petsciiCode != 0)
                    c64.Keyboard.KeyPressed(petsciiCode);
            }
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

        private bool GetNextPetsciiCode(out byte petsciiCode)
        {
            petsciiCode = 0;
            if (_inputHandlerContext.KeysReceived.Count == 0 && _inputHandlerContext.SpecialKeyReceived.Count == 0)
                return false;

            // Check if modifier key is down.
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

            // If received key is not a normal PC/Mac character (or it wasn't mapped to PetscII character above), 
            // check if we have a map for the key.
            // If the special key is also a "normal" character received, skip it here (and process it below for normal characters)
            if (_inputHandlerContext.SpecialKeyReceived.Count > 0)
            {
                Key? inspectSilkNetKey = null;
                bool foundValidSilkNetKey = false;
                while (!foundValidSilkNetKey && _inputHandlerContext.SpecialKeyReceived.Count > 0)
                {
                    // Special (non-character like Enter, Backspace, etc.)
                    inspectSilkNetKey = _inputHandlerContext.SpecialKeyReceived.First();
                    _inputHandlerContext.SpecialKeyReceived.Remove(inspectSilkNetKey.Value);

                    foundValidSilkNetKey = !C64SilkNetKeyboard.AllModifierKeys.Contains(inspectSilkNetKey.Value);
                }

                if (foundValidSilkNetKey)
                {
                    Key silkNetKey = inspectSilkNetKey.Value;
                    if (!_inputHandlerContext.KeysReceived.Contains((char)silkNetKey))
                    {

                        Dictionary<Key, byte> specialKeyMap;
                        // Check if any special key is pressed based on modifier key.
                        if (modifierKeyDown.HasValue && C64SilkNetKeyboard.SpecialKeyMaps.ContainsKey(modifierKeyDown.Value))
                            specialKeyMap = C64SilkNetKeyboard.SpecialKeyMaps[modifierKeyDown.Value];
                        else
                            specialKeyMap = C64SilkNetKeyboard.SpecialKeys; // With no modifier

                        if (specialKeyMap.ContainsKey(silkNetKey))
                        {
                            petsciiCode = specialKeyMap[silkNetKey];
                            System.Diagnostics.Debug.WriteLine($"SilkNet special key pressed: {silkNetKey} with modifier: {modifierKeyDown} and mapped to Petscii: {petsciiCode}");
                            return true;
                        }
                    }

                }
            }

            // Normal PC/Mac characters received.
            if (_inputHandlerContext.KeysReceived.Count > 0)
            {
                // Get ASCII character
                var silkNetCharacter = _inputHandlerContext.KeysReceived.First();
                _inputHandlerContext.KeysReceived.Remove(silkNetCharacter);
                if (!Petscii.CharToPetscii.ContainsKey(silkNetCharacter))
                {
                    System.Diagnostics.Debug.WriteLine($"SilkNet character pressed but not mapped: {silkNetCharacter}");
                }
                else
                {
                    // Map to PetscII
                    petsciiCode = Petscii.CharToPetscii[silkNetCharacter];
                    System.Diagnostics.Debug.WriteLine($"SilkNet normal character pressed {silkNetCharacter} and mapped to Petscii: {petsciiCode}");
                    return true;
                }
            }


            return false;
        }
    }
}
