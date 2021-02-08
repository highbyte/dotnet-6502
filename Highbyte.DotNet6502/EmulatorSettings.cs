namespace Highbyte.DotNet6502
{
    public class EmulatorSettings
    {
        public bool ThrowExceptionOnUnknownOpCodeDetected { get; set;}
        public byte? StopAtPC { get; set;}
   }
}