using System;
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

        /// <summary>
        /// Render contents om emulator screen memory to SadConsole screen
        /// </summary>
        public void RenderEmulatorScreenMemory()
        {
            RenderMainScreen();
            if(_emulatorScreenConfig.BorderCols > 0 || _emulatorScreenConfig.BorderRows > 0 )
                RenderBorder();
        }

        private void RenderMainScreen()
        {
            // // Top Left
            // DrawEmulatorCharacterOnScreen(0, 0, 65, 0x01, 0x05);
            // // Bottom Right
            // DrawEmulatorCharacterOnScreen(_emulatorMemoryConfig.Cols-1, _emulatorMemoryConfig.Rows-1 , 66, 0x01, 0x05);
            // return;

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
                    DrawEmulatorCharacterOnScreen(
                        col, 
                        row,
                        charByte, 
                        colorByte, 
                        bgColor);
                }
            }
        }

        private void RenderBorder()
        {
            byte borderCharacter = 0;    // 0 = no character
            byte borderBgColor = _emulatorMem[_emulatorScreenConfig.ScreenBorderColorAddress];
            byte borderFgColor = borderBgColor;

            for (int row = 0; row < (_emulatorScreenConfig.Rows + (_emulatorScreenConfig.BorderRows*2)); row++)
            {
                for (int col = 0; col < (_emulatorScreenConfig.Cols + (_emulatorScreenConfig.BorderCols*2)); col++)
                {
                    if(row < _emulatorScreenConfig.BorderRows || row >= (_emulatorScreenConfig.Rows + _emulatorScreenConfig.BorderRows)
                        || col < _emulatorScreenConfig.BorderCols || col >= (_emulatorScreenConfig.Cols + _emulatorScreenConfig.BorderCols))
                    {
                        DrawEmulatorCharacterOnScreen(
                            col, 
                            row,
                            borderCharacter, 
                            borderFgColor,
                            borderBgColor,
                            adjustPosForBorder: false
                            );
                    }
                }
            }
        }

        /// <summary>
        /// Set emulator screen memory initial state
        /// </summary>
        public void InitEmulatorScreenMemory()
        {
            // Common bg and border color for entire screen, controlled by specific address
            _emulatorMem[_emulatorScreenConfig.ScreenBorderColorAddress]     = _emulatorScreenConfig.DefaultBorderColor;
            _emulatorMem[_emulatorScreenConfig.ScreenBackgroundColorAddress] = _emulatorScreenConfig.DefaultBgColor;

            ushort currentScreenAddress = _emulatorScreenConfig.ScreenStartAddress;
            ushort currentColorAddress  = _emulatorScreenConfig.ScreenColorStartAddress;
            for (int row = 0; row < _emulatorScreenConfig.Rows; row++)
            {
                for (int col = 0; col < _emulatorScreenConfig.Cols; col++)
                {
                    _emulatorMem[currentScreenAddress++] = 0x20;    // 32 (0x20) = space
                    _emulatorMem[currentColorAddress++] = _emulatorScreenConfig.DefaultFgColor;
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
        public void DrawEmulatorCharacterOnScreen(int x, int y, byte emulatorCharacter, byte emulatorFgColor, byte emulatorBgColor, bool adjustPosForBorder = true)
        {
            if(adjustPosForBorder)
            {
                x += _emulatorScreenConfig.BorderCols;
                y += _emulatorScreenConfig.BorderRows;

            }

            if(!_emulatorScreenConfig.ColorMap.ContainsKey(emulatorFgColor))
                throw new Exception($"Color value (foreground) {emulatorFgColor} is not mapped.");
            if(!_emulatorScreenConfig.ColorMap.ContainsKey(emulatorBgColor))
                throw new Exception($"Color value (background) {emulatorBgColor} is not mapped.");
            
            byte sadConsoleCharacter;
            if(_emulatorScreenConfig.UseAscIICharacters)
                sadConsoleCharacter = emulatorCharacter;
            else
            {
                if(!_emulatorScreenConfig.CharacterMap.ContainsKey(emulatorCharacter))
                    throw new Exception($"Character value {emulatorCharacter} is not mapped.");

                sadConsoleCharacter = _emulatorScreenConfig.CharacterMap[emulatorCharacter];
            }
                
            _getSadConsoleScreen().DrawCharacter(
                x, 
                y,
                emulatorCharacter,  // Assume character set used by 6502 program is ASCII
                _emulatorScreenConfig.ColorMap[emulatorFgColor], 
                _emulatorScreenConfig.ColorMap[emulatorBgColor]
                );

            
        }
    }
}
