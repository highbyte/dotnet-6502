namespace Highbyte.DotNet6502;

public struct AddrModeCalcResult
{

    public required OpCode OpCode { get; set; }
    public byte? InsValue { get; set; }
    public ushort? InsAddress { get; set; }
    public bool AddressCalculationCrossedPageBoundary { get; set; }
}
