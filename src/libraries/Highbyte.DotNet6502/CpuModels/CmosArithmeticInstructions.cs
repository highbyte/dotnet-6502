using Highbyte.DotNet6502.Instructions;

namespace Highbyte.DotNet6502;

/// <summary>
/// 65C02 variant of ADC: binary mode is identical to the NMOS 6502; decimal mode uses
/// valid N/Z flags (from the final decimal result) and costs one extra cycle.
/// Opcode metadata (bytes, sizes, base cycles) is shared with the NMOS ADC — only the
/// decimal-mode execution differs.
/// </summary>
internal sealed class CmosAdc : Instruction, IInstructionUsesByte
{
    private static readonly ADC s_nmosTemplate = new();
    public override string Name => "ADC";
    public override List<OpCode> OpCodes => s_nmosTemplate.OpCodes;

    public ulong ExecuteWithByte(CPU cpu, Memory mem, byte value, AddrModeCalcResult addrModeCalcResult)
    {
        var extraCycles = InstructionExtraCyclesCalculator.CalculateExtraCycles(
            addrModeCalcResult.OpCode.AddressingMode,
            addrModeCalcResult.AddressCalculationCrossedPageBoundary);

        if (cpu.ProcessorStatus.Decimal)
        {
            cpu.A = DecimalArithmeticHelpers.AddWithCarryAndOverFlowDecimalModeCmos(cpu.A, value, ref cpu.ProcessorStatus);
            return extraCycles + 1; // 65C02: decimal mode costs one extra cycle
        }

        cpu.A = BinaryArithmeticHelpers.AddWithCarryAndOverflow(cpu.A, value, ref cpu.ProcessorStatus);
        return extraCycles;
    }
}

/// <summary>
/// 65C02 variant of SBC: binary mode is identical to the NMOS 6502; decimal mode uses
/// the 65C02's own correction sequence, valid N/Z flags, and costs one extra cycle.
/// </summary>
internal sealed class CmosSbc : Instruction, IInstructionUsesByte
{
    private static readonly SBC s_nmosTemplate = new();
    public override string Name => "SBC";
    public override List<OpCode> OpCodes => s_nmosTemplate.OpCodes;

    public ulong ExecuteWithByte(CPU cpu, Memory mem, byte value, AddrModeCalcResult addrModeCalcResult)
    {
        var extraCycles = InstructionExtraCyclesCalculator.CalculateExtraCycles(
            addrModeCalcResult.OpCode.AddressingMode,
            addrModeCalcResult.AddressCalculationCrossedPageBoundary);

        if (cpu.ProcessorStatus.Decimal)
        {
            cpu.A = DecimalArithmeticHelpers.SubtractWithCarryAndOverflowDecimalModeCmos(cpu.A, value, ref cpu.ProcessorStatus);
            return extraCycles + 1; // 65C02: decimal mode costs one extra cycle
        }

        cpu.A = BinaryArithmeticHelpers.SubtractWithCarryAndOverflow(cpu.A, value, ref cpu.ProcessorStatus);
        return extraCycles;
    }
}
