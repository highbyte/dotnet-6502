namespace Highbyte.DotNet6502.Monitor
{
    public class BreakPoint
    {
        public bool Enabled { get; set; }

        public BreakPoint()
        {
            Enabled = true;
        }
    }
}