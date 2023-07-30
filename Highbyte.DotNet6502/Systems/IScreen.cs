namespace Highbyte.DotNet6502.Systems;

public interface IScreen
{
    public int DrawableAreaWidth { get; }
    public int DrawableAreaHeight { get; }
    public int VisibleWidth { get; }
    public int VisibleHeight { get; }

    public bool HasBorder { get; }
    public int VisibleLeftRightBorderWidth { get; }
    public int VisibleTopBottomBorderHeight { get; }

    public float RefreshFrequencyHz { get; }
}
