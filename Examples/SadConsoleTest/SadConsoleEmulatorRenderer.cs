using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using SadConsole;
using SadConsole.Effects;
using Highbyte.DotNet6502;

namespace SadConsoleTest
{
    public class SadConsoleEmulatorRenderer
    {
        private readonly System.Func<SadConsoleScreen> _getSadConsoleScreen;
        private readonly Memory _emulatorMem;
        private readonly EmulatorScreenConfig _emulatorScreenConfig;


        public SadConsoleEmulatorRenderer(
            System.Func<SadConsoleScreen> getSadConsoleScreen,
            Memory emulatorMem,
            EmulatorScreenConfig emulatorScreenConfig)
        {
            _getSadConsoleScreen = getSadConsoleScreen;
            _emulatorMem = emulatorMem;
            _emulatorScreenConfig = emulatorScreenConfig;            
        }

        public void RenderEmulatorScreenMemory()
        {
            // TODO: Have commong bg color like C64 or allow separate bg color per character in another memory range?
            byte bgColor = _emulatorMem[_emulatorScreenConfig.ScreenBackgroundColorAddress];

            // Build screen data characters based on emulator memory contents (byte)
            ushort currentScreenAddress = _emulatorScreenConfig.ScreenStartAddress;
            ushort currentColorAddress = _emulatorScreenConfig.ScreenColorStartAddress;
            for (int row = 0; row < _emulatorScreenConfig.Rows; row++)
            {
                for (int col = 0; col < _emulatorScreenConfig.Cols; col++)
                {
                    byte charByte = _emulatorMem[currentScreenAddress++];;
                    byte colorByte = _emulatorMem[currentColorAddress++];;
                    DrawEmulatorCharacterOnScreen(col, row, charByte, colorByte, bgColor);
                }
            }
        }

        public void DrawEmulatorCharacterOnScreen(int x, int y, byte emulatorCharacter, byte emulatorFgColor, byte emulatorBgColor)
        {
            _getSadConsoleScreen().DrawCharacter(x, y,
                SadConsoleEmulatorRendererHelper.GetSadConsoleCharCode(emulatorCharacter), 
                SadConsoleEmulatorRendererHelper.ColorToXNAColor(emulatorFgColor), 
                SadConsoleEmulatorRendererHelper.ColorToXNAColor(emulatorBgColor)
                );
        }
    }
}
