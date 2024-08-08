namespace Highbyte.DotNet6502.Systems;

public interface ITextMode
{
    public int TextCols { get; }
    public int TextRows { get; }

    public int CharacterWidth { get; }
    public int CharacterHeight { get; }
}
