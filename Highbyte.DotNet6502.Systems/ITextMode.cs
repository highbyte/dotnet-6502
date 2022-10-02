namespace Highbyte.DotNet6502.Systems
{
    public interface ITextMode
    {
        public int Cols { get; }
        public int Rows { get; }
        public bool HasBorder { get; }
        public int BorderCols { get; }
        public int BorderRows { get; }
    }
}