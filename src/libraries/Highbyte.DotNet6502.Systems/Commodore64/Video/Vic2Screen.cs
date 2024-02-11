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

    public int DrawableAreaWidth => _vic2Model.DrawableAreaWidth;
    public int DrawableAreaHeight => _vic2Model.DrawableAreaHeight;

    // Total screen and border sizes (that the VIC2 "sees" internally)
    public int TotalWidth => _vic2Model.TotalWidth;
    public int TotalHeight => _vic2Model.TotalHeight;
    public int TotalLeftRightBorderWidth => (int)Math.Floor((double)((TotalWidth - DrawableAreaWidth) / 2.0d));
    public int TotalTopBottomBorderHeight => (int)Math.Floor((double)((TotalHeight - DrawableAreaHeight) / 2.0d));

    // Visible screen and border sizes (that can be seen on a monitor)
    public int VisibleWidth => _vic2Model.MaxVisibleWidth;
    public int VisibleHeight => _vic2Model.MaxVisibleHeight;
    public int VisibleLeftRightBorderWidth => (int)Math.Floor((double)((VisibleWidth - DrawableAreaWidth) / 2.0d));
    public int VisibleTopBottomBorderHeight => (int)Math.Floor((double)((VisibleHeight - DrawableAreaHeight) / 2.0d));

    public float RefreshFrequencyHz => _cpuFrequencyHz / _vic2Model.CyclesPerFrame;

    #endregion

    public Vic2Screen(Vic2ModelBase vic2Model, float cpuFrequencyHz)
    {
        _vic2Model = vic2Model;
        _cpuFrequencyHz = cpuFrequencyHz;
    }

    public Vic2ScreenLayout GetLayout(bool visible = true, bool normalizeToVisible = true, bool for24RowMode = false, bool for38ColMode = false)
    {
        var verticalPositions = GetVerticalPositions(visible: visible, normalizeToVisible: normalizeToVisible, for24RowMode: for24RowMode);
        var horizontalPositions = GetHorizontalPositions(visible: visible, normalizeToVisible: normalizeToVisible, for38ColMode: for38ColMode);

        var layout = new Vic2ScreenLayout
        {
            TopBorder = new Vic2Area
            {
                Start = new IntVector2(horizontalPositions.leftBorderStartX, verticalPositions.topBorderStartY),
                End = new IntVector2(horizontalPositions.rightBorderEndX, verticalPositions.topBorderEndY)
            },
            LeftBorder = new Vic2Area
            {
                Start = new IntVector2(horizontalPositions.leftBorderStartX, verticalPositions.topBorderStartY),
                End = new IntVector2(horizontalPositions.leftBorderEndX, verticalPositions.bottomBorderEndY)
            },
            Screen = new Vic2Area
            {
                Start = new IntVector2(horizontalPositions.screenStartX, verticalPositions.screenStartY),
                End = new IntVector2(horizontalPositions.screenEndX, verticalPositions.screenEndY)
            },
            BottomBorder = new Vic2Area
            {
                Start = new IntVector2(horizontalPositions.leftBorderStartX, verticalPositions.bottomBorderStartY),
                End = new IntVector2(horizontalPositions.rightBorderEndX, verticalPositions.bottomBorderEndY)
            },
            RightBorder = new Vic2Area
            {
                Start = new IntVector2(horizontalPositions.rightBorderStartX, verticalPositions.topBorderStartY),
                End = new IntVector2(horizontalPositions.rightBorderEndX, verticalPositions.bottomBorderEndY)
            }
        };
        return layout;
    }

    public (int topBorderStartY, int topBorderEndY,
            int screenStartY, int screenEndY,
            int bottomBorderStartY, int bottomBorderEndY)
            GetVerticalPositions(bool visible, bool normalizeToVisible = true, bool for24RowMode = false)
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
            borderHeight = VisibleTopBottomBorderHeight;
            bottomBorderEndY = TotalHeight - topBorderStartY - 1;
        }
        else
        {
            topBorderStartY = 0;
            borderHeight = TotalTopBottomBorderHeight;
            bottomBorderEndY = TotalHeight - 1;
        }

        var topBorderEndY = topBorderStartY + borderHeight - 1;
        var screenStartY = topBorderEndY + 1;
        var screenEndY = screenStartY + DrawableAreaHeight - 1;
        var bottomBorderStartY = screenEndY + 1;
        //var bottomBorderEndY = bottomBorderStartY + borderHeight - 1;

        // Adjust for 24 row mode (instead of default 25 rows)
        if (for24RowMode)
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
            GetHorizontalPositions(bool visible, bool normalizeToVisible = true, bool for38ColMode = false)
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
            borderWidth = VisibleLeftRightBorderWidth;
            rightBorderEndX = TotalWidth - leftBorderStartX - 1;
        }
        else
        {
            leftBorderStartX = 0;
            borderWidth = TotalLeftRightBorderWidth;
            rightBorderEndX = TotalWidth;
        }

        var leftBorderEndX = leftBorderStartX + borderWidth - 1;
        var screenStartX = leftBorderEndX + 1;
        var screenEndX = screenStartX + DrawableAreaWidth - 1;
        var rightBorderStartX = screenEndX + 1;
        //var rightBorderEndX = rightBorderStartX + borderWidth - 1;

        // Adjust for 38 column mode (instead of default 40 columns)
        if (for38ColMode)
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
}

