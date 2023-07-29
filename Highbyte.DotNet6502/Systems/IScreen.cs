namespace Highbyte.DotNet6502.Systems;

public interface IScreen
{
    public int DrawWidth { get; }
    public int DrawHeight { get; }
    public int VisibleWidth { get; }
    public int VisibleHeight { get; }

    public bool HasBorder { get; }
    public int VisibleBorderWidth { get; }
    public int VisibleBorderHeight { get; }

    public float RefreshFrequencyHz { get; }
}