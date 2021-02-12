namespace Highbyte.DotNet6502
{
    public class AddrModeCalcResult
    {
        public OpCode OpCode { get; private set; }
        public byte? InsValue { get; set; }
        public ushort? InsAddress { get; set; }
        public bool AddressCalculationCrossedPageBoundary { get; set; }

        public AddrModeCalcResult(OpCode opCode)
        {
            OpCode = opCode;            
        }
    }
}