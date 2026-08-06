using Highbyte.DotNet6502.Systems.Apple2.Config;
using Highbyte.DotNet6502.Systems.Apple2.Video;
using Highbyte.DotNet6502.Systems.Rendering;
using Highbyte.DotNet6502.Systems.Rendering.VideoCommands;
using Highbyte.DotNet6502.Systems.Utils;
using Apple2System = Highbyte.DotNet6502.Systems.Apple2.Apple2;

namespace Highbyte.DotNet6502.Systems.Apple2.Render;

/// <summary>
/// Glyph-based Apple II text render path: emits one <see cref="DrawGlyph"/> per character cell
/// of the active 40x24 text page, resolving the interleaved row addressing and the
/// inverse/flash attributes carried in bits 7-6 of each screen byte.
///
/// The glyph command vocabulary cannot express pixel graphics, so this path always renders the
/// text page, even while the display soft switches select a graphics mode. Graphics modes are
/// rendered by the default <see cref="Apple2Rasterizer"/> provider.
/// </summary>
[DisplayName("Text Commands")]
[HelpText("Emits the Apple II 40x24 text page as DrawGlyph video commands.")]
public class Apple2VideoCommandStream : IRenderProvider, IVideoCommandStream
{
    public string Name => "Apple2CommandStream";

    private readonly Apple2System _apple2;
    private readonly Queue<IVideoCommand> _commands = new();

    // Some hosts run emulation on a background thread and render on the UI thread. Protect the
    // command queue so OnEndFrame production cannot interleave with DequeueAll consumption.
    private readonly object _commandsLock = new();

    private int _frameCounter;

    public event EventHandler? FrameCompleted;

    public Apple2VideoCommandStream(Apple2System apple2)
    {
        _apple2 = apple2;
    }

    /// <summary>Whether flashing characters are currently in their inverted phase.</summary>
    public bool FlashPhaseInverted => (_frameCounter / Apple2Config.FlashFramesPerToggle) % 2 == 1;

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

    private void GenerateCommands()
    {
        _commands.Enqueue(new SetConfig(GlyphToUnicodeConverter: Apple2CharSet.ScreenCodeToUnicode));

        var mem = _apple2.Mem;
        var pageBaseAddress = _apple2.SoftSwitches.ActiveTextPageBaseAddress;
        var foreground = Apple2Colors.GetForeground(_apple2.Apple2Config.MonitorColor);
        var background = Apple2Colors.Background;
        var flashInverted = FlashPhaseInverted;

        for (var row = 0; row < Apple2Config.Rows; row++)
        {
            var rowStartAddress = Apple2TextScreen.GetRowStartAddress(row, pageBaseAddress);
            for (var col = 0; col < Apple2Config.Cols; col++)
            {
                var screenByte = mem[(ushort)(rowStartAddress + col)];

                var inverted = Apple2CharSet.GetAttribute(screenByte) switch
                {
                    Apple2TextAttribute.Inverse => true,
                    Apple2TextAttribute.Flash => flashInverted,
                    _ => false,
                };

                _commands.Enqueue(inverted
                    ? new DrawGlyph(col, row, screenByte, background, foreground)
                    : new DrawGlyph(col, row, screenByte, foreground, background));
            }
        }
    }
}
