namespace Highbyte.DotNet6502
{
    public class AddrModeCalcResult
    {
        public OpCode OpCode { get; set; }
        public byte? InsValue { get; set; }
        public ushort? InsAddress { get; set; }
        public bool AddressCalculationCrossedPageBoundary { get; set; }
    }
}