public class Vic2ScreenLayouts
{
    private readonly Vic2 _vic2;
    private readonly Dictionary<LayoutType, Vic2ScreenLayout>? _normal_layouts;
    private readonly Dictionary<LayoutType, Vic2ScreenLayout>? _24row_layouts;
    private readonly Dictionary<LayoutType, Vic2ScreenLayout>? _38col_layouts;
    private readonly Dictionary<LayoutType, Vic2ScreenLayout>? _24row_and_38col_layouts;

    public Vic2ScreenLayouts(Vic2 vic2)
    {
        _vic2 = vic2;

        var vic2Screen = _vic2.Vic2Screen;

        _normal_layouts = new()
        {
            { LayoutType.Normal,            vic2Screen.GetLayout(visible: false, normalizeToVisible: false, for24RowMode: false, for38ColMode: false) },
            { LayoutType.Visible,           vic2Screen.GetLayout(visible: true, normalizeToVisible: false, for24RowMode: false, for38ColMode: false) },
            { LayoutType.VisibleNormalized, vic2Screen.GetLayout(visible: true, normalizeToVisible: true, for24RowMode: false, for38ColMode: false) },
        };

        _24row_layouts = new()
        {
            { LayoutType.Normal,            vic2Screen.GetLayout(visible: false, normalizeToVisible: false, for24RowMode: true, for38ColMode: false) },
            { LayoutType.Visible,           vic2Screen.GetLayout(visible: true, normalizeToVisible: false, for24RowMode: true, for38ColMode: false) },
            { LayoutType.VisibleNormalized, vic2Screen.GetLayout(visible: true, normalizeToVisible: true, for24RowMode: true, for38ColMode: false) },

        };

        _38col_layouts = new()
        {
            { LayoutType.Normal,            vic2Screen.GetLayout(visible: false, normalizeToVisible: false, for24RowMode: false, for38ColMode: true) },
            { LayoutType.Visible,           vic2Screen.GetLayout(visible: true, normalizeToVisible: false, for24RowMode: false, for38ColMode: true) },
            { LayoutType.VisibleNormalized, vic2Screen.GetLayout(visible: true, normalizeToVisible: true, for24RowMode: false, for38ColMode: true) },
        };

        _24row_and_38col_layouts = new()
        {
            { LayoutType.Normal,            vic2Screen.GetLayout(visible: false, normalizeToVisible: false, for24RowMode: true, for38ColMode: true) },
            { LayoutType.Visible,           vic2Screen.GetLayout(visible: true, normalizeToVisible: false, for24RowMode: true, for38ColMode: true) },
            { LayoutType.VisibleNormalized, vic2Screen.GetLayout(visible: true, normalizeToVisible: true, for24RowMode: true, for38ColMode: true) }
        };
        _vic2 = vic2;
    }

    public Vic2ScreenLayout GetLayout(LayoutType layoutType)
    {
        return GetLayout(layoutType, _vic2.Is24RowDisplayEnabled, _vic2.Is38ColumnDisplayEnabled);
    }

    public Vic2ScreenLayout GetLayout(LayoutType layoutType, bool for24RowMode, bool for38ColMode)
    {
        if (_normal_layouts == null || _24row_layouts == null || _38col_layouts == null || _24row_and_38col_layouts == null)
            throw new DotNet6502Exception("ScreenLayouts not initialized. Call ScreenLayouts.Init() first.");

        if (!for24RowMode && !for38ColMode)
        {
            return _normal_layouts[layoutType];
        }
        else if (for24RowMode && !for38ColMode)
        {
            return _24row_layouts[layoutType];
        }
        else if (!for24RowMode && for38ColMode)
        {
            return _38col_layouts[layoutType];
        }
        else if (for24RowMode && for38ColMode)
        {
            return _24row_and_38col_layouts[layoutType];
        }
        throw new DotNet6502Exception("Internal error. ScreenLayouts.GetLayout(), unknown C64 state.");
    }

    public enum LayoutType
    {
        Normal,
        Visible,
        VisibleNormalized
    }
}

public class Vic2ScreenLayout
{
    public required Vic2Area TopBorder { get; set; }
    public required Vic2Area LeftBorder { get; set; }
    public required Vic2Area Screen { get; set; }
    public required Vic2Area BottomBorder { get; set; }
    public required Vic2Area RightBorder { get; set; }
}

public class Vic2Area
{
    public required IntVector2 Start { get; set; }
    public required IntVector2 End { get; set; }
}

public class IntVector2
{
    public int X { get; private set; }
    public int Y { get; private set; }
    public IntVector2(int x, int y)
    {
        X = x;
        Y = y;
    }
}
