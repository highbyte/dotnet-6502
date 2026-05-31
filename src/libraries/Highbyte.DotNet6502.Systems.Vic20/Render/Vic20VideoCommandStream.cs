using System.Drawing;
using Highbyte.DotNet6502.Systems.Rendering;
using Highbyte.DotNet6502.Systems.Rendering.VideoCommands;
using Highbyte.DotNet6502.Systems.Vic20.Config;
using Highbyte.DotNet6502.Systems.Vic20.Video;
using Vic20ScreenCode = Highbyte.DotNet6502.Systems.Vic20.Video.Vic20ScreenCode;

namespace Highbyte.DotNet6502.Systems.Vic20.Render;

/// <summary>
/// Lightweight glyph-based VIC-20 render path. This intentionally emits character cells rather
/// than exact pixels so hosts can get a simple text-like view without a rasterizer.
/// </summary>
public class Vic20VideoCommandStream : IRenderProvider, IVideoCommandStream
{
    public string Name => "Vic20CommandStream";

    private readonly Vic20 _vic20;
    private readonly Queue<IVideoCommand> _commands = new();

    public event EventHandler? FrameCompleted;

    public Vic20VideoCommandStream(Vic20 vic20)
    {
        _vic20 = vic20;
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
        _commands.Enqueue(new SetConfig(GlyphToUnicodeConverter: Vic20ScreenCode.ScreenCodeToUnicode));
        RenderBorder();
        RenderMainScreen();
    }

    private void RenderMainScreen()
    {
        var mem = _vic20.Mem;
        var layout = _vic20.CurrentVideoLayout;
        var bgColorIndex = layout.BackgroundColor;

        var cols = Math.Min(layout.Columns, Vic20Config.Cols);
        var rows = Math.Min(layout.Rows, Vic20Config.Rows);
        const int borderCols = Vic20Config.BorderCols;
        const int borderRows = Vic20Config.BorderRows;

        for (var row = 0; row < rows; row++)
        {
            for (var col = 0; col < cols; col++)
            {
                var screenAddress = (ushort)(layout.ScreenStartAddress + (row * layout.Columns) + col);
                var colorAddress = (ushort)(layout.ColorStartAddress + (row * layout.Columns) + col);
                var charByte = mem[screenAddress];
                var fgColorIndex = (byte)(mem[colorAddress] & 0x07);

                _commands.Enqueue(MakeDrawGlyph(
                    col + borderCols,
                    row + borderRows,
                    charByte,
                    fgColorIndex,
                    bgColorIndex));
            }
        }
    }

    private void RenderBorder()
    {
        var layout = _vic20.CurrentVideoLayout;
        var cols = Math.Min(layout.Columns, Vic20Config.Cols);
        var rows = Math.Min(layout.Rows, Vic20Config.Rows);
        const int borderCols = Vic20Config.BorderCols;
        const int borderRows = Vic20Config.BorderRows;
        var borderColorByte = layout.BorderColor;

        var totalCols = cols + borderCols * 2;
        var totalRows = rows + borderRows * 2;

        for (var row = 0; row < totalRows; row++)
        {
            for (var col = 0; col < totalCols; col++)
            {
                var isBorder = row < borderRows
                    || row >= rows + borderRows
                    || col < borderCols
                    || col >= cols + borderCols;

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
