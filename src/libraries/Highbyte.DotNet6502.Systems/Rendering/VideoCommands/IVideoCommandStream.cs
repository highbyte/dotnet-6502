using System.Drawing;

namespace Highbyte.DotNet6502.Systems.Rendering.VideoCommands;

public interface IVideoCommandStream : IRenderSource
{
    public IEnumerable<IVideoCommand> DequeueAll();
    public event EventHandler? FrameCompleted;

}

public interface IVideoCommand { }

public sealed record FillRect(int X, int Y, int W, int H, uint ColorArgb) : IVideoCommand;
public sealed record DrawGlyphArgb(int X, int Y, int GlyphId, uint ForeColorArgb, uint BackColorArgb) : IVideoCommand;

public sealed record DrawGlyph(int X, int Y, int GlyphId, Color ForeColor, Color BackColor) : IVideoCommand;
