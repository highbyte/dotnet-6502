namespace Highbyte.DotNet6502.Systems;

public interface ITextMode
{
    public int Cols { get; }
    public int Rows { get; }

    public int CharacterWidth { get; }
    public int CharacterHeight { get; }
}
