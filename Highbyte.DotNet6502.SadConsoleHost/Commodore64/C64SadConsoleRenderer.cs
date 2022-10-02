using System;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Config;

namespace Highbyte.DotNet6502.SadConsoleHost.Commodore64
{
    public class C64SadConsoleRenderer : IRenderer<C64>, IRenderer
    {
        public int Width => 320;
        public int Height => 200;

        private readonly Func<SadConsoleScreenObject> _getSadConsoleScreen;

        public C64SadConsoleRenderer(
            Func<SadConsoleScreenObject> getSadConsoleScreen)
        {
            _getSadConsoleScreen = getSadConsoleScreen;
        }

        public void Draw(C64 c64)
        {
            RenderMainScreen(c64);
            RenderBorder(c64);
        }

        public void Draw(ISystem system)
        {
            Draw((C64)system);
        }

        private void RenderMainScreen(C64 c64)
        {
            var emulatorMem = c64.Mem;
            // // Top Left
            // DrawEmulatorCharacterOnScreen(0, 0, 65, 0x01, 0x05);
            // // Bottom Right
            // DrawEmulatorCharacterOnScreen(_emulatorMemoryConfig.Cols-1, _emulatorMemoryConfig.Rows-1 , 66, 0x01, 0x05);
            // return;

            byte bgColor = emulatorMem[Vic2Addr.BACKGROUND_COLOR];

            // Build screen data characters based on emulator memory contents (byte)
            ushort currentScreenAddress = Vic2Addr.SCREEN_RAM_START;
            ushort currentColorAddress = Vic2Addr.COLOR_RAM_START;
            for (int row = 0; row < Vic2.ROWS; row++)
            {
                for (int col = 0; col < Vic2.COLS; col++)
                {
                    byte charByte = emulatorMem[currentScreenAddress++];
                    byte colorByte = emulatorMem[currentColorAddress++];
                    DrawEmulatorCharacterOnScreen(
                        col,
                        row,
                        charByte,
                        colorByte,
                        bgColor,
                        c64,
                        adjustForBorder: true
                        );
                }
            }
        }

        private void RenderBorder(C64 c64)
        {
            var emulatorMem = c64.Mem;

            byte borderCharacter = 0;    // 0 = no character
            byte borderBgColor = emulatorMem[Vic2Addr.BORDER_COLOR];
            byte borderFgColor = borderBgColor;

            int border_cols = c64.BorderCols;
            int border_rows = c64.BorderRows;

            for (int row = 0; row < (Vic2.ROWS + (border_rows * 2)); row++)
            {
                for (int col = 0; col < (Vic2.COLS + (border_cols * 2)); col++)
                {
                    if (row < border_rows || row >= (Vic2.ROWS + border_rows)
                        || col < border_cols || col >= (Vic2.COLS + border_cols))
                    {
                        DrawEmulatorCharacterOnScreen(
                            col,
                            row,
                            borderCharacter,
                            borderFgColor,
                            borderBgColor,
                            c64,
                            adjustForBorder: false
                            );
                    }
                }
            }
        }

        /// <summary>
        /// Draw character to screen, with adjusted position for border.
        /// Colors are translated from emulator to SadConsole.
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="emulatorCharacter"></param>
        /// <param name="emulatorFgColor"></param>
        /// <param name="emulatorBgColor"></param>
        public void DrawEmulatorCharacterOnScreen(int x, int y, byte emulatorCharacter, byte emulatorFgColor, byte emulatorBgColor, C64 c64, bool adjustForBorder)
        {
            if (adjustForBorder)
            {
                x += c64.BorderCols;
                y += c64.BorderRows;
            }

            byte sadConsoleCharacter;
            // Default to C64 screen codes as source
            sadConsoleCharacter = TranslateC64ScreenCodeToSadConsoleC64Font(emulatorCharacter);

            _getSadConsoleScreen().DrawCharacter(
                x,
                y,
                sadConsoleCharacter,
                ColorMaps.C64ColorMap[emulatorFgColor],
                ColorMaps.C64ColorMap[emulatorBgColor]
                );
        }

        private byte TranslateC64ScreenCodeToSadConsoleC64Font(byte sourceByte)
        {
            switch (sourceByte & 0xff)
            {
                case 0xa0:  //160, C64 inverted space
                    return 219; // Inverted square in SadConsole C64 font
                case 0xe0:  //224, Also C64 inverted space?
                    return 219; // Inverted square in SadConsole C64 font
                default:

                    // Convert C64 screen code to PETSCII
                    var sadConsoleCharacter = CharacterMaps.C64ScreenCodeToPETSCII(sourceByte);
                    // TODO: Also convert to ASCII?  Would depend on the font being used?
                    //sadConsoleCharacter = CharacterMaps.PETSCIICodeToASCII(sadConsoleCharacter);

                    return sadConsoleCharacter;
            }
        }
    }
}
