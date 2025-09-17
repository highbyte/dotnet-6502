using Highbyte.DotNet6502.Systems.Generic.Config;
using Highbyte.DotNet6502.Systems.Generic.Video;
using Highbyte.DotNet6502.Systems.Rendering;
using Highbyte.DotNet6502.Systems.Rendering.VideoCommands;
using Highbyte.DotNet6502.Systems.Utils;

namespace Highbyte.DotNet6502.Systems.Generic.Render;

[DisplayName("Video Commands")]
[HelpText("Generates a stream of video commands for each frame,\nbased on the GenericComputer screen memory and color memory.")]
public class GenericVideoCommandStream : IRenderProvider, IVideoCommandStream
{
    public string Name => "GenericComputerCommandStream";

    private readonly GenericComputer _genericComputer;
    private readonly EmulatorScreenConfig _emulatorScreenConfig;

    private readonly Queue<IVideoCommand> _commands = new();

    public event EventHandler? FrameCompleted;

    public GenericVideoCommandStream(GenericComputer genericComputer)
    {
        _genericComputer = genericComputer;
        _emulatorScreenConfig = genericComputer.GenericComputerConfig.Memory.Screen;
    }

    public void OnAfterInstruction()
    {
    }

    public void OnEndFrame()
    {
        GenerateCommands();
        FrameCompleted?.Invoke(this, EventArgs.Empty);
    }

    public IEnumerable<IVideoCommand> DequeueAll()
    {
        while (_commands.Count > 0)
            yield return _commands.Dequeue();
    }

    // Only called from legacy render pipeline
    public void GenerateCommands()
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
                    bgColor,
                    adjustPosForBorder: true);
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
    /// Draw character to screen, with adjusted position for border.
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <param name="emulatorCharacter"></param>
    /// <param name="emulatorFgColor"></param>
    /// <param name="emulatorBgColor"></param>
    private void DrawEmulatorCharacterOnScreen(int x, int y, byte emulatorCharacter, byte emulatorFgColor, byte emulatorBgColor, bool adjustPosForBorder = true)
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

        byte drawCharacter;
        if (_emulatorScreenConfig.UseAscIICharacters)
        {
            drawCharacter = emulatorCharacter;
        }
        else
        {
            var dictKey = emulatorCharacter.ToString();
            if (_emulatorScreenConfig.CharacterMap.ContainsKey(dictKey))
                drawCharacter = _emulatorScreenConfig.CharacterMap[dictKey];
            else
                drawCharacter = emulatorCharacter;
        }

        var drawGlyphCommand = GenerateDrawGlyphCommand(
            x,
            y,
            drawCharacter,
            emulatorFgColor,
            emulatorBgColor
            );

        _commands.Enqueue(drawGlyphCommand);
    }

    private DrawGlyph GenerateDrawGlyphCommand(int x, int y, int character, byte fgColor, byte bgColor)
    {
        return new DrawGlyph(x, y, character, ColorMaps.GenericColorMap[fgColor], ColorMaps.GenericColorMap[bgColor]);
    }
}
