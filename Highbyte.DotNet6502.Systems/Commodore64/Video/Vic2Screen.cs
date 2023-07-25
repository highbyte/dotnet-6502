using Highbyte.DotNet6502.Systems.Commodore64.Models;

namespace Highbyte.DotNet6502.Systems.Commodore64.Video;

/// <summary>
/// </summary>
public class Vic2Screen : ITextMode, IScreen
{
    #region Interface implementations
    public int Cols { get; init; }
    public int Rows { get; init; }
    public int CharacterWidth { get; init; }
    public int CharacterHeight { get; init; }

    public int Width { get; init; }
    public int Height { get; init; }
    public int VisibleWidth { get; init; }
    public int VisibleHeight { get; init; }
    public bool HasBorder => true;
    public int BorderWidth => (int)Math.Floor((double)((VisibleWidth - Width) / 2.0d));
    public int BorderHeight => (int)Math.Floor((double)((VisibleHeight - Height) / 2.0d));
    public float RefreshFrequencyHz { get; init; }

    #endregion

    public int FirstScreenLineOfMainScreen { get; init; }
    public int LastScreenLineOfMainScreen { get; init; }
    public int FirstVisibleScreenLineOfMainScreen { get; init; }
    public int LastVisibleScreenLineOfMainScreen { get; init; }


    public Vic2Screen(Vic2ModelBase vic2Model, float cpuFrequencyHz)
    {
        Cols = vic2Model.Cols;
        Rows = vic2Model.Rows;
        CharacterWidth = vic2Model.CharacterWidth;
        CharacterHeight = vic2Model.CharacterHeight;
        Width = vic2Model.Width;    // Main screen width (not including border)
        Height = vic2Model.Height;  // Main screen height (not including border)

        VisibleWidth = (int)vic2Model.PixelsPerLineVisible;
        //VisibleWidth = (int)vic2Model.PixelsPerLine;

        VisibleHeight = (int)vic2Model.LinesVisible;
        //VisibleHeight = (int)vic2Model.Lines;

        RefreshFrequencyHz = (float) cpuFrequencyHz / vic2Model.CyclesPerFrame;

        // Entire screen (including border) first/last line in emulators screen buffer.
        var visibleLinesDifference = (int)vic2Model.Lines - VisibleHeight;
        var halfVisibleLinesDifference = (int)Math.Floor((double)(visibleLinesDifference / 2.0d));
        FirstVisibleScreenLineOfMainScreen = halfVisibleLinesDifference;
        LastVisibleScreenLineOfMainScreen = (int)vic2Model.Lines - FirstVisibleScreenLineOfMainScreen - 1;

        // Main screen (not border) first/last line in emulators screen buffer.
        FirstScreenLineOfMainScreen = vic2Model.ConvertRasterLineToScreenLine((ushort)vic2Model.FirstRasterLineOfMainScreen);
        LastScreenLineOfMainScreen = FirstScreenLineOfMainScreen + Height - 1;
    }
}
