using System.Drawing;
using Highbyte.DotNet6502.Systems.Rendering;
using Highbyte.DotNet6502.Systems.Rendering.VideoCommands;
using Highbyte.DotNet6502.Systems.Vic20.Config;
using Highbyte.DotNet6502.Systems.Vic20.Video;

namespace Highbyte.DotNet6502.Systems.Vic20.Render;

/// <summary>
/// Generates a stream of video commands each frame from VIC-20 screen and color RAM.
/// Implements the command-stream render path (DrawGlyph commands consumed by AvaloniaCommandTarget).
/// No rasterizer needed for the 22×23 text mode the VIC-20 uses by default.
/// </summary>
public class Vic20VideoCommandStream : IRenderProvider, IVideoCommandStream
{
    public string Name => "Vic20CommandStream";

    private readonly Vic20 _vic20;
    private readonly Vic20Config _config;

    private readonly Queue<IVideoCommand> _commands = new();

    public event EventHandler? FrameCompleted;

    public Vic20VideoCommandStream(Vic20 vic20)
    {
        _vic20 = vic20;
        _config = vic20.Vic20Config;
    }

    public void OnAfterInstruction() { }

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

    private void GenerateCommands()
    {
        RenderBorder();
        RenderMainScreen();
    }

    private void RenderMainScreen()
    {
        var mem = _vic20.Mem;
        // VIC-I $900F: background color is in bits 7-4 (high nibble, 16 colors)
        var bgColorIndex = (byte)((mem[_config.BackgroundColorAddress] >> 4) & 0x0F);

        var screenAddr = _config.ScreenStartAddress;
        var colorAddr = _config.ColorStartAddress;

        for (var row = 0; row < Vic20Config.Rows; row++)
        {
            for (var col = 0; col < Vic20Config.Cols; col++)
            {
                var charByte = mem[screenAddr++];
                // Color RAM stores 3-bit foreground color in bits 2-0
                var fgColorIndex = (byte)(mem[colorAddr++] & 0x07);

                _commands.Enqueue(MakeDrawGlyph(
                    col + Vic20Config.BorderCols,
                    row + Vic20Config.BorderRows,
                    charByte,
                    fgColorIndex,
                    bgColorIndex));
            }
        }
    }

    private void RenderBorder()
    {
        var mem = _vic20.Mem;
        // VIC-I $900F: border color is in bits 2-0 (low 3 bits, 8 colors)
        var borderColorByte = (byte)(mem[_config.BorderColorAddress] & 0x07);

        var totalCols = Vic20Config.Cols + Vic20Config.BorderCols * 2;
        var totalRows = Vic20Config.Rows + Vic20Config.BorderRows * 2;

        for (var row = 0; row < totalRows; row++)
        {
            for (var col = 0; col < totalCols; col++)
            {
                var isBorder = row < Vic20Config.BorderRows
                    || row >= Vic20Config.Rows + Vic20Config.BorderRows
                    || col < Vic20Config.BorderCols
                    || col >= Vic20Config.Cols + Vic20Config.BorderCols;

                if (isBorder)
                    _commands.Enqueue(MakeDrawGlyph(col, row, 0, borderColorByte, borderColorByte));
            }
        }
    }

    private DrawGlyph MakeDrawGlyph(int x, int y, byte character, byte fgColorByte, byte bgColorByte)
    {
        if (!ColorMaps.Vic20ColorMap.TryGetValue(fgColorByte, out var fgColor))
            fgColor = Color.White;
        if (!ColorMaps.Vic20ColorMap.TryGetValue(bgColorByte, out var bgColor))
            bgColor = Color.Black;

        return new DrawGlyph(x, y, character, fgColor, bgColor);
    }
}
