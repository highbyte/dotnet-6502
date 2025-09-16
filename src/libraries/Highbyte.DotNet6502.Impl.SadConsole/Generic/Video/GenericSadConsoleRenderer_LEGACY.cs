//using Highbyte.DotNet6502.Systems;
//using Highbyte.DotNet6502.Systems.Generic;
//using Highbyte.DotNet6502.Systems.Generic.Config;
//using Highbyte.DotNet6502.Systems.Instrumentation;

//namespace Highbyte.DotNet6502.Impl.SadConsole.Generic.Video;


//// TODO: Is this completely replace in new render pipeline with a VideoCommand render target + custom adjustment?
//public class GenericSadConsoleRenderer_LEGACY
//{
//    private readonly GenericComputer _genericComputer;
//    public ISystem System => _genericComputer;
//    private readonly SadConsoleRenderContext _sadConsoleRenderContext;
//    private readonly EmulatorScreenConfig _emulatorScreenConfig;

//    public Instrumentations Instrumentations { get; } = new();


//    public GenericSadConsoleRenderer_LEGACY(GenericComputer genericComputer, SadConsoleRenderContext sadConsoleRenderContext)
//    {
//        _genericComputer = genericComputer;
//        _sadConsoleRenderContext = sadConsoleRenderContext;
//        _emulatorScreenConfig = genericComputer.GenericComputerConfig.Memory.Screen;
//    }

//    public void Init()
//    {
//    }

//    public void Cleanup()
//    {
//    }

//    public void GenerateFrame()
//    {
//        // All drawing is done in DrawFrame with commands against SadConsole screen, so nothing to do here.
//    }

//    public void DrawFrame()
//    {
//        RenderMainScreen(_genericComputer);
//        if (_emulatorScreenConfig.BorderCols > 0 || _emulatorScreenConfig.BorderRows > 0)
//            RenderBorder(_genericComputer);
//    }


//    private void RenderMainScreen(GenericComputer system)
//    {
//        var emulatorMem = system.Mem;
//        // // Top Left
//        // DrawEmulatorCharacterOnScreen(0, 0, 65, 0x01, 0x05);
//        // // Bottom Right
//        // DrawEmulatorCharacterOnScreen(_emulatorMemoryConfig.Cols-1, _emulatorMemoryConfig.Rows-1 , 66, 0x01, 0x05);
//        // return;

//        // TODO: Have common bg color like C64 or allow separate bg color per character in another memory range?
//        var bgColor = emulatorMem[_emulatorScreenConfig.ScreenBackgroundColorAddress];

//        // Build screen data characters based on emulator memory contents (byte)
//        var currentScreenAddress = _emulatorScreenConfig.ScreenStartAddress;
//        var currentColorAddress = _emulatorScreenConfig.ScreenColorStartAddress;
//        for (var row = 0; row < _emulatorScreenConfig.Rows; row++)
//        {
//            for (var col = 0; col < _emulatorScreenConfig.Cols; col++)
//            {
//                var charByte = emulatorMem[currentScreenAddress++]; ;
//                var colorByte = emulatorMem[currentColorAddress++]; ;
//                DrawEmulatorCharacterOnScreen(
//                    col,
//                    row,
//                    charByte,
//                    colorByte,
//                    bgColor);
//            }
//        }
//    }

//    private void RenderBorder(GenericComputer system)
//    {
//        var emulatorMem = system.Mem;

//        byte borderCharacter = 0;    // 0 = no character
//        var borderBgColor = emulatorMem[_emulatorScreenConfig.ScreenBorderColorAddress];
//        var borderFgColor = borderBgColor;

//        for (var row = 0; row < _emulatorScreenConfig.Rows + _emulatorScreenConfig.BorderRows * 2; row++)
//        {
//            for (var col = 0; col < _emulatorScreenConfig.Cols + _emulatorScreenConfig.BorderCols * 2; col++)
//            {
//                if (row < _emulatorScreenConfig.BorderRows || row >= _emulatorScreenConfig.Rows + _emulatorScreenConfig.BorderRows
//                    || col < _emulatorScreenConfig.BorderCols || col >= _emulatorScreenConfig.Cols + _emulatorScreenConfig.BorderCols)
//                {
//                    DrawEmulatorCharacterOnScreen(
//                        col,
//                        row,
//                        borderCharacter,
//                        borderFgColor,
//                        borderBgColor,
//                        adjustPosForBorder: false
//                        );
//                }
//            }
//        }
//    }

//    /// <summary>
//    /// Draw character to screen, with adjusted position for border.
//    /// Colors are translated from emulator to SadConsole.
//    /// </summary>
//    /// <param name="x"></param>
//    /// <param name="y"></param>
//    /// <param name="emulatorCharacter"></param>
//    /// <param name="emulatorFgColor"></param>
//    /// <param name="emulatorBgColor"></param>
//    public void DrawEmulatorCharacterOnScreen(int x, int y, byte emulatorCharacter, byte emulatorFgColor, byte emulatorBgColor, bool adjustPosForBorder = true)
//    {
//        if (adjustPosForBorder)
//        {
//            x += _emulatorScreenConfig.BorderCols;
//            y += _emulatorScreenConfig.BorderRows;
//        }

//        if (!_emulatorScreenConfig.ColorMap.ContainsKey(emulatorFgColor))
//            throw new DotNet6502Exception($"Color value (foreground) {emulatorFgColor} is not mapped.");
//        if (!_emulatorScreenConfig.ColorMap.ContainsKey(emulatorBgColor))
//            throw new DotNet6502Exception($"Color value (background) {emulatorBgColor} is not mapped.");

//        byte sadConsoleCharacter;
//        if (_emulatorScreenConfig.UseAscIICharacters)
//        {
//            sadConsoleCharacter = emulatorCharacter;
//        }
//        else
//        {
//            var dictKey = emulatorCharacter.ToString();
//            if (_emulatorScreenConfig.CharacterMap.ContainsKey(dictKey))
//                sadConsoleCharacter = _emulatorScreenConfig.CharacterMap[dictKey];
//            else
//                sadConsoleCharacter = emulatorCharacter;
//        }

//        DrawCharacter(
//            x,
//            y,
//            sadConsoleCharacter,
//            GenericSadConsoleColors.SystemToSadConsoleColorMap[_emulatorScreenConfig.ColorMap[emulatorFgColor]],
//            GenericSadConsoleColors.SystemToSadConsoleColorMap[_emulatorScreenConfig.ColorMap[emulatorBgColor]]
//            );
//    }

//    private void DrawCharacter(int x, int y, int sadConsoleCharCode, Color fgColor, Color bgColor)
//    {
//        _sadConsoleRenderContext.Console.SetGlyph(x, y, sadConsoleCharCode, fgColor, bgColor);
//    }
//}
