namespace Highbyte.DotNet6502.Systems;

public interface IScreen
{
    public int Width { get; }
    public int Height { get; }
    public int VisibleWidth { get; }
    public int VisibleHeight { get; }

    public bool HasBorder { get; }
    public int BorderWidth { get; }
    public int BorderHeight { get; }

    public float RefreshFrequencyHz { get; }
}