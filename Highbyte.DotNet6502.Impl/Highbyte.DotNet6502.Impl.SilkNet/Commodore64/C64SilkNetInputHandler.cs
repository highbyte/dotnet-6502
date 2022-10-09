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
            HandleNonPrintedC64Keys(c64Keyboard);
            HandlePrintedC64Keys(c64Keyboard);
        }

        private void HandleNonPrintedC64Keys(
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

        private void HandlePrintedC64Keys(
            Systems.Commodore64.Keyboard c64Keyboard)
        {
            // Check if modifier key is down.
            var modifierKeyDown = Key.Unknown;
            foreach (var modifierKey in C64SilkNetKeyboard.AllModifierKeys)
            {
                var modifierKeyPressed = _inputHandlerContext.IsKeyPressed(modifierKey);
                if (modifierKeyPressed)
                {
                    modifierKeyDown = modifierKey;
                    break;
                }
            }

            if (C64SilkNetKeyboard.SpecialKeyMaps.ContainsKey(modifierKeyDown))
            {
                var specialKeyMap = C64SilkNetKeyboard.SpecialKeyMaps[modifierKeyDown];

                foreach (var key in specialKeyMap.Keys)
                {
                    if (_inputHandlerContext.KeysDown.Contains(key))
                    {
                        var petsciiCode = specialKeyMap[key];
                        Debug.WriteLine($"SilkNet special key pressed: {key} with modifier: {modifierKeyDown} and mapped to Petscii: {petsciiCode}");
                        c64Keyboard.KeyPressed(petsciiCode);

                        // If we detected a special Key/Combo pressed, don't process anymore. Some of them may also be in the _inputHandlerContext.CharactersReceived list processed below.
                        return;
                    }
                }
            }

            // Check if nothing to do with captured characters.
            if (_inputHandlerContext.CharactersReceived.Count == 0)
                return;

            foreach (var character in _inputHandlerContext.CharactersReceived)
            {
                if (!Petscii.CharToPetscii.ContainsKey(character))
                {
                    Debug.WriteLine($"SilkNet normal character pressed {character} couldn't be found as a Petscii code.");
                    continue;
                }
                var petsciiCode = Petscii.CharToPetscii[character];
                Debug.WriteLine($"SilkNet normal character pressed {character} and mapped to Petscii: {petsciiCode}");
                c64Keyboard.KeyPressed(petsciiCode);
            }
        }
    }
}
