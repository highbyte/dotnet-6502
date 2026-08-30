using System.Drawing;
using Highbyte.DotNet6502.Systems.Rendering;
using Highbyte.DotNet6502.Systems.Rendering.VideoCommands;
using Highbyte.DotNet6502.Systems.Utils;
using OricMachine = Highbyte.DotNet6502.Systems.Oric.Oric;

namespace Highbyte.DotNet6502.Systems.Oric.Render;

/// <summary>
/// Glyph-based Oric text renderer for hosts that cannot display the pixel rasterizer. It emits the
/// 40x28 text screen while preserving serial ink/paper attributes, inverse video, and flashing text.
/// Hi-res pixels and RAM-defined character shapes cannot be represented by glyph commands.
/// </summary>
[DisplayName("Text Commands")]
[HelpText("Emits the Oric 40x28 text screen as DrawGlyph video commands.")]
public sealed class OricVideoCommandStream : IRenderProvider, IVideoCommandStream
{
    private static readonly Color[] s_palette =
    [
        Color.FromArgb(0x00, 0x00, 0x00),
        Color.FromArgb(0xff, 0x00, 0x00),
        Color.FromArgb(0x00, 0xff, 0x00),
        Color.FromArgb(0xff, 0xff, 0x00),
        Color.FromArgb(0x00, 0x00, 0xff),
        Color.FromArgb(0xff, 0x00, 0xff),
        Color.FromArgb(0x00, 0xff, 0xff),
        Color.FromArgb(0xff, 0xff, 0xff),
    ];

    private readonly OricMachine _oric;
    private readonly Queue<IVideoCommand> _commands = new();
    private readonly object _commandsLock = new();
    private int _frameCounter;

    public OricVideoCommandStream(OricMachine oric) => _oric = oric;

    public string Name => nameof(OricVideoCommandStream);

    public event EventHandler? FrameCompleted;

    public void OnAfterInstruction() { }

    public void OnEndFrame()
    {
        lock (_commandsLock)
        {
            _frameCounter++;
            GenerateCommands();
        }
        FrameCompleted?.Invoke(this, EventArgs.Empty);
    }

    public IEnumerable<IVideoCommand> DequeueAll()
    {
        lock (_commandsLock)
        {
            var commands = _commands.ToArray();
            _commands.Clear();
            return commands;
        }
    }

    public void Reset()
    {
        lock (_commandsLock)
        {
            _commands.Clear();
            _frameCounter = 0;
        }
    }

    internal int SnapshotFrameCounter => _frameCounter;

    internal void RestoreSnapshotState(int frameCounter)
    {
        lock (_commandsLock)
        {
            _frameCounter = frameCounter;
            _commands.Clear();
            GenerateCommands();
        }
    }

    public static string ScreenCodeToUnicode(byte screenCode)
    {
        var character = (byte)(screenCode & 0x7f);
        return character switch
        {
            0x60 => "©",
            >= 0x20 and <= 0x7e => ((char)character).ToString(),
            0x7f => "█",
            _ => " ",
        };
    }

    private void GenerateCommands()
    {
        _commands.Enqueue(new SetConfig(ScreenCodeToUnicode));

        var flashBlanked = ((_frameCounter / 25) & 1) != 0;
        for (var row = 0; row < _oric.TextRows; row++)
        {
            byte ink = 7;
            byte paper = 0;
            byte characterAttributes = 0;

            for (var col = 0; col < _oric.TextCols; col++)
            {
                var address = (ushort)(OricRasterizer.TextScreenAddress + row * _oric.TextCols + col);
                var value = _oric.Mem[address];

                if ((value & 0x60) == 0)
                {
                    ApplyAttribute(value, ref ink, ref paper, ref characterAttributes);
                    var attributeInverse = (value & 0x80) != 0;
                    var paperColor = s_palette[attributeInverse ? paper ^ 0x07 : paper];
                    _commands.Enqueue(new DrawGlyph(col, row, (byte)' ', paperColor, paperColor));
                    continue;
                }

                var inverse = (value & 0x80) != 0;
                var foreground = s_palette[inverse ? ink ^ 0x07 : ink];
                var background = s_palette[inverse ? paper ^ 0x07 : paper];
                if ((characterAttributes & 0x04) != 0 && flashBlanked)
                    foreground = background;

                _commands.Enqueue(new DrawGlyph(
                    col,
                    row,
                    value & 0x7f,
                    foreground,
                    background));
            }
        }
    }

    private static void ApplyAttribute(
        byte value,
        ref byte ink,
        ref byte paper,
        ref byte characterAttributes)
    {
        var setting = (byte)(value & 0x07);
        switch (value & 0x18)
        {
            case 0x00: ink = setting; break;
            case 0x08: characterAttributes = setting; break;
            case 0x10: paper = setting; break;
        }
    }
}
