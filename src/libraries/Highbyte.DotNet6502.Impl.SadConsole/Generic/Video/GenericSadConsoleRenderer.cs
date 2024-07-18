using Highbyte.DotNet6502.Instrumentation;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Generic;
using Highbyte.DotNet6502.Systems.Generic.Config;

namespace Highbyte.DotNet6502.Impl.SadConsole.Generic.Video;

public class GenericSadConsoleRenderer : IRenderer
{
    private readonly GenericComputer _genericComputer;
    public ISystem System => _genericComputer;
    private readonly SadConsoleRenderContext _sadConsoleRenderContext;
    private readonly EmulatorScreenConfig _emulatorScreenConfig;

    public Instrumentations Instrumentations { get; } = new();


    public GenericSadConsoleRenderer(GenericComputer genericComputer, SadConsoleRenderContext sadConsoleRenderContext, EmulatorScreenConfig emulatorScreenConfig)
    {
        _genericComputer = genericComputer;
        _sadConsoleRenderContext = sadConsoleRenderContext;
        _emulatorScreenConfig = emulatorScreenConfig;

        Init();
    }

    public void Init()
    {

        InitEmulatorScreenMemory(_genericComputer);
    }

    public void Cleanup()
    {
    }

    public void DrawFrame()
    {
        RenderMainScreen(_genericComputer);
        if (_emulatorScreenConfig.BorderCols > 0 || _emulatorScreenConfig.BorderRows > 0)
            RenderBorder(_genericComputer);
    }


    private void RenderMainScreen(GenericComputer system)
    {
        var emulatorMem = system.Mem;
        // // Top Left
        // DrawEmulatorCharacterOnScreen(0, 0, 65, 0x01, 0x05);
        // // Bottom Right
        // DrawEmulatorCharacterOnScreen(_emulatorMemoryConfig.Cols-1, _emulatorMemoryConfig.Rows-1 , 66, 0x01, 0x05);
        // return;

        // TODO: Have common bg color like C64 or allow separate bg color per character in another memory range?
        var bgColor = emulatorMem[_emulatorScreenConfig.ScreenBackgroundColorAddress];

        // Build screen data characters based on emulator memory contents (byte)
        var currentScreenAddress = _emulatorScreenConfig.ScreenStartAddress;
        var currentColorAddress = _emulatorScreenConfig.ScreenColorStartAddress;
        for (var row = 0; row < _emulatorScreenConfig.Rows; row++)
        {
            for (var col = 0; col < _emulatorScreenConfig.Cols; col++)
            {
                var charByte = emulatorMem[currentScreenAddress++]; ;
                var colorByte = emulatorMem[currentColorAddress++]; ;
                DrawEmulatorCharacterOnScreen(
                    col,
                    row,
                    charByte,
                    colorByte,
                    bgColor);
            }
        }
    }

    private void RenderBorder(GenericComputer system)
    {
        var emulatorMem = system.Mem;

        byte borderCharacter = 0;    // 0 = no character
        var borderBgColor = emulatorMem[_emulatorScreenConfig.ScreenBorderColorAddress];
        var borderFgColor = borderBgColor;

        for (var row = 0; row < _emulatorScreenConfig.Rows + _emulatorScreenConfig.BorderRows * 2; row++)
        {
            for (var col = 0; col < _emulatorScreenConfig.Cols + _emulatorScreenConfig.BorderCols * 2; col++)
            {
                if (row < _emulatorScreenConfig.BorderRows || row >= _emulatorScreenConfig.Rows + _emulatorScreenConfig.BorderRows
                    || col < _emulatorScreenConfig.BorderCols || col >= _emulatorScreenConfig.Cols + _emulatorScreenConfig.BorderCols)
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
    private void InitEmulatorScreenMemory(GenericComputer system)
    {
        var emulatorMem = system.Mem;

        // Common bg and border color for entire screen, controlled by specific address
        emulatorMem[_emulatorScreenConfig.ScreenBorderColorAddress] = _emulatorScreenConfig.DefaultBorderColor;
        emulatorMem[_emulatorScreenConfig.ScreenBackgroundColorAddress] = _emulatorScreenConfig.DefaultBgColor;

        var currentScreenAddress = _emulatorScreenConfig.ScreenStartAddress;
        var currentColorAddress = _emulatorScreenConfig.ScreenColorStartAddress;
        for (var row = 0; row < _emulatorScreenConfig.Rows; row++)
        {
            for (var col = 0; col < _emulatorScreenConfig.Cols; col++)
            {
                emulatorMem[currentScreenAddress++] = 0x20;    // 32 (0x20) = space
                emulatorMem[currentColorAddress++] = _emulatorScreenConfig.DefaultFgColor;
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
        if (adjustPosForBorder)
        {
            x += _emulatorScreenConfig.BorderCols;
            y += _emulatorScreenConfig.BorderRows;
        }

        if (!_emulatorScreenConfig.ColorMap.ContainsKey(emulatorFgColor))
            throw new DotNet6502Exception($"Color value (foreground) {emulatorFgColor} is not mapped.");
        if (!_emulatorScreenConfig.ColorMap.ContainsKey(emulatorBgColor))
            throw new DotNet6502Exception($"Color value (background) {emulatorBgColor} is not mapped.");

        byte sadConsoleCharacter;
        if (_emulatorScreenConfig.UseAscIICharacters)
        {
            sadConsoleCharacter = emulatorCharacter;
        }
        else
        {
            var dictKey = emulatorCharacter.ToString();
            if (_emulatorScreenConfig.CharacterMap.ContainsKey(dictKey))
                sadConsoleCharacter = _emulatorScreenConfig.CharacterMap[dictKey];
            else
                sadConsoleCharacter = emulatorCharacter;
        }

        _sadConsoleRenderContext.Screen.DrawCharacter(
            x,
            y,
            sadConsoleCharacter,
            GenericSadConsoleColors.SystemToSadConsoleColorMap[_emulatorScreenConfig.ColorMap[emulatorFgColor]],
            GenericSadConsoleColors.SystemToSadConsoleColorMap[_emulatorScreenConfig.ColorMap[emulatorBgColor]]
            );
    }
}
