namespace Highbyte.DotNet6502.Systems;

/// <summary>
/// Immutable screen geometry for cases where a host knows the selected system/variant but does
/// not yet have a built emulator instance. This lets UI code size the emulator display from
/// configuration-derived dimensions without forcing CPU, memory, ROM, or render setup.
/// </summary>
public sealed class ScreenInfo : IScreen
{
    /// <summary>
    /// Creates a screen descriptor using drawable and visible pixel dimensions.
    /// </summary>
    public ScreenInfo(
        int drawableAreaWidth,
        int drawableAreaHeight,
        int visibleWidth,
        int visibleHeight,
        float refreshFrequencyHz)
    {
        DrawableAreaWidth = drawableAreaWidth;
        DrawableAreaHeight = drawableAreaHeight;
        VisibleWidth = visibleWidth;
        VisibleHeight = visibleHeight;
        RefreshFrequencyHz = refreshFrequencyHz;
    }

    public int DrawableAreaWidth { get; }
    public int DrawableAreaHeight { get; }
    public int VisibleWidth { get; }
    public int VisibleHeight { get; }
    public bool HasBorder => VisibleWidth > DrawableAreaWidth || VisibleHeight > DrawableAreaHeight;
    public int VisibleLeftRightBorderWidth => (VisibleWidth - DrawableAreaWidth) / 2;
    public int VisibleTopBottomBorderHeight => (VisibleHeight - DrawableAreaHeight) / 2;
    public float RefreshFrequencyHz { get; }
}
