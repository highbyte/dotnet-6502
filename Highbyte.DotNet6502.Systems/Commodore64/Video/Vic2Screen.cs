using Highbyte.DotNet6502.Systems.Commodore64.Models;

namespace Highbyte.DotNet6502.Systems.Commodore64.Video;

/// <summary>
/// </summary>
public class Vic2Screen : ITextMode, IScreen
{
    private readonly Vic2ModelBase _vic2Model;
    private readonly float _cpuFrequencyHz;

    #region Interface implementations
    public int TextCols => _vic2Model.TextCols;
    public int TextRows => _vic2Model.TextRows;
    public int CharacterWidth => _vic2Model.CharacterWidth;
    public int CharacterHeight => _vic2Model.CharacterHeight;
    public bool HasBorder => true;

    public int DrawWidth => _vic2Model.DrawWidth;
    public int DrawHeight => _vic2Model.DrawHeight;

    // Total screen and border sizes (that the VIC2 "sees" internally)
    public int TotalWidth => _vic2Model.TotalWidth;
    public int TotalHeight => _vic2Model.TotalHeight;
    public int TotalBorderWidth => (int)Math.Floor((double)((TotalWidth - DrawWidth) / 2.0d));
    public int TotalBorderHeight => (int)Math.Floor((double)((TotalHeight - DrawHeight) / 2.0d));

    // Visible screen and border sizes (that can be seen on a monitor)
    public int VisibleWidth => _vic2Model.MaxVisibleWidth;
    public int VisibleHeight => _vic2Model.MaxVisibleHeight;
    public int VisibleBorderWidth => (int)Math.Floor((double)((VisibleWidth - DrawWidth) / 2.0d));
    public int VisibleBorderHeight => (int)Math.Floor((double)((VisibleHeight - DrawHeight) / 2.0d));

    public float RefreshFrequencyHz => _cpuFrequencyHz / _vic2Model.CyclesPerFrame;

    #endregion

    public (int topBorderStartY, int topBorderEndY,
            int screenStartY, int screenEndY,
            int bottomBorderStartY, int bottomBorderEndY)
            GetVerticalPositions(C64 c64, bool visible, bool normalizeToVisible = true, bool adjustIf24RowMode = true)
    {
        int topBorderStartY;
        int borderHeight;
        int bottomBorderEndY;
        if (visible)
        {
            if (normalizeToVisible)
                topBorderStartY = 0;
            else
                topBorderStartY = (int)Math.Floor((double)((TotalHeight - VisibleHeight) / 2.0d));
            borderHeight = VisibleBorderHeight;
            bottomBorderEndY = TotalHeight - topBorderStartY - 1;
        }
        else
        {
            topBorderStartY = 0;
            borderHeight = TotalBorderHeight;
            bottomBorderEndY = TotalHeight - 1;
        }

        var topBorderEndY = topBorderStartY + borderHeight - 1;
        var screenStartY = topBorderEndY + 1;
        var screenEndY = screenStartY + DrawHeight - 1;
        var bottomBorderStartY = screenEndY + 1;
        //var bottomBorderEndY = bottomBorderStartY + borderHeight - 1;

        // Adjust for 24 row mode (instead of default 25 rows)
        if (adjustIf24RowMode && c64.Vic2.Is24RowDisplayEnabled)
        {
            topBorderEndY += 4;
            screenStartY += 4;
            screenEndY -= 4;
            bottomBorderStartY -= 4;
        }

        return (
            topBorderStartY, topBorderEndY,
            screenStartY, screenEndY,
            bottomBorderStartY, bottomBorderEndY
            );
    }

    public (int leftBorderStartX, int leftBorderEndX,
            int screenStartX, int screenEndX,
            int rightBorderStartX, int rightBorderEndX)
            GetHorizontalPositions(C64 c64, bool visible, bool normalizeToVisible = true, bool adjustIf38ColMode = true)
    {
        int leftBorderStartX;
        int borderWidth;
        int rightBorderEndX;

        if (visible)
        {
            if (normalizeToVisible)
                leftBorderStartX = 0;
            else
                leftBorderStartX = (int)Math.Floor((double)((TotalWidth - VisibleWidth) / 2.0d));

            borderWidth = VisibleBorderWidth;
            rightBorderEndX = TotalWidth - leftBorderStartX - 1;
        }
        else
        {
            leftBorderStartX = 0;
            borderWidth = TotalBorderWidth;
            rightBorderEndX = TotalWidth - 1;
        }

        var leftBorderEndX = leftBorderStartX + borderWidth - 1;
        var screenStartX = leftBorderEndX + 1;
        var screenEndX = screenStartX + DrawWidth - 1;
        var rightBorderStartX = screenEndX + 1;
        //var rightBorderEndX = rightBorderStartX + borderWidth - 1;

        // Adjust for 38 column mode (instead of default 40 columns)
        if (adjustIf38ColMode && c64.Vic2.Is38ColumnDisplayEnabled)
        {
            leftBorderEndX += 8;
            screenStartX += 8;
            screenEndX -= 8;
            rightBorderStartX -= 8;
        }

        return (
            leftBorderStartX, leftBorderEndX,
            screenStartX, screenEndX,
            rightBorderStartX, rightBorderEndX
            );
    }

    public Vic2Screen(Vic2ModelBase vic2Model, float cpuFrequencyHz)
    {
        _vic2Model = vic2Model;
        _cpuFrequencyHz = cpuFrequencyHz;
    }
}